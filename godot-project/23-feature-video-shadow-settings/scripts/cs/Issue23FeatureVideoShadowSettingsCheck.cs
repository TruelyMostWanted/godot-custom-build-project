using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue23FeatureVideoShadowSettings;

/// <summary>
/// C# checks of 23-feature-video-shadow-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>.
/// </summary>
public partial class Issue23FeatureVideoShadowSettingsCheck : FeatureCheckCs
{
    private const string ShadowFile = "user://settings/video_shadow.ini";

    private static readonly string[] Names =
    {
        "directional_shadow_atlas_size", "directional_shadow_16_bits", "directional_soft_shadow_filter_quality",
        "positional_shadow_atlas_size", "positional_shadow_atlas_16_bits", "positional_shadow_atlas_quad_0",
        "positional_shadow_atlas_quad_1", "positional_shadow_atlas_quad_2", "positional_shadow_atlas_quad_3",
        "positional_soft_shadow_filter_quality",
    };

    // Enum values by number: the C# names of the numbered constants (1, 16, 64 shadows) are not needed for the checks.
    private static readonly Viewport.PositionalShadowAtlasQuadrantSubdiv Subdiv1 = (Viewport.PositionalShadowAtlasQuadrantSubdiv)1;
    private static readonly Viewport.PositionalShadowAtlasQuadrantSubdiv Subdiv16 = (Viewport.PositionalShadowAtlasQuadrantSubdiv)3;
    private static readonly Viewport.PositionalShadowAtlasQuadrantSubdiv Subdiv64 = (Viewport.PositionalShadowAtlasQuadrantSubdiv)4;

    protected override string CheckName => "23-feature-video-shadow-settings";

    protected override async Task RunChecks()
    {
        await CheckTypedSignals();

        var server = VideoShadowSettingsServer.Singleton;
        Expect(Engine.HasSingleton("VideoShadowSettingsServer"), "VideoShadowSettingsServer is a registered singleton");
        Expect(server is VideoShadowSettingsServerInstance, "VideoShadowSettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("VideoShadowSettingsServer") is SettingsServer, "VideoShadowSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("video_shadow") == server, "the domain is registered as \"video_shadow\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Scene, "applied after the root viewport exists");
        ExpectEqual(server.GetFilePath(), ShadowFile, "file path");
        Expect(server.GetSettings() is VideoShadowSettings, "the settings resource is a VideoShadowSettings");
        var names = server.GetSettingNames();
        ExpectEqual(names.Length, Names.Length, "10 values");
        foreach (string setting in Names)
        {
            Expect(Array.IndexOf(names, setting) >= 0, $"setting \"{setting}\" exists");
            Expect(!server.GetSettingFlags(setting).HasFlag(SettingsServer.SettingFlags.RestartRequired), $"\"{setting}\" takes effect at once");
        }

        server.ResetToDefaults();
        CheckDefaults();
        CheckSupport();
        CheckApply();
        CheckViewports();
        await CheckGroup();
        CheckValidation();
        CheckStorage();
        await CheckScene();

        // Leave no player file and no changed values behind.
        server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(ShadowFile);
    }

    private static Variant Project(string path) => ProjectSettings.GetSettingWithOverride(path);

    private void CheckDefaults()
    {
        var server = VideoShadowSettingsServer.Singleton;
        var root = GetTree().Root;
        ExpectEqual(server.GetDefaultValue("directional_shadow_atlas_size").AsInt32(), Project("rendering/lights_and_shadows/directional_shadow/size").AsInt32(), "default directional size is the project setting");
        ExpectEqual(server.GetDefaultValue("directional_soft_shadow_filter_quality").AsInt32(), Project("rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality").AsInt32(), "default directional quality is the project setting");
        ExpectEqual(server.GetDefaultValue("positional_shadow_atlas_size").AsInt32(), Project("rendering/lights_and_shadows/positional_shadow/atlas_size").AsInt32(), "default positional size is the project setting");
        ExpectEqual(server.GetDefaultValue("positional_shadow_atlas_quad_3").AsInt32(), Project("rendering/lights_and_shadows/positional_shadow/atlas_quadrant_3_subdiv").AsInt32(), "default quadrant 3 is the project setting");
        ExpectEqual(root.PositionalShadowAtlasSize, VideoShadowSettingsServer.GetPositionalShadowAtlasSize(), "the root viewport has the default positional size");
        ExpectEqual(root.PositionalShadowAtlasQuad0, VideoShadowSettingsServer.GetPositionalShadowAtlasQuadrantSubdiv(0), "the root viewport has the default quadrant 0");
    }

    private void CheckSupport()
    {
        string method = RenderingServer.GetCurrentRenderingMethod();
        var unsupported = VideoShadowSettingsServer.GetUnsupportedSettingNames();
        ExpectEqual(unsupported.Length, 0, "every renderer uses every value");
        foreach (string setting in Names)
        {
            Expect(VideoShadowSettingsServer.Singleton.IsValueSupported(setting), $"\"{setting}\" is supported");
            Expect(VideoShadowSettingsServer.IsRendererSupported(setting), $"IsRendererSupported(\"{setting}\")");
        }
        GD.Print($"    renderer: {method}, unsupported: [{string.Join(", ", unsupported)}]");
    }

    private void CheckApply()
    {
        var server = VideoShadowSettingsServer.Singleton;
        var root = GetTree().Root;
        VideoShadowSettingsServer.SetPositionalShadowAtlasSize(2048);
        ExpectEqual(root.PositionalShadowAtlasSize, 2048, "positional_shadow_atlas_size reaches the root at once");
        VideoShadowSettingsServer.SetPositionalShadowAtlas16Bits(false);
        Expect(!root.PositionalShadowAtlas16Bits, "positional_shadow_atlas_16_bits reaches the root at once");
        for (int i = 0; i < 4; i++)
        {
            VideoShadowSettingsServer.SetPositionalShadowAtlasQuadrantSubdiv(i, Subdiv16);
            ExpectEqual(root.GetPositionalShadowAtlasQuadrantSubdiv(i), Subdiv16, $"quadrant {i} reaches the root at once");
        }

        // Global values: no getter on RenderingServer, the domain keeps them.
        VideoShadowSettingsServer.SetDirectionalShadowAtlasSize(2048);
        ExpectEqual(VideoShadowSettingsServer.GetDirectionalShadowAtlasSize(), 2048, "directional_shadow_atlas_size is kept");
        VideoShadowSettingsServer.SetDirectionalShadow16Bits(false);
        Expect(!VideoShadowSettingsServer.IsDirectionalShadow16BitsEnabled(), "directional_shadow_16_bits is kept");
        VideoShadowSettingsServer.SetDirectionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.SoftHigh);
        ExpectEqual(VideoShadowSettingsServer.GetDirectionalSoftShadowFilterQuality(), RenderingServer.ShadowQuality.SoftHigh, "directional quality is kept");
        VideoShadowSettingsServer.SetPositionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.Hard);
        ExpectEqual(VideoShadowSettingsServer.GetPositionalSoftShadowFilterQuality(), RenderingServer.ShadowQuality.Hard, "positional quality is kept");

