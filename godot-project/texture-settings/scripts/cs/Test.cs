using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TextureSettings;

using TextureSettings = Godot.TextureSettings;

/// <summary>
/// Full API test of Texture Settings (TextureSettingsServer, TextureSettings). Same tests as scripts/gd/Test.gd.
/// The Compatibility renderer ignores the light projector and decal filter (stored, not pushed).
/// </summary>
public partial class Test : FeatureTest
{
    private const string TextureFile = "user://settings/texture.ini";
    private const Viewport.DefaultCanvasItemTextureFilter Nearest = Viewport.DefaultCanvasItemTextureFilter.Nearest;

    private readonly Godot.Collections.Dictionary _received = [];

    protected override async Task RunTests()
    {
        TextureSettingsServer.Singleton.ResetToDefaults();
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
        TextureSettingsServer.Singleton.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(TextureFile);
    }

    private void TestDefaults()
    {
        int projectLevel = (int)ProjectSettings.GetSetting("rendering/textures/default_filters/anisotropic_filtering_level");
        int projectFilter = (int)ProjectSettings.GetSetting("rendering/textures/canvas_textures/default_texture_filter");
        bool projectSnap = (bool)ProjectSettings.GetSetting("rendering/2d/snap/snap_2d_transforms_to_pixel");
        int projectAtlas = (int)ProjectSettings.GetSetting("rendering/2d/shadow_atlas/size");
        int projectDecal = (int)ProjectSettings.GetSetting("rendering/textures/decals/filter");
        ExpectEqual((int)TextureSettingsServer.GetAnisotropicFilteringLevel(), projectLevel, "the default anisotropy is the project setting");
        ExpectEqual((int)TextureSettingsServer.GetCanvasItemDefaultTextureFilter(), projectFilter, "the default canvas filter is the project setting");
        ExpectEqual(TextureSettingsServer.IsSnap2DTransformsToPixelEnabled(), projectSnap, "the default pixel snap is the project setting");
        ExpectEqual(TextureSettingsServer.GetCanvasShadowAtlasSize(), projectAtlas, "the default 2D shadow atlas is the project setting");
        ExpectEqual((int)TextureSettingsServer.GetDecalFilter(), projectDecal, "the default decal filter is the project setting");
        ExpectEqual((int)GetTree().Root.AnisotropicFilteringLevel, projectLevel, "the root viewport has the default anisotropy");
    }

