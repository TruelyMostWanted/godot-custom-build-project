using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue25FeatureVideoPresets;

/// <summary>
/// C# checks of 25-feature-video-presets. Check the same things as scripts/gd/check.gd.
/// A <see cref="VideoPreset"/> holds one typed resource per video domain (null: the domain is left alone).
/// Typed methods are called on the generated static class, the methods of the base class through
/// <c>Singleton</c>. The video domains are reached through <see cref="SettingsServerManager"/>.
/// </summary>
public partial class Issue25FeatureVideoPresetsCheck : FeatureCheckCs
{
    private const string FilePath = "user://settings/video_preset.ini";
    private const string ProjectHigh = "res://25-feature-video-presets/resources/project_high_preset.tres";
    private const string ProjectPotato = "res://25-feature-video-presets/resources/project_potato_preset.tres";

    private static readonly string[] Domains = { "video_quality", "video_texture", "video_shadow", "video_effect" };
    private static readonly string[] Builtin = { "Low", "Medium", "High", "Ultra" };

    private readonly List<string> _changedNames = new();

    protected override string CheckName => "25-feature-video-presets";

    protected override Task RunChecks()
    {
        var server = VideoPresetSettingsServer.Singleton;
        Expect(Engine.HasSingleton("VideoPresetSettingsServer"), "VideoPresetSettingsServer is a registered singleton");
        Expect(server is VideoPresetSettingsServerInstance, "VideoPresetSettingsServer.Singleton is the instance type");
        Expect(Engine.GetSingleton("VideoPresetSettingsServer") is SettingsServer, "VideoPresetSettingsServerInstance derives from SettingsServer");
        Expect(SettingsServerManager.GetServer("video_preset") == server, "the domain is registered as \"video_preset\"");
        Expect(server.IsStarted(), "the domain was started before the main scene");
        ExpectEqual(server.GetApplyPhase(), SettingsServer.ApplyPhase.Scene, "apply phase");
        ExpectEqual(server.GetFilePath(), FilePath, "file path");
        Expect(server.GetSettings() is VideoPresetSettings, "the settings resource is a VideoPresetSettings");
        ExpectEqual(string.Join(",", VideoPreset.GetDomains()), string.Join(",", Domains), "a preset covers the four video domains");

        var callable = Callable.From<string>(OnPresetChanged);
        server.Connect(VideoPresetSettingsServerInstance.SignalName.PresetChanged, callable);
        Reset();
        CheckBuiltin();
        CheckApply("Low");
        CheckApply("Ultra");
        CheckUnsupported();
        CheckCustom();
        CheckStorage();
        CheckProjectPresets();
        CheckRuntimePresets();
        server.Disconnect(VideoPresetSettingsServerInstance.SignalName.PresetChanged, callable);

        // Leave no player files behind.
        Reset();
        DirAccess.RemoveAbsolute(FilePath);
        foreach (string domain in Domains)
        {
            DirAccess.RemoveAbsolute($"user://settings/{domain}.ini");
        }
        return Task.CompletedTask;
    }

    private void OnPresetChanged(string presetName) => _changedNames.Add(presetName);

    private static SettingsServer Domain(string domain) => SettingsServerManager.GetServer(domain);

    private static void Reset()
    {
        foreach (string domain in Domains)
        {
            Domain(domain).ResetToDefaults();
            Domain(domain).ConfirmChanges();
        }
        VideoPresetSettingsServer.Singleton.ResetToDefaults();
    }

    private static void ConfirmAll()
    {
        foreach (string domain in Domains)
        {
            Domain(domain).ConfirmChanges();
        }
    }

    private static bool IsNumber(Variant value) => value.VariantType is Variant.Type.Int or Variant.Type.Float;

