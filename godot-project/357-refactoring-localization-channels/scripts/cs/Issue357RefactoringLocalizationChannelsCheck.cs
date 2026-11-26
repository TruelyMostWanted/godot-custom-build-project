using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue357RefactoringLocalizationChannels;

/// <summary>C# checks of 357-refactoring-localization-channels. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue357RefactoringLocalizationChannelsCheck : FeatureCheckCs
{
    private const string DataDir = "user://check_357_cs";
    private const string Voice = "res://voice/line_1.ogg";
    private const string Logo = "res://ui/logo.png";

    private readonly Array _events = new();
    private int _translationChanged;

    protected override string CheckName => "357-refactoring-localization-channels";

    // This node is in the tree: it counts NOTIFICATION_TRANSLATION_CHANGED.
    public override void _Notification(int what)
    {
        if (what == (int)NotificationTranslationChanged)
        {
            _translationChanged++;
        }
    }

    private static TextLocalizationCatalog UiCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("HELLO", "en", "Hello");
        catalog.SetText("HELLO", "de", "Hallo");
        catalog.SetText("HELLO", "ja", "konnichiwa");
        return catalog;
    }

    private static TextLocalizationCatalog SubtitleCatalog()
    {
        var catalog = new TextLocalizationCatalog { Channel = "subtitles" };
        catalog.SetText("LINE", "en", "Where are we?");
        catalog.SetText("LINE", "de", "Wo sind wir?");
        catalog.SetText("LINE", "ja", "doko desu ka");
        return catalog;
    }

    private static void ResetChannels()
    {
        foreach (string channel in TranslationServer.GetLocaleChannels())
        {
            TranslationServer.ClearLocaleFor(channel);
        }
    }

    protected override async Task RunChecks()
    {
        TextLocalizationServer textServer = TextLocalizationServer.Singleton;
        ResourceLocalizationServer resourceServer = ResourceLocalizationServer.Singleton;
        string previousLocale = TranslationServer.GetLocale();
        textServer.ClearCatalogs();
        resourceServer.ClearCatalogs();
        ResetChannels();
        TranslationServer.SetLocale("en");
        var onLocaleChanged = Callable.From((StringName channel, string locale, string previous) => _events.Add(new Array { channel, locale, previous }));
        TranslationServer.Singleton.Connect("locale_changed", onLocaleChanged);

        // Removed API.
        Expect(!ClassDB.ClassExists("LocalizationCatalogList"), "LocalizationCatalogList is removed");
        Expect(!ClassDB.ClassHasMethod("LocalizationServer", "set_locale", true), "LocalizationServer.set_locale is removed");
        Expect(!ClassDB.ClassHasMethod("TextLocalizationServer", "get_translation_key", true), "get_translation_key is removed");
        Expect(!ClassDB.ClassHasMethod("LocalizationServer", "add_catalog_list", true), "add_catalog_list is removed");
        Expect(!ProjectSettings.HasSetting("internationalization/localization/external_catalogs"), "external_catalogs setting is removed");
        Expect(ProjectSettings.HasSetting("internationalization/localization/catalog_manifests"), "setting catalog_manifests");

        // Channels: UI German, audio Japanese, subtitles English.
        string[] channels = TranslationServer.GetLocaleChannels();
        Expect(channels.Contains("interface") && channels.Contains("audio") && channels.Contains("subtitles"), "default channels: " + string.Join(",", channels));
        TranslationServer.SetLocaleFor("interface", "de");
        ExpectEvent("interface", "de", "en", "locale_changed(channel, locale, previous_locale)");
        TranslationServer.SetLocaleFor("audio", "ja");
        TranslationServer.SetLocaleFor("subtitles", "en");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "de", "GetLocaleFor interface");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "ja", "GetLocaleFor audio");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "en", "GetLocaleFor subtitles");
        ExpectEqual(TranslationServer.GetLocale(), "en", "the general locale is untouched");
        TranslationServer.SetLocale("fr");
        ExpectEvent("", "fr", "en", "the general locale emits the channel \"\"");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "de", "an override survives a general change");
        TranslationServer.SetLocale("en");
        TranslationServer.SetLocaleFor("custom", "pl");
        Expect(TranslationServer.GetLocaleChannels().Contains("custom"), "a custom channel is listed once set");
        TranslationServer.ClearLocaleFor("custom");
        ExpectEqual(TranslationServer.GetLocaleFor("custom"), "en", "empty override -> general locale");

        // Text catalogs: Tr() follows interface, the domain "subtitles" follows subtitles.
        textServer.AddCatalog(UiCatalog());
        textServer.AddCatalog(SubtitleCatalog());
        ExpectEqual(Tr("HELLO"), "Hallo", "Tr() follows the interface channel");
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Hallo", "Translate() uses the channel locale of the key");
        TranslationDomain subtitlesDomain = TranslationServer.GetOrAddDomain("subtitles");
        ExpectEqual((string)subtitlesDomain.Translate("LINE"), "Where are we?", "domain subtitles follows the subtitles channel");
        ExpectEqual(Tr("LINE"), "LINE", "a subtitle key is not in the main domain");
        var node = new Node { TranslationDomain = "subtitles" };
        ExpectEqual(node.Tr("LINE"), "Where are we?", "Node.TranslationDomain picks the channel");
        ExpectEqual(TextLocalizationServer.TranslateForLocale("de", "LINE"), "Wo sind wir?", "TranslateForLocale");
        TranslationServer.SetLocaleFor("subtitles", "de");
        ExpectEqual((string)subtitlesDomain.Translate("LINE"), "Wo sind wir?", "subtitles channel change");
        ExpectEqual(Tr("HELLO"), "Hallo", "interface texts stay");
        TranslationServer.ClearLocaleFor("subtitles");
        ExpectEqual(TextLocalizationServer.Translate("LINE"), "Where are we?", "cleared override follows the general locale");
        Expect(TextLocalizationServer.GetMissingKeys("fr").Contains("HELLO"), "GetMissingKeys(locale)");
        node.Free();

        // NOTIFICATION_TRANSLATION_CHANGED: a channel with a domain notifies, an audio-only change does not.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        int before = _translationChanged;
        TranslationServer.SetLocaleFor("audio", "de");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(_translationChanged, before, "an audio-only change sends no notification");
        TranslationServer.SetLocaleFor("subtitles", "fr");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(_translationChanged > before, "a subtitles change sends NOTIFICATION_TRANSLATION_CHANGED");
        TranslationServer.ClearLocaleFor("subtitles");
        TranslationServer.SetLocaleFor("audio", "ja");

        // Resource catalog: an AudioStream key follows the audio channel only.
        var resources = new ResourceLocalizationCatalog();
        ExpectEqual(resources.Channel.ToString(), "", "ResourceLocalizationCatalog default channel is by key type");
        resources.SetExpectedType(Voice, "AudioStream");
        resources.SetLocalizedPath(Voice, "de", "res://voice/line_1_de.ogg");
        resources.SetLocalizedPath(Voice, "ja", "res://voice/line_1_ja.ogg");
        resources.SetLocalizedPath(Logo, "de", "res://ui/logo_de.png");
        resources.SetLocalizedPath(Logo, "ja", "res://ui/logo_ja.png");
        resourceServer.AddCatalog(resources);
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(Voice), "res://voice/line_1_ja.ogg", "the voice file follows audio (ja)");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(Logo), "res://ui/logo_de.png", "other resources follow interface (de)");
        TranslationServer.SetLocaleFor("audio", "de");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(Voice), "res://voice/line_1_de.ogg", "audio channel change");
        TranslationServer.ClearLocaleFor("audio");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(Voice), Voice, "audio follows the general locale (en, no entry)");
        TranslationServer.SetLocaleFor("audio", "ja");

        // The channel property round trips through the file formats.
        DirAccess.MakeDirRecursiveAbsolute(DataDir + "/formats");
        foreach (string extension in new[] { "json", "yaml", "lcat", "tres" })
        {
            string path = DataDir + "/formats/subs." + extension;
            ExpectEqual(SubtitleCatalog().SaveToFile(path), Error.Ok, "save " + extension);
            var loaded = ResourceLoader.Load(path, "TextLocalizationCatalog", ResourceLoader.CacheMode.Ignore) as TextLocalizationCatalog;
            Expect(loaded != null && loaded.Channel.ToString() == "subtitles", "channel round trip: " + extension);
        }
        string[] extensions = LocalizationCatalog.GetFileExtensions();
        Expect(extensions.Contains("lcat") && extensions.Contains("json"), "GetFileExtensions");

        // Mods: one manifest with a wildcard folder, the list order is the priority.
        textServer.ClearCatalogs();
        foreach (string mod in new[] { "a", "b", "c" })
        {
            DirAccess.MakeDirRecursiveAbsolute(DataDir + "/mods/" + mod + "/translations");
        }
        UiCatalog().SaveToFile(DataDir + "/game.lcat");
        var modJson = new TextLocalizationCatalog();
        modJson.SetText("MOD_JSON", "de", "aus json");
        modJson.SaveToFile(DataDir + "/mods/a/translations/mod.json");
        var modYaml = new TextLocalizationCatalog { Channel = "subtitles" };
        modYaml.SetText("MOD_YAML", "en", "aus yaml");
        modYaml.SaveToFile(DataDir + "/mods/b/translations/mod.yaml");
        var modLcat = new TextLocalizationCatalog();
        modLcat.SetText("HELLO", "de", "Servus");
        modLcat.SaveToFile(DataDir + "/mods/c/translations/mod.lcat");
        var baseManifest = new FilesManifest { FilePaths = new[] { DataDir + "/game.lcat" } };
        var modManifest = new FilesManifest { TypeHint = "TextLocalizationCatalog", FolderPaths = new[] { DataDir + "/mods/*/translations" } };
        ExpectEqual(textServer.AddCatalogManifest(baseManifest), 1, "AddCatalogManifest loads the game");
        ExpectEqual(textServer.AddCatalogManifest(modManifest, 10), 3, "AddCatalogManifest loads json, yaml and lcat of every mod");
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Servus", "the mod (higher priority) wins");
        ExpectEqual(TextLocalizationServer.Translate("MOD_JSON"), "aus json", "json mod (interface, de)");
        ExpectEqual((string)subtitlesDomain.Translate("MOD_YAML"), "aus yaml", "yaml mod follows its channel (subtitles, en)");

        // The locale chain: the closest other locale comes before the fallback.
        textServer.ClearCatalogs();
        var austrian = new TextLocalizationCatalog();
        austrian.SetText("HELLO", "de_AT", "Servus");
        austrian.SetText("HELLO", "en", "Hello");
        textServer.AddCatalog(austrian);
        string[] chain = textServer.GetLocaleChain("de_DE");
        Expect(chain.Length >= 3 && chain[0] == "de_DE" && chain[1] == "de" && chain[2] == "de_AT", "chain de_DE -> de -> de_AT: " + string.Join(",", chain));
        TranslationServer.SetLocaleFor("interface", "de_DE");
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Servus", "de_DE falls back to de_AT before en");

        TranslationServer.Singleton.Disconnect("locale_changed", onLocaleChanged);
        textServer.ClearCatalogs();
        resourceServer.ClearCatalogs();
        ResetChannels();
        TranslationServer.SetLocale(previousLocale);
    }

    private void ExpectEvent(string channel, string locale, string previous, string message)
    {
        Expect(_events.Count > 0, message + " (no event)");
        if (_events.Count == 0)
        {
            return;
        }
        var last = (Array)_events[^1];
        Expect(last[0].AsStringName().ToString() == channel && (string)last[1] == locale && (string)last[2] == previous, message);
    }
}
