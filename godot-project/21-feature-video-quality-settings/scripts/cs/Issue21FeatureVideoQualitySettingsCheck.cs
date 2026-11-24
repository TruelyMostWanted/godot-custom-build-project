using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue21FeatureVideoQualitySettings;

/// <summary>
/// C# checks of 21-feature-video-quality-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. Values the current renderer ignores are stored, but not pushed; the checks follow IsValueSupported().
/// </summary>
public partial class Issue21FeatureVideoQualitySettingsCheck : FeatureCheckCs
{
    private const string QualityFile = "user://settings/video_quality.ini";

    private static readonly string[] Names =
    {
        "scaling_3d_mode", "scaling_3d_scale", "fsr_sharpness", "mesh_lod_threshold", "msaa_2d", "msaa_3d", "screen_space_aa",
        "use_taa", "use_debanding", "vrs_mode", "use_occlusion_culling", "occlusion_rays_per_thread", "occlusion_culling_build_quality",
    };

    protected override string CheckName => "21-feature-video-quality-settings";

    protected override async Task RunChecks()
    {
        var server = VideoQualitySettingsServer.Singleton;
        Expect(Engine.HasSingleton("VideoQualitySettingsServer"), "VideoQualitySettingsServer is a registered singleton");
        Expect(server is VideoQualitySettingsServerInstance, "VideoQualitySettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("VideoQualitySettingsServer") is SettingsServer, "VideoQualitySettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("video_quality") == server, "the domain is registered as \"video_quality\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Scene, "applied after the root viewport exists");
        ExpectEqual(server.GetFilePath(), QualityFile, "file path");
        Expect(server.GetSettings() is VideoQualitySettings, "the settings resource is a VideoQualitySettings");
        var names = server.GetSettingNames();
        ExpectEqual(names.Length, Names.Length, "13 values");
        foreach (string setting in Names)
        {
            Expect(Array.IndexOf(names, setting) >= 0, $"setting \"{setting}\" exists");
        }

        server.ResetToDefaults();
        CheckDefaults();
        CheckSupport();
        CheckApply();
        CheckViewports();
        await CheckGroup();
        CheckValidation();
        CheckStorage();

        // Leave no player file and no changed root values behind.
        server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(QualityFile);
    }

    private static bool Supported(string setting) => VideoQualitySettingsServer.Singleton.IsValueSupported(setting);

    private static bool Approx(double a, double b) => Mathf.IsEqualApprox(a, b);

    private void CheckDefaults()
    {
        var server = VideoQualitySettingsServer.Singleton;
        ExpectEqual(server.GetDefaultValue("msaa_3d").AsInt32(), ProjectSettings.GetSetting("rendering/anti_aliasing/quality/msaa_3d").AsInt32(), "default msaa_3d is the project setting");
        ExpectEqual(server.GetDefaultValue("scaling_3d_mode").AsInt32(), ProjectSettings.GetSetting("rendering/scaling_3d/mode").AsInt32(), "default scaling mode is the project setting");
        Expect(Approx(server.GetDefaultValue("scaling_3d_scale").AsDouble(), ProjectSettings.GetSetting("rendering/scaling_3d/scale").AsDouble()), "default scale is the project setting");
        Expect(Approx(server.GetDefaultValue("mesh_lod_threshold").AsDouble(), ProjectSettings.GetSetting("rendering/mesh_lod/lod_change/threshold_pixels").AsDouble()), "default LOD threshold is the project setting");
        ExpectEqual(server.GetDefaultValue("use_occlusion_culling").AsBool(), ProjectSettings.GetSetting("rendering/occlusion_culling/use_occlusion_culling").AsBool(), "default occlusion culling is the project setting");
        ExpectEqual(server.GetDefaultValue("occlusion_rays_per_thread").AsInt32(), ProjectSettings.GetSetting("rendering/occlusion_culling/occlusion_rays_per_thread").AsInt32(), "default occlusion rays is the project setting");
        ExpectEqual(GetTree().Root.Msaa3D, VideoQualitySettingsServer.GetMsaa3D(), "the root viewport has the default msaa_3d");
    }

    private void CheckSupport()
    {
        string method = RenderingServer.GetCurrentRenderingMethod();
        var unsupported = VideoQualitySettingsServer.GetUnsupportedSettingNames();
        foreach (string setting in Names)
        {
            ExpectEqual(Array.IndexOf(unsupported, setting) >= 0, !Supported(setting), $"GetUnsupportedSettingNames() matches IsValueSupported(\"{setting}\")");
            ExpectEqual(Supported(setting), VideoQualitySettingsServer.IsRendererSupported(setting), $"IsRendererSupported(\"{setting}\")");
        }
        foreach (string setting in new[] { "scaling_3d_mode", "scaling_3d_scale", "mesh_lod_threshold", "msaa_2d", "msaa_3d" })
        {
            Expect(Supported(setting), $"\"{setting}\" works in every renderer");
        }
        Expect(VideoQualitySettingsServer.IsScaling3DModeSupported(Viewport.Scaling3DModeEnum.Bilinear), "bilinear scaling works everywhere");
        Expect(VideoQualitySettingsServer.IsScaling3DModeSupported(Viewport.Scaling3DModeEnum.Nearest), "nearest scaling works everywhere");
        if (method == "gl_compatibility")
        {
            foreach (string setting in new[] { "fsr_sharpness", "screen_space_aa", "use_taa", "use_debanding", "vrs_mode" })
            {
                Expect(!Supported(setting), $"compatibility: \"{setting}\" is unsupported");
            }
            Expect(!VideoQualitySettingsServer.IsScaling3DModeSupported(Viewport.Scaling3DModeEnum.Fsr), "compatibility: no FSR");
        }
        else if (method == "mobile")
        {
            Expect(!Supported("use_taa"), "mobile: no TAA");
            Expect(!VideoQualitySettingsServer.IsScaling3DModeSupported(Viewport.Scaling3DModeEnum.Fsr2), "mobile: no FSR 2");
        }
        else if (method == "forward_plus")
        {
            Expect(VideoQualitySettingsServer.IsScaling3DModeSupported(Viewport.Scaling3DModeEnum.Fsr2), "forward+: FSR 2");
            Expect(Supported("use_taa"), "forward+: TAA");
        }
    }

    private void CheckApply()
    {
        var server = VideoQualitySettingsServer.Singleton;
        var root = GetTree().Root;
        VideoQualitySettingsServer.SetScaling3DMode(Viewport.Scaling3DModeEnum.Nearest);
        ExpectEqual(root.Scaling3DMode, Viewport.Scaling3DModeEnum.Nearest, "scaling_3d_mode reaches the root at once");
        VideoQualitySettingsServer.SetScaling3DScale(0.5f);
        Expect(Approx(root.Scaling3DScale, 0.5), "scaling_3d_scale reaches the root at once");
        VideoQualitySettingsServer.SetMeshLodThreshold(8.0f);
        Expect(Approx(root.MeshLodThreshold, 8.0), "mesh_lod_threshold reaches the root at once");
        VideoQualitySettingsServer.SetMsaa2D(Viewport.Msaa.Msaa4X);
        ExpectEqual(root.Msaa2D, Viewport.Msaa.Msaa4X, "msaa_2d reaches the root at once");
        VideoQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa2X);
        ExpectEqual(root.Msaa3D, Viewport.Msaa.Msaa2X, "msaa_3d reaches the root at once");

        // Pushed where the renderer supports them, stored either way.
        VideoQualitySettingsServer.SetFsrSharpness(1.0f);
        VideoQualitySettingsServer.SetScreenSpaceAA(Viewport.ScreenSpaceAAEnum.Fxaa);
        VideoQualitySettingsServer.SetUseTaa(true);
        VideoQualitySettingsServer.SetUseDebanding(true);
        VideoQualitySettingsServer.SetUseOcclusionCulling(true);
        ExpectEqual(VideoQualitySettingsServer.GetScreenSpaceAA(), Viewport.ScreenSpaceAAEnum.Fxaa, "screen_space_aa is stored");
        Expect(VideoQualitySettingsServer.IsUsingTaa(), "use_taa is stored");
        if (Supported("fsr_sharpness"))
        {
            Expect(Approx(root.FsrSharpness, 1.0), "fsr_sharpness reaches the root at once");
        }
        ExpectEqual(root.ScreenSpaceAA == Viewport.ScreenSpaceAAEnum.Fxaa, Supported("screen_space_aa"), "screen_space_aa reaches the root only if supported");
        ExpectEqual(root.UseTaa, Supported("use_taa"), "use_taa reaches the root only if supported");
        ExpectEqual(root.UseDebanding, Supported("use_debanding"), "use_debanding reaches the root only if supported");
        ExpectEqual(root.UseOcclusionCulling, Supported("use_occlusion_culling"), "use_occlusion_culling reaches the root only if supported");
        if (Supported("vrs_mode"))
        {
            VideoQualitySettingsServer.SetVrsMode(Viewport.VrsModeEnum.Texture);
            ExpectEqual(root.VrsMode, Viewport.VrsModeEnum.Texture, "vrs_mode reaches the root at once");
        }

        // A scaling mode the renderer can't use is stored; the root scales bilinearly.
        VideoQualitySettingsServer.SetScaling3DMode(Viewport.Scaling3DModeEnum.Fsr2);
        ExpectEqual(VideoQualitySettingsServer.GetScaling3DMode(), Viewport.Scaling3DModeEnum.Fsr2, "FSR 2 is stored");
        var expectedMode = VideoQualitySettingsServer.IsScaling3DModeSupported(Viewport.Scaling3DModeEnum.Fsr2) ? Viewport.Scaling3DModeEnum.Fsr2 : Viewport.Scaling3DModeEnum.Bilinear;
        ExpectEqual(root.Scaling3DMode, expectedMode, "the root gets FSR 2 or the bilinear fallback");

        // The global occlusion values: no getter on RenderingServer, the domain keeps them.
        VideoQualitySettingsServer.SetOcclusionRaysPerThread(256);
        ExpectEqual(VideoQualitySettingsServer.GetOcclusionRaysPerThread(), 256, "occlusion_rays_per_thread is kept");
        VideoQualitySettingsServer.SetOcclusionCullingBuildQuality(RenderingServer.ViewportOcclusionCullingBuildQuality.Low);
        ExpectEqual(VideoQualitySettingsServer.GetOcclusionCullingBuildQuality(), RenderingServer.ViewportOcclusionCullingBuildQuality.Low, "occlusion_culling_build_quality is kept");

        // Changed on the root directly, read back.
        root.Msaa2D = Viewport.Msaa.Msaa8X;
        server.ReadFromEngine();
        ExpectEqual(VideoQualitySettingsServer.GetMsaa2D(), Viewport.Msaa.Msaa8X, "ReadFromEngine() reads the root back");
        Expect(VideoQualitySettingsServer.IsUsingTaa(), "ReadFromEngine() keeps skipped values as stored");

        server.ResetToDefaults();
        ExpectEqual((int)root.Msaa2D, ProjectSettings.GetSetting("rendering/anti_aliasing/quality/msaa_2d").AsInt32(), "ResetToDefaults() gives back msaa_2d");
        Expect(Approx(root.Scaling3DScale, ProjectSettings.GetSetting("rendering/scaling_3d/scale").AsDouble()), "ResetToDefaults() gives back the scale");
    }