    // Floats compared as float: the domains store float, presets double.
    private static bool ValuesEqual(Variant a, Variant b)
    {
        if (IsNumber(a) && IsNumber(b))
        {
            return Mathf.IsEqualApprox((float)a.AsDouble(), (float)b.AsDouble());
        }
        if (a.VariantType == Variant.Type.Bool && b.VariantType == Variant.Type.Bool)
        {
            return a.AsBool() == b.AsBool();
        }
        return Equals(a.Obj, b.Obj);
    }

    private static string Join(string[] names) => string.Join(",", names);

    // The stored values of the domain resource of a preset: setting name to value (empty for a null resource).
    private static Dictionary<string, Variant> PresetValues(VideoPreset preset, string domain)
    {
        var values = new Dictionary<string, Variant>();
        Resource domainSettings = preset.GetDomainSettings(domain);
        if (domainSettings == null)
        {
            return values;
        }
        foreach (Godot.Collections.Dictionary property in domainSettings.GetPropertyList())
        {
            string name = property["name"].AsString();
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            if ((usage & PropertyUsageFlags.Storage) == 0 || name == "script" || name.StartsWith("resource_") || name.StartsWith("metadata/"))
            {
                continue;
            }
            values[name] = domainSettings.Get(name);
        }
        return values;
    }

    private void CheckBuiltin()
    {
        ExpectEqual(VideoPresetSettingsServer.GetCustomPresetName(), "Custom", "the custom name");
        ExpectEqual(Join(VideoPresetSettingsServer.GetBuiltinPresetNames()), Join(Builtin), "built-in names");
        ExpectEqual(Join(VideoPresetSettingsServer.GetPresetNames()), Join(Builtin), "the project uses the built-in presets");
        ExpectEqual(VideoPresetSettingsServer.GetPresets().Count, 4, "four presets");
        VideoPreset low = VideoPresetSettingsServer.CreateBuiltinPreset("Low");
        VideoPreset ultra = VideoPresetSettingsServer.CreateBuiltinPreset("Ultra");
        Expect(low != null && ultra != null, "CreateBuiltinPreset()");
        ExpectEqual(low.PresetName, "Low", "PresetName");
        foreach (string domain in Domains)
        {
            Expect(low.HasDomainSettings(domain), $"Low has settings for \"{domain}\"");
            ExpectEqual(low.GetDomainSettings(domain).GetClass(), Domain(domain).GetSettings().GetClass(), $"Low's \"{domain}\" has the class of the domain");
            foreach (string setting in PresetValues(ultra, domain).Keys)
            {
                Expect(Domain(domain).HasSetting(setting), $"\"{domain}/{setting}\" exists in the domain");
            }
        }
        Expect(low.VideoQuality != null && low.VideoTexture != null && low.VideoShadow != null && low.VideoEffect != null, "typed domain resources");
        Expect(low.VideoShadow.DirectionalShadowAtlasSize < ultra.VideoShadow.DirectionalShadowAtlasSize, "Ultra has larger shadow maps than Low");
        Expect(low.VideoTexture.AnisotropicFilteringLevel < ultra.VideoTexture.AnisotropicFilteringLevel, "Ultra has more anisotropic filtering than Low");
        Expect(Mathf.IsEqualApprox((float)low.VideoQuality.Scaling3DScale, 0.75f), "Low renders at 75%");
        string suggestion = VideoPresetSettingsServer.SuggestPreset();
        Expect(VideoPresetSettingsServer.HasPreset(suggestion), $"SuggestPreset() names a preset: {suggestion}");
    }

    private void CheckApply(string presetName)
    {
        _changedNames.Clear();
        ExpectEqual(VideoPresetSettingsServer.ApplyPreset(presetName), Error.Ok, $"ApplyPreset(\"{presetName}\")");
        ConfirmAll();
        VideoPreset preset = VideoPresetSettingsServer.GetPreset(presetName);
        foreach (string domain in Domains)
        {
            SettingsServer server = Domain(domain);
            Expect(!server.IsDirty(), $"\"{domain}\" applied the values at once");
            foreach (var pair in PresetValues(preset, domain))
            {
                string setting = pair.Key;
                if (server.IsValueSupported(setting))
                {
                    Expect(ValuesEqual(server.GetValue(setting), pair.Value), $"{presetName}: {domain}/{setting} = {pair.Value}");
                }
            }
        }
        ExpectEqual(VideoPresetSettingsServer.GetCurrentPreset(), presetName, "the current preset");
        Expect(!VideoPresetSettingsServer.IsCustom(), "not custom");
        Expect(VideoPresetSettingsServer.MatchesPreset(presetName), "the values match the preset");
        Expect(_changedNames.Contains(presetName), "PresetChanged was emitted");
    }

