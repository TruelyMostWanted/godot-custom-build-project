using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.RenderQualitySettings;

using RenderQualitySettings = Godot.RenderQualitySettings;

/// <summary>
/// Full API test of Render Quality Settings (RenderQualitySettingsServer, RenderQualitySettings). Same tests as scripts/gd/Test.gd.
/// Values the current renderer ignores are stored but not pushed, so only the values every renderer supports are checked on the viewport.
/// </summary>
public partial class Test : FeatureTest
{
    private const string QualityFile = "user://settings/render_quality.ini";

    private static readonly string[] Names =
    [
        "scaling_3d_mode", "scaling_3d_scale", "fsr_sharpness", "mesh_lod_threshold", "msaa_2d", "msaa_3d", "screen_space_aa",
        "use_taa", "use_debanding", "vrs_mode", "use_occlusion_culling", "occlusion_rays_per_thread", "occlusion_culling_build_quality",
    ];

    private readonly Godot.Collections.Dictionary _received = [];

    protected override async Task RunTests()
    {
        RenderQualitySettingsServer.Singleton.ResetToDefaults();
        TestDefaults();
        TestValues();
        TestSupport();
        TestRootViewport();
        TestViewports();
        await TestGroup();
        TestValidation();
        TestSignals();
        TestStorage();
        TestResource();
        RenderQualitySettingsServer.Singleton.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(QualityFile);
    }

    private void TestDefaults()
    {
        int projectMsaa = (int)ProjectSettings.GetSetting("rendering/anti_aliasing/quality/msaa_3d");
        int projectMode = (int)ProjectSettings.GetSetting("rendering/scaling_3d/mode");
        float projectScale = (float)ProjectSettings.GetSetting("rendering/scaling_3d/scale");
        float projectLod = (float)ProjectSettings.GetSetting("rendering/mesh_lod/lod_change/threshold_pixels");
        ExpectEqual((int)RenderQualitySettingsServer.GetMsaa3D(), projectMsaa, "the default msaa_3d is the project setting");
        ExpectEqual((int)RenderQualitySettingsServer.GetScaling3DMode(), projectMode, "the default scaling mode is the project setting");
        ExpectEqual(RenderQualitySettingsServer.GetScaling3DScale(), projectScale, "the default scale is the project setting");
        ExpectEqual(RenderQualitySettingsServer.GetMeshLodThreshold(), projectLod, "the default LOD threshold is the project setting");
        ExpectEqual(GetTree().Root.Msaa3D, RenderQualitySettingsServer.GetMsaa3D(), "the root viewport has the default msaa_3d");
    }

