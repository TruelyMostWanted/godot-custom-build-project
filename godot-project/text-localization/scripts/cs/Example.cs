using Godot;
using Godot.Collections;

namespace TruelyInteractive.TextLocalization;

/// <summary>Minimal example of Text Localization: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Setup: a game lists ui.csv under Catalog Manifests and player_name in text_values.tres; here both come from code.
        var game = new TextLocalizationCatalog();
        game.LoadFromFile("res://text-localization/resources/ui.csv");
        TextLocalizationServer.Singleton.AddCatalog(game);
        TextInsertionServer.SetValue("player_name", "Traveler");

        // Using the API
        TranslationServer.SetLocale("de");
        GD.Print(string.Join(", ", TextLocalizationServer.Singleton.GetLocaleChain("de_AT")));
        GD.Print(TextLocalizationServer.Translate("menu.play"));
        GD.Print(Tr("menu.quit"));
        GD.Print(TextLocalizationServer.Translate("unknown.key"));
        GD.Print(TextLocalizationServer.Translate("enemy.count", "", 3, new Dictionary { { "count", 3 } }));
        GD.Print(TextInsertionServer.Insert(TextLocalizationServer.Translate("npc.greeting")));
        GD.Print(TextLocalizationServer.TranslateForLocale("en", "menu.play"));
        int quitId = TextLocalizationServer.GetKeyId("menu.quit");
        GD.Print(TextLocalizationServer.TranslateId(quitId));

        // The greeting in the player's language (GreetingLabel and LanguageMenu are nodes; their core lines)
        var greeting = new Label();
        greeting.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        greeting.Text = TextInsertionServer.Insert(TextLocalizationServer.Translate("npc.greeting"));
        GD.Print(greeting.Text);
        foreach (string locale in TextLocalizationServer.Singleton.GetAvailableLocales())
        {
            GD.Print(locale, ": ", TranslationServer.GetLocaleName(locale));
        }
        greeting.Free();

        // A French mod (the wiki loads it from user://mods/french/translations/ui.csv)
        var french = new TextLocalizationCatalog();
        french.LoadFromFile("res://text-localization/resources/french_ui.csv");
        TextLocalizationServer.Singleton.AddCatalog(french, 100);
        TranslationServer.SetLocale("fr");
        GD.Print(TextLocalizationServer.Translate("enemy.count", "", 3, new Dictionary { { "count", 3 } }));
        TextLocalizationServer.Singleton.RemoveCatalog(french);

        TextLocalizationServer.Singleton.ClearCatalogs();
        TextInsertionServer.RemoveValue("player_name");
        TranslationServer.SetLocale("en");
        GetTree().Quit();
    }
}
