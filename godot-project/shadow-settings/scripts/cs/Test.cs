using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.ShadowSettings;

using ShadowSettings = Godot.ShadowSettings;

/// <summary>
/// Full API test of Shadow Settings (ShadowSettingsServer, ShadowSettings). Same tests as scripts/gd/Test.gd.
/// The positional values go to the root viewport, the directional ones to the RenderingServer (no getter there, so the domain keeps them).
/// </summary>
public partial class Test : FeatureTest
{
    private const string ShadowFile = "user://settings/shadow.ini";
    private const Viewport.PositionalShadowAtlasQuadrantSubdiv Subdiv16 = Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv16;

    private readonly Godot.Collections.Dictionary _received = [];

    protected override async Task RunTests()
    {
        ShadowSettingsServer.Singleton.ResetToDefaults();
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
        ShadowSettingsServer.Singleton.ResetToDefaults();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DirAccess.RemoveAbsolute(ShadowFile);
    }

    private void TestDefaults()
    {
        int projectPositional = (int)ProjectSettings.GetSettingWithOverride("rendering/lights_and_shadows/positional_shadow/atlas_size");
        int projectDirectional = (int)ProjectSettings.GetSettingWithOverride("rendering/lights_and_shadows/directional_shadow/size");
        ExpectEqual(ShadowSettingsServer.GetPositionalShadowAtlasSize(), projectPositional, "the default positional size is the project setting");
        ExpectEqual(ShadowSettingsServer.GetDirectionalShadowAtlasSize(), projectDirectional, "the default directional size is the project setting");
        ExpectEqual(GetTree().Root.PositionalShadowAtlasSize, projectPositional, "the root viewport has the default positional size");
    }