    private void CheckUnsupported()
    {
        Reset();
        VideoPreset low = VideoPresetSettingsServer.GetPreset("Low");
        var before = new Dictionary<string, Variant>();
        foreach (string domain in Domains)
        {
            foreach (string setting in PresetValues(low, domain).Keys)
            {
                before[$"{domain}/{setting}"] = Domain(domain).GetValue(setting);
            }
        }
        VideoPresetSettingsServer.ApplyPreset("Low");
        ConfirmAll();
        int skipped = 0;
        foreach (string domain in Domains)
        {
            foreach (string setting in PresetValues(low, domain).Keys)
            {
                if (!Domain(domain).IsValueSupported(setting))
                {
                    skipped++;
                    Expect(ValuesEqual(Domain(domain).GetValue(setting), before[$"{domain}/{setting}"]), $"unsupported {domain}/{setting} keeps its value");
                }
            }
        }
        GD.Print($"  renderer {RenderingServer.GetCurrentRenderingMethod()}: {skipped} values of Low skipped");
    }

    private void CheckCustom()
    {
        SettingsServer shadow = Domain("video_shadow");
        VideoPresetSettingsServer.ApplyPreset("High");
        ConfirmAll();
        ExpectEqual(VideoPresetSettingsServer.GetCurrentPreset(), "High", "High applied");
        _changedNames.Clear();
        shadow.SetValue("directional_shadow_atlas_size", 2048);
        ExpectEqual(VideoPresetSettingsServer.GetCurrentPreset(), "Custom", "a single change gives Custom");
        Expect(VideoPresetSettingsServer.IsCustom(), "IsCustom()");
        Expect(_changedNames.Contains("Custom"), "PresetChanged(\"Custom\")");
        shadow.SetValue("directional_shadow_atlas_size", 4096);
        ExpectEqual(VideoPresetSettingsServer.GetCurrentPreset(), "High", "the preset value again gives the preset again");
        shadow.SetValue("directional_shadow_atlas_size", 2048);
        ExpectEqual(VideoPresetSettingsServer.GetCurrentPreset(), "Custom", "Custom again");
    }

    private void CheckStorage()
    {
        // Save() writes the applied values; the domains are in APPLY_MODE_IMMEDIATE.
        foreach (string domain in Domains)
        {
            ExpectEqual(Domain(domain).Save(), Error.Ok, $"Save() of \"{domain}\"");
        }
        var server = VideoPresetSettingsServer.Singleton;
        ExpectEqual(server.Save(), Error.Ok, "Save() of \"video_preset\"");
        Expect(FileAccess.GetFileAsString(FilePath).Contains("Custom"), "the file stores \"Custom\"");
        Reset();
        ExpectEqual(((VideoPresetSettings)server.GetSettings()).Preset, "", "reset: no preset stored");
        foreach (string domain in Domains)
        {
            Domain(domain).Load();
        }
        ExpectEqual(server.Load(), Error.Ok, "Load()");
        ExpectEqual(VideoPresetSettingsServer.GetCurrentPreset(), "Custom", "Custom is loaded");
        ExpectEqual(Domain("video_shadow").GetValue("directional_shadow_atlas_size").AsInt32(), 2048, "the changed value is loaded");
    }

