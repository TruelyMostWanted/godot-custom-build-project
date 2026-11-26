using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue344FeatureResourceLocalizationServer;

/// <summary>C# checks of 344-feature-resource-localization-server. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue344FeatureResourceLocalizationServerCheck : FeatureCheckCs
{
    private const string DataDir = "user://check_344_cs";
    private const string RemapsSetting = "internationalization/locale/translation_remaps";

    protected override string CheckName => "344-feature-resource-localization-server";

    // A voice line (a Resource named after its locale) and a logo (ImageTexture, 8 px wide in English, 16 px in German).
    private static void WriteFiles()
    {
        DirAccess.MakeDirRecursiveAbsolute(DataDir + "/mods/polish/voice");
        foreach (string locale in new[] { "en", "de", "pl" })
        {
            var voice = new Resource { ResourceName = locale };
            string folder = locale == "pl" ? DataDir + "/mods/polish/voice" : DataDir;
            ResourceSaver.Save(voice, folder + (locale == "en" ? "/voice.tres" : $"/voice_{locale}.tres"));
        }
        foreach (int size in new[] { 8, 16 })
        {
            var logo = ImageTexture.CreateFromImage(Image.CreateEmpty(size, 4, false, Image.Format.Rgba8));
            ResourceSaver.Save(logo, DataDir + (size == 8 ? "/logo.res" : "/logo_de.res"));
        }
    }

    private async Task<string[]> Relocalized()
    {
        Variant[] args = await ToSignal(ResourceLocalizationServer.Singleton, "resources_relocalized");
        return args.Length > 0 ? args[0].AsStringArray() : System.Array.Empty<string>();
    }

    protected override async Task RunChecks()
    {
        Expect(Engine.HasSingleton("ResourceLocalizationServer"), "ResourceLocalizationServer is a registered singleton");
        Expect(ClassDB.IsParentClass("ResourceLocalizationServer", "LocalizationServer"), "inherits LocalizationServer");
        Expect(ProjectSettings.HasSetting("internationalization/localization/catalog_manifests"), "setting catalog_manifests");
        string previousLocale = TranslationServer.GetLocale();
        // Inherited members are reached through the singleton instance.
        ResourceLocalizationServer.Singleton.ClearCatalogs();
        TranslationServer.SetLocale("en");
        WriteFiles();

        string voicePath = DataDir + "/voice.tres";
        string logoPath = DataDir + "/logo.res";
        var game = new ResourceLocalizationCatalog();
        game.SetLocalizedPath(voicePath, "de", DataDir + "/voice_de.tres");
        game.SetLocalizedPath(logoPath, "de", DataDir + "/logo_de.res");
        game.SetLocalizedPath(logoPath, "fr", DataDir + "/logo_fr.res"); // Missing file: falls back with a warning.
        ResourceLocalizationServer.Singleton.AddCatalog(game);

        // Paths.
        Expect(ResourceLocalizationServer.HasLocalization(voicePath) && !ResourceLocalizationServer.HasLocalization(DataDir + "/other.tres"), "HasLocalization");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(voicePath), voicePath, "no English entry: the original path");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(voicePath, "de_DE"), DataDir + "/voice_de.tres", "de_DE takes de");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(DataDir + "/other.tres", "de"), DataDir + "/other.tres", "unknown path unchanged");
        Dictionary<string, string> paths = ResourceLocalizationServer.GetLocalizedPaths(logoPath);
        Expect(paths.Count == 2 && paths["de"] == DataDir + "/logo_de.res" && paths["fr"] == DataDir + "/logo_fr.res", "GetLocalizedPaths");
        Expect(ResourceLocalizationServer.GetMissingLocales(voicePath).SequenceEqual(new[] { "fr" }), "GetMissingLocales");

        // Load() follows the locale; cached resources are updated in place in the background.
        // Engine-cached resources: not disposed here.
        Resource voice = GD.Load(voicePath);
        Texture2D logo = GD.Load<Texture2D>(logoPath);
        ExpectEqual(voice.ResourceName, "en", "Load() in English");
        ExpectEqual(logo.GetWidth(), 8, "English logo");
        Task<string[]> relocalized = Relocalized();
        TranslationServer.SetLocale("de");
        string[] relocalizedPaths = await relocalized;
        Expect(relocalizedPaths.Contains(voicePath) && relocalizedPaths.Contains(logoPath), "resources_relocalized(paths)");
        Expect(!ResourceLocalizationServer.IsRelocalizing(), "IsRelocalizing() false after the signal");
        ExpectEqual(voice.ResourceName, "de", "voice updated in place");
        ExpectEqual(logo.GetWidth(), 16, "logo updated in place");
        Expect(GD.Load(voicePath) == voice, "Load() returns the same instance");

        // A missing localized file: the original is loaded.
        relocalized = Relocalized();
        TranslationServer.SetLocale("fr");
        await relocalized;
        ExpectEqual(logo.GetWidth(), 8, "missing fr file falls back to the original");

        // A mod adds Polish voice files through a catalog manifest with a wildcard folder (priority 100).
        var mod = new ResourceLocalizationCatalog();
        mod.SetLocalizedPath(voicePath, "pl", DataDir + "/mods/polish/voice/voice_pl.tres");
        ExpectEqual(mod.SaveToFile(DataDir + "/mods/polish/voice/voice.lcat"), Error.Ok, "mod catalog saved as .lcat");
        var manifest = new FilesManifest { TypeHint = "ResourceLocalizationCatalog", FolderPaths = new[] { DataDir + "/mods/*/voice" } };
        ExpectEqual(ResourceLocalizationServer.Singleton.AddCatalogManifest(manifest, 100), 1, "AddCatalogManifest() adds the mod");
        relocalized = Relocalized();
        TranslationServer.SetLocale("pl");
        await relocalized;
        ExpectEqual(voice.ResourceName, "pl", "Polish voice from the mod");
        Expect(ResourceLocalizationServer.Singleton.GetAvailableLocales().Contains("pl"), "pl available");

        // Old translation_remaps are imported as a layer.
        ResourceLocalizationServer.Singleton.ClearCatalogs();
        Variant previousRemaps = ProjectSettings.GetSetting(RemapsSetting, new string[0]);
        ProjectSettings.SetSetting(RemapsSetting, new Godot.Collections.Dictionary { { voicePath, new[] { DataDir + "/voice_de.tres:de" } } });
        ExpectEqual(ResourceLocalizationServer.Singleton.LoadProjectCatalogs(), 1, "LoadProjectCatalogs() imports translation_remaps");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(voicePath, "de"), DataDir + "/voice_de.tres", "remap entry resolved");
        ProjectSettings.SetSetting(RemapsSetting, previousRemaps);

        ResourceLocalizationServer.Singleton.ClearCatalogs();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        TranslationServer.SetLocale(previousLocale);
    }
}