    private void TestValues()
    {
        ShadowSettingsServer.SetDirectionalShadowAtlasSize(2048);
        ExpectEqual(ShadowSettingsServer.GetDirectionalShadowAtlasSize(), 2048, "directional_shadow_atlas_size round trip");
        ShadowSettingsServer.SetDirectionalShadow16Bits(false);
        Expect(!ShadowSettingsServer.IsDirectionalShadow16BitsEnabled(), "directional_shadow_16_bits round trip");
        ShadowSettingsServer.SetDirectionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.SoftHigh);
        ExpectEqual(ShadowSettingsServer.GetDirectionalSoftShadowFilterQuality(), RenderingServer.ShadowQuality.SoftHigh, "directional_soft_shadow_filter_quality round trip");
        ShadowSettingsServer.SetPositionalShadowAtlasSize(1024);
        ExpectEqual(ShadowSettingsServer.GetPositionalShadowAtlasSize(), 1024, "positional_shadow_atlas_size round trip");
        ShadowSettingsServer.SetPositionalShadowAtlas16Bits(false);
        Expect(!ShadowSettingsServer.IsPositionalShadowAtlas16BitsEnabled(), "positional_shadow_atlas_16_bits round trip");
        ShadowSettingsServer.SetPositionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.Hard);
        ExpectEqual(ShadowSettingsServer.GetPositionalSoftShadowFilterQuality(), RenderingServer.ShadowQuality.Hard, "positional_soft_shadow_filter_quality round trip");
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            ShadowSettingsServer.SetPositionalShadowAtlasQuadrantSubdiv(quadrant, Subdiv16);
            ExpectEqual(ShadowSettingsServer.GetPositionalShadowAtlasQuadrantSubdiv(quadrant), Subdiv16, $"quadrant {quadrant} subdiv round trip");
        }
        ShadowSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSupport()
    {
        string[] unsupported = ShadowSettingsServer.GetUnsupportedSettingNames();
        Expect(unsupported.Length == 0, "every renderer uses every value");
        Expect(ShadowSettingsServer.Singleton.IsValueSupported("directional_shadow_atlas_size"), "directional_shadow_atlas_size is supported");
        Expect(ShadowSettingsServer.IsRendererSupported("positional_shadow_atlas_size"), "IsRendererSupported is true for positional_shadow_atlas_size");
    }

    private void TestRootViewport()
    {
        Window root = GetTree().Root;
        ShadowSettingsServer.SetPositionalShadowAtlasSize(2048);
        ExpectEqual(root.PositionalShadowAtlasSize, 2048, "positional_shadow_atlas_size reaches the root at once");
        ShadowSettingsServer.SetPositionalShadowAtlas16Bits(false);
        Expect(!root.PositionalShadowAtlas16Bits, "positional_shadow_atlas_16_bits reaches the root at once");
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            ShadowSettingsServer.SetPositionalShadowAtlasQuadrantSubdiv(quadrant, Subdiv16);
            ExpectEqual(root.GetPositionalShadowAtlasQuadrantSubdiv(quadrant), Subdiv16, $"quadrant {quadrant} reaches the root at once");
        }
        root.PositionalShadowAtlasSize = 1024;
        ShadowSettingsServer.Singleton.ReadFromEngine();
        ExpectEqual(ShadowSettingsServer.GetPositionalShadowAtlasSize(), 1024, "ReadFromEngine reads the root back");
        ShadowSettingsServer.Singleton.ResetToDefaults();
        bool projectBits = (bool)ProjectSettings.GetSetting("rendering/lights_and_shadows/positional_shadow/atlas_16_bits");
        ExpectEqual(root.PositionalShadowAtlas16Bits, projectBits, "ResetToDefaults gives the bit depth back");
    }

    private void TestViewports()
    {
        var added = new SubViewport();
        ShadowSettingsServer.SetPositionalShadowAtlasSize(1024);
        ShadowSettingsServer.AddViewport(added);
        Expect(ShadowSettingsServer.HasViewport(added), "AddViewport: HasViewport is true");
        ExpectEqual(added.PositionalShadowAtlasSize, 1024, "an added viewport gets the values at once");
        ShadowSettingsServer.SetPositionalShadowAtlasQuadrantSubdiv(2, Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv1);
        ExpectEqual(added.PositionalShadowAtlasQuad2, Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv1, "an added viewport gets later changes");
        ShadowSettingsServer.RemoveViewport(added);
        Expect(!ShadowSettingsServer.HasViewport(added), "RemoveViewport: HasViewport is false");
        ShadowSettingsServer.SetPositionalShadowAtlasSize(512);
        ExpectEqual(added.PositionalShadowAtlasSize, 1024, "a removed viewport keeps its values");
        added.Free();
        ShadowSettingsServer.Singleton.ResetToDefaults();
    }

    private async Task TestGroup()
    {
        StringName group = ShadowSettingsServer.GetViewportGroup();
        ExpectEqual(group, new StringName("shadow_settings"), "GetViewportGroup returns the group name");
        ShadowSettingsServer.SetPositionalShadowAtlasSize(1024);
        var grouped = new SubViewport();
        grouped.AddToGroup(group);
        AddChild(grouped);
        ExpectEqual(grouped.PositionalShadowAtlasSize, 1024, "a viewport in the group gets the values when it enters the tree");
        ShadowSettingsServer.SetPositionalShadowAtlas16Bits(false);
        Expect(!grouped.PositionalShadowAtlas16Bits, "a viewport in the group gets later changes");
        var other = new SubViewport();
        AddChild(other);
        ExpectEqual(other.PositionalShadowAtlasSize, 2048, "a viewport outside the group keeps the Viewport default");
        grouped.QueueFree();
        other.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ShadowSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestValidation()
    {
        ShadowSettingsServer.SetDirectionalShadowAtlasSize(3000);
        ExpectEqual(ShadowSettingsServer.GetDirectionalShadowAtlasSize(), 4096, "sizes are rounded up to a power of two");
        ShadowSettingsServer.SetDirectionalShadowAtlasSize(16);
        ExpectEqual(ShadowSettingsServer.GetDirectionalShadowAtlasSize(), 256, "the directional size is clamped to 256");
        ShadowSettingsServer.SetPositionalShadowAtlasSize(100000);
        ExpectEqual(ShadowSettingsServer.GetPositionalShadowAtlasSize(), 16384, "the positional size is clamped to 16384");
        ShadowSettingsServer.SetPositionalShadowAtlasSize(0);
        ExpectEqual(ShadowSettingsServer.GetPositionalShadowAtlasSize(), 0, "0 disables positional shadows");
        ExpectEqual(GetTree().Root.PositionalShadowAtlasSize, 0, "0 reaches the root");
        ShadowSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSignals()
    {
        ShadowSettingsServerInstance server = ShadowSettingsServer.Singleton;
        server.DirectionalShadowAtlasSizeChanged += size => _received["directional_size"] = size;
        server.DirectionalSoftShadowFilterQualityChanged += quality => _received["directional_quality"] = (int)quality;
        server.PositionalShadowAtlasSizeChanged += size => _received["positional_size"] = size;
        server.PositionalSoftShadowFilterQualityChanged += quality => _received["positional_quality"] = (int)quality;
        ShadowSettingsServer.SetDirectionalShadowAtlasSize(8192);
        ShadowSettingsServer.SetDirectionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.SoftHigh);
        ShadowSettingsServer.SetPositionalShadowAtlasSize(2048);
        ShadowSettingsServer.SetPositionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.SoftVeryLow);
        ExpectEqual((int)_received["directional_size"], 8192, "DirectionalShadowAtlasSizeChanged passes the size");
        ExpectEqual((int)_received["directional_quality"], (int)RenderingServer.ShadowQuality.SoftHigh, "DirectionalSoftShadowFilterQualityChanged passes the quality");
        ExpectEqual((int)_received["positional_size"], 2048, "PositionalShadowAtlasSizeChanged passes the size");
        ExpectEqual((int)_received["positional_quality"], (int)RenderingServer.ShadowQuality.SoftVeryLow, "PositionalSoftShadowFilterQualityChanged passes the quality");
        server.ResetToDefaults();
    }

    private void TestStorage()
    {
        ShadowSettingsServerInstance server = ShadowSettingsServer.Singleton;
        ShadowSettingsServer.SetPositionalShadowAtlasSize(2048);
        ShadowSettingsServer.SetDirectionalSoftShadowFilterQuality(RenderingServer.ShadowQuality.SoftUltra);
        ShadowSettingsServer.SetPositionalShadowAtlasQuadrantSubdiv(1, Subdiv16);
        ExpectEqual(server.Save(), Error.Ok, "Save writes the file");
        server.ResetToDefaults();
        ExpectEqual(server.Load(), Error.Ok, "Load reads the file");
        ExpectEqual(ShadowSettingsServer.GetPositionalShadowAtlasSize(), 2048, "the positional size is loaded");
        ExpectEqual(ShadowSettingsServer.GetDirectionalSoftShadowFilterQuality(), RenderingServer.ShadowQuality.SoftUltra, "the directional quality is loaded");
        ExpectEqual(ShadowSettingsServer.GetPositionalShadowAtlasQuadrantSubdiv(1), Subdiv16, "the quadrant subdiv is loaded");
        server.Apply();
        ExpectEqual(GetTree().Root.PositionalShadowAtlasSize, 2048, "the loaded value reaches the root after Apply");
    }

    private void TestResource()
    {
        var settings = new ShadowSettings();
        ExpectEqual(settings.DirectionalShadowAtlasSize, 4096, "ShadowSettings: default DirectionalShadowAtlasSize");
        Expect(settings.DirectionalShadow16Bits, "ShadowSettings: DirectionalShadow16Bits by default");
        ExpectEqual(settings.DirectionalSoftShadowFilterQuality, RenderingServer.ShadowQuality.SoftLow, "ShadowSettings: default directional quality");
        ExpectEqual(settings.PositionalShadowAtlasSize, 4096, "ShadowSettings: default PositionalShadowAtlasSize");
        ExpectEqual(settings.PositionalShadowAtlasQuad0, Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv4, "ShadowSettings: default PositionalShadowAtlasQuad0");
        ExpectEqual(settings.PositionalShadowAtlasQuad3, Viewport.PositionalShadowAtlasQuadrantSubdiv.Subdiv64, "ShadowSettings: default PositionalShadowAtlasQuad3");
        settings.PositionalShadowAtlasQuad1 = Subdiv16;
        ExpectEqual(settings.PositionalShadowAtlasQuad1, Subdiv16, "ShadowSettings: PositionalShadowAtlasQuad1 is a plain property");
        ExpectEqual(settings.GetPositionalShadowAtlasQuadrantSubdiv(1), Subdiv16, "ShadowSettings: GetPositionalShadowAtlasQuadrantSubdiv reads quad 1");
        settings.SetPositionalShadowAtlasQuadrantSubdiv(2, Subdiv16);
        ExpectEqual(settings.PositionalShadowAtlasQuad2, Subdiv16, "ShadowSettings: SetPositionalShadowAtlasQuadrantSubdiv writes quad 2");
    }
}
