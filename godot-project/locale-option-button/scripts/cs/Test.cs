using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.LocaleOptionButtons;

/// <summary>Full API test of the Locale Option Button (LocaleOptionButton): native names, project locales, fallback, channels. Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    protected override Task RunTests()
    {
        string originalLocale = TranslationServer.GetLocale();
        var translations = new List<Translation>();
        foreach (string locale in new[] { "fr", "ja", "de_DE", "de_AT" })
        {
            var translation = new Translation { Locale = locale };
            TranslationServer.AddTranslation(translation);
            translations.Add(translation);
        }

        // Native names.
        Expect(LocaleOptionButton.GetNativeName("de") == "Deutsch", "native name of de");
        Expect(LocaleOptionButton.GetNativeName("fr") == "Français", "native name of fr");
        Expect(LocaleOptionButton.GetNativeName("ja") == "日本語", "native name of ja");
        Expect(LocaleOptionButton.GetNativeName("pt_BR") == "Português (Brasil)", "native name of pt_BR");
        Expect(LocaleOptionButton.GetNativeName("de_AT", true).StartsWith("Deutsch ("), "region is appended on request");

        // The list shows the locales of the project.
        var menu = new LocaleOptionButton();
        AddChild(menu);
        string[] locales = menu.GetLocales();
        Expect(Array.IndexOf(locales, "fr") >= 0 && Array.IndexOf(locales, "ja") >= 0, "the list has the translations of the project");
        Expect(menu.ItemCount == locales.Length, "one entry per locale");
        Expect(menu.GetItemText(Array.IndexOf(locales, "fr")) == "Français", "entries show native names");
        Expect(menu.GetItemText(Array.IndexOf(locales, "de_AT")) != menu.GetItemText(Array.IndexOf(locales, "de_DE")), "locales of one language are told apart");
        menu.ShowNativeNames = false;
        Expect(menu.GetItemText(Array.IndexOf(menu.GetLocales(), "fr")) == TranslationServer.GetLocaleName("fr"), "show_native_names off shows engine names");
        menu.ShowNativeNames = true;

        // The fallback locale.
        string fallback = TranslationServer.GetFallbackLocale();
        menu.IncludeFallback = true;
        Expect(Array.IndexOf(menu.GetLocales(), fallback) >= 0, "include_fallback lists the fallback locale");
        menu.IncludeFallback = false;

        // Selecting sets the locale of the target; the selection follows locale_changed.
        var selected = new List<string>();
        menu.LocaleSelected += locale => selected.Add(locale);
        var subtitles = new LocaleOptionButton { Target = "subtitles" };
        AddChild(subtitles);
        TranslationServer.SetLocale("fr");
        Expect(menu.GetSelectedLocale() == "fr", "the selection follows the language");
        menu.EmitSignal(OptionButton.SignalName.ItemSelected, Array.IndexOf(menu.GetLocales(), "ja"));
        Expect(TranslationServer.GetLocale() == "ja", "selecting sets the language");
        Expect(menu.GetSelectedLocale() == "ja", "the selection shows the language");
        Expect(selected.Count == 1 && selected[0] == "ja", "locale_selected is emitted");
        subtitles.EmitSignal(OptionButton.SignalName.ItemSelected, Array.IndexOf(subtitles.GetLocales(), "fr"));
        Expect(TranslationServer.GetLocaleFor("subtitles") == "fr", "the subtitles channel is set alone");
        Expect(TranslationServer.GetLocale() == "ja", "the language stays");
        Expect(menu.GetSelectedLocale() == "ja", "the general menu keeps its entry");
        TranslationServer.ClearLocaleFor("subtitles");
        Expect(subtitles.GetSelectedLocale() == "ja", "a channel without own locale follows the language");

        menu.QueueFree();
        subtitles.QueueFree();
        foreach (Translation translation in translations)
        {
            TranslationServer.RemoveTranslation(translation);
        }
        foreach (string channel in new[] { "interface", "audio", "subtitles" })
        {
            LanguageSettingsServer.SetLocaleFor(channel, "");
        }
        LanguageSettingsServer.SetLocale("");
        TranslationServer.SetLocale(originalLocale);
        return Task.CompletedTask;
    }
}