    private void CheckViewports()
    {
        var added = new SubViewport();
        VideoQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa4X);
        VideoQualitySettingsServer.AddViewport(added);
        Expect(VideoQualitySettingsServer.HasViewport(added), "HasViewport()");
        ExpectEqual(added.Msaa3D, Viewport.Msaa.Msaa4X, "an added viewport gets the values at once");
        VideoQualitySettingsServer.SetMeshLodThreshold(16.0f);
        Expect(Approx(added.MeshLodThreshold, 16.0), "an added viewport gets later changes");
        VideoQualitySettingsServer.RemoveViewport(added);
        Expect(!VideoQualitySettingsServer.HasViewport(added), "RemoveViewport()");
        VideoQualitySettingsServer.SetMeshLodThreshold(2.0f);
        Expect(Approx(added.MeshLodThreshold, 16.0), "a removed viewport keeps its values");
        added.Free();
        VideoQualitySettingsServer.Singleton.ResetToDefaults();
    }

    private async Task CheckGroup()
    {
        ExpectEqual(VideoQualitySettingsServer.GetViewportGroup(), (StringName)"video_quality_settings", "GetViewportGroup()");
        VideoQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa4X);
        var grouped = new SubViewport();
        grouped.AddToGroup(VideoQualitySettingsServer.GetViewportGroup());
        AddChild(grouped);
        ExpectEqual(grouped.Msaa3D, Viewport.Msaa.Msaa4X, "a viewport in the group gets the values when it enters the tree");
        VideoQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa8X);
        ExpectEqual(grouped.Msaa3D, Viewport.Msaa.Msaa8X, "a viewport in the group gets later changes");
        var other = new SubViewport();
        AddChild(other);
        ExpectEqual(other.Msaa3D, Viewport.Msaa.Disabled, "a viewport outside the group is untouched");
        grouped.QueueFree();
        other.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        VideoQualitySettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckValidation()
    {
        var server = VideoQualitySettingsServer.Singleton;
        VideoQualitySettingsServer.SetScaling3DScale(5.0f);
        Expect(Approx(VideoQualitySettingsServer.GetScaling3DScale(), 2.0), "the scale is clamped to 2.0");
        VideoQualitySettingsServer.SetOcclusionRaysPerThread(0);
        ExpectEqual(VideoQualitySettingsServer.GetOcclusionRaysPerThread(), 1, "the occlusion rays are clamped to 1");
        server.SetValue("msaa_3d", 9);
        ExpectEqual(server.GetValue("msaa_3d").AsInt32(), server.GetDefaultValue("msaa_3d").AsInt32(), "an invalid MSAA value falls back to the default");
        server.ResetToDefaults();
    }

    private void CheckStorage()
    {
        var server = VideoQualitySettingsServer.Singleton;
        VideoQualitySettingsServer.SetScaling3DScale(0.75f);
        VideoQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa2X);
        VideoQualitySettingsServer.SetUseTaa(true);
        ExpectEqual(server.Save(), Error.Ok, "Save()");
        Expect(FileAccess.FileExists(QualityFile), "the file exists");
        Expect(FileAccess.GetFileAsString(QualityFile).Contains("msaa_3d"), "the INI file has msaa_3d");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load()");
        Expect(Approx(VideoQualitySettingsServer.GetScaling3DScale(), 0.75), "the scale is loaded");
        ExpectEqual(VideoQualitySettingsServer.GetMsaa3D(), Viewport.Msaa.Msaa2X, "msaa_3d is loaded");
        Expect(VideoQualitySettingsServer.IsUsingTaa(), "use_taa is loaded (also where the renderer ignores it)");
        ExpectEqual(GetTree().Root.Msaa3D, Viewport.Msaa.Msaa2X, "the loaded value reaches the root");
    }
}
