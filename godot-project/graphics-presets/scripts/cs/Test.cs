using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.GraphicsPresets;

/// <summary>
/// Full API test of Graphics Presets (GraphicsPresetSettingsServer, GraphicsPreset, GraphicsPresetSettings). Same tests as scripts/gd/Test.gd.
/// ApplyPreset sets all values of the typed domain resources of a GraphicsPreset in the four video domains and skips the values
/// the renderer ignores. A single change gives "Custom".
/// </summary>
public partial class Test : FeatureTest
{
    private const string PresetFile = "user://settings/graphics_preset.ini";
    private const string ProjectHigh = "res://graphics-presets/resources/project_high_preset.tres";
    private const string ProjectPotato = "res://graphics-presets/resources/project_potato_preset.tres";

    private static readonly string[] Domains = ["render_quality", "texture", "shadow", "render_effect"];
    private static readonly string[] Builtin = ["Low", "Medium", "High", "Ultra"];

    private readonly List<string> _changedNames = [];

    protected override Task RunTests()
    {
        GraphicsPresetSettingsServer.Singleton.PresetChanged += OnPresetChanged;
        Reset();
        TestBuiltin();
        TestApply();
        TestUnsupported();
        TestCustom();
        TestStorage();
        TestProjectPresets();
        TestRuntimePresets();
        TestResources();
        GraphicsPresetSettingsServer.Singleton.PresetChanged -= OnPresetChanged;
        Reset();
        DirAccess.RemoveAbsolute(PresetFile);
        foreach (string domain in Domains)
        {
            DirAccess.RemoveAbsolute($"user://settings/{domain}.ini");
        }
        return Task.CompletedTask;
    }

    private void OnPresetChanged(string presetName)
    {
        _changedNames.Add(presetName);
    }

    private static void Reset()
    {
        foreach (string domain in Domains)
        {
            SettingsServerManager.GetServer(domain).ResetToDefaults();
            SettingsServerManager.GetServer(domain).ConfirmChanges();
        }
        GraphicsPresetSettingsServer.Singleton.ResetToDefaults();
    }

    private static void ConfirmAll()
    {
        foreach (string domain in Domains)
        {
            SettingsServerManager.GetServer(domain).ConfirmChanges();
        }
    }

    private static bool ValuesEqual(Variant a, Variant b)
    {
        bool aNumber = a.VariantType is Variant.Type.Int or Variant.Type.Float;
        bool bNumber = b.VariantType is Variant.Type.Int or Variant.Type.Float;
        if (aNumber && bNumber)
        {
            return Mathf.IsEqualApprox((float)a.AsDouble(), (float)b.AsDouble());
        }
        return a.Equals(b);
    }

    // The stored values of the domain resource of a preset: setting name to value.
    private static Dictionary<string, Variant> PresetValues(GraphicsPreset preset, string domain)
    {
        var values = new Dictionary<string, Variant>();
        Resource domainSettings = preset.GetDomainSettings(domain);
        foreach (Godot.Collections.Dictionary property in domainSettings.GetPropertyList())
        {
            string name = property["name"].AsString();
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            bool stored = (usage & PropertyUsageFlags.Storage) != 0;
            if (stored && name != "script" && !name.StartsWith("resource_") && !name.StartsWith("metadata/"))
            {
                values[name] = domainSettings.Get(name);
            }
        }
        return values;
    }

