using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.RenderEffectSettings;

using RenderEffectSettings = Godot.RenderEffectSettings;

/// <summary>
/// Full API test of Render Effect Settings (RenderEffectSettingsServer, RenderEffectSettings). Same tests as scripts/gd/Test.gd.
/// The effect quality goes to the RenderingServer, the environment overrides (allow_*, brightness, contrast, saturation)
/// are applied at render time: Environment resources keep their authored values.
/// </summary>
public partial class Test : FeatureTest
{
    private const string EffectFile = "user://settings/render_effect.ini";

    private readonly Godot.Collections.Dictionary _received = [];

    protected override async Task RunTests()
    {
        RenderEffectSettingsServer.Singleton.ResetToDefaults();
        TestDefaults();
        TestValues();
        TestSupport();
        await TestOverrides();
        await TestIgnoreGroup();
        TestValidation();
        TestSignals();
        TestStorage();
        TestResource();
        RenderEffectSettingsServer.Singleton.ResetToDefaults();
        DirAccess.RemoveAbsolute(EffectFile);
    }

    private void TestDefaults()
    {
        int projectSsao = (int)ProjectSettings.GetSetting("rendering/environment/ssao/quality");
        int projectBlur = (int)ProjectSettings.GetSetting("rendering/environment/ssil/blur_passes");
        int projectFog = (int)ProjectSettings.GetSetting("rendering/environment/volumetric_fog/volume_size");
        int projectBokeh = (int)ProjectSettings.GetSetting("rendering/camera/depth_of_field/depth_of_field_bokeh_shape");
        int projectRays = (int)ProjectSettings.GetSetting("rendering/global_illumination/sdfgi/probe_ray_count");
        ExpectEqual((int)RenderEffectSettingsServer.GetSsaoQuality(), projectSsao, "the default SSAO quality is the project setting");
        ExpectEqual(RenderEffectSettingsServer.GetSsilBlurPasses(), projectBlur, "the default SSIL blur passes are the project setting");
        ExpectEqual(RenderEffectSettingsServer.GetVolumetricFogVolumeSize(), projectFog, "the default fog volume size is the project setting");
        ExpectEqual((int)RenderEffectSettingsServer.GetDofBokehShape(), projectBokeh, "the default bokeh shape is the project setting");
        ExpectEqual((int)RenderEffectSettingsServer.GetSdfgiRayCount(), projectRays, "the default SDFGI ray count is the project setting");
        Expect(RenderEffectSettingsServer.IsSsaoAllowed(), "SSAO is allowed by default");
        Expect(RenderEffectSettingsServer.IsGlowAllowed(), "glow is allowed by default");
        ExpectEqual(RenderEffectSettingsServer.GetBrightness(), 1.0f, "brightness is 1.0 by default");
    }