        // Changed on the root directly, read back.
        root.PositionalShadowAtlasSize = 1024;
        server.ReadFromEngine();
        ExpectEqual(VideoShadowSettingsServer.GetPositionalShadowAtlasSize(), 1024, "ReadFromEngine() reads the root back");
        ExpectEqual(VideoShadowSettingsServer.GetDirectionalShadowAtlasSize(), 2048, "ReadFromEngine() keeps the global values");

        server.ResetToDefaults();
        ExpectEqual(root.PositionalShadowAtlasSize, Project("rendering/lights_and_shadows/positional_shadow/atlas_size").AsInt32(), "ResetToDefaults() gives back the positional size");
        ExpectEqual(root.PositionalShadowAtlas16Bits, ProjectSettings.GetSetting("rendering/lights_and_shadows/positional_shadow/atlas_16_bits").AsBool(), "ResetToDefaults() gives back the bit depth");
    }

    private void CheckViewports()
    {
        var added = new SubViewport();
        VideoShadowSettingsServer.SetPositionalShadowAtlasSize(1024);
        VideoShadowSettingsServer.AddViewport(added);
        Expect(VideoShadowSettingsServer.HasViewport(added), "HasViewport()");
        ExpectEqual(added.PositionalShadowAtlasSize, 1024, "an added viewport gets the values at once");
        VideoShadowSettingsServer.SetPositionalShadowAtlasQuadrantSubdiv(2, Subdiv1);
        ExpectEqual(added.PositionalShadowAtlasQuad2, Subdiv1, "an added viewport gets later changes");
        VideoShadowSettingsServer.RemoveViewport(added);
        Expect(!VideoShadowSettingsServer.HasViewport(added), "RemoveViewport()");
        VideoShadowSettingsServer.SetPositionalShadowAtlasSize(512);
        ExpectEqual(added.PositionalShadowAtlasSize, 1024, "a removed viewport keeps its values");
        added.Free();
        VideoShadowSettingsServer.Singleton.ResetToDefaults();
    }

    private async Task CheckGroup()
    {
        ExpectEqual(VideoShadowSettingsServer.GetViewportGroup(), new StringName("video_shadow_settings"), "GetViewportGroup()");
        VideoShadowSettingsServer.SetPositionalShadowAtlasSize(1024);
        var grouped = new SubViewport();
        grouped.AddToGroup(VideoShadowSettingsServer.GetViewportGroup());
        AddChild(grouped);
        ExpectEqual(grouped.PositionalShadowAtlasSize, 1024, "a viewport in the group gets the values when it enters the tree");
        VideoShadowSettingsServer.SetPositionalShadowAtlas16Bits(false);
        Expect(!grouped.PositionalShadowAtlas16Bits, "a viewport in the group gets later changes");
        var other = new SubViewport();
        AddChild(other);
        ExpectEqual(other.PositionalShadowAtlasSize, 2048, "a viewport outside the group is untouched (Viewport default)");
        grouped.QueueFree();
        other.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        VideoShadowSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckValidation()
    {
        var server = VideoShadowSettingsServer.Singleton;
        VideoShadowSettingsServer.SetDirectionalShadowAtlasSize(3000);
        ExpectEqual(VideoShadowSettingsServer.GetDirectionalShadowAtlasSize(), 4096, "sizes are rounded up to a power of two");
        VideoShadowSettingsServer.SetDirectionalShadowAtlasSize(16);
        ExpectEqual(VideoShadowSettingsServer.GetDirectionalShadowAtlasSize(), 256, "the directional size is clamped to 256");
        VideoShadowSettingsServer.SetPositionalShadowAtlasSize(100000);
        ExpectEqual(VideoShadowSettingsServer.GetPositionalShadowAtlasSize(), 16384, "the positional size is clamped to 16384");
        VideoShadowSettingsServer.SetPositionalShadowAtlasSize(0);
        ExpectEqual(VideoShadowSettingsServer.GetPositionalShadowAtlasSize(), 0, "0 disables positional shadows");
        ExpectEqual(GetTree().Root.PositionalShadowAtlasSize, 0, "0 reaches the root");
        server.SetValue("directional_soft_shadow_filter_quality", 9.0);
        ExpectEqual(server.GetValue("directional_soft_shadow_filter_quality").AsInt32(), server.GetDefaultValue("directional_soft_shadow_filter_quality").AsInt32(), "an invalid quality falls back to the default");
        server.ResetToDefaults();
    }

    private void CheckStorage()
    {
        var server = VideoShadowSettingsServer.Singleton;
        VideoShadowSettingsServer.SetDirectionalShadowAtlasSize(2048);
        VideoShadowSettingsServer.SetPositionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.SoftUltra);
        VideoShadowSettingsServer.SetPositionalShadowAtlasQuadrantSubdiv(1, Subdiv64);
        ExpectEqual(server.Save(), Error.Ok, "Save()");
        Expect(FileAccess.FileExists(ShadowFile), "the file exists");
        Expect(FileAccess.GetFileAsString(ShadowFile).Contains("directional_shadow_atlas_size"), "the INI file has directional_shadow_atlas_size");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load()");
        ExpectEqual(VideoShadowSettingsServer.GetDirectionalShadowAtlasSize(), 2048, "the directional size is loaded");
        ExpectEqual(VideoShadowSettingsServer.GetPositionalSoftShadowFilterQuality(), RenderingServer.ShadowQuality.SoftUltra, "the positional quality is loaded");
        ExpectEqual(VideoShadowSettingsServer.GetPositionalShadowAtlasQuadrantSubdiv(1), Subdiv64, "quadrant 1 is loaded");
        server.Apply();
        ExpectEqual(GetTree().Root.PositionalShadowAtlasQuad1, Subdiv64, "the loaded value reaches the root after Apply()");
        server.ResetToDefaults();
    }

    // A box on a floor, lit by a directional and an omni light with shadows.
    private Node3D BuildScene()
    {
        var scene = new Node3D();
        AddChild(scene);
        var camera = new Camera3D { Position = new Vector3(0, 3, 5) };
        scene.AddChild(camera);
        camera.LookAt(Vector3.Zero);
        camera.Current = true;
        scene.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(10, 10) } });
        scene.AddChild(new MeshInstance3D { Mesh = new BoxMesh(), Position = new Vector3(0, 0.5f, 0) });
        scene.AddChild(new DirectionalLight3D { ShadowEnabled = true, RotationDegrees = new Vector3(-50, 30, 0) });
        scene.AddChild(new OmniLight3D { ShadowEnabled = true, OmniRange = 8.0f, Position = new Vector3(1.5f, 2, 1.5f) });
        return scene;
    }

    private async Task<byte[]> FrameData()
    {
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        return GetTree().Root.GetTexture().GetImage().GetData();
    }

    // Milliseconds of the first frame after a change (the atlas is reallocated there).
    private async Task<double> Measure(Action change)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        change();
        ulong start = Time.GetTicksUsec();
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        return (Time.GetTicksUsec() - start) / 1000.0;
    }

    private async Task CheckScene()
    {
        if (DisplayServer.GetName() == "headless")
        {
            GD.Print("    headless: shadow scene skipped");
            return;
        }
        var scene = BuildScene();
        for (int i = 0; i < 5; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        string method = RenderingServer.GetCurrentRenderingMethod();

        byte[] before = await FrameData();
        double ms = await Measure(() => VideoShadowSettingsServer.SetDirectionalShadowAtlasSize(256));
        byte[] after = await FrameData();
        Expect(!before.AsSpan().SequenceEqual(after), "the directional shadow changes at once (4096 -> 256)");
        GD.Print($"    [{method}] directional atlas 256: {ms:F2} ms");
        ms = await Measure(() => VideoShadowSettingsServer.SetDirectionalShadowAtlasSize(8192));
        GD.Print($"    [{method}] directional atlas 8192: {ms:F2} ms");

        before = await FrameData();
        ms = await Measure(() => VideoShadowSettingsServer.SetPositionalShadowAtlasSize(0));
        after = await FrameData();
        Expect(!before.AsSpan().SequenceEqual(after), "the omni shadow disappears at once (positional atlas 0)");
        GD.Print($"    [{method}] positional atlas 0: {ms:F2} ms");

        VideoShadowSettingsServer.SetDirectionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.Hard);
        VideoShadowSettingsServer.SetPositionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.Hard);
        byte[] hard = await FrameData();
        VideoShadowSettingsServer.SetDirectionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.SoftUltra);
        VideoShadowSettingsServer.SetPositionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.SoftUltra);
        byte[] ultra = await FrameData();
        Expect(!hard.AsSpan().SequenceEqual(ultra), "the soft shadow quality changes the shadows at once (Hard -> Ultra)");

        scene.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.</summary>
    private async Task CheckTypedSignals()
    {
        if (!Engine.HasSingleton("VideoShadowSettingsServer"))
        {
            return;
        }
        var s = (SettingsServer)Engine.GetSingleton("VideoShadowSettingsServer");
        Expect(s.HasSignal("directional_shadow_atlas_size_changed"), "VideoShadowSettingsServer has the typed signal directional_shadow_atlas_size_changed");
        string filePath = s.GetFilePath();
        bool hadFile = FileAccess.FileExists(filePath);
        var previousMode = s.ApplyMode;
        s.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        s.ResetValue("directional_shadow_atlas_size");
        long newValue = s.GetAppliedValue("directional_shadow_atlas_size").AsInt64() != 2048L ? 2048L : 1024L;
        var received = new System.Collections.Generic.List<Variant>();
        var onChanged = Callable.From((Variant v) => received.Add(v));
        s.Connect("directional_shadow_atlas_size_changed", onChanged);

        s.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        s.SetValue("directional_shadow_atlas_size", newValue);
        ExpectEqual(received.Count, 0, "directional_shadow_atlas_size_changed does not fire for a pending change in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 1, "directional_shadow_atlas_size_changed fires once on Apply()");
        if (received.Count > 0)
        {
            ExpectEqual(received[0].VariantType, Variant.Type.Int, "directional_shadow_atlas_size_changed passes a typed value");
            ExpectEqual(received[0].AsInt64(), newValue, "directional_shadow_atlas_size_changed passes the applied value");
        }
        s.Apply();
        ExpectEqual(received.Count, 1, "directional_shadow_atlas_size_changed does not fire when nothing changed");
        s.ResetValue("directional_shadow_atlas_size");
        ExpectEqual(received.Count, 1, "directional_shadow_atlas_size_changed waits for Apply() after a reset in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 2, "directional_shadow_atlas_size_changed fires again when the default is applied");

        s.Disconnect("directional_shadow_atlas_size_changed", onChanged);
        s.ApplyMode = previousMode;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!hadFile)
        {
            DirAccess.RemoveAbsolute(filePath);
        }
    }
}
