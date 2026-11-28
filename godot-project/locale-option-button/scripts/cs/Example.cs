using Godot;

namespace TruelyInteractive.LocaleOptionButtons;

/// <summary>Minimal example of Locale Option Button: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Where the list comes from.
        string[] locales = LanguageSettingsServer.GetAvailableLocalesFor("subtitles");
        GD.Print("Subtitle locales: ", string.Join(", ", locales));

        // Adding the menu.
        var menu = new LocaleOptionButton();
        menu.LocaleSelected += locale => GD.Print("Language: ", locale);
        AddChild(menu);

        // Channels.
        var subtitles = new LocaleOptionButton { Target = "subtitles" };
        AddChild(subtitles);

        GetTree().Quit();
    }
}