    private void TestBuiltin()
    {
        ExpectEqual(GraphicsPresetSettingsServer.GetCustomPresetName(), "Custom", "the custom name is Custom");
        Expect(GraphicsPresetSettingsServer.GetBuiltinPresetNames().AsSpan().SequenceEqual(Builtin), "the built-in names");
        Expect(GraphicsPresetSettingsServer.GetPresetNames().AsSpan().SequenceEqual(Builtin), "the project uses the built-in presets");
        ExpectEqual(GraphicsPresetSettingsServer.GetPresets().Count, 4, "GetPresets returns four presets");
        GraphicsPreset low = GraphicsPresetSettingsServer.CreateBuiltinPreset("Low");
        GraphicsPreset ultra = GraphicsPresetSettingsServer.CreateBuiltinPreset("Ultra");
        ExpectEqual(low.PresetName, "Low", "CreateBuiltinPreset sets the preset name");
        foreach (string domain in Domains)
        {
            SettingsServer server = SettingsServerManager.GetServer(domain);
            Expect(low.HasDomainSettings(domain), $"Low has settings for {domain}");
            ExpectEqual(low.GetDomainSettings(domain).GetClass(), server.GetSettings().GetClass(), $"the {domain} resource of Low has the class of the domain");
            foreach (string setting in PresetValues(ultra, domain).Keys)
            {
                Expect(server.HasSetting(setting), $"{domain}/{setting} exists in the domain");
            }
        }
        Expect(low.RenderQuality != null, "Low has a typed render_quality");
        Expect(low.Texture != null, "Low has a typed texture");
        Expect(low.Shadow != null, "Low has a typed shadow");
        Expect(low.RenderEffect != null, "Low has a typed render_effect");
        Expect(low.Shadow.DirectionalShadowAtlasSize < ultra.Shadow.DirectionalShadowAtlasSize, "Ultra has larger shadow maps than Low");
        Expect((int)low.Texture.AnisotropicFilteringLevel < (int)ultra.Texture.AnisotropicFilteringLevel, "Ultra has more anisotropic filtering than Low");
        Expect(Mathf.IsEqualApprox(low.RenderQuality.Scaling3DScale, 0.75f), "Low renders at 75%");
        string suggestion = GraphicsPresetSettingsServer.SuggestPreset();
        Expect(GraphicsPresetSettingsServer.HasPreset(suggestion), "SuggestPreset names a preset");
    }

    private void TestApply()
    {
        _changedNames.Clear();
        ExpectEqual(GraphicsPresetSettingsServer.ApplyPreset("Ultra"), Error.Ok, "ApplyPreset(Ultra) succeeds");
        ConfirmAll();
        GraphicsPreset preset = GraphicsPresetSettingsServer.GetPreset("Ultra");
        foreach (string domain in Domains)
        {
            SettingsServer server = SettingsServerManager.GetServer(domain);
            Expect(!server.IsDirty(), $"{domain} applied the values at once");
            foreach (KeyValuePair<string, Variant> pair in PresetValues(preset, domain))
            {
                if (server.IsValueSupported(pair.Key))
                {
                    Expect(ValuesEqual(server.GetValue(pair.Key), pair.Value), $"Ultra: {domain}/{pair.Key} = {pair.Value}");
                }
            }
        }
        ExpectEqual(GraphicsPresetSettingsServer.GetCurrentPreset(), "Ultra", "the current preset is Ultra");
        Expect(!GraphicsPresetSettingsServer.IsCustom(), "the preset is not custom");
        Expect(GraphicsPresetSettingsServer.MatchesPreset("Ultra"), "the values match the preset");
        ExpectEqual(GraphicsPresetSettingsServer.DetectPreset(), "Ultra", "DetectPreset finds Ultra");
        Expect(_changedNames.Contains("Ultra"), "PresetChanged is emitted with Ultra");
    }

    private void TestUnsupported()
    {
        Reset();
        GraphicsPreset low = GraphicsPresetSettingsServer.GetPreset("Low");
        var before = new Dictionary<string, Variant>();
        foreach (string domain in Domains)
        {
            SettingsServer server = SettingsServerManager.GetServer(domain);
            foreach (string setting in PresetValues(low, domain).Keys)
            {
                before[domain + "/" + setting] = server.GetValue(setting);
            }
        }
        GraphicsPresetSettingsServer.ApplyPreset("Low");
        ConfirmAll();
        foreach (string domain in Domains)
        {
            SettingsServer server = SettingsServerManager.GetServer(domain);
            foreach (string setting in PresetValues(low, domain).Keys)
            {
                if (!server.IsValueSupported(setting))
                {
                    Expect(ValuesEqual(server.GetValue(setting), before[domain + "/" + setting]), $"unsupported {domain}/{setting} keeps its value");
                }
            }
        }
    }