    private void CheckProjectPresets()
    {
        ProjectSettings.SetSetting("settings/video_preset/presets", new string[] { ProjectHigh, ProjectPotato });
        VideoPresetSettingsServer.ReloadPresets();
        ExpectEqual(Join(VideoPresetSettingsServer.GetPresetNames()), "Low,Medium,High,Ultra,Potato", "the project replaces High and adds Potato");
        VideoPreset high = VideoPresetSettingsServer.GetPreset("High");
        Expect(high.VideoShadow != null, "the project's High loads a typed VideoShadowSettings");
        ExpectEqual(high.VideoShadow.DirectionalShadowAtlasSize, 2048, "the project's High");
        Expect(high.VideoEffect == null && !high.HasDomainSettings("video_effect"), "the project's High leaves video_effect alone");
        SettingsServer shadow = Domain("video_shadow");
        shadow.SetValue("directional_shadow_atlas_size", 1024);
        ExpectEqual(VideoPresetSettingsServer.ApplyPreset("Potato"), Error.Ok, "ApplyPreset(\"Potato\")");
        ConfirmAll();
        ExpectEqual(VideoPresetSettingsServer.GetCurrentPreset(), "Potato", "Potato is current");
        ExpectEqual(Domain("video_texture").GetValue("anisotropic_filtering_level").AsInt32(), 0, "Potato's anisotropic filtering");
        Expect(Mathf.IsEqualApprox((float)Domain("video_quality").GetValue("scaling_3d_scale").AsDouble(), 0.5f), "Potato's resolution scale");
        ExpectEqual(shadow.GetValue("directional_shadow_atlas_size").AsInt32(), 1024, "Potato leaves video_shadow as it is");
        shadow.SetValue("directional_shadow_atlas_size", 2048);
        ExpectEqual(VideoPresetSettingsServer.GetCurrentPreset(), "Potato", "a change in a domain Potato doesn't touch keeps Potato");
        Domain("video_texture").SetValue("anisotropic_filtering_level", 4); // Viewport.ANISOTROPY_16X
        ExpectEqual(VideoPresetSettingsServer.GetCurrentPreset(), "Custom", "a change in a domain Potato sets gives Custom");

        ProjectSettings.SetSetting("settings/video_preset/use_builtin_presets", false);
        VideoPresetSettingsServer.ReloadPresets();
        ExpectEqual(Join(VideoPresetSettingsServer.GetPresetNames()), "High,Potato", "without the built-in presets");

        ProjectSettings.SetSetting("settings/video_preset/use_builtin_presets", true);
        ProjectSettings.SetSetting("settings/video_preset/presets", Array.Empty<string>());
        VideoPresetSettingsServer.ReloadPresets();
        ExpectEqual(Join(VideoPresetSettingsServer.GetPresetNames()), Join(Builtin), "back to the built-in presets");
    }

    private void CheckRuntimePresets()
    {
        VideoPreset cinematic = VideoPresetSettingsServer.CreateBuiltinPreset("Ultra");
        cinematic.PresetName = "Cinematic";
        cinematic.VideoEffect.DofBlurQuality = RenderingServer.DofBlurQuality.High;
        Expect(cinematic.VideoEffect != VideoPresetSettingsServer.GetPreset("Ultra").VideoEffect, "CreateBuiltinPreset() returns new domain resources");
        VideoPresetSettingsServer.AddPreset(cinematic);
        Expect(VideoPresetSettingsServer.HasPreset("Cinematic"), "AddPreset()");
        Expect(VideoPresetSettingsServer.GetPreset("Cinematic") == cinematic, "GetPreset()");
        VideoPresetSettingsServer.RemovePreset("Cinematic");
        Expect(!VideoPresetSettingsServer.HasPreset("Cinematic"), "RemovePreset()");
        ExpectEqual(VideoPresetSettingsServer.ApplyPreset("Custom"), Error.InvalidParameter, "\"Custom\" can't be applied");
        ExpectEqual(VideoPresetSettingsServer.ApplyPreset("Missing"), Error.DoesNotExist, "a missing preset");
    }
}