    private void TestValues()
    {
        RenderEffectSettingsServer.SetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.High);
        ExpectEqual(RenderEffectSettingsServer.GetSsaoQuality(), RenderingServer.EnvironmentSsaoQuality.High, "ssao_quality round trip");
        RenderEffectSettingsServer.SetSsaoHalfSize(false);
        Expect(!RenderEffectSettingsServer.IsSsaoHalfSizeEnabled(), "ssao_half_size round trip");
        RenderEffectSettingsServer.SetSsaoAdaptiveTarget(0.25f);
        ExpectEqual(RenderEffectSettingsServer.GetSsaoAdaptiveTarget(), 0.25f, "ssao_adaptive_target round trip");
        RenderEffectSettingsServer.SetSsaoBlurPasses(1);
        ExpectEqual(RenderEffectSettingsServer.GetSsaoBlurPasses(), 1, "ssao_blur_passes round trip");
        RenderEffectSettingsServer.SetSsaoFadeOutFrom(10.0f);
        ExpectEqual(RenderEffectSettingsServer.GetSsaoFadeOutFrom(), 10.0f, "ssao_fadeout_from round trip");
        RenderEffectSettingsServer.SetSsaoFadeOutTo(100.0f);
        ExpectEqual(RenderEffectSettingsServer.GetSsaoFadeOutTo(), 100.0f, "ssao_fadeout_to round trip");
        RenderEffectSettingsServer.SetSsilQuality(RenderingServer.EnvironmentSsilQuality.Low);
        ExpectEqual(RenderEffectSettingsServer.GetSsilQuality(), RenderingServer.EnvironmentSsilQuality.Low, "ssil_quality round trip");
        RenderEffectSettingsServer.SetSsilHalfSize(false);
        Expect(!RenderEffectSettingsServer.IsSsilHalfSizeEnabled(), "ssil_half_size round trip");
        RenderEffectSettingsServer.SetSsilAdaptiveTarget(0.25f);
        ExpectEqual(RenderEffectSettingsServer.GetSsilAdaptiveTarget(), 0.25f, "ssil_adaptive_target round trip");
        RenderEffectSettingsServer.SetSsilBlurPasses(1);
        ExpectEqual(RenderEffectSettingsServer.GetSsilBlurPasses(), 1, "ssil_blur_passes round trip");
        RenderEffectSettingsServer.SetSsilFadeOutFrom(10.0f);
        ExpectEqual(RenderEffectSettingsServer.GetSsilFadeOutFrom(), 10.0f, "ssil_fadeout_from round trip");
        RenderEffectSettingsServer.SetSsilFadeOutTo(100.0f);
        ExpectEqual(RenderEffectSettingsServer.GetSsilFadeOutTo(), 100.0f, "ssil_fadeout_to round trip");
        RenderEffectSettingsServer.SetSdfgiRayCount(RenderingServer.EnvironmentSdfgiRayCount.Count32);
        ExpectEqual(RenderEffectSettingsServer.GetSdfgiRayCount(), RenderingServer.EnvironmentSdfgiRayCount.Count32, "sdfgi_ray_count round trip");
        RenderEffectSettingsServer.SetSdfgiFramesToConverge(RenderingServer.EnvironmentSdfgiFramesToConverge.In15Frames);
        ExpectEqual(RenderEffectSettingsServer.GetSdfgiFramesToConverge(), RenderingServer.EnvironmentSdfgiFramesToConverge.In15Frames, "sdfgi_frames_to_converge round trip");
        RenderEffectSettingsServer.SetSdfgiFramesToUpdateLight(RenderingServer.EnvironmentSdfgiFramesToUpdateLight.In4Frames);
        ExpectEqual(RenderEffectSettingsServer.GetSdfgiFramesToUpdateLight(), RenderingServer.EnvironmentSdfgiFramesToUpdateLight.In4Frames, "sdfgi_frames_to_update_light round trip");
        RenderEffectSettingsServer.SetVoxelGIQuality(RenderingServer.VoxelGIQuality.High);
        ExpectEqual(RenderEffectSettingsServer.GetVoxelGIQuality(), RenderingServer.VoxelGIQuality.High, "voxel_gi_quality round trip");
        RenderEffectSettingsServer.SetVolumetricFogVolumeSize(96);
        ExpectEqual(RenderEffectSettingsServer.GetVolumetricFogVolumeSize(), 96, "volumetric_fog_volume_size round trip");
        RenderEffectSettingsServer.SetVolumetricFogVolumeDepth(32);
        ExpectEqual(RenderEffectSettingsServer.GetVolumetricFogVolumeDepth(), 32, "volumetric_fog_volume_depth round trip");
        RenderEffectSettingsServer.SetVolumetricFogUseFilter(false);
        Expect(!RenderEffectSettingsServer.IsVolumetricFogUsingFilter(), "volumetric_fog_use_filter round trip");
        RenderEffectSettingsServer.SetSssQuality(RenderingServer.SubSurfaceScatteringQuality.High);
        ExpectEqual(RenderEffectSettingsServer.GetSssQuality(), RenderingServer.SubSurfaceScatteringQuality.High, "sss_quality round trip");
        RenderEffectSettingsServer.SetSssScale(0.5f);
        ExpectEqual(RenderEffectSettingsServer.GetSssScale(), 0.5f, "sss_scale round trip");
        RenderEffectSettingsServer.SetSssDepthScale(0.25f);
        ExpectEqual(RenderEffectSettingsServer.GetSssDepthScale(), 0.25f, "sss_depth_scale round trip");
        RenderEffectSettingsServer.SetDofBokehShape(RenderingServer.DofBokehShape.Circle);
        ExpectEqual(RenderEffectSettingsServer.GetDofBokehShape(), RenderingServer.DofBokehShape.Circle, "dof_bokeh_shape round trip");
        RenderEffectSettingsServer.SetDofBlurQuality(RenderingServer.DofBlurQuality.High);
        ExpectEqual(RenderEffectSettingsServer.GetDofBlurQuality(), RenderingServer.DofBlurQuality.High, "dof_blur_quality round trip");
        RenderEffectSettingsServer.SetDofUseJitter(true);
        Expect(RenderEffectSettingsServer.IsDofUsingJitter(), "dof_use_jitter round trip");
        RenderEffectSettingsServer.SetAllowGlow(false);
        Expect(!RenderEffectSettingsServer.IsGlowAllowed(), "allow_glow round trip");
        RenderEffectSettingsServer.SetAllowSsao(false);
        Expect(!RenderEffectSettingsServer.IsSsaoAllowed(), "allow_ssao round trip");
        RenderEffectSettingsServer.SetAllowSsil(false);
        Expect(!RenderEffectSettingsServer.IsSsilAllowed(), "allow_ssil round trip");
        RenderEffectSettingsServer.SetAllowSsr(false);
        Expect(!RenderEffectSettingsServer.IsSsrAllowed(), "allow_ssr round trip");
        RenderEffectSettingsServer.SetAllowFog(false);
        Expect(!RenderEffectSettingsServer.IsFogAllowed(), "allow_fog round trip");
        RenderEffectSettingsServer.SetAllowVolumetricFog(false);
        Expect(!RenderEffectSettingsServer.IsVolumetricFogAllowed(), "allow_volumetric_fog round trip");
        RenderEffectSettingsServer.SetBrightness(1.5f);
        ExpectEqual(RenderEffectSettingsServer.GetBrightness(), 1.5f, "brightness round trip");
        RenderEffectSettingsServer.SetContrast(1.25f);
        ExpectEqual(RenderEffectSettingsServer.GetContrast(), 1.25f, "contrast round trip");
        RenderEffectSettingsServer.SetSaturation(0.75f);
        ExpectEqual(RenderEffectSettingsServer.GetSaturation(), 0.75f, "saturation round trip");
        RenderEffectSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSupport()
    {
        string[] unsupported = RenderEffectSettingsServer.GetUnsupportedSettingNames();
        foreach (string setting in new[] { "allow_glow", "allow_ssao", "brightness", "voxel_gi_quality", "dof_blur_quality", "volumetric_fog_volume_size" })
        {
            bool supported = RenderEffectSettingsServer.Singleton.IsValueSupported(setting);
            ExpectEqual(System.Array.IndexOf(unsupported, setting) < 0, supported, $"GetUnsupportedSettingNames matches IsValueSupported({setting})");
            ExpectEqual(RenderEffectSettingsServer.IsRendererSupported(setting), supported, $"IsRendererSupported matches IsValueSupported({setting})");
        }
        foreach (string setting in new[] { "allow_glow", "allow_fog", "brightness", "contrast", "saturation" })
        {
            Expect(RenderEffectSettingsServer.Singleton.IsValueSupported(setting), $"{setting} works in every renderer");
        }
        bool forwardPlus = RenderingServer.GetCurrentRenderingMethod() == "forward_plus";
        ExpectEqual(RenderEffectSettingsServer.Singleton.IsValueSupported("allow_ssao"), forwardPlus, "SSAO is supported on Forward+ only");
    }

