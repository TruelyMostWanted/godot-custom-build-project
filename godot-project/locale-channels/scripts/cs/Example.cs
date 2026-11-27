using Godot;

namespace TruelyInteractive.LocaleChannels;

/// <summary>Minimal example of Locale Channels: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private static readonly string[] MenuLists = ["LanguageList", "InterfaceList", "AudioList", "SubtitlesList"];

    public override void _Ready()
    {
        // Set a channel.
        TranslationServer.LocaleChanged += (channel, locale, previous) =>
            GD.Print($"[{channel}] {previous} -> {locale}");

        TranslationServer.SetLocale("en");
        TranslationServer.SetLocaleFor("interface", "de");
        TranslationServer.SetLocaleFor("audio", "ja");
        GD.Print(TranslationServer.GetLocaleFor("subtitles"));
        GD.Print(string.Join(", ", TranslationServer.GetLocaleChannels()));

        TranslationServer.ClearLocaleFor("audio");

        // Choose the channel of a control. The labels are added here; in a game they are in the scene.
        AddChild(new Label { Name = "SubtitleLabel" });
        AddChild(new Label { Name = "TutorialLabel" });
        GetNode<Label>("SubtitleLabel").SetTranslationDomain("subtitles");
        GetNode<Label>("TutorialLabel").SetTranslationDomain("tutorial");
        TranslationServer.SetLocaleFor("tutorial", "fr");
        GD.Print("Tutorial label locale: ", TranslationServer.GetLocaleFor(GetNode<Label>("TutorialLabel").GetTranslationDomain()));

        // Store the player's choice. Another file than the game's user://settings/language.ini, so other scenes start clean.
        LanguageSettingsServer.Singleton.SetFilePath("user://locale_channels/language.ini");
        LanguageSettingsServer.SetLocale("en");
        LanguageSettingsServer.SetLocaleFor("interface", "de");
        LanguageSettingsServer.SetLocaleFor("audio", "ja");
        LanguageSettingsServer.SetLocaleFor("subtitles", "fr");
        GD.Print(LanguageSettingsServer.GetLocaleFor("audio"));
        GD.Print(TranslationServer.GetLocaleFor("audio"));
        LanguageSettingsServer.SetLocaleFor("audio", "");
        LanguageSettingsServer.Singleton.Save();

        // A complete example: a language menu. Picking an entry needs user input; the filled lists are printed.
        var menu = new LanguageMenu();
        foreach (string listName in MenuLists)
        {
            menu.AddChild(new OptionButton { Name = listName });
        }
        AddChild(menu);
        foreach (string listName in MenuLists)
        {
            var list = menu.GetNode<OptionButton>(listName);
            GD.Print(listName, ": ", list.GetItemText(list.Selected), " of ", list.ItemCount, " entries");
        }

        LanguageSettingsServer.Singleton.ResetToDefaults();
        TranslationServer.ClearLocaleFor("tutorial");
        GetTree().Quit();
    }
}