    private void TestValues()
    {
        TextureSettingsServer.SetAnisotropicFilteringLevel(Viewport.AnisotropicFiltering.Anisotropy16X);
        ExpectEqual(TextureSettingsServer.GetAnisotropicFilteringLevel(), Viewport.AnisotropicFiltering.Anisotropy16X, "anisotropic_filtering_level round trip");
        TextureSettingsServer.SetTextureMipmapBias(-0.5f);
        ExpectEqual(TextureSettingsServer.GetTextureMipmapBias(), -0.5f, "texture_mipmap_bias round trip");
        TextureSettingsServer.SetCanvasItemDefaultTextureFilter(Nearest);
        ExpectEqual(TextureSettingsServer.GetCanvasItemDefaultTextureFilter(), Nearest, "canvas_item_default_texture_filter round trip");
        TextureSettingsServer.SetCanvasItemDefaultTextureRepeat(Viewport.DefaultCanvasItemTextureRepeat.Mirror);
        ExpectEqual(TextureSettingsServer.GetCanvasItemDefaultTextureRepeat(), Viewport.DefaultCanvasItemTextureRepeat.Mirror, "canvas_item_default_texture_repeat round trip");
        TextureSettingsServer.SetSnap2DTransformsToPixel(true);
        Expect(TextureSettingsServer.IsSnap2DTransformsToPixelEnabled(), "snap_2d_transforms_to_pixel round trip");
        TextureSettingsServer.SetSnap2DVerticesToPixel(true);
        Expect(TextureSettingsServer.IsSnap2DVerticesToPixelEnabled(), "snap_2d_vertices_to_pixel round trip");
        TextureSettingsServer.SetSdfOversize(Viewport.SdfOversizeEnum.Oversize200Percent);
        ExpectEqual(TextureSettingsServer.GetSdfOversize(), Viewport.SdfOversizeEnum.Oversize200Percent, "sdf_oversize round trip");
        TextureSettingsServer.SetSdfScale(Viewport.SdfScaleEnum.Scale100Percent);
        ExpectEqual(TextureSettingsServer.GetSdfScale(), Viewport.SdfScaleEnum.Scale100Percent, "sdf_scale round trip");
        TextureSettingsServer.SetCanvasShadowAtlasSize(4096);
        ExpectEqual(TextureSettingsServer.GetCanvasShadowAtlasSize(), 4096, "canvas_shadow_atlas_size round trip");
        TextureSettingsServer.SetDecalFilter(RenderingServer.DecalFilter.Nearest);
        ExpectEqual(TextureSettingsServer.GetDecalFilter(), RenderingServer.DecalFilter.Nearest, "decal_filter round trip");
        TextureSettingsServer.SetLightProjectorFilter(RenderingServer.LightProjectorFilter.Linear);
        ExpectEqual(TextureSettingsServer.GetLightProjectorFilter(), RenderingServer.LightProjectorFilter.Linear, "light_projector_filter round trip");
        TextureSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSupport()
    {
        string[] unsupported = TextureSettingsServer.GetUnsupportedSettingNames();
        foreach (string setting in new[] { "anisotropic_filtering_level", "decal_filter", "light_projector_filter", "sdf_scale" })
        {
            bool supported = TextureSettingsServer.Singleton.IsValueSupported(setting);
            ExpectEqual(System.Array.IndexOf(unsupported, setting) < 0, supported, $"GetUnsupportedSettingNames matches IsValueSupported({setting})");
            ExpectEqual(TextureSettingsServer.IsRendererSupported(setting), supported, $"IsRendererSupported matches IsValueSupported({setting})");
        }
        bool compatibility = RenderingServer.GetCurrentRenderingMethod() == "gl_compatibility";
        ExpectEqual(TextureSettingsServer.Singleton.IsValueSupported("decal_filter"), !compatibility, "the decal filter is not supported in Compatibility");
        ExpectEqual(TextureSettingsServer.Singleton.IsValueSupported("light_projector_filter"), !compatibility, "the light projector filter is not supported in Compatibility");
        Expect(TextureSettingsServer.Singleton.IsValueSupported("anisotropic_filtering_level"), "anisotropic_filtering_level works in every renderer");
    }

    private void TestRootViewport()
    {
        Window root = GetTree().Root;
        TextureSettingsServer.SetAnisotropicFilteringLevel(Viewport.AnisotropicFiltering.Anisotropy16X);
        ExpectEqual(root.AnisotropicFilteringLevel, Viewport.AnisotropicFiltering.Anisotropy16X, "anisotropic_filtering_level reaches the root at once");
        TextureSettingsServer.SetTextureMipmapBias(-0.5f);
        ExpectEqual(root.TextureMipmapBias, -0.5f, "texture_mipmap_bias reaches the root at once");
        TextureSettingsServer.SetCanvasItemDefaultTextureFilter(Nearest);
        ExpectEqual(root.CanvasItemDefaultTextureFilter, Nearest, "canvas_item_default_texture_filter reaches the root at once");
        TextureSettingsServer.SetCanvasItemDefaultTextureRepeat(Viewport.DefaultCanvasItemTextureRepeat.Mirror);
        ExpectEqual(root.CanvasItemDefaultTextureRepeat, Viewport.DefaultCanvasItemTextureRepeat.Mirror, "canvas_item_default_texture_repeat reaches the root at once");
        TextureSettingsServer.SetSnap2DTransformsToPixel(true);
        Expect(root.Snap2DTransformsToPixel, "snap_2d_transforms_to_pixel reaches the root at once");
        TextureSettingsServer.SetSnap2DVerticesToPixel(true);
        Expect(root.Snap2DVerticesToPixel, "snap_2d_vertices_to_pixel reaches the root at once");
        TextureSettingsServer.SetSdfOversize(Viewport.SdfOversizeEnum.Oversize200Percent);
        ExpectEqual(root.SdfOversize, Viewport.SdfOversizeEnum.Oversize200Percent, "sdf_oversize reaches the root at once");
        TextureSettingsServer.SetSdfScale(Viewport.SdfScaleEnum.Scale100Percent);
        ExpectEqual(root.SdfScale, Viewport.SdfScaleEnum.Scale100Percent, "sdf_scale reaches the root at once");
        root.SdfScale = Viewport.SdfScaleEnum.Scale25Percent;
        TextureSettingsServer.Singleton.ReadFromEngine();
        ExpectEqual(TextureSettingsServer.GetSdfScale(), Viewport.SdfScaleEnum.Scale25Percent, "ReadFromEngine reads the root back");
        TextureSettingsServer.Singleton.ResetToDefaults();
        Expect(!root.Snap2DTransformsToPixel, "ResetToDefaults gives the pixel snap back");
    }

    private void TestViewports()
    {
        var added = new SubViewport();
        TextureSettingsServer.SetCanvasItemDefaultTextureFilter(Nearest);
        TextureSettingsServer.AddViewport(added);
        Expect(TextureSettingsServer.HasViewport(added), "AddViewport: HasViewport is true");
        ExpectEqual(added.CanvasItemDefaultTextureFilter, Nearest, "an added viewport gets the values at once");
        TextureSettingsServer.SetSnap2DTransformsToPixel(true);
        Expect(added.Snap2DTransformsToPixel, "an added viewport gets later changes");
        TextureSettingsServer.RemoveViewport(added);
        Expect(!TextureSettingsServer.HasViewport(added), "RemoveViewport: HasViewport is false");
        TextureSettingsServer.SetSnap2DTransformsToPixel(false);
        Expect(added.Snap2DTransformsToPixel, "a removed viewport keeps its values");
        added.Free();
        TextureSettingsServer.Singleton.ResetToDefaults();
    }

    private async Task TestGroup()
    {
        StringName group = TextureSettingsServer.GetViewportGroup();
        ExpectEqual(group, new StringName("texture_settings"), "GetViewportGroup returns the group name");
        TextureSettingsServer.SetCanvasItemDefaultTextureFilter(Nearest);
        var grouped = new SubViewport();
        grouped.AddToGroup(group);
        AddChild(grouped);
        ExpectEqual(grouped.CanvasItemDefaultTextureFilter, Nearest, "a viewport in the group gets the values when it enters the tree");
        TextureSettingsServer.SetAnisotropicFilteringLevel(Viewport.AnisotropicFiltering.Anisotropy2X);
        ExpectEqual(grouped.AnisotropicFilteringLevel, Viewport.AnisotropicFiltering.Anisotropy2X, "a viewport in the group gets later changes");
        var other = new SubViewport();
        AddChild(other);
        ExpectEqual(other.CanvasItemDefaultTextureFilter, Viewport.DefaultCanvasItemTextureFilter.Linear, "a viewport outside the group is untouched");
        grouped.QueueFree();
        other.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        TextureSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestValidation()
    {
        TextureSettingsServer.SetTextureMipmapBias(5.0f);
        ExpectEqual(TextureSettingsServer.GetTextureMipmapBias(), 2.0f, "the mipmap bias is clamped to 2.0");
        TextureSettingsServer.SetCanvasShadowAtlasSize(16);
        ExpectEqual(TextureSettingsServer.GetCanvasShadowAtlasSize(), 128, "the 2D shadow atlas is clamped to 128");
        TextureSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSignals()
    {
        TextureSettingsServerInstance server = TextureSettingsServer.Singleton;
        server.AnisotropicFilteringLevelChanged += level => _received["level"] = (int)level;
        server.TextureMipmapBiasChanged += bias => _received["bias"] = bias;
        server.CanvasItemDefaultTextureFilterChanged += filter => _received["filter"] = (int)filter;
        server.CanvasItemDefaultTextureRepeatChanged += repeat => _received["repeat"] = (int)repeat;
        TextureSettingsServer.SetAnisotropicFilteringLevel(Viewport.AnisotropicFiltering.Anisotropy8X);
        TextureSettingsServer.SetTextureMipmapBias(1.0f);
        TextureSettingsServer.SetCanvasItemDefaultTextureFilter(Nearest);
        TextureSettingsServer.SetCanvasItemDefaultTextureRepeat(Viewport.DefaultCanvasItemTextureRepeat.Enabled);
        ExpectEqual((int)_received["level"], (int)Viewport.AnisotropicFiltering.Anisotropy8X, "AnisotropicFilteringLevelChanged passes the level");
        ExpectEqual((float)_received["bias"], 1.0f, "TextureMipmapBiasChanged passes the bias");
        ExpectEqual((int)_received["filter"], (int)Nearest, "CanvasItemDefaultTextureFilterChanged passes the filter");
        ExpectEqual((int)_received["repeat"], (int)Viewport.DefaultCanvasItemTextureRepeat.Enabled, "CanvasItemDefaultTextureRepeatChanged passes the repeat mode");
        server.ResetToDefaults();
    }

    private void TestStorage()
    {
        TextureSettingsServerInstance server = TextureSettingsServer.Singleton;
        TextureSettingsServer.SetAnisotropicFilteringLevel(Viewport.AnisotropicFiltering.Anisotropy8X);
        TextureSettingsServer.SetSnap2DVerticesToPixel(true);
        TextureSettingsServer.SetDecalFilter(RenderingServer.DecalFilter.Linear);
        ExpectEqual(server.Save(), Error.Ok, "Save writes the file");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load reads the file");
        ExpectEqual(TextureSettingsServer.GetAnisotropicFilteringLevel(), Viewport.AnisotropicFiltering.Anisotropy8X, "the anisotropy is loaded");
        Expect(TextureSettingsServer.IsSnap2DVerticesToPixelEnabled(), "the vertex snap is loaded");
        ExpectEqual(TextureSettingsServer.GetDecalFilter(), RenderingServer.DecalFilter.Linear, "the decal filter is loaded, also where the renderer ignores it");
        server.Apply();
        ExpectEqual(GetTree().Root.AnisotropicFilteringLevel, Viewport.AnisotropicFiltering.Anisotropy8X, "the loaded value reaches the root after Apply");
    }

    private void TestResource()
    {
        var settings = new TextureSettings();
        ExpectEqual(settings.AnisotropicFilteringLevel, Viewport.AnisotropicFiltering.Anisotropy4X, "TextureSettings: default AnisotropicFilteringLevel");
        ExpectEqual(settings.TextureMipmapBias, 0.0f, "TextureSettings: default TextureMipmapBias");
        ExpectEqual(settings.CanvasItemDefaultTextureFilter, Viewport.DefaultCanvasItemTextureFilter.Linear, "TextureSettings: default CanvasItemDefaultTextureFilter");
        ExpectEqual(settings.CanvasShadowAtlasSize, 2048, "TextureSettings: default CanvasShadowAtlasSize");
        ExpectEqual(settings.DecalFilter, RenderingServer.DecalFilter.LinearMipmaps, "TextureSettings: default DecalFilter");
        Expect(!settings.Snap2DTransformsToPixel, "TextureSettings: default Snap2DTransformsToPixel");
        settings.TextureMipmapBias = 1.5f;
        ExpectEqual(settings.TextureMipmapBias, 1.5f, "TextureSettings: TextureMipmapBias is a plain property");
    }
}