    private void TestCustom()
    {
        SettingsServer shadow = SettingsServerManager.GetServer("shadow");
        GraphicsPresetSettingsServer.ApplyPreset("High");
        ConfirmAll();
        ExpectEqual(GraphicsPresetSettingsServer.GetCurrentPreset(), "High", "High is applied");
        _changedNames.Clear();
        shadow.SetValue("directional_shadow_atlas_size", 2048);
        ExpectEqual(GraphicsPresetSettingsServer.GetCurrentPreset(), "Custom", "a single change gives Custom");
        Expect(GraphicsPresetSettingsServer.IsCustom(), "IsCustom is true");
        Expect(!GraphicsPresetSettingsServer.MatchesPreset("High"), "the values no longer match High");
        Expect(_changedNames.Contains("Custom"), "PresetChanged is emitted with Custom");
        shadow.SetValue("directional_shadow_atlas_size", 4096);
        ExpectEqual(GraphicsPresetSettingsServer.GetCurrentPreset(), "High", "the preset value again gives the preset again");
        shadow.SetValue("directional_shadow_atlas_size", 2048);
    }

    private void TestStorage()
    {
        foreach (string domain in Domains)
        {
            ExpectEqual(SettingsServerManager.GetServer(domain).Save(), Error.Ok, $"Save writes the {domain} file");
        }
        ExpectEqual(GraphicsPresetSettingsServer.Singleton.Save(), Error.Ok, "Save writes the graphics_preset file");
        Expect(FileAccess.GetFileAsString(PresetFile).Contains("Custom"), "the file stores Custom");
        Reset();
        var stored = (GraphicsPresetSettings)GraphicsPresetSettingsServer.Singleton.GetSettings();
        ExpectEqual(stored.Preset, "", "after a reset no preset is stored");
        foreach (string domain in Domains)
        {
            SettingsServerManager.GetServer(domain).Load();
        }
        ExpectEqual(GraphicsPresetSettingsServer.Singleton.Load(), Error.Ok, "Load reads the file");
        ExpectEqual(GraphicsPresetSettingsServer.GetCurrentPreset(), "Custom", "Custom is loaded");
        int size = SettingsServerManager.GetServer("shadow").GetValue("directional_shadow_atlas_size").AsInt32();
        ExpectEqual(size, 2048, "the changed value is loaded");
    }

