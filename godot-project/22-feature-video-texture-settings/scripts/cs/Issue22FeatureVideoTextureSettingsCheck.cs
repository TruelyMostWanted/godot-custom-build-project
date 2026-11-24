using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue22FeatureVideoTextureSettings;

/// <summary>
/// C# checks of 22-feature-video-texture-settings. Check the same things as scripts/gd/check.gd.
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The streaming domain exists only with the texture_streaming module, so it is reached
/// through <c>Engine.GetSingleton()</c> and <c>Call()</c>, and the script also compiles against builds without it.
/// </summary>
public partial class Issue22FeatureVideoTextureSettingsCheck : FeatureCheckCs
{
    private const string TextureFile = "user://settings/video_texture.ini";
    private const string StreamingFile = "user://settings/video_texture_streaming.ini";

    private static readonly string[] Names =
    {
        "anisotropic_filtering_level", "texture_mipmap_bias", "light_projector_filter", "decal_filter",
        "canvas_item_default_texture_filter", "canvas_item_default_texture_repeat", "snap_2d_transforms_to_pixel",
        "snap_2d_vertices_to_pixel", "sdf_oversize", "sdf_scale", "canvas_shadow_atlas_size",
    };

    private static readonly string[] StreamingNames = { "enabled", "memory_budget_enabled", "memory_budget_mb", "min_lod", "max_lod" };

    // Enum values by number: the C# names of the numbered constants (2X, 16X, 100%) are not needed for the checks.
    private static readonly Viewport.AnisotropicFiltering Anisotropy2X = (Viewport.AnisotropicFiltering)1;
    private static readonly Viewport.AnisotropicFiltering Anisotropy8X = (Viewport.AnisotropicFiltering)3;
    private static readonly Viewport.AnisotropicFiltering Anisotropy16X = (Viewport.AnisotropicFiltering)4;
    private static readonly Viewport.SdfOversizeEnum SdfOversize200 = (Viewport.SdfOversizeEnum)3;
    private static readonly Viewport.SdfScaleEnum SdfScale100 = (Viewport.SdfScaleEnum)0;
    private static readonly Viewport.SdfScaleEnum SdfScale25 = (Viewport.SdfScaleEnum)2;

    protected override string CheckName => "22-feature-video-texture-settings";

