using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue24FeatureVideoEffectSettings;

/// <summary>
/// C# checks of 24-feature-video-effect-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The environment overrides are applied at render time: the <see cref="Godot.Environment"/>
/// resources keep the authored values.
/// </summary>
public partial class Issue24FeatureVideoEffectSettingsCheck : FeatureCheckCs
{
    private const string FilePath = "user://settings/video_effect.ini";

    private static readonly string[] Names =
    {
        "ssao_quality", "ssao_half_size", "ssao_adaptive_target", "ssao_blur_passes", "ssao_fadeout_from", "ssao_fadeout_to",
        "ssil_quality", "ssil_half_size", "ssil_adaptive_target", "ssil_blur_passes", "ssil_fadeout_from", "ssil_fadeout_to",
        "sdfgi_ray_count", "sdfgi_frames_to_converge", "sdfgi_frames_to_update_light", "voxel_gi_quality",
        "volumetric_fog_volume_size", "volumetric_fog_volume_depth", "volumetric_fog_use_filter",
        "sss_quality", "sss_scale", "sss_depth_scale", "dof_bokeh_shape", "dof_blur_quality", "dof_use_jitter",
        "allow_glow", "allow_ssao", "allow_ssil", "allow_ssr", "allow_fog", "allow_volumetric_fog",
        "brightness", "contrast", "saturation",
    };

    private static readonly string[] EveryRenderer = { "allow_glow", "allow_fog", "brightness", "contrast", "saturation" };

    // Enum values by number: the C# names of the numbered constants (32 rays) are not needed for the checks.
    private static readonly RenderingServer.EnvironmentSdfgiRayCount RayCount32 = (RenderingServer.EnvironmentSdfgiRayCount)3;

    protected override string CheckName => "24-feature-video-effect-settings";