    private void TestValues()
    {
        RenderQualitySettingsServer.SetScaling3DMode(Viewport.Scaling3DModeEnum.Nearest);
        ExpectEqual(RenderQualitySettingsServer.GetScaling3DMode(), Viewport.Scaling3DModeEnum.Nearest, "scaling_3d_mode round trip");
        RenderQualitySettingsServer.SetScaling3DScale(0.5f);
        ExpectEqual(RenderQualitySettingsServer.GetScaling3DScale(), 0.5f, "scaling_3d_scale round trip");
        RenderQualitySettingsServer.SetFsrSharpness(1.0f);
        ExpectEqual(RenderQualitySettingsServer.GetFsrSharpness(), 1.0f, "fsr_sharpness round trip");
        RenderQualitySettingsServer.SetMeshLodThreshold(8.0f);
        ExpectEqual(RenderQualitySettingsServer.GetMeshLodThreshold(), 8.0f, "mesh_lod_threshold round trip");
        RenderQualitySettingsServer.SetMsaa2D(Viewport.Msaa.Msaa4X);
        ExpectEqual(RenderQualitySettingsServer.GetMsaa2D(), Viewport.Msaa.Msaa4X, "msaa_2d round trip");
        RenderQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa2X);
        ExpectEqual(RenderQualitySettingsServer.GetMsaa3D(), Viewport.Msaa.Msaa2X, "msaa_3d round trip");
        RenderQualitySettingsServer.SetScreenSpaceAA(Viewport.ScreenSpaceAAEnum.Fxaa);
        ExpectEqual(RenderQualitySettingsServer.GetScreenSpaceAA(), Viewport.ScreenSpaceAAEnum.Fxaa, "screen_space_aa round trip");
        RenderQualitySettingsServer.SetUseTaa(true);
        Expect(RenderQualitySettingsServer.IsUsingTaa(), "use_taa round trip");
        RenderQualitySettingsServer.SetUseDebanding(true);
        Expect(RenderQualitySettingsServer.IsUsingDebanding(), "use_debanding round trip");
        RenderQualitySettingsServer.SetVrsMode(Viewport.VrsModeEnum.Texture);
        ExpectEqual(RenderQualitySettingsServer.GetVrsMode(), Viewport.VrsModeEnum.Texture, "vrs_mode round trip");
        RenderQualitySettingsServer.SetUseOcclusionCulling(true);
        Expect(RenderQualitySettingsServer.IsUsingOcclusionCulling(), "use_occlusion_culling round trip");
        RenderQualitySettingsServer.SetOcclusionRaysPerThread(256);
        ExpectEqual(RenderQualitySettingsServer.GetOcclusionRaysPerThread(), 256, "occlusion_rays_per_thread round trip");
        RenderQualitySettingsServer.SetOcclusionCullingBuildQuality(RenderingServer.ViewportOcclusionCullingBuildQuality.Low);
        ExpectEqual(RenderQualitySettingsServer.GetOcclusionCullingBuildQuality(), RenderingServer.ViewportOcclusionCullingBuildQuality.Low, "occlusion_culling_build_quality round trip");
        RenderQualitySettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSupport()
    {
        string[] unsupported = RenderQualitySettingsServer.GetUnsupportedSettingNames();
        foreach (string setting in Names)
        {
            bool supported = RenderQualitySettingsServer.Singleton.IsValueSupported(setting);
            ExpectEqual(System.Array.IndexOf(unsupported, setting) < 0, supported, $"GetUnsupportedSettingNames matches IsValueSupported({setting})");
            ExpectEqual(RenderQualitySettingsServer.IsRendererSupported(setting), supported, $"IsRendererSupported matches IsValueSupported({setting})");
        }
        Expect(RenderQualitySettingsServer.IsScaling3DModeSupported(Viewport.Scaling3DModeEnum.Bilinear), "bilinear scaling works everywhere");
        Expect(RenderQualitySettingsServer.IsScaling3DModeSupported(Viewport.Scaling3DModeEnum.Nearest), "nearest scaling works everywhere");
        Expect(RenderQualitySettingsServer.Singleton.IsValueSupported("msaa_3d"), "msaa_3d works in every renderer");
        Expect(RenderQualitySettingsServer.Singleton.IsValueSupported("scaling_3d_scale"), "scaling_3d_scale works in every renderer");
    }