    private void TestProjectPresets()
    {
        ProjectSettings.SetSetting("settings/graphics_preset/presets", new string[] { ProjectHigh, ProjectPotato });
        GraphicsPresetSettingsServer.ReloadPresets();
        string[] names = GraphicsPresetSettingsServer.GetPresetNames();
        Expect(names.AsSpan().SequenceEqual(["Low", "Medium", "High", "Ultra", "Potato"]), "the project replaces High and adds Potato");
        GraphicsPreset high = GraphicsPresetSettingsServer.GetPreset("High");
        ExpectEqual(high.Shadow.DirectionalShadowAtlasSize, 2048, "the project High has smaller shadow maps");
        Expect(high.RenderEffect == null && !high.HasDomainSettings("render_effect"), "the project High leaves render_effect alone");
        SettingsServer shadow = SettingsServerManager.GetServer("shadow");
        shadow.SetValue("directional_shadow_atlas_size", 1024);
        ExpectEqual(GraphicsPresetSettingsServer.ApplyPreset("Potato"), Error.Ok, "ApplyPreset(Potato) succeeds");
        ConfirmAll();
        ExpectEqual(GraphicsPresetSettingsServer.GetCurrentPreset(), "Potato", "Potato is current");
        int level = SettingsServerManager.GetServer("texture").GetValue("anisotropic_filtering_level").AsInt32();
        ExpectEqual(level, 0, "Potato sets the anisotropic filtering");
        float scale = SettingsServerManager.GetServer("render_quality").GetValue("scaling_3d_scale").AsSingle();
        ExpectEqual(scale, 0.5f, "Potato sets the resolution scale");
        int kept = shadow.GetValue("directional_shadow_atlas_size").AsInt32();
        ExpectEqual(kept, 1024, "Potato leaves shadow as it is");
        shadow.SetValue("directional_shadow_atlas_size", 2048);
        ExpectEqual(GraphicsPresetSettingsServer.GetCurrentPreset(), "Potato", "a change in a domain Potato does not set keeps Potato");
        SettingsServerManager.GetServer("texture").SetValue("anisotropic_filtering_level", (int)Viewport.AnisotropicFiltering.Anisotropy16X);
        ExpectEqual(GraphicsPresetSettingsServer.GetCurrentPreset(), "Custom", "a change in a domain Potato sets gives Custom");
        ProjectSettings.SetSetting("settings/graphics_preset/use_builtin_presets", false);
        GraphicsPresetSettingsServer.ReloadPresets();
        Expect(GraphicsPresetSettingsServer.GetPresetNames().AsSpan().SequenceEqual(["High", "Potato"]), "without the built-in presets");
        ProjectSettings.SetSetting("settings/graphics_preset/use_builtin_presets", true);
        ProjectSettings.SetSetting("settings/graphics_preset/presets", System.Array.Empty<string>());
        GraphicsPresetSettingsServer.ReloadPresets();
        Expect(GraphicsPresetSettingsServer.GetPresetNames().AsSpan().SequenceEqual(Builtin), "back to the built-in presets");
    }

    private void TestRuntimePresets()
    {
        GraphicsPreset cinematic = GraphicsPresetSettingsServer.CreateBuiltinPreset("Ultra");
        cinematic.PresetName = "Cinematic";
        cinematic.RenderEffect.DofBlurQuality = RenderingServer.DofBlurQuality.High;
        GraphicsPreset ultra = GraphicsPresetSettingsServer.GetPreset("Ultra");
        Expect(cinematic.RenderEffect != ultra.RenderEffect, "CreateBuiltinPreset returns new domain resources");
        GraphicsPresetSettingsServer.AddPreset(cinematic);
        Expect(GraphicsPresetSettingsServer.HasPreset("Cinematic"), "AddPreset adds a preset");
        Expect(GraphicsPresetSettingsServer.GetPreset("Cinematic") == cinematic, "GetPreset returns the added preset");
        GraphicsPresetSettingsServer.RemovePreset("Cinematic");
        Expect(!GraphicsPresetSettingsServer.HasPreset("Cinematic"), "RemovePreset removes it");
        ExpectEqual(GraphicsPresetSettingsServer.ApplyPreset("Custom"), Error.InvalidParameter, "Custom can't be applied");
        ExpectEqual(GraphicsPresetSettingsServer.ApplyPreset("Missing"), Error.DoesNotExist, "a missing preset can't be applied");
    }

    private void TestResources()
    {
        Expect(GraphicsPreset.GetDomains().AsSpan().SequenceEqual(Domains), "a preset covers the four video domains");
        var preset = new GraphicsPreset();
        ExpectEqual(preset.PresetName, "", "GraphicsPreset: the name is empty by default");
        Expect(!preset.HasDomainSettings("shadow"), "GraphicsPreset: a new preset has no domain settings");
        var shadow = new Godot.ShadowSettings();
        preset.SetDomainSettings("shadow", shadow);
        Expect(preset.Shadow == shadow, "GraphicsPreset: SetDomainSettings sets the Shadow property");
        Expect(preset.GetDomainSettings("shadow") == shadow, "GraphicsPreset: GetDomainSettings returns it");
        var stored = new GraphicsPresetSettings();
        ExpectEqual(stored.Preset, "", "GraphicsPresetSettings: no preset by default");
        stored.Preset = "High";
        ExpectEqual(stored.Preset, "High", "GraphicsPresetSettings: Preset is a plain property");
    }
}