    protected override async Task RunChecks()
    {
        var server = VideoEffectSettingsServer.Singleton;
        Expect(Engine.HasSingleton("VideoEffectSettingsServer"), "VideoEffectSettingsServer is a registered singleton");
        Expect(server is VideoEffectSettingsServerInstance, "VideoEffectSettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("VideoEffectSettingsServer") is SettingsServer, "VideoEffectSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("video_effect") == server, "the domain is registered as \"video_effect\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Servers, "applied after the renderer has read the project settings");
        ExpectEqual(server.GetFilePath(), FilePath, "file path");
        Expect(server.GetSettings() is VideoEffectSettings, "the settings resource is a VideoEffectSettings");
        var names = server.GetSettingNames();
        ExpectEqual(names.Length, Names.Length, "34 values");
        foreach (string setting in Names)
        {
            Expect(Array.IndexOf(names, setting) >= 0, $"setting \"{setting}\" exists");
            Expect(!server.GetSettingFlags(setting).HasFlag(SettingsServer.SettingFlags.RestartRequired), $"\"{setting}\" takes effect at once");
        }

        server.ResetToDefaults();
        CheckDefaults(server);
        CheckSupport(server);
        CheckQuality(server);
        await CheckOverrides(server);
        await CheckIgnoreGroup();
        CheckValidation(server);
        CheckStorage(server);

        // Leave no player file and no overrides behind.
        server.ResetToDefaults();
        DirAccess.RemoveAbsolute(FilePath);
    }

    private void CheckDefaults(VideoEffectSettingsServerInstance server)
    {
        ExpectEqual((int)server.GetDefaultValue("ssao_quality"), (int)ProjectSettings.GetSetting("rendering/environment/ssao/quality"), "default SSAO quality is the project setting");
        ExpectEqual((int)server.GetDefaultValue("volumetric_fog_volume_size"), (int)ProjectSettings.GetSetting("rendering/environment/volumetric_fog/volume_size"), "default fog volume size is the project setting");
        ExpectEqual((int)server.GetDefaultValue("dof_blur_quality"), (int)ProjectSettings.GetSetting("rendering/camera/depth_of_field/depth_of_field_bokeh_quality"), "default DoF quality is the project setting");
        Expect(VideoEffectSettingsServer.IsSsrAllowed() && VideoEffectSettingsServer.IsGlowAllowed(), "the effects are allowed by default");
        Expect(Mathf.IsEqualApprox(VideoEffectSettingsServer.GetBrightness(), 1.0f), "brightness is 1.0 by default");
    }

    private void CheckSupport(VideoEffectSettingsServerInstance server)
    {
        string method = RenderingServer.GetCurrentRenderingMethod();
        var unsupported = VideoEffectSettingsServer.GetUnsupportedSettingNames();
        foreach (string setting in Names)
        {
            ExpectEqual(Array.IndexOf(unsupported, setting) >= 0, !server.IsValueSupported(setting), $"GetUnsupportedSettingNames() matches IsValueSupported(\"{setting}\")");
            ExpectEqual(server.IsValueSupported(setting), VideoEffectSettingsServer.IsRendererSupported(setting), $"IsRendererSupported(\"{setting}\")");
        }
        foreach (string setting in EveryRenderer)
        {
            Expect(server.IsValueSupported(setting), $"\"{setting}\" works in every renderer");
        }
        ExpectEqual(server.IsValueSupported("allow_ssr"), method != "gl_compatibility" && method != "mobile", "SSR: Forward+ only");
        ExpectEqual(server.IsValueSupported("dof_blur_quality"), method != "gl_compatibility", "DoF: Forward+ and Mobile");
        GD.Print($"    renderer: {method}, unsupported: {string.Join(", ", unsupported)}");
    }

    private void CheckQuality(VideoEffectSettingsServerInstance server)
    {
        VideoEffectSettingsServer.SetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.High);
        ExpectEqual(VideoEffectSettingsServer.GetSsaoQuality(), RenderingServer.EnvironmentSsaoQuality.High, "SSAO quality");
        VideoEffectSettingsServer.SetSsaoHalfSize(false);
        Expect(!VideoEffectSettingsServer.IsSsaoHalfSizeEnabled(), "SSAO half size");
        VideoEffectSettingsServer.SetSdfgiRayCount(RayCount32);
        ExpectEqual(VideoEffectSettingsServer.GetSdfgiRayCount(), RayCount32, "SDFGI ray count");
        VideoEffectSettingsServer.SetVolumetricFogVolumeSize(96);
        ExpectEqual(VideoEffectSettingsServer.GetVolumetricFogVolumeSize(), 96, "volumetric fog volume size");
        VideoEffectSettingsServer.SetSssQuality(RenderingServer.SubSurfaceScatteringQuality.High);
        ExpectEqual(VideoEffectSettingsServer.GetSssQuality(), RenderingServer.SubSurfaceScatteringQuality.High, "SSS quality");
        VideoEffectSettingsServer.SetDofBlurQuality(RenderingServer.DofBlurQuality.High);
        ExpectEqual(VideoEffectSettingsServer.GetDofBlurQuality(), RenderingServer.DofBlurQuality.High, "DoF blur quality");
        ExpectEqual((int)server.GetAppliedValue("ssao_quality"), (int)RenderingServer.EnvironmentSsaoQuality.High, "applied at once (APPLY_MODE_IMMEDIATE)");
        server.ResetToDefaults();
    }

    private async Task CheckOverrides(VideoEffectSettingsServerInstance server)
    {
        var authored = new Godot.Environment { SsrEnabled = true, GlowEnabled = true };
        VideoEffectSettingsServer.SetAllowSsr(false);
        VideoEffectSettingsServer.SetAllowGlow(false);
        Expect(!VideoEffectSettingsServer.IsSsrAllowed(), "SSR is turned off");
        Expect(authored.SsrEnabled && authored.GlowEnabled, "the environment keeps its authored values");

        // A scene loaded after the change.
        var later = new WorldEnvironment { Environment = new Godot.Environment { SsrEnabled = true } };
        AddChild(later);
        Expect(later.Environment.SsrEnabled, "a later scene keeps its authored value (the override is applied at render time)");
        Expect(!VideoEffectSettingsServer.IsEnvironmentIgnored(later.Environment), "a later scene is not excluded");

        VideoEffectSettingsServer.SetAllowSsr(true);
        VideoEffectSettingsServer.SetAllowGlow(true);
        Expect(VideoEffectSettingsServer.IsSsrAllowed(), "SSR is allowed again");

        VideoEffectSettingsServer.SetBrightness(1.3f);
        Expect(Mathf.IsEqualApprox(VideoEffectSettingsServer.GetBrightness(), 1.3f), "brightness");
        Expect(!authored.AdjustmentEnabled, "the global adjustment does not enable the environment's adjustment");
        later.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        server.ResetToDefaults();
    }

