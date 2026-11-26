using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue343FeatureTextLocalizationServer;

/// <summary>C# checks of 343-feature-text-localization-server. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue343FeatureTextLocalizationServerCheck : FeatureCheckCs
{
    private const string DataDir = "user://check_343_cs";

    protected override string CheckName => "343-feature-text-localization-server";

    private static TextLocalizationCatalog GameCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("HELLO", "en", "Hello");
        catalog.SetText("HELLO", "de", "Hallo");
        catalog.SetText("HELLO", "de", "Hallo (Menü)", "menu");
        catalog.SetText("BYE", "en", "Bye");
        catalog.SetText("GREET", "de", "Hallo {name}");
        catalog.SetPluralRule("de", "nplurals=2; plural=(n != 1);");
        catalog.SetPluralTexts("APPLE", "de", new[] { "{n} Apfel", "{n} Äpfel" });
        catalog.SetText("HELLO", "fr", "Bonjour");
        return catalog;
    }

    private static TextLocalizationCatalog ModCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("HELLO", "de", "Servus");
        catalog.SetText("MOD_ONLY", "de", "Nur im Mod");
        return catalog;
    }

    protected override async Task RunChecks()
    {
        Expect(Engine.HasSingleton("TextLocalizationServer"), "TextLocalizationServer is a registered singleton");
        Expect(ClassDB.ClassExists("LocalizationServer") && !ClassDB.CanInstantiate("LocalizationServer"), "LocalizationServer is abstract");
        string previousLocale = TranslationServer.GetLocale();
        TextLocalizationServer.Singleton.ClearCatalogs();
        TranslationServer.SetLocale("en");

        var changes = new Array();
        var onLocaleChanged = Callable.From((StringName channel, string locale, string previous) => changes.Add(new Array { channel, locale, previous }));
        TranslationServer.Singleton.Connect("locale_changed", onLocaleChanged);

        // Locale (TranslationServer) and chain (the closest other locale comes before the fallback).
        string fallback = TranslationServer.GetFallbackLocale();
        Expect(TextLocalizationServer.Singleton.GetLocaleChain("de_DE").SequenceEqual(new[] { "de_DE", "de", fallback }), "chain de_DE -> de -> fallback");

        // Game and mod layers: the mod (priority 100) overrides the game.
        TextLocalizationCatalog game = GameCatalog();
        TextLocalizationCatalog mod = ModCatalog();
        TextLocalizationServer.Singleton.AddCatalog(game);
        TextLocalizationServer.Singleton.AddCatalog(mod, 100);
        TranslationServer.SetLocale("de_DE");
        Expect(changes.Count == 1 && ((StringName)((Array)changes[0])[0]).ToString() == "" && (string)((Array)changes[0])[1] == "de_DE" && (string)((Array)changes[0])[2] == "en", "TranslationServer.locale_changed(channel, locale, previous_locale)");
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Servus", "mod wins over game");
        ExpectEqual(TextLocalizationServer.Translate("HELLO", "menu"), "Hallo (Menü)", "context");
        ExpectEqual(TextLocalizationServer.Translate("BYE"), "Bye", "fallback locale");
        ExpectEqual(TextLocalizationServer.Translate("UNKNOWN"), "UNKNOWN", "missing key returns the key");
        ExpectEqual(TextLocalizationServer.TranslateForLocale("fr", "HELLO"), "Bonjour", "TranslateForLocale");
        Expect(TextLocalizationServer.HasTranslation("MOD_ONLY") && !TextLocalizationServer.HasTranslation("UNKNOWN"), "HasTranslation");

        // Tr() and plurals through the adapter.
        ExpectEqual(Tr("HELLO"), "Servus", "Tr() through the adapter");
        ExpectEqual(Tr("HELLO", "menu"), "Hallo (Menü)", "Tr() with context");
        ExpectEqual(TrN("APPLE", "APPLES", 5), "{n} Äpfel", "TrN() through the adapter");

        // Key ids, plurals and arguments.
        int helloId = TextLocalizationServer.GetKeyId("HELLO");
        Expect(helloId >= 0 && TextLocalizationServer.GetKeyId("UNKNOWN") == -1, "GetKeyId");
        ExpectEqual(TextLocalizationServer.TranslateId(helloId), "Servus", "TranslateId");
        TextLocalizationServer.Singleton.RemoveCatalog(mod);
        ExpectEqual(TextLocalizationServer.GetKeyId("HELLO"), helloId, "key ids stay valid after RemoveCatalog");
        ExpectEqual(TextLocalizationServer.TranslateId(helloId), "Hallo", "TranslateId after RemoveCatalog");
        TextLocalizationServer.Singleton.AddCatalog(mod, 100);
        ExpectEqual(TextLocalizationServer.Translate("APPLE", "", 3, new Dictionary { { "n", 3 } }), "3 Äpfel", "plural form with args");
        ExpectEqual(TextLocalizationServer.Translate("GREET", "", -1, new Dictionary { { "name", "Ann" } }), "Hallo Ann", "placeholders from args");
        ExpectEqual(TextInsertionServer.Insert("{k:t}!", new Dictionary { { "k", "HELLO" } }), "Servus!", "t modifier uses TextLocalizationServer");

        // Missing keys.
        Expect(TextLocalizationServer.GetMissingKeys("de").Contains("BYE"), "GetMissingKeys");

        // Catalog changes are applied on the next idle frame.
        game.SetText("BYE", "de", "Tschüss");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(TextLocalizationServer.Translate("BYE"), "Tschüss", "catalog change picked up");

        // Catalog manifests with folder wildcards and on-demand locales.
        TextLocalizationServer.Singleton.ClearCatalogs();
        DirAccess.MakeDirRecursiveAbsolute(DataDir + "/mods/knights/translations");
        game.SaveToFile(DataDir + "/game.lcat");
        ModCatalog().SaveToFile(DataDir + "/mods/knights/translations/texts.lcat");
        var baseManifest = new FilesManifest { FilePaths = new[] { DataDir + "/game.lcat" } };
        var modManifest = new FilesManifest { TypeHint = "TextLocalizationCatalog", FolderPaths = new[] { DataDir + "/mods/*/translations" } };
        ExpectEqual(TextLocalizationServer.Singleton.AddCatalogManifest(baseManifest), 1, "AddCatalogManifest loads the game");
        ExpectEqual(TextLocalizationServer.Singleton.AddCatalogManifest(modManifest, 100), 1, "AddCatalogManifest loads the mod (wildcard folder)");
        var loaded = (LocalizationCatalog)TextLocalizationServer.Singleton.GetCatalogs()[0];
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Servus", "mod from user://mods/*/translations");
        Expect(loaded.IsLocaleLoaded("de") && !loaded.IsLocaleLoaded("fr"), "only the chain locales are read");

        // Language settings list and accept a locale that only a catalog has (#352).
        TextLocalizationServer.Singleton.ClearCatalogs();
        var italian = new TextLocalizationCatalog();
        italian.SetText("HELLO", "it", "Ciao");
        TextLocalizationServer.Singleton.AddCatalog(italian);
        string settingsLocale = LanguageSettingsServer.GetLocale();
#pragma warning disable CS0618 // TranslationServer is deprecated in favour of the localization servers.
        Expect(!TranslationServer.GetLoadedLocales().Contains("it"), "no Italian translation in TranslationServer");
#pragma warning restore CS0618
        Expect(LanguageSettingsServer.GetAvailableLocales().Contains("it"), "LanguageSettingsServer lists the catalog locale");
        LanguageSettingsServer.SetLocale("it");
        ExpectEqual(TranslationServer.GetLocale(), "it", "LanguageSettingsServer.SetLocale switches to a catalog-only locale");
        ExpectEqual(Tr("HELLO"), "Ciao", "Tr() after the switch");
        LanguageSettingsServer.SetLocale(settingsLocale);

        TranslationServer.Singleton.Disconnect("locale_changed", onLocaleChanged);
        TextLocalizationServer.Singleton.ClearCatalogs();
        TranslationServer.SetLocale(previousLocale);
    }
}