    private void TestRootViewport()
    {
        Window root = GetTree().Root;
        RenderQualitySettingsServer.SetScaling3DMode(Viewport.Scaling3DModeEnum.Nearest);
        ExpectEqual(root.Scaling3DMode, Viewport.Scaling3DModeEnum.Nearest, "scaling_3d_mode reaches the root at once");
        RenderQualitySettingsServer.SetScaling3DScale(0.5f);
        ExpectEqual(root.Scaling3DScale, 0.5f, "scaling_3d_scale reaches the root at once");
        RenderQualitySettingsServer.SetMeshLodThreshold(8.0f);
        ExpectEqual(root.MeshLodThreshold, 8.0f, "mesh_lod_threshold reaches the root at once");
        RenderQualitySettingsServer.SetMsaa2D(Viewport.Msaa.Msaa4X);
        ExpectEqual(root.Msaa2D, Viewport.Msaa.Msaa4X, "msaa_2d reaches the root at once");
        RenderQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa2X);
        ExpectEqual(root.Msaa3D, Viewport.Msaa.Msaa2X, "msaa_3d reaches the root at once");
        root.Msaa2D = Viewport.Msaa.Msaa8X;
        RenderQualitySettingsServer.Singleton.ReadFromEngine();
        ExpectEqual(RenderQualitySettingsServer.GetMsaa2D(), Viewport.Msaa.Msaa8X, "ReadFromEngine reads the root back");
        RenderQualitySettingsServer.Singleton.ResetToDefaults();
        ExpectEqual(root.Msaa2D, Viewport.Msaa.Disabled, "ResetToDefaults gives msaa_2d back");
    }

    private void TestViewports()
    {
        var added = new SubViewport();
        RenderQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa4X);
        RenderQualitySettingsServer.AddViewport(added);
        Expect(RenderQualitySettingsServer.HasViewport(added), "AddViewport: HasViewport is true");
        ExpectEqual(added.Msaa3D, Viewport.Msaa.Msaa4X, "an added viewport gets the values at once");
        RenderQualitySettingsServer.SetMeshLodThreshold(16.0f);
        ExpectEqual(added.MeshLodThreshold, 16.0f, "an added viewport gets later changes");
        RenderQualitySettingsServer.RemoveViewport(added);
        Expect(!RenderQualitySettingsServer.HasViewport(added), "RemoveViewport: HasViewport is false");
        RenderQualitySettingsServer.SetMeshLodThreshold(2.0f);
        ExpectEqual(added.MeshLodThreshold, 16.0f, "a removed viewport keeps its values");
        added.Free();
        RenderQualitySettingsServer.Singleton.ResetToDefaults();
    }

    private async Task TestGroup()
    {
        StringName group = RenderQualitySettingsServer.GetViewportGroup();
        ExpectEqual(group, new StringName("render_quality_settings"), "GetViewportGroup returns the group name");
        RenderQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa4X);
        var grouped = new SubViewport();
        grouped.AddToGroup(group);
        AddChild(grouped);
        ExpectEqual(grouped.Msaa3D, Viewport.Msaa.Msaa4X, "a viewport in the group gets the values when it enters the tree");
        RenderQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa8X);
        ExpectEqual(grouped.Msaa3D, Viewport.Msaa.Msaa8X, "a viewport in the group gets later changes");
        var other = new SubViewport();
        AddChild(other);
        ExpectEqual(other.Msaa3D, Viewport.Msaa.Disabled, "a viewport outside the group is untouched");
        grouped.QueueFree();
        other.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderQualitySettingsServer.Singleton.ResetToDefaults();
    }

    private void TestValidation()
    {
        RenderQualitySettingsServer.SetScaling3DScale(5.0f);
        ExpectEqual(RenderQualitySettingsServer.GetScaling3DScale(), 2.0f, "the scale is clamped to 2.0");
        RenderQualitySettingsServer.SetOcclusionRaysPerThread(0);
        ExpectEqual(RenderQualitySettingsServer.GetOcclusionRaysPerThread(), 1, "the occlusion rays are clamped to 1");
        RenderQualitySettingsServer.Singleton.SetValue("msaa_3d", 9);
        ExpectEqual(RenderQualitySettingsServer.GetMsaa3D(), Viewport.Msaa.Disabled, "an invalid MSAA value is rejected and the old value is kept");
        RenderQualitySettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSignals()
    {
        RenderQualitySettingsServerInstance server = RenderQualitySettingsServer.Singleton;
        server.Scaling3DModeChanged += mode => _received["scaling_3d_mode"] = (int)mode;
        server.Scaling3DScaleChanged += scale => _received["scaling_3d_scale"] = scale;
        server.FsrSharpnessChanged += sharpness => _received["fsr_sharpness"] = sharpness;
        server.MeshLodThresholdChanged += threshold => _received["mesh_lod_threshold"] = threshold;
        server.Msaa2DChanged += msaa => _received["msaa_2d"] = (int)msaa;
        server.Msaa3DChanged += msaa => _received["msaa_3d"] = (int)msaa;
        server.ScreenSpaceAAChanged += mode => _received["screen_space_aa"] = (int)mode;
        server.UseTaaChanged += enabled => _received["use_taa"] = enabled;
        server.UseDebandingChanged += enabled => _received["use_debanding"] = enabled;
        RenderQualitySettingsServer.SetScaling3DMode(Viewport.Scaling3DModeEnum.Nearest);
        RenderQualitySettingsServer.SetScaling3DScale(0.5f);
        RenderQualitySettingsServer.SetMeshLodThreshold(8.0f);
        RenderQualitySettingsServer.SetMsaa2D(Viewport.Msaa.Msaa4X);
        RenderQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa2X);
        ExpectEqual((int)_received["scaling_3d_mode"], (int)Viewport.Scaling3DModeEnum.Nearest, "Scaling3DModeChanged passes the mode");
        ExpectEqual((float)_received["scaling_3d_scale"], 0.5f, "Scaling3DScaleChanged passes the scale");
        ExpectEqual((float)_received["mesh_lod_threshold"], 8.0f, "MeshLodThresholdChanged passes the threshold");
        ExpectEqual((int)_received["msaa_2d"], (int)Viewport.Msaa.Msaa4X, "Msaa2DChanged passes the mode");
        ExpectEqual((int)_received["msaa_3d"], (int)Viewport.Msaa.Msaa2X, "Msaa3DChanged passes the mode");
        // The next four are only pushed, and so only signalled, where the renderer supports them.
        RenderQualitySettingsServer.SetFsrSharpness(1.0f);
        RenderQualitySettingsServer.SetScreenSpaceAA(Viewport.ScreenSpaceAAEnum.Fxaa);
        RenderQualitySettingsServer.SetUseTaa(true);
        RenderQualitySettingsServer.SetUseDebanding(true);
        if (server.IsValueSupported("fsr_sharpness"))
        {
            ExpectEqual((float)_received["fsr_sharpness"], 1.0f, "FsrSharpnessChanged passes the sharpness");
        }
        if (server.IsValueSupported("screen_space_aa"))
        {
            ExpectEqual((int)_received["screen_space_aa"], (int)Viewport.ScreenSpaceAAEnum.Fxaa, "ScreenSpaceAAChanged passes the mode");
        }
        if (server.IsValueSupported("use_taa"))
        {
            Expect((bool)_received["use_taa"], "UseTaaChanged passes the flag");
        }
        if (server.IsValueSupported("use_debanding"))
        {
            Expect((bool)_received["use_debanding"], "UseDebandingChanged passes the flag");
        }
        server.ResetToDefaults();
    }

    private void TestStorage()
    {
        RenderQualitySettingsServerInstance server = RenderQualitySettingsServer.Singleton;
        RenderQualitySettingsServer.SetScaling3DScale(0.75f);
        RenderQualitySettingsServer.SetMsaa3D(Viewport.Msaa.Msaa2X);
        RenderQualitySettingsServer.SetUseTaa(true);
        ExpectEqual(server.Save(), Error.Ok, "Save writes the file");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load reads the file");
        ExpectEqual(RenderQualitySettingsServer.GetScaling3DScale(), 0.75f, "the scale is loaded");
        ExpectEqual(RenderQualitySettingsServer.GetMsaa3D(), Viewport.Msaa.Msaa2X, "msaa_3d is loaded");
        Expect(RenderQualitySettingsServer.IsUsingTaa(), "use_taa is loaded, also where the renderer ignores it");
        server.Apply();
        ExpectEqual(GetTree().Root.Msaa3D, Viewport.Msaa.Msaa2X, "the loaded value reaches the root after Apply");
    }

    private void TestResource()
    {
        var settings = new RenderQualitySettings();
        ExpectEqual(settings.Scaling3DMode, Viewport.Scaling3DModeEnum.Bilinear, "RenderQualitySettings: default Scaling3DMode");
        ExpectEqual(settings.Scaling3DScale, 1.0f, "RenderQualitySettings: default Scaling3DScale");
        Expect(Mathf.IsEqualApprox(settings.FsrSharpness, 0.2f), "RenderQualitySettings: default FsrSharpness");
        ExpectEqual(settings.MeshLodThreshold, 1.0f, "RenderQualitySettings: default MeshLodThreshold");
        ExpectEqual(settings.Msaa3D, Viewport.Msaa.Disabled, "RenderQualitySettings: default Msaa3D");
        ExpectEqual(settings.OcclusionRaysPerThread, 512, "RenderQualitySettings: default OcclusionRaysPerThread");
        ExpectEqual(settings.OcclusionCullingBuildQuality, RenderingServer.ViewportOcclusionCullingBuildQuality.High, "RenderQualitySettings: default OcclusionCullingBuildQuality");
        settings.Msaa3D = Viewport.Msaa.Msaa8X;
        ExpectEqual(settings.Msaa3D, Viewport.Msaa.Msaa8X, "RenderQualitySettings: Msaa3D is a plain property");
    }
}