    protected override async Task RunChecks()
    {
        await CheckTypedSignals();

        var server = VideoTextureSettingsServer.Singleton;
        Expect(Engine.HasSingleton("VideoTextureSettingsServer"), "VideoTextureSettingsServer is a registered singleton");
        Expect(server is VideoTextureSettingsServerInstance, "VideoTextureSettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("VideoTextureSettingsServer") is SettingsServer, "VideoTextureSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("video_texture") == server, "the domain is registered as \"video_texture\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Scene, "applied after the root viewport exists");
        ExpectEqual(server.GetFilePath(), TextureFile, "file path");
        Expect(server.GetSettings() is VideoTextureSettings, "the settings resource is a VideoTextureSettings");
        var names = server.GetSettingNames();
        ExpectEqual(names.Length, Names.Length, "11 values");
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
        await CheckStreaming();

        // Leave no player file and no changed root values behind.
        server.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(TextureFile);
    }

    private static bool Supported(string setting) => VideoTextureSettingsServer.Singleton.IsValueSupported(setting);

    private static Variant Project(string path) => ProjectSettings.GetSetting(path);

    private void CheckDefaults()
    {
        var server = VideoTextureSettingsServer.Singleton;
        var root = GetTree().Root;
        ExpectEqual(server.GetDefaultValue("anisotropic_filtering_level").AsInt32(), Project("rendering/textures/default_filters/anisotropic_filtering_level").AsInt32(), "default anisotropy is the project setting");
        ExpectEqual(server.GetDefaultValue("canvas_item_default_texture_filter").AsInt32(), Project("rendering/textures/canvas_textures/default_texture_filter").AsInt32(), "default canvas filter is the project setting");
        ExpectEqual(server.GetDefaultValue("snap_2d_transforms_to_pixel").AsBool(), Project("rendering/2d/snap/snap_2d_transforms_to_pixel").AsBool(), "default pixel snap is the project setting");
        ExpectEqual(server.GetDefaultValue("canvas_shadow_atlas_size").AsInt32(), Project("rendering/2d/shadow_atlas/size").AsInt32(), "default 2D shadow atlas is the project setting");
        ExpectEqual(root.AnisotropicFilteringLevel, VideoTextureSettingsServer.GetAnisotropicFilteringLevel(), "the root viewport has the default anisotropy");
        ExpectEqual(root.CanvasItemDefaultTextureFilter, VideoTextureSettingsServer.GetCanvasItemDefaultTextureFilter(), "the root viewport has the default canvas filter");
    }

    private void CheckSupport()
    {
        string method = RenderingServer.GetCurrentRenderingMethod();
        var unsupported = VideoTextureSettingsServer.GetUnsupportedSettingNames();
        foreach (string setting in Names)
        {
            ExpectEqual(Array.IndexOf(unsupported, setting) >= 0, !Supported(setting), $"GetUnsupportedSettingNames() matches IsValueSupported(\"{setting}\")");
            ExpectEqual(Supported(setting), VideoTextureSettingsServer.IsRendererSupported(setting), $"IsRendererSupported(\"{setting}\")");
        }
        ExpectEqual(Supported("decal_filter"), method != "gl_compatibility", "decal filter: not in Compatibility");
        ExpectEqual(Supported("light_projector_filter"), method != "gl_compatibility", "light projector filter: not in Compatibility");
        Expect(Supported("canvas_item_default_texture_filter"), "the canvas filter works in every renderer");
        GD.Print($"    renderer: {method}, unsupported: [{string.Join(", ", unsupported)}]");
    }

    private void CheckApply()
    {
        var server = VideoTextureSettingsServer.Singleton;
        var root = GetTree().Root;
        VideoTextureSettingsServer.SetAnisotropicFilteringLevel(Anisotropy16X);
        ExpectEqual(root.AnisotropicFilteringLevel, Anisotropy16X, "anisotropic_filtering_level reaches the root at once");
        VideoTextureSettingsServer.SetTextureMipmapBias(-0.5f);
        Expect(Mathf.IsEqualApprox(root.TextureMipmapBias, -0.5f), "texture_mipmap_bias reaches the root at once");
        VideoTextureSettingsServer.SetCanvasItemDefaultTextureFilter(Viewport.DefaultCanvasItemTextureFilter.Nearest);
        ExpectEqual(root.CanvasItemDefaultTextureFilter, Viewport.DefaultCanvasItemTextureFilter.Nearest, "canvas_item_default_texture_filter reaches the root at once");
        VideoTextureSettingsServer.SetCanvasItemDefaultTextureRepeat(Viewport.DefaultCanvasItemTextureRepeat.Mirror);
        ExpectEqual(root.CanvasItemDefaultTextureRepeat, Viewport.DefaultCanvasItemTextureRepeat.Mirror, "canvas_item_default_texture_repeat reaches the root at once");
        VideoTextureSettingsServer.SetSnap2DTransformsToPixel(true);
        Expect(root.Snap2DTransformsToPixel, "snap_2d_transforms_to_pixel reaches the root at once");
        VideoTextureSettingsServer.SetSnap2DVerticesToPixel(true);
        Expect(root.Snap2DVerticesToPixel, "snap_2d_vertices_to_pixel reaches the root at once");
        VideoTextureSettingsServer.SetSdfOversize(SdfOversize200);
        ExpectEqual(root.SdfOversize, SdfOversize200, "sdf_oversize reaches the root at once");
        VideoTextureSettingsServer.SetSdfScale(SdfScale100);
        ExpectEqual(root.SdfScale, SdfScale100, "sdf_scale reaches the root at once");

        // Global values: no getter on RenderingServer, the domain keeps them.
        VideoTextureSettingsServer.SetCanvasShadowAtlasSize(4096);
        ExpectEqual(VideoTextureSettingsServer.GetCanvasShadowAtlasSize(), 4096, "canvas_shadow_atlas_size is kept");
        VideoTextureSettingsServer.SetDecalFilter(RenderingServer.DecalFilter.Nearest);
        ExpectEqual(VideoTextureSettingsServer.GetDecalFilter(), RenderingServer.DecalFilter.Nearest, "decal_filter is stored");
        VideoTextureSettingsServer.SetLightProjectorFilter(RenderingServer.LightProjectorFilter.Linear);
        ExpectEqual(VideoTextureSettingsServer.GetLightProjectorFilter(), RenderingServer.LightProjectorFilter.Linear, "light_projector_filter is stored");

        // Changed on the root directly, read back.
        root.SdfScale = SdfScale25;
        server.ReadFromEngine();
        ExpectEqual(VideoTextureSettingsServer.GetSdfScale(), SdfScale25, "ReadFromEngine() reads the root back");
        ExpectEqual(VideoTextureSettingsServer.GetCanvasShadowAtlasSize(), 4096, "ReadFromEngine() keeps the global values");

        server.ResetToDefaults();
        ExpectEqual((int)root.AnisotropicFilteringLevel, Project("rendering/textures/default_filters/anisotropic_filtering_level").AsInt32(), "ResetToDefaults() gives back the anisotropy");
        ExpectEqual(root.Snap2DTransformsToPixel, Project("rendering/2d/snap/snap_2d_transforms_to_pixel").AsBool(), "ResetToDefaults() gives back the pixel snap");
    }

    private void CheckViewports()
    {
        var added = new SubViewport();
        VideoTextureSettingsServer.SetCanvasItemDefaultTextureFilter(Viewport.DefaultCanvasItemTextureFilter.Nearest);
        VideoTextureSettingsServer.AddViewport(added);
        Expect(VideoTextureSettingsServer.HasViewport(added), "HasViewport()");
        ExpectEqual(added.CanvasItemDefaultTextureFilter, Viewport.DefaultCanvasItemTextureFilter.Nearest, "an added viewport gets the values at once");
        VideoTextureSettingsServer.SetSnap2DTransformsToPixel(true);
        Expect(added.Snap2DTransformsToPixel, "an added viewport gets later changes");
        VideoTextureSettingsServer.RemoveViewport(added);
        Expect(!VideoTextureSettingsServer.HasViewport(added), "RemoveViewport()");
        VideoTextureSettingsServer.SetSnap2DTransformsToPixel(false);
        Expect(added.Snap2DTransformsToPixel, "a removed viewport keeps its values");
        added.Free();
        VideoTextureSettingsServer.Singleton.ResetToDefaults();
    }

    private async Task CheckGroup()
    {
        ExpectEqual(VideoTextureSettingsServer.GetViewportGroup(), new StringName("video_texture_settings"), "GetViewportGroup()");
        VideoTextureSettingsServer.SetCanvasItemDefaultTextureFilter(Viewport.DefaultCanvasItemTextureFilter.Nearest);
        var grouped = new SubViewport();
        grouped.AddToGroup(VideoTextureSettingsServer.GetViewportGroup());
        AddChild(grouped);
        ExpectEqual(grouped.CanvasItemDefaultTextureFilter, Viewport.DefaultCanvasItemTextureFilter.Nearest, "a viewport in the group gets the values when it enters the tree");
        VideoTextureSettingsServer.SetAnisotropicFilteringLevel(Anisotropy2X);
        ExpectEqual(grouped.AnisotropicFilteringLevel, Anisotropy2X, "a viewport in the group gets later changes");
        var other = new SubViewport();
        AddChild(other);
        ExpectEqual(other.CanvasItemDefaultTextureFilter, Viewport.DefaultCanvasItemTextureFilter.Linear, "a viewport outside the group is untouched");
        grouped.QueueFree();
        other.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        VideoTextureSettingsServer.Singleton.ResetToDefaults();
    }

    private void CheckValidation()
    {
        var server = VideoTextureSettingsServer.Singleton;
        VideoTextureSettingsServer.SetTextureMipmapBias(5.0f);
        Expect(Mathf.IsEqualApprox(VideoTextureSettingsServer.GetTextureMipmapBias(), 2.0f), "the mipmap bias is clamped to 2.0");
        VideoTextureSettingsServer.SetCanvasShadowAtlasSize(16);
        ExpectEqual(VideoTextureSettingsServer.GetCanvasShadowAtlasSize(), 128, "the 2D shadow atlas is clamped to 128");
        server.SetValue("anisotropic_filtering_level", 9);
        ExpectEqual(server.GetValue("anisotropic_filtering_level").AsInt32(), server.GetDefaultValue("anisotropic_filtering_level").AsInt32(), "an invalid anisotropy falls back to the default");
        server.ResetToDefaults();
    }

    private void CheckStorage()
    {
        var server = VideoTextureSettingsServer.Singleton;
        VideoTextureSettingsServer.SetAnisotropicFilteringLevel(Anisotropy8X);
        VideoTextureSettingsServer.SetSnap2DVerticesToPixel(true);
        VideoTextureSettingsServer.SetDecalFilter(RenderingServer.DecalFilter.Linear);
        ExpectEqual(server.Save(), Error.Ok, "Save()");
        Expect(FileAccess.FileExists(TextureFile), "the file exists");
        Expect(FileAccess.GetFileAsString(TextureFile).Contains("anisotropic_filtering_level"), "the INI file has anisotropic_filtering_level");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load()");
        ExpectEqual(VideoTextureSettingsServer.GetAnisotropicFilteringLevel(), Anisotropy8X, "the anisotropy is loaded");
        Expect(VideoTextureSettingsServer.IsSnap2DVerticesToPixelEnabled(), "the vertex snap is loaded");
        ExpectEqual(VideoTextureSettingsServer.GetDecalFilter(), RenderingServer.DecalFilter.Linear, "the decal filter is loaded (also where the renderer ignores it)");
        server.Apply();
        ExpectEqual(GetTree().Root.AnisotropicFilteringLevel, Anisotropy8X, "the loaded value reaches the root after Apply()");
    }

    private async Task CheckStreaming()
    {
        if (!ClassDB.ClassExists("TextureStreaming"))
        {
            Expect(!Engine.HasSingleton("VideoTextureStreamingSettingsServer"), "without the texture_streaming module, the streaming domain is missing");
            Expect(SettingsServerManager.GetServer("video_texture_streaming") == null, "no \"video_texture_streaming\" domain");
            return;
        }
        Expect(Engine.HasSingleton("VideoTextureStreamingSettingsServer"), "VideoTextureStreamingSettingsServer exists with the texture_streaming module");
        var streaming = Engine.GetSingleton("VideoTextureStreamingSettingsServer") as SettingsServer;
        Expect(streaming != null, "the streaming domain is a SettingsServer");
        if (streaming == null)
        {
            return;
        }
        Expect(SettingsServerManager.GetServer("video_texture_streaming") == streaming, "the domain is registered as \"video_texture_streaming\"");
        Expect(streaming.IsStarted(), "the streaming domain was started");
        ExpectEqual(streaming.GetApplyPhase(), SettingsServer.ApplyPhase.PreDisplay, "applied before the renderers start");
        ExpectEqual(streaming.GetFilePath(), StreamingFile, "streaming file path");
        var names = streaming.GetSettingNames();
        ExpectEqual(names.Length, StreamingNames.Length, "5 streaming values");
        foreach (string setting in StreamingNames)
        {
            Expect(Array.IndexOf(names, setting) >= 0, $"streaming setting \"{setting}\" exists");
        }
        Expect(streaming.GetSettingFlags("enabled").HasFlag(SettingsServer.SettingFlags.RestartRequired), "\"enabled\" needs a restart");
        Expect(!streaming.GetSettingFlags("memory_budget_mb").HasFlag(SettingsServer.SettingFlags.RestartRequired), "\"memory_budget_mb\" takes effect at once");
        ExpectEqual(streaming.GetDefaultValue("memory_budget_mb").AsInt32(), Project("rendering/textures/streaming/memory_budget_mb").AsInt32(), "default budget is the project setting");

        bool supported = streaming.Call("is_renderer_supported").AsBool();
        ExpectEqual(supported, RenderingServer.GetCurrentRenderingMethod() != "gl_compatibility", "streaming: Forward+ and Mobile only");
        // A shared engine singleton: not disposed.
        GodotObject textureStreaming = Engine.GetSingleton("TextureStreaming");
        long usedBefore = streaming.Call("get_memory_budget_bytes_used").AsInt64();
        streaming.Call("set_memory_budget_enabled", true);
        streaming.Call("set_memory_budget_mb", 1);
        streaming.Call("set_min_lod", 2);
        ExpectEqual(streaming.Call("get_memory_budget_mb").AsInt32(), 1, "memory_budget_mb is stored");
        if (supported)
        {
            ExpectEqual(textureStreaming.Get("memory_budget_mb_override").AsInt64(), 1L, "memory_budget_mb reaches TextureStreaming.memory_budget_mb_override at once");
            ExpectEqual(textureStreaming.Get("min_lod_override").AsInt32(), 2, "min_lod reaches TextureStreaming.min_lod_override at once");
            Expect(Project("rendering/textures/streaming/memory_budget_enabled").AsBool(), "memory_budget_enabled reaches the project setting");
            for (int i = 0; i < 30; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            long usedAfter = streaming.Call("get_memory_budget_bytes_used").AsInt64();
            if (usedBefore > 0)
            {
                Expect(usedAfter <= usedBefore, $"a lower budget does not raise the VRAM of streamed textures ({usedBefore} -> {usedAfter} bytes)");
            }
            GD.Print($"    streamed texture memory: {usedBefore} -> {usedAfter} bytes (streaming at startup: {Project("rendering/textures/streaming/enabled")})");
        }

        streaming.Call("set_enabled", !streaming.GetDefaultValue("enabled").AsBool());
        Expect(Array.IndexOf(streaming.GetRestartRequiredNames(), "enabled") >= 0, "changing \"enabled\" asks for a restart");
        streaming.ResetToDefaults();
        if (supported)
        {
            ExpectEqual(textureStreaming.Get("memory_budget_mb_override").AsInt64(), (long)Project("rendering/textures/streaming/memory_budget_mb").AsInt32(), "ResetToDefaults() gives back the project budget");
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(StreamingFile);
    }

    /// <summary>Issue #173: the typed signal fires once per applied change, not for pending changes in manual mode.</summary>
    private async Task CheckTypedSignals()
    {
        if (!Engine.HasSingleton("VideoTextureSettingsServer"))
        {
            return;
        }
        var s = (SettingsServer)Engine.GetSingleton("VideoTextureSettingsServer");
        Expect(s.HasSignal("texture_mipmap_bias_changed"), "VideoTextureSettingsServer has the typed signal texture_mipmap_bias_changed");
        string filePath = s.GetFilePath();
        bool hadFile = FileAccess.FileExists(filePath);
        var previousMode = s.ApplyMode;
        s.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        s.ResetValue("texture_mipmap_bias");
        double newValue = s.GetAppliedValue("texture_mipmap_bias").AsDouble() != 0.5 ? 0.5 : -0.5;
        var received = new System.Collections.Generic.List<Variant>();
        var onChanged = Callable.From((Variant v) => received.Add(v));
        s.Connect("texture_mipmap_bias_changed", onChanged);

        s.ApplyMode = SettingsServer.ApplyModeEnum.Manual;
        s.SetValue("texture_mipmap_bias", newValue);
        ExpectEqual(received.Count, 0, "texture_mipmap_bias_changed does not fire for a pending change in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 1, "texture_mipmap_bias_changed fires once on Apply()");
        if (received.Count > 0)
        {
            ExpectEqual(received[0].VariantType, Variant.Type.Float, "texture_mipmap_bias_changed passes a typed value");
            ExpectEqual((float)received[0].AsDouble(), (float)newValue, "texture_mipmap_bias_changed passes the applied value");
        }
        s.Apply();
        ExpectEqual(received.Count, 1, "texture_mipmap_bias_changed does not fire when nothing changed");
        s.ResetValue("texture_mipmap_bias");
        ExpectEqual(received.Count, 1, "texture_mipmap_bias_changed waits for Apply() after a reset in manual mode");
        s.Apply();
        ExpectEqual(received.Count, 2, "texture_mipmap_bias_changed fires again when the default is applied");

        s.Disconnect("texture_mipmap_bias_changed", onChanged);
        s.ApplyMode = previousMode;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!hadFile)
        {
            DirAccess.RemoveAbsolute(filePath);
        }
    }
}
