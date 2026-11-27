using Godot;

namespace TruelyInteractive.LanguageSettingsGuide;

/// <summary>Minimal example of Language Settings: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API
        // The locales of the loaded translations, for a language menu.
        GD.Print(string.Join(", ", LanguageSettingsServer.GetAvailableLocales())); // de, fr

        // Set: applied to the TranslationServer at once (APPLY_MODE_IMMEDIATE, the default).
        LanguageSettingsServer.SetLocale("fr");

        // Get: the player's choice, and the locale in use. An empty locale follows the system.
        GD.Print(LanguageSettingsServer.GetLocale()); // fr
        GD.Print(TranslationServer.GetLocale()); // fr

        // Methods of the base class SettingsServer go through Singleton.
        // In APPLY_MODE_MANUAL, changes wait for Apply().
        LanguageSettingsServer.Singleton.Apply();
        // Autosave writes the file at the end of the frame; Save() writes it now.
        LanguageSettingsServer.Singleton.Save();
        // Load() reads the file into the pending values; Apply() pushes them to the TranslationServer.
        LanguageSettingsServer.Singleton.Load();
        LanguageSettingsServer.Singleton.Apply();

        // A language button
        var button = new LanguageButton();
        AddChild(button);
        GD.Print("Items: ", button.ItemCount);
        GD.Print("Selected: ", button.GetItemMetadata(button.Selected));

        LanguageSettingsServer.Singleton.ResetToDefaults();
        LanguageSettingsServer.Singleton.Save();
        GetTree().Quit();
    }
}
