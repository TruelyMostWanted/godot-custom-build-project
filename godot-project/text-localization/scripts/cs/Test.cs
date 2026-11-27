using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.TextLocalization;

/// <summary>
/// Full API test of Text Localization (TextLocalizationServer and its base LocalizationServer): every method and signal.
/// Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string DataDir = "user://text_localization_test_cs";
    private const string PluralRuleDe = "nplurals=2; plural=(n != 1);";

    private readonly TextLocalizationCatalog _game = GameCatalog();
    private readonly TextLocalizationCatalog _mod = ModCatalog();
    private readonly List<string> _localeChanges = [];
    private readonly List<string> _missingKeys = [];
    private readonly List<string> _loadedLocales = [];
    private int _catalogsChanged;

    private static TextLocalizationServerInstance Server => TextLocalizationServer.Singleton;

    protected override async Task RunTests()
    {
        Server.ClearCatalogs();
        TranslationServer.SetLocale("en");
        TestSingleton();
        TestLocaleChain();
        TestCatalogs();
        TestLocaleSource();
        TestLayers();
        TestKeyMissingSignal();
        TestAdapter();
        TestKeyIds();
        TestPluralsAndArgs();
        TestReverseLookup();
        TestAvailableLocales();
        await TestCatalogChange();
        TestManifests();
        TestCatalogOnlyLocale();
        Server.ClearCatalogs();
        TranslationServer.SetLocale("en");
    }

    private void TestSingleton()
    {
        Expect(Engine.HasSingleton("TextLocalizationServer"), "TextLocalizationServer is a registered singleton");
        Expect(ClassDB.ClassExists("LocalizationServer"), "LocalizationServer exists");
        Expect(!ClassDB.CanInstantiate("LocalizationServer"), "LocalizationServer is abstract");
    }

    private void TestLocaleChain()
    {
        string fallback = TranslationServer.GetFallbackLocale();
        string[] chain = Server.GetLocaleChain("de_DE");
        Expect(chain.SequenceEqual(["de_DE", "de", fallback]), "GetLocaleChain: de_DE, de, then the fallback");
    }

    private void TestCatalogs()
    {
        Server.CatalogsChanged += OnCatalogsChanged;
        Server.AddCatalog(_game);
        Server.AddCatalog(_mod, 100);
        ExpectEqual(_catalogsChanged, 2, "CatalogsChanged is emitted per AddCatalog");
        Expect(Server.HasCatalog(_mod), "HasCatalog finds an added catalog");
        ExpectEqual(Server.GetCatalogs().Count, 2, "GetCatalogs lists both catalogs");
        ExpectEqual(Server.GetKeyCount(), 6, "GetKeyCount counts the keys of all catalogs");
        Server.RemoveCatalog(_mod);
        Expect(!Server.HasCatalog(_mod), "RemoveCatalog removes a catalog");
        ExpectEqual(_catalogsChanged, 3, "CatalogsChanged is emitted by RemoveCatalog");
        Server.CatalogsChanged -= OnCatalogsChanged;
        Server.AddCatalog(_mod, 100);
    }

    private void TestLocaleSource()
    {
        TranslationServer.Singleton.LocaleChanged += OnLocaleChanged;
        TranslationServer.SetLocale("de_DE");
        TranslationServer.Singleton.LocaleChanged -= OnLocaleChanged;
        Expect(_localeChanges.SequenceEqual(["|de_DE|en"]), "TranslationServer.LocaleChanged(channel, locale, previousLocale)");
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Servus", "TranslationServer.SetLocale reaches the server");
    }

    private void TestLayers()
    {
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Servus", "the mod (priority 100) wins over the game");
        ExpectEqual(TextLocalizationServer.Translate("HELLO", "menu"), "Hallo (Menü)", "Translate with a context");
        ExpectEqual(TextLocalizationServer.Translate("BYE"), "Bye", "a key without German text falls back to English");
        ExpectEqual(TextLocalizationServer.Translate("UNKNOWN"), "UNKNOWN", "a missing key returns the key");
        ExpectEqual(TextLocalizationServer.TranslateForLocale("fr", "HELLO"), "Bonjour", "TranslateForLocale reads another locale");
        Expect(TextLocalizationServer.HasTranslation("MOD_ONLY"), "HasTranslation finds a key of the mod");
        Expect(TextLocalizationServer.HasTranslation("HELLO", "menu"), "HasTranslation finds a key with context");
        Expect(!TextLocalizationServer.HasTranslation("UNKNOWN"), "HasTranslation: unknown key");
    }

    private void TestKeyMissingSignal()
    {
        Server.KeyMissing += OnKeyMissing;
        TextLocalizationServer.Translate("NOT_THERE");
        TextLocalizationServer.Translate("NOT_THERE");
        Server.KeyMissing -= OnKeyMissing;
        Expect(_missingKeys.SequenceEqual(["NOT_THERE|de_DE"]), "KeyMissing is emitted once per key and locale");
    }

    private void TestAdapter()
    {
        ExpectEqual(Tr("HELLO"), "Servus", "Tr() reads the catalogs through the adapter");
        ExpectEqual(Tr("HELLO", "menu"), "Hallo (Menü)", "Tr() with a context");
        ExpectEqual(Atr("HELLO"), "Servus", "Atr() reads the catalogs through the adapter");
        ExpectEqual(TrN("APPLE", "APPLES", 5), "{n} Äpfel", "TrN() picks the plural form");
        TranslationServer.PseudolocalizationEnabled = true;
        string pseudo = Tr("HELLO");
        TranslationServer.PseudolocalizationEnabled = false;
        Expect(pseudo.StartsWith('['), "pseudolocalization still applies to Tr(): " + pseudo);
    }

    private void TestKeyIds()
    {
        int helloId = TextLocalizationServer.GetKeyId("HELLO");
        Expect(helloId >= 0, "GetKeyId returns an id");
        ExpectEqual(TextLocalizationServer.GetKeyId("UNKNOWN"), -1, "GetKeyId of an unknown key is -1");
        Expect(TextLocalizationServer.GetKeyId("HELLO", "menu") != helloId, "GetKeyId: a context is its own id");
        ExpectEqual(TextLocalizationServer.TranslateId(helloId), "Servus", "TranslateId");
        Server.RemoveCatalog(_mod);
        ExpectEqual(TextLocalizationServer.GetKeyId("HELLO"), helloId, "key ids stay valid after RemoveCatalog");
        ExpectEqual(TextLocalizationServer.TranslateId(helloId), "Hallo", "TranslateId after RemoveCatalog");
        Server.AddCatalog(_mod, 100);
    }

    private void TestPluralsAndArgs()
    {
        ExpectEqual(TextLocalizationServer.Translate("APPLE", "", 1, new Dictionary { { "n", 1 } }), "1 Apfel", "plural form for 1");
        ExpectEqual(TextLocalizationServer.Translate("APPLE", "", 3, new Dictionary { { "n", 3 } }), "3 Äpfel", "plural form for 3 with args");
        ExpectEqual(TextLocalizationServer.Translate("APPLE", "", 3), "{n} Äpfel", "without args the placeholders stay");
        ExpectEqual(TextLocalizationServer.Translate("GREET", "", -1, new Dictionary { { "name", "Ann" } }), "Hallo Ann", "placeholders from args");
        int appleId = TextLocalizationServer.GetKeyId("APPLE");
        ExpectEqual(TextLocalizationServer.TranslateId(appleId, 2, new Dictionary { { "n", 2 } }), "2 Äpfel", "TranslateId with count and args");
        string deText = TextLocalizationServer.TranslateForLocale("de", "APPLE", "", 1, new Dictionary { { "n", 1 } });
        ExpectEqual(deText, "1 Apfel", "TranslateForLocale with count and args");
        ExpectEqual(TextInsertionServer.Insert("{k:t}!", new Dictionary { { "k", "HELLO" } }), "Servus!", "the t modifier of TextInsertionServer translates");
    }

    private void TestReverseLookup()
    {
        Expect(TextLocalizationServer.GetTranslationKeys("Bye").SequenceEqual(["BYE"]), "GetTranslationKeys finds the key of a text");
        Expect(TextLocalizationServer.GetMissingKeys("de").Contains("BYE"), "GetMissingKeys lists a key without German text");
        Expect(!TextLocalizationServer.GetMissingKeys("de").Contains("HELLO"), "GetMissingKeys skips translated keys");
        Expect(TextLocalizationServer.GetMissingKeys().Contains("BYE"), "GetMissingKeys without locale uses the current locale");
    }

    private void TestAvailableLocales()
    {
        string[] locales = Server.GetAvailableLocales();
        Expect(locales.Contains("fr"), "GetAvailableLocales lists French");
        Expect(locales.Contains("de"), "GetAvailableLocales lists German");
        string[] interfaceLocales = Server.GetAvailableLocalesFor("interface");
        Expect(interfaceLocales.Contains("fr"), "GetAvailableLocalesFor(interface) lists the text catalog locales");
    }

    private async Task TestCatalogChange()
    {
        _game.SetText("BYE", "de", "Tschüss");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(TextLocalizationServer.Translate("BYE"), "Tschüss", "a catalog change is picked up on the next frame");
    }

    private void TestManifests()
    {
        Server.ClearCatalogs();
        ExpectEqual(Server.GetCatalogs().Count, 0, "ClearCatalogs removes all catalogs");
        DirAccess.MakeDirRecursiveAbsolute(DataDir + "/mods/knights/translations");
        _game.SaveToFile(DataDir + "/game.lcat");
        _mod.SaveToFile(DataDir + "/mods/knights/translations/texts.lcat");
        var gameManifest = new FilesManifest();
        gameManifest.FilePaths = [DataDir + "/game.lcat"];
        var modManifest = new FilesManifest();
        modManifest.TypeHint = "TextLocalizationCatalog";
        modManifest.FolderPaths = [DataDir + "/mods/*/translations"];
        Server.LocaleLoaded += OnLocaleLoaded;
        ExpectEqual(Server.AddCatalogManifest(gameManifest), 1, "AddCatalogManifest loads the game catalog");
        ExpectEqual(Server.AddCatalogManifest(modManifest, 100), 1, "AddCatalogManifest loads the mod from a wildcard folder");
        Server.LocaleLoaded -= OnLocaleLoaded;
        Expect(_loadedLocales.Contains("de"), "LocaleLoaded is emitted for a chain locale of an .lcat");
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Servus", "the mod from user://mods/*/translations wins");
        LocalizationCatalog loaded = Server.GetCatalogs()[0];
        Expect(loaded.IsLocaleLoaded("de"), "the chain locale de is read");
        Expect(!loaded.IsLocaleLoaded("fr"), "the locale fr outside the chain is not read");
        Server.ClearCatalogs();
        ExpectEqual(Server.LoadProjectCatalogs(), 0, "LoadProjectCatalogs: this project lists no catalog manifests");
    }

    private void TestCatalogOnlyLocale()
    {
        var italian = new TextLocalizationCatalog();
        italian.SetText("HELLO", "it", "Ciao");
        Server.AddCatalog(italian);
        string settingsLocale = LanguageSettingsServer.GetLocale();
        Expect(!TranslationServer.GetLoadedLocales().Contains("it"), "no Italian translation in TranslationServer");
        Expect(LanguageSettingsServer.GetAvailableLocales().Contains("it"), "LanguageSettingsServer lists a catalog-only locale");
        LanguageSettingsServer.SetLocale("it");
        ExpectEqual(TranslationServer.GetLocale(), "it", "LanguageSettingsServer.SetLocale switches to a catalog-only locale");
        ExpectEqual(Tr("HELLO"), "Ciao", "Tr() after the switch");
        LanguageSettingsServer.SetLocale(settingsLocale);
    }

    private static TextLocalizationCatalog GameCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("HELLO", "en", "Hello");
        catalog.SetText("HELLO", "de", "Hallo");
        catalog.SetText("HELLO", "de", "Hallo (Menü)", "menu");
        catalog.SetText("HELLO", "fr", "Bonjour");
        catalog.SetText("BYE", "en", "Bye");
        catalog.SetText("GREET", "de", "Hallo {name}");
        catalog.SetPluralRule("de", PluralRuleDe);
        catalog.SetPluralTexts("APPLE", "de", ["{n} Apfel", "{n} Äpfel"]);
        return catalog;
    }

    private static TextLocalizationCatalog ModCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("HELLO", "de", "Servus");
        catalog.SetText("MOD_ONLY", "de", "Nur im Mod");
        return catalog;
    }

    private void OnCatalogsChanged()
    {
        _catalogsChanged++;
    }

    private void OnLocaleChanged(StringName channel, string locale, string previousLocale)
    {
        _localeChanges.Add($"{channel}|{locale}|{previousLocale}");
    }

    private void OnKeyMissing(StringName key, string locale)
    {
        _missingKeys.Add($"{key}|{locale}");
    }

    private void OnLocaleLoaded(string locale)
    {
        _loadedLocales.Add(locale);
    }
}