    private async Task TestOverrides()
    {
        var authored = new Environment();
        authored.SsrEnabled = true;
        authored.GlowEnabled = true;
        RenderEffectSettingsServer.SetAllowSsr(false);
        RenderEffectSettingsServer.SetAllowGlow(false);
        Expect(authored.SsrEnabled, "the environment keeps its authored SSR value");
        Expect(authored.GlowEnabled, "the environment keeps its authored glow value");
        var later = new WorldEnvironment();
        later.Environment = new Environment();
        later.Environment.SsrEnabled = true;
        AddChild(later);
        Expect(later.Environment.SsrEnabled, "a later scene keeps its authored value, the override is applied at render time");
        Expect(!RenderEffectSettingsServer.IsEnvironmentIgnored(later.Environment), "a later scene is not excluded");
        RenderEffectSettingsServer.SetBrightness(1.5f);
        Expect(!authored.AdjustmentEnabled, "the global adjustment does not enable the adjustment of the environment");
        later.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderEffectSettingsServer.Singleton.ResetToDefaults();
    }

    private async Task TestIgnoreGroup()
    {
        StringName group = RenderEffectSettingsServer.GetIgnoreGroup();
        ExpectEqual(group, new StringName("ignore_quality_settings"), "GetIgnoreGroup returns the group name");
        var menu = new WorldEnvironment();
        menu.Environment = new Environment();
        menu.AddToGroup(group);
        AddChild(menu);
        Expect(RenderEffectSettingsServer.IsEnvironmentIgnored(menu.Environment), "an environment in the group is excluded when it enters the tree");
        var freeEnvironment = new Environment();
        RenderEffectSettingsServer.SetEnvironmentIgnored(freeEnvironment, true);
        Expect(RenderEffectSettingsServer.IsEnvironmentIgnored(freeEnvironment), "SetEnvironmentIgnored(true) excludes an environment");
        RenderEffectSettingsServer.SetEnvironmentIgnored(freeEnvironment, false);
        Expect(!RenderEffectSettingsServer.IsEnvironmentIgnored(freeEnvironment), "SetEnvironmentIgnored(false) includes it again");
        menu.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void TestValidation()
    {
        RenderEffectSettingsServer.SetSsaoAdaptiveTarget(2.0f);
        ExpectEqual(RenderEffectSettingsServer.GetSsaoAdaptiveTarget(), 1.0f, "the adaptive target is clamped to 1.0");
        RenderEffectSettingsServer.SetVolumetricFogVolumeSize(4);
        ExpectEqual(RenderEffectSettingsServer.GetVolumetricFogVolumeSize(), 16, "the fog volume size is clamped to 16");
        RenderEffectSettingsServer.SetBrightness(100.0f);
        ExpectEqual(RenderEffectSettingsServer.GetBrightness(), 8.0f, "brightness is clamped to 8.0");
        RenderEffectSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSignals()
    {
        RenderEffectSettingsServerInstance server = RenderEffectSettingsServer.Singleton;
        server.AllowGlowChanged += allowed => _received["allow_glow"] = allowed;
        server.AllowSsaoChanged += allowed => _received["allow_ssao"] = allowed;
        server.AllowSsilChanged += allowed => _received["allow_ssil"] = allowed;
        server.AllowSsrChanged += allowed => _received["allow_ssr"] = allowed;
        server.AllowFogChanged += allowed => _received["allow_fog"] = allowed;
        server.AllowVolumetricFogChanged += allowed => _received["allow_volumetric_fog"] = allowed;
        server.BrightnessChanged += brightness => _received["brightness"] = brightness;
        server.ContrastChanged += contrast => _received["contrast"] = contrast;
        server.SaturationChanged += saturation => _received["saturation"] = saturation;
        RenderEffectSettingsServer.SetAllowGlow(false);
        RenderEffectSettingsServer.SetAllowSsao(false);
        RenderEffectSettingsServer.SetAllowSsil(false);
        RenderEffectSettingsServer.SetAllowSsr(false);
        RenderEffectSettingsServer.SetAllowFog(false);
        RenderEffectSettingsServer.SetAllowVolumetricFog(false);
        RenderEffectSettingsServer.SetBrightness(1.5f);
        RenderEffectSettingsServer.SetContrast(1.25f);
        RenderEffectSettingsServer.SetSaturation(0.5f);
        Expect(!(bool)_received["allow_glow"], "AllowGlowChanged passes the flag");
        Expect(!(bool)_received["allow_ssao"], "AllowSsaoChanged passes the flag");
        Expect(!(bool)_received["allow_ssil"], "AllowSsilChanged passes the flag");
        Expect(!(bool)_received["allow_ssr"], "AllowSsrChanged passes the flag");
        Expect(!(bool)_received["allow_fog"], "AllowFogChanged passes the flag");
        Expect(!(bool)_received["allow_volumetric_fog"], "AllowVolumetricFogChanged passes the flag");
        ExpectEqual((float)_received["brightness"], 1.5f, "BrightnessChanged passes the brightness");
        ExpectEqual((float)_received["contrast"], 1.25f, "ContrastChanged passes the contrast");
        ExpectEqual((float)_received["saturation"], 0.5f, "SaturationChanged passes the saturation");
        server.ResetToDefaults();
    }

    private void TestStorage()
    {
        RenderEffectSettingsServerInstance server = RenderEffectSettingsServer.Singleton;
        RenderEffectSettingsServer.SetSsilQuality(RenderingServer.EnvironmentSsilQuality.High);
        RenderEffectSettingsServer.SetAllowVolumetricFog(false);
        RenderEffectSettingsServer.SetContrast(1.25f);
        ExpectEqual(server.Save(), Error.Ok, "Save writes the file");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load reads the file");
        ExpectEqual(RenderEffectSettingsServer.GetSsilQuality(), RenderingServer.EnvironmentSsilQuality.High, "the SSIL quality is loaded, also where the renderer ignores it");
        Expect(!RenderEffectSettingsServer.IsVolumetricFogAllowed(), "the fog switch is loaded");
        ExpectEqual(RenderEffectSettingsServer.GetContrast(), 1.25f, "the contrast is loaded");
    }

    private void TestResource()
    {
        var settings = new RenderEffectSettings();
        ExpectEqual(settings.SsaoQuality, RenderingServer.EnvironmentSsaoQuality.Medium, "RenderEffectSettings: default SsaoQuality");
        Expect(settings.SsaoHalfSize, "RenderEffectSettings: SsaoHalfSize by default");
        ExpectEqual(settings.SsilBlurPasses, 4, "RenderEffectSettings: default SsilBlurPasses");
        ExpectEqual(settings.VolumetricFogVolumeSize, 64, "RenderEffectSettings: default VolumetricFogVolumeSize");
        ExpectEqual(settings.DofBokehShape, RenderingServer.DofBokehShape.Hexagon, "RenderEffectSettings: default DofBokehShape");
        Expect(settings.AllowSsr, "RenderEffectSettings: AllowSsr by default");
        ExpectEqual(settings.Brightness, 1.0f, "RenderEffectSettings: default Brightness");
        Expect(Mathf.IsEqualApprox(settings.SssDepthScale, 0.01f), "RenderEffectSettings: default SssDepthScale");
        settings.Brightness = 1.5f;
        ExpectEqual(settings.Brightness, 1.5f, "RenderEffectSettings: Brightness is a plain property");
    }
}
