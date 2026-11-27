using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.LocaleChannels;

/// <summary>
/// Full API test of Locale Channels: the channels of TranslationServer, the channel of catalogs, the channel locales of
/// LanguageSettingsServer and LanguageSettings, and ResourceLocalizationServer. Same tests as scripts/gd/Test.gd.
/// The project has two translations (de, fr, see internationalization/locale/translations).
/// Expected errors in the output: a custom channel in LanguageSettingsServer.SetLocaleFor(), the missing file logo_fr.res.
/// </summary>
public partial class Test : FeatureTest
{
    private const string DataDir = "user://locale_channels_cs";
    private const string RemapsSetting = "internationalization/locale/translation_remaps";
    private const string Voice = "res://voice/line_1.ogg";
    private const string Logo = "res://ui/logo.png";

    private readonly List<string> _events = [];
    private int _translationChanged;

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(DataDir + "/mods/polish/voice");
        string previousLocale = TranslationServer.GetLocale();
        LanguageSettingsServerInstance settings = LanguageSettingsServer.Singleton;
        settings.ApplyMode = SettingsServer.ApplyModeEnum.Immediate;
        settings.SetFilePath(DataDir + "/language.ini");
        TextLocalizationServer.Singleton.ClearCatalogs();
        ResourceLocalizationServer.Singleton.ClearCatalogs();
        TranslationServer.SetLocale("en");
        TestChannels();
        TestLocaleChanged();
        TestCustomChannel();
        TestCatalogChannel();
        TestTextChannels();
        await TestTranslationChangedNotification();
        TestAvailableLocales();
        TestLanguageSettingsResource();
        TestLanguageSettingsChannels();
        TestLanguageSettingsCustomChannel();
        TestSaveAndLoad();
        TestFileWithoutChannels();
        TestResourceChannels();
        TestResourcePaths();
        await TestResourceRelocalize();
        await TestResourceMod();
        TestResourceRemaps();
        TextLocalizationServer.Singleton.ClearCatalogs();
        ResourceLocalizationServer.Singleton.ClearCatalogs();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        settings.ResetToDefaults();
        TranslationServer.SetLocale(previousLocale);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged)
        {
            _translationChanged++;
        }
    }

    private void OnLocaleChanged(StringName channel, string locale, string previousLocale)
    {
        _events.Add($"{channel}|{locale}|{previousLocale}");
    }

    private static void ClearChannels()
    {
        foreach (string channel in TranslationServer.GetLocaleChannels())
        {
            TranslationServer.ClearLocaleFor(channel);
        }
    }

    private static TextLocalizationCatalog UiCatalog()
    {
        var catalog = new TextLocalizationCatalog();
        catalog.SetText("HELLO", "en", "Hello");
        catalog.SetText("HELLO", "de", "Hallo");
        return catalog;
    }

    private static TextLocalizationCatalog SubtitleCatalog()
    {
        var catalog = new TextLocalizationCatalog { Channel = "subtitles" };
        catalog.SetText("LINE", "en", "Where are we?");
        catalog.SetText("LINE", "de", "Wo sind wir?");
        catalog.SetText("LINE", "es", "¿Dónde estamos?");
        return catalog;
    }

    // A voice line (a Resource named after its locale) and a logo (ImageTexture, 8 px wide in English, 16 px in German).
    private static void WriteResourceFiles()
    {
        ResourceSaver.Save(new Resource { ResourceName = "en" }, DataDir + "/voice.tres");
        ResourceSaver.Save(new Resource { ResourceName = "de" }, DataDir + "/voice_de.tres");
        ResourceSaver.Save(new Resource { ResourceName = "pl" }, DataDir + "/mods/polish/voice/voice_pl.tres");
        ImageTexture logo = ImageTexture.CreateFromImage(Image.CreateEmpty(8, 4, false, Image.Format.Rgba8));
        ResourceSaver.Save(logo, DataDir + "/logo.res");
        ImageTexture logoDe = ImageTexture.CreateFromImage(Image.CreateEmpty(16, 4, false, Image.Format.Rgba8));
        ResourceSaver.Save(logoDe, DataDir + "/logo_de.res");
    }

    private async Task<string[]> Relocalized()
    {
        Variant[] args = await ToSignal(ResourceLocalizationServer.Singleton, ResourceLocalizationServer.SignalName.ResourcesRelocalized);
        return args[0].AsStringArray();
    }

    private void TestChannels()
    {
        string[] channels = TranslationServer.GetLocaleChannels();
        Expect(channels is ["interface", "audio", "subtitles"], "GetLocaleChannels lists the three default channels");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "en", "a channel without its own locale follows the general locale");
        TranslationServer.SetLocaleFor("interface", "de");
        TranslationServer.SetLocaleFor("audio", "ja");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "de", "SetLocaleFor sets the interface locale");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "ja", "SetLocaleFor sets the audio locale");
        ExpectEqual(TranslationServer.GetLocale(), "en", "SetLocaleFor leaves the general locale");
        TranslationServer.SetLocale("fr");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "de", "a channel locale survives a change of the general locale");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "fr", "a following channel moves with the general locale");
        TranslationServer.ClearLocaleFor("audio");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "fr", "ClearLocaleFor lets the channel follow again");
        TranslationServer.SetLocale("en");
        ClearChannels();
    }

    private void TestLocaleChanged()
    {
        TranslationServer.LocaleChanged += OnLocaleChanged;
        TranslationServer.SetLocaleFor("interface", "de");
        TranslationServer.SetLocale("fr");
        TranslationServer.ClearLocaleFor("interface");
        TranslationServer.LocaleChanged -= OnLocaleChanged;
        ExpectEqual(_events[0], "interface|de|en", "LocaleChanged(channel, locale, previousLocale) for a channel");
        ExpectEqual(_events[1], "|fr|en", "LocaleChanged has the channel \"\" for the general locale");
        ExpectEqual(_events[^1], "interface|fr|de", "LocaleChanged for ClearLocaleFor");
        TranslationServer.SetLocale("en");
    }

    private void TestCustomChannel()
    {
        TranslationServer.SetLocaleFor("tutorial", "pl");
        Expect(TranslationServer.GetLocaleChannels().Contains("tutorial"), "a custom channel is listed once set");
        ExpectEqual(TranslationServer.GetLocaleFor("tutorial"), "pl", "a custom channel has its own locale");
        TranslationServer.ClearLocaleFor("tutorial");
        ExpectEqual(TranslationServer.GetLocaleFor("tutorial"), "en", "a cleared custom channel follows the general locale");
    }

    private void TestCatalogChannel()
    {
        var textCatalog = new TextLocalizationCatalog();
        ExpectEqual(textCatalog.Channel, new StringName("interface"), "a text catalog is in the interface channel by default");
        var resourceCatalog = new ResourceLocalizationCatalog();
        ExpectEqual(resourceCatalog.Channel, new StringName(""), "a resource catalog decides by key type by default");
        foreach (string extension in new[] { "json", "lcat" })
        {
            string path = DataDir + "/subtitles." + extension;
            SubtitleCatalog().SaveToFile(path);
            var loaded = new TextLocalizationCatalog();
            loaded.LoadFromFile(path);
            ExpectEqual(loaded.Channel, new StringName("subtitles"), "Channel round-trips through ." + extension);
        }
        string tresPath = DataDir + "/subtitles.tres";
        ResourceSaver.Save(SubtitleCatalog(), tresPath);
        var tres = ResourceLoader.Load<TextLocalizationCatalog>(tresPath, "", ResourceLoader.CacheMode.Ignore);
        ExpectEqual(tres.Channel, new StringName("subtitles"), "Channel round-trips through .tres");
    }

    private void TestTextChannels()
    {
        TextLocalizationServer.Singleton.AddCatalog(UiCatalog());
        TextLocalizationServer.Singleton.AddCatalog(SubtitleCatalog());
        TranslationServer.SetLocaleFor("interface", "de");
        ExpectEqual(Tr("HELLO"), "Hallo", "Tr() follows the interface channel");
        ExpectEqual(TextLocalizationServer.Translate("HELLO"), "Hallo", "Translate() uses the channel of the key");
        TranslationDomain subtitlesDomain = TranslationServer.GetOrAddDomain("subtitles");
        ExpectEqual(subtitlesDomain.Translate("LINE"), new StringName("Where are we?"), "the domain subtitles follows the subtitles channel");
        ExpectEqual(Tr("LINE"), "LINE", "a subtitle key is not in the main domain");
        var node = new Node();
        node.SetTranslationDomain("subtitles");
        ExpectEqual(node.Tr("LINE"), "Where are we?", "the translation domain of a node picks the channel");
        node.Free();
        TranslationServer.SetLocaleFor("subtitles", "de");
        ExpectEqual(subtitlesDomain.Translate("LINE"), new StringName("Wo sind wir?"), "a subtitles channel change changes the subtitles");
        ExpectEqual(Tr("HELLO"), "Hallo", "a subtitles channel change leaves the interface texts");
        TranslationServer.ClearLocaleFor("subtitles");
        ExpectEqual(TextLocalizationServer.Translate("LINE"), "Where are we?", "a cleared channel follows the general locale");
        ClearChannels();
    }

    private async Task TestTranslationChangedNotification()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        int before = _translationChanged;
        TranslationServer.SetLocaleFor("audio", "de");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ExpectEqual(_translationChanged, before, "an audio-only change sends no NotificationTranslationChanged");
        TranslationServer.SetLocaleFor("subtitles", "de");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Expect(_translationChanged > before, "a subtitles change sends NotificationTranslationChanged");
        ClearChannels();
    }

    private void TestAvailableLocales()
    {
        string[] subtitleLocales = TextLocalizationServer.Singleton.GetAvailableLocalesFor("subtitles");
        Expect(subtitleLocales is ["en", "de", "es"], "TextLocalizationServer.GetAvailableLocalesFor lists the locales of the channel in catalog order");
        Expect(TextLocalizationServer.Singleton.GetAvailableLocalesFor("audio").Length == 0, "TextLocalizationServer.GetAvailableLocalesFor a channel without catalogs is empty");
        string[] settingsSubtitleLocales = LanguageSettingsServer.GetAvailableLocalesFor("subtitles");
        Expect(settingsSubtitleLocales is ["de", "en", "es"], "LanguageSettingsServer.GetAvailableLocalesFor is sorted");
        string[] interfaceLocales = LanguageSettingsServer.GetAvailableLocalesFor("interface");
        Expect(interfaceLocales.Contains("de"), "interface: the locales of the catalogs");
        Expect(interfaceLocales.Contains("fr"), "interface: also the locales of the project translations");
        Expect(!interfaceLocales.Contains("es"), "interface: not a subtitle-only locale");
        string[] allLocales = LanguageSettingsServer.GetAvailableLocales();
        Expect(allLocales.Contains("es"), "GetAvailableLocales has the locales of all catalogs");
        Expect(LanguageSettingsServer.GetAvailableLocalesFor("audio").SequenceEqual(allLocales), "a channel no catalog declares offers all locales");
        TextLocalizationServer.Singleton.ClearCatalogs();
    }

    private void TestLanguageSettingsResource()
    {
        var resource = new LanguageSettings();
        ExpectEqual(resource.InterfaceLocale, "", "LanguageSettings.InterfaceLocale is empty by default (same as language)");
        ExpectEqual(resource.AudioLocale, "", "LanguageSettings.AudioLocale is empty by default");
        ExpectEqual(resource.SubtitlesLocale, "", "LanguageSettings.SubtitlesLocale is empty by default");
        resource.InterfaceLocale = "de";
        resource.AudioLocale = "ja";
        resource.SubtitlesLocale = "fr";
        ExpectEqual(resource.InterfaceLocale, "de", "LanguageSettings.InterfaceLocale is a plain property");
        ExpectEqual(resource.AudioLocale, "ja", "LanguageSettings.AudioLocale is a plain property");
        ExpectEqual(resource.SubtitlesLocale, "fr", "LanguageSettings.SubtitlesLocale is a plain property");
        LanguageSettingsServerInstance settings = LanguageSettingsServer.Singleton;
        settings.ResetToDefaults();
        string[] names = settings.GetSettingNames();
        foreach (string setting in new[] { "interface_locale", "audio_locale", "subtitles_locale" })
        {
            Expect(names.Contains(setting), "LanguageSettingsServer has the setting " + setting);
            ExpectEqual(settings.GetDefaultValue(setting).AsString(), "", $"the default of {setting} is empty");
        }
    }

    private void TestLanguageSettingsChannels()
    {
        LanguageSettingsServer.SetLocale("fr");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("audio"), "", "GetLocaleFor is empty for a channel that follows the language");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor(""), "fr", "GetLocaleFor(\"\") is the language");
        LanguageSettingsServer.SetLocaleFor("interface", "de");
        LanguageSettingsServer.SetLocaleFor("audio", "de");
        LanguageSettingsServer.SetLocaleFor("subtitles", "fr");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("interface"), "de", "GetLocaleFor returns the stored interface locale");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "de", "SetLocaleFor applies the interface locale");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "de", "SetLocaleFor applies the audio locale");
        ExpectEqual(TranslationServer.GetLocale(), "fr", "SetLocaleFor leaves the language");
        ExpectEqual(LanguageSettingsServer.Singleton.GetValue("audio_locale").AsString(), "de", "the setting audio_locale has the value");
        LanguageSettingsServer.SetLocaleFor("audio", "");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "fr", "an empty channel locale follows the language again");
        LanguageSettingsServer.SetLocale("de");
        ExpectEqual(TranslationServer.GetLocaleFor("audio"), "de", "a language change moves the channels that follow it");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "fr", "a channel with its own locale stays");
        LanguageSettingsServer.Singleton.ResetToDefaults();
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), TranslationServer.GetLocale(), "ResetToDefaults clears the channels");
    }

    private void TestLanguageSettingsCustomChannel()
    {
        LanguageSettingsServer.SetLocale("fr");
        LanguageSettingsServer.SetLocaleFor("tutorial", "de");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("tutorial"), "", "a custom channel is not stored");
        ExpectEqual(TranslationServer.GetLocaleFor("tutorial"), "fr", "a custom channel follows the language");
        LanguageSettingsServer.Singleton.ResetToDefaults();
    }

    private void TestSaveAndLoad()
    {
        LanguageSettingsServerInstance settings = LanguageSettingsServer.Singleton;
        LanguageSettingsServer.SetLocale("fr");
        LanguageSettingsServer.SetLocaleFor("interface", "de");
        LanguageSettingsServer.SetLocaleFor("audio", "de");
        ExpectEqual(settings.Save(), Error.Ok, "Save writes the channels");
        string text = FileAccess.GetFileAsString(DataDir + "/language.ini");
        Expect(text.Contains("interface_locale"), "the file has interface_locale");
        Expect(text.Contains("audio_locale"), "the file has audio_locale");
        settings.ResetToDefaults();
        ExpectEqual(settings.Load(), Error.Ok, "Load reads the file");
        ExpectEqual(settings.Apply(), Error.Ok, "Apply uses the loaded values");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("interface"), "de", "interface_locale is loaded");
        ExpectEqual(LanguageSettingsServer.GetLocaleFor("subtitles"), "", "subtitles_locale stays empty");
        ExpectEqual(TranslationServer.GetLocaleFor("interface"), "de", "the loaded interface locale is applied");
        ExpectEqual(TranslationServer.GetLocaleFor("subtitles"), "fr", "the subtitles follow the loaded language");
        settings.ResetToDefaults();
    }

    private void TestFileWithoutChannels()
    {
        LanguageSettingsServerInstance settings = LanguageSettingsServer.Singleton;
        string path = DataDir + "/before_channels.json";
        using (FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            file.StoreString("{ \"locale\": \"fr\" }");
        }
        settings.SetFilePath(path);
        ExpectEqual(settings.Load(), Error.Ok, "a file written before the channels loads");
        settings.Apply();
        ExpectEqual(LanguageSettingsServer.GetLocale(), "fr", "old file: the language is loaded");
        foreach (string channel in new[] { "interface", "audio", "subtitles" })
        {
            ExpectEqual(TranslationServer.GetLocaleFor(channel), "fr", $"old file: {channel} follows the language");
        }
        settings.SetFilePath(DataDir + "/language.ini");
        settings.ResetToDefaults();
    }

    private void TestResourceChannels()
    {
        TranslationServer.SetLocale("en");
        TranslationServer.SetLocaleFor("interface", "de");
        TranslationServer.SetLocaleFor("audio", "ja");
        var catalog = new ResourceLocalizationCatalog();
        catalog.SetExpectedType(Voice, "AudioStream");
        catalog.SetLocalizedPath(Voice, "de", "res://voice/line_1_de.ogg");
        catalog.SetLocalizedPath(Voice, "ja", "res://voice/line_1_ja.ogg");
        catalog.SetLocalizedPath(Logo, "de", "res://ui/logo_de.png");
        catalog.SetLocalizedPath(Logo, "ja", "res://ui/logo_ja.png");
        ResourceLocalizationServer.Singleton.AddCatalog(catalog);
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(Voice), "res://voice/line_1_ja.ogg", "an AudioStream follows the audio channel");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(Logo), "res://ui/logo_de.png", "other resources follow the interface channel");
        TranslationServer.ClearLocaleFor("audio");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(Voice), Voice, "a cleared audio channel follows the general locale (en: the original)");
        ResourceLocalizationServer.Singleton.ClearCatalogs();
        ClearChannels();
    }

    private void TestResourcePaths()
    {
        WriteResourceFiles();
        string voicePath = DataDir + "/voice.tres";
        string logoPath = DataDir + "/logo.res";
        string otherPath = DataDir + "/other.tres";
        var game = new ResourceLocalizationCatalog();
        game.SetLocalizedPath(voicePath, "de", DataDir + "/voice_de.tres");
        game.SetLocalizedPath(logoPath, "de", DataDir + "/logo_de.res");
        game.SetLocalizedPath(logoPath, "fr", DataDir + "/logo_fr.res");
        ResourceLocalizationServer.Singleton.AddCatalog(game);
        Expect(ResourceLocalizationServer.HasLocalization(voicePath), "HasLocalization is true for a key");
        Expect(!ResourceLocalizationServer.HasLocalization(otherPath), "HasLocalization is false for another path");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(voicePath), voicePath, "GetLocalizedPath without an English entry is the original");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(voicePath, "de_DE"), DataDir + "/voice_de.tres", "GetLocalizedPath for de_DE takes de");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(otherPath, "de"), otherPath, "GetLocalizedPath of an unknown path is the path");
        Godot.Collections.Dictionary<string, string> paths = ResourceLocalizationServer.GetLocalizedPaths(logoPath);
        ExpectEqual(paths.Count, 2, "GetLocalizedPaths lists two locales");
        ExpectEqual(paths["de"], DataDir + "/logo_de.res", "GetLocalizedPaths has the de path");
        ExpectEqual(paths["fr"], DataDir + "/logo_fr.res", "GetLocalizedPaths has the fr path");
        Expect(ResourceLocalizationServer.GetMissingLocales(voicePath) is ["fr"], "GetMissingLocales lists the locales without a path");
    }

    private async Task TestResourceRelocalize()
    {
        string voicePath = DataDir + "/voice.tres";
        string logoPath = DataDir + "/logo.res";
        // Engine-cached resources: shared, not disposed here.
        Resource voice = GD.Load(voicePath);
        Texture2D logo = GD.Load<Texture2D>(logoPath);
        ExpectEqual(voice.ResourceName, "en", "Load() gives the English voice");
        ExpectEqual(logo.GetWidth(), 8, "Load() gives the English logo");
        Task<string[]> relocalized = Relocalized();
        TranslationServer.SetLocale("de");
        string[] paths = await relocalized;
        Expect(paths.Contains(voicePath), "ResourcesRelocalized lists the voice");
        Expect(paths.Contains(logoPath), "ResourcesRelocalized lists the logo");
        Expect(!ResourceLocalizationServer.IsRelocalizing(), "IsRelocalizing is false after the signal");
        ExpectEqual(voice.ResourceName, "de", "the cached voice is updated in place");
        ExpectEqual(logo.GetWidth(), 16, "the cached logo is updated in place");
        Expect(GD.Load(voicePath) == voice, "Load() returns the same instance");
        relocalized = Relocalized();
        TranslationServer.SetLocale("fr");
        await relocalized;
        ExpectEqual(logo.GetWidth(), 8, "a missing localized file falls back to the original");
    }

    private async Task TestResourceMod()
    {
        Resource voice = GD.Load(DataDir + "/voice.tres");
        var mod = new ResourceLocalizationCatalog();
        mod.SetLocalizedPath(DataDir + "/voice.tres", "pl", DataDir + "/mods/polish/voice/voice_pl.tres");
        ExpectEqual(mod.SaveToFile(DataDir + "/mods/polish/voice/voice.lcat"), Error.Ok, "the mod catalog is saved as .lcat");
        var manifest = new FilesManifest { TypeHint = "ResourceLocalizationCatalog", FolderPaths = [DataDir + "/mods/*/voice"] };
        ExpectEqual(ResourceLocalizationServer.Singleton.AddCatalogManifest(manifest, 100), 1, "AddCatalogManifest adds the mod catalog");
        Task<string[]> relocalized = Relocalized();
        TranslationServer.SetLocale("pl");
        await relocalized;
        ExpectEqual(voice.ResourceName, "pl", "the Polish voice comes from the mod");
        Expect(ResourceLocalizationServer.Singleton.GetAvailableLocales().Contains("pl"), "GetAvailableLocales has the locale of the mod");
    }

    private void TestResourceRemaps()
    {
        ResourceLocalizationServer.Singleton.ClearCatalogs();
        string voicePath = DataDir + "/voice.tres";
        Variant previousRemaps = ProjectSettings.GetSetting(RemapsSetting, new Godot.Collections.Dictionary());
        var remaps = new Godot.Collections.Dictionary { { voicePath, new[] { DataDir + "/voice_de.tres:de" } } };
        ProjectSettings.SetSetting(RemapsSetting, remaps);
        ExpectEqual(ResourceLocalizationServer.Singleton.LoadProjectCatalogs(), 1, "LoadProjectCatalogs imports translation_remaps");
        ExpectEqual(ResourceLocalizationServer.GetLocalizedPath(voicePath, "de"), DataDir + "/voice_de.tres", "a translation_remaps entry is resolved");
        ProjectSettings.SetSetting(RemapsSetting, previousRemaps);
    }
}
