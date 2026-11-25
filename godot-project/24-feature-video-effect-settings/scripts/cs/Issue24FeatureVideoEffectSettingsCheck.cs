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
    private const string FilePath = "user://settings/render_effect.ini";

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
        await CheckTypedSignals();

        var server = RenderEffectSettingsServer.Singleton;
        Expect(Engine.HasSingleton("RenderEffectSettingsServer"), "RenderEffectSettingsServer is a registered singleton");
        Expect(server is RenderEffectSettingsServerInstance, "RenderEffectSettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("RenderEffectSettingsServer") is SettingsServer, "RenderEffectSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("render_effect") == server, "the domain is registered as \"render_effect\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Servers, "applied after the renderer has read the project settings");
        ExpectEqual(server.GetFilePath(), FilePath, "file path");
        Expect(server.GetSettings() is RenderEffectSettings, "the settings resource is a RenderEffectSettings");
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

    private void CheckDefaults(RenderEffectSettingsServerInstance server)
    {
        ExpectEqual((int)server.GetDefaultValue("ssao_quality"), (int)ProjectSettings.GetSetting("rendering/environment/ssao/quality"), "default SSAO quality is the project setting");
        ExpectEqual((int)server.GetDefaultValue("volumetric_fog_volume_size"), (int)ProjectSettings.GetSetting("rendering/environment/volumetric_fog/volume_size"), "default fog volume size is the project setting");
        ExpectEqual((int)server.GetDefaultValue("dof_blur_quality"), (int)ProjectSettings.GetSetting("rendering/camera/depth_of_field/depth_of_field_bokeh_quality"), "default DoF quality is the project setting");
        Expect(RenderEffectSettingsServer.IsSsrAllowed() && RenderEffectSettingsServer.IsGlowAllowed(), "the effects are allowed by default");
        Expect(Mathf.IsEqualApprox(RenderEffectSettingsServer.GetBrightness(), 1.0f), "brightness is 1.0 by default");
    }

    private void CheckSupport(RenderEffectSettingsServerInstance server)
    {
        string method = RenderingServer.GetCurrentRenderingMethod();
        var unsupported = RenderEffectSettingsServer.GetUnsupportedSettingNames();
        foreach (string setting in Names)
        {
            ExpectEqual(Array.IndexOf(unsupported, setting) >= 0, !server.IsValueSupported(setting), $"GetUnsupportedSettingNames() matches IsValueSupported(\"{setting}\")");
            ExpectEqual(server.IsValueSupported(setting), RenderEffectSettingsServer.IsRendererSupported(setting), $"IsRendererSupported(\"{setting}\")");
        }
        foreach (string setting in EveryRenderer)
        {
            Expect(server.IsValueSupported(setting), $"\"{setting}\" works in every renderer");
        }
        ExpectEqual(server.IsValueSupported("allow_ssr"), method != "gl_compatibility" && method != "mobile", "SSR: Forward+ only");
        ExpectEqual(server.IsValueSupported("dof_blur_quality"), method != "gl_compatibility", "DoF: Forward+ and Mobile");
        GD.Print($"    renderer: {method}, unsupported: {string.Join(", ", unsupported)}");
    }

    private void CheckQuality(RenderEffectSettingsServerInstance server)
    {
        RenderEffectSettingsServer.SetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.High);
        ExpectEqual(RenderEffectSettingsServer.GetSsaoQuality(), RenderingServer.EnvironmentSsaoQuality.High, "SSAO quality");
        RenderEffectSettingsServer.SetSsaoHalfSize(false);
        Expect(!RenderEffectSettingsServer.IsSsaoHalfSizeEnabled(), "SSAO half size");
        RenderEffectSettingsServer.SetSdfgiRayCount(RayCount32);
        ExpectEqual(RenderEffectSettingsServer.GetSdfgiRayCount(), RayCount32, "SDFGI ray count");
        RenderEffectSettingsServer.SetVolumetricFogVolumeSize(96);
        ExpectEqual(RenderEffectSettingsServer.GetVolumetricFogVolumeSize(), 96, "volumetric fog volume size");
        RenderEffectSettingsServer.SetSssQuality(RenderingServer.SubSurfaceScatteringQuality.High);
        ExpectEqual(RenderEffectSettingsServer.GetSssQuality(), RenderingServer.SubSurfaceScatteringQuality.High, "SSS quality");
        RenderEffectSettingsServer.SetDofBlurQuality(RenderingServer.DofBlurQuality.High);
        ExpectEqual(RenderEffectSettingsServer.GetDofBlurQuality(), RenderingServer.DofBlurQuality.High, "DoF blur quality");
        ExpectEqual((int)server.GetAppliedValue("ssao_quality"), (int)RenderingServer.EnvironmentSsaoQuality.High, "applied at once (APPLY_MODE_IMMEDIATE)");
        server.ResetToDefaults();
    }

    private async Task CheckOverrides(RenderEffectSettingsServerInstance server)
    {
        var authored = new Godot.Environment { SsrEnabled = true, GlowEnabled = true };
        RenderEffectSettingsServer.SetAllowSsr(false);
        RenderEffectSettingsServer.SetAllowGlow(false);
        Expect(!RenderEffectSettingsServer.IsSsrAllowed(), "SSR is turned off");
        Expect(authored.SsrEnabled && authored.GlowEnabled, "the environment keeps its authored values");

        // A scene loaded after the change.
        var later = new WorldEnvironment { Environment = new Godot.Environment { SsrEnabled = true } };
        AddChild(later);
        Expect(later.Environment.SsrEnabled, "a later scene keeps its authored value (the override is applied at render time)");
        Expect(!RenderEffectSettingsServer.IsEnvironmentIgnored(later.Environment), "a later scene is not excluded");

        RenderEffectSettingsServer.SetAllowSsr(true);
        RenderEffectSettingsServer.SetAllowGlow(true);
        Expect(RenderEffectSettingsServer.IsSsrAllowed(), "SSR is allowed again");

        RenderEffectSettingsServer.SetBrightness(1.3f);
        Expect(Mathf.IsEqualApprox(RenderEffectSettingsServer.GetBrightness(), 1.3f), "brightness");
        Expect(!authored.AdjustmentEnabled, "the global adjustment does not enable the environment's adjustment");
        later.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        server.ResetToDefaults();
    }

    private async Task CheckIgnoreGroup()
    {
        ExpectEqual(RenderEffectSettingsServer.GetIgnoreGroup(), new StringName("ignore_quality_settings"), "GetIgnoreGroup()");
        var menu = new WorldEnvironment { Environment = new Godot.Environment() };
        menu.AddToGroup(RenderEffectSettingsServer.GetIgnoreGroup());
        AddChild(menu);
        Expect(RenderEffectSettingsServer.IsEnvironmentIgnored(menu.Environment), "an environment in the group is excluded when it enters the tree");
        var freeEnvironment = new Godot.Environment();
        RenderEffectSettingsServer.SetEnvironmentIgnored(freeEnvironment, true);
        Expect(RenderEffectSettingsServer.IsEnvironmentIgnored(freeEnvironment), "SetEnvironmentIgnored(true)");
        RenderEffectSettingsServer.SetEnvironmentIgnored(freeEnvironment, false);
        Expect(!RenderEffectSettingsServer.IsEnvironmentIgnored(freeEnvironment), "SetEnvironmentIgnored(false)");
        menu.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void CheckValidation(RenderEffectSettingsServerInstance server)
    {
        RenderEffectSettingsServer.SetSsaoAdaptiveTarget(2.0f);
        Expect(Mathf.IsEqualApprox(RenderEffectSettingsServer.GetSsaoAdaptiveTarget(), 1.0f), "adaptive target is clamped to 1.0");
        RenderEffectSettingsServer.SetVolumetricFogVolumeSize(4);
        ExpectEqual(RenderEffectSettingsServer.GetVolumetricFogVolumeSize(), 16, "fog volume size is clamped to 16");
        RenderEffectSettingsServer.SetBrightness(100.0f);
        Expect(Mathf.IsEqualApprox(RenderEffectSettingsServer.GetBrightness(), 8.0f), "brightness is clamped to 8.0");
        server.SetValue("ssao_quality", 42);
        ExpectEqual((int)RenderEffectSettingsServer.GetSsaoQuality(), (int)server.GetDefaultValue("ssao_quality"), "an invalid enum value becomes the default");
        server.ResetToDefaults();
    }

    private void CheckStorage(RenderEffectSettingsServerInstance server)
    {
        RenderEffectSettingsServer.SetSsilQuality(RenderingServer.EnvironmentSsilQuality.High);
        RenderEffectSettingsServer.SetAllowVolumetricFog(false);
        RenderEffectSettingsServer.SetContrast(1.2f);
        ExpectEqual(server.Save(), Error.Ok, "Save()");
        Expect(FileAccess.FileExists(FilePath), "the file exists");
        Expect(FileAccess.GetFileAsString(FilePath).Contains("allow_volumetric_fog"), "the INI file has allow_volumetric_fog");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load()");
        ExpectEqual(RenderEffectSettingsServer.GetSsilQuality(), RenderingServer.EnvironmentSsilQuality.High, "the SSIL quality is loaded (also where the renderer ignores it)");
        Expect(!RenderEffectSettingsServer.IsVolumetricFogAllowed(), "the fog switch is loaded");
        Expect(Mathf.IsEqualApprox(RenderEffectSettingsServer.GetContrast(), 1.2f), "the contrast is loaded");
    }

    /// <summary>Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.</summary>
    private async Task CheckTypedSignals()
    {
        if (!Engine.HasSingleton("RenderEffectSettingsServer"))
        {
            return;
        }
        var s = (SettingsServer)Engine.GetSingleton("RenderEffectSettingsServer");
        Expect(s.HasSignal("brightness_changed"), "RenderEffectSettingsServer has the typed signal brightness_changed");
        string filePath = s.GetFilePath();
        bool hadFile = FileAccess.FileExists(filePath);
        var previousMode = s.ApplyMode;
        s.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        s.ResetValue("brightness");
        double newValue = s.GetAppliedValue("brightness").AsDouble() != 1.5 ? 1.5 : 2.0;
        var received = new System.Collections.Generic.List<Variant>();
        var onChanged = Callable.From((Variant v) => received.Add(v));
        s.Connect("brightness_changed", onChanged);

        s.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        s.SetValue("brightness", newValue);
        ExpectEqual(received.Count, 0, "brightness_changed does not fire for a pending change in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 1, "brightness_changed fires once on Apply()");
        if (received.Count > 0)
        {
            ExpectEqual(received[0].VariantType, Variant.Type.Float, "brightness_changed passes a typed value");
            ExpectEqual((float)received[0].AsDouble(), (float)newValue, "brightness_changed passes the applied value");
        }
        s.Apply();
        ExpectEqual(received.Count, 1, "brightness_changed does not fire when nothing changed");
        s.ResetValue("brightness");
        ExpectEqual(received.Count, 1, "brightness_changed waits for Apply() after a reset in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 2, "brightness_changed fires again when the default is applied");

        s.Disconnect("brightness_changed", onChanged);
        s.ApplyMode = previousMode;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!hadFile)
        {
            DirAccess.RemoveAbsolute(filePath);
        }
    }
}