    private async Task CheckIgnoreGroup()
    {
        ExpectEqual(VideoEffectSettingsServer.GetIgnoreGroup(), new StringName("ignore_quality_settings"), "GetIgnoreGroup()");
        var menu = new WorldEnvironment { Environment = new Godot.Environment() };
        menu.AddToGroup(VideoEffectSettingsServer.GetIgnoreGroup());
        AddChild(menu);
        Expect(VideoEffectSettingsServer.IsEnvironmentIgnored(menu.Environment), "an environment in the group is excluded when it enters the tree");
        var freeEnvironment = new Godot.Environment();
        VideoEffectSettingsServer.SetEnvironmentIgnored(freeEnvironment, true);
        Expect(VideoEffectSettingsServer.IsEnvironmentIgnored(freeEnvironment), "SetEnvironmentIgnored(true)");
        VideoEffectSettingsServer.SetEnvironmentIgnored(freeEnvironment, false);
        Expect(!VideoEffectSettingsServer.IsEnvironmentIgnored(freeEnvironment), "SetEnvironmentIgnored(false)");
        menu.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void CheckValidation(VideoEffectSettingsServerInstance server)
    {
        VideoEffectSettingsServer.SetSsaoAdaptiveTarget(2.0f);
        Expect(Mathf.IsEqualApprox(VideoEffectSettingsServer.GetSsaoAdaptiveTarget(), 1.0f), "adaptive target is clamped to 1.0");
        VideoEffectSettingsServer.SetVolumetricFogVolumeSize(4);
        ExpectEqual(VideoEffectSettingsServer.GetVolumetricFogVolumeSize(), 16, "fog volume size is clamped to 16");
        VideoEffectSettingsServer.SetBrightness(100.0f);
        Expect(Mathf.IsEqualApprox(VideoEffectSettingsServer.GetBrightness(), 8.0f), "brightness is clamped to 8.0");
        server.SetValue("ssao_quality", 42);
        ExpectEqual((int)VideoEffectSettingsServer.GetSsaoQuality(), (int)server.GetDefaultValue("ssao_quality"), "an invalid enum value becomes the default");
        server.ResetToDefaults();
    }

    private void CheckStorage(VideoEffectSettingsServerInstance server)
    {
        VideoEffectSettingsServer.SetSsilQuality(RenderingServer.EnvironmentSsilQuality.High);
        VideoEffectSettingsServer.SetAllowVolumetricFog(false);
        VideoEffectSettingsServer.SetContrast(1.2f);
        ExpectEqual(server.Save(), Error.Ok, "Save()");
        Expect(FileAccess.FileExists(FilePath), "the file exists");
        Expect(FileAccess.GetFileAsString(FilePath).Contains("allow_volumetric_fog"), "the INI file has allow_volumetric_fog");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load()");
        ExpectEqual(VideoEffectSettingsServer.GetSsilQuality(), RenderingServer.EnvironmentSsilQuality.High, "the SSIL quality is loaded (also where the renderer ignores it)");
        Expect(!VideoEffectSettingsServer.IsVolumetricFogAllowed(), "the fog switch is loaded");
        Expect(Mathf.IsEqualApprox(VideoEffectSettingsServer.GetContrast(), 1.2f), "the contrast is loaded");
    }
}
