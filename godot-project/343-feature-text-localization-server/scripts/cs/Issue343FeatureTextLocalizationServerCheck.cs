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
        string previousLocale = LocalizationServer.GetLocale();
        TextLocalizationServer.Singleton.ClearCatalogs();
        LocalizationServer.SetLocale("en");

        var changes = new Array();
        GodotObject server = Engine.GetSingleton("TextLocalizationServer");
        var onLocaleChanged = Callable.From((string locale, string previous) => changes.Add(new Array { locale, previous }));
        server.Connect("locale_changed", onLocaleChanged);

        // Locale and chain, forwarded to TranslationServer.
        string fallback = LocalizationServer.GetFallbackLocale();
        Expect(LocalizationServer.GetLocaleChain("de_DE").SequenceEqual(new[] { "de_DE", "de", fallback }), "chain de_DE -> de -> fallback");

        // Game and mod layers: the mod (priority 100) overrides the game.
        TextLocalizationCatalog game = GameCatalog();
        TextLocalizationCatalog mod = ModCatalog();
        TextLocalizationServer.Singleton.AddCatalog(game);
        TextLocalizationServer.Singleton.AddCatalog(mod, 100);
        LocalizationServer.SetLocale("de_DE");
        Expect(changes.Count == 1 && (string)((Array)changes[0])[0] == "de_DE" && (string)((Array)changes[0])[1] == "en", "locale_changed(locale, previous_locale)");
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

        // Reverse lookup and missing keys.
        ExpectEqual(TextLocalizationServer.GetTranslationKey("Servus").ToString(), "HELLO", "GetTranslationKey");
        ExpectEqual(TextLocalizationServer.GetTranslationKeyForLocale("fr", "Bonjour").ToString(), "HELLO", "GetTranslationKeyForLocale");
        Expect(TextLocalizationServer.GetMissingKeys("de").Contains("BYE"), "GetMissingKeys");

        // Catalog changes are applied on the next idle frame.
        game.SetText("BYE", "de", "Tschüss");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(TextLocalizationServer.Translate("BYE"), "Tschüss", "catalog change picked up");

        // Catalog files with folder wildcards and on-demand locales.
        TextLocalizationServer.Singleton.ClearCatalogs();
        DirAccess.MakeDirRecursiveAbsolute(DataDir + "/mods/knights/translations");
        game.SaveToFile(DataDir + "/game.lcat");
        ModCatalog().SaveToFile(DataDir + "/mods/knights/translations/texts.lcat");
        var list = new LocalizationCatalogList();
        list.AddEntry(DataDir + "/*.lcat");
        list.AddEntry(DataDir + "/mods/*/translations/*.lcat", 100);
        ExpectEqual(TextLocalizationServer.Singleton.AddCatalogList(list), 2, "AddCatalogList loads game and mod");
        var loaded = (LocalizationCatalog)TextLocalizationServer.Singleton.GetCatalogs()[0];
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Servus", "mod from user://mods/*/translations");
        Expect(loaded.IsLocaleLoaded("de") && !loaded.IsLocaleLoaded("fr"), "only the chain locales are read");

        server.Disconnect("locale_changed", onLocaleChanged);
        TextLocalizationServer.Singleton.ClearCatalogs();
        LocalizationServer.SetLocale(previousLocale);
    }
}
