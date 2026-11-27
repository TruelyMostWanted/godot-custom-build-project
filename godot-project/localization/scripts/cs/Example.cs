using Godot;

namespace TruelyInteractive.Localization;

/// <summary>Minimal example of Localization: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Migrate from CSV and translation files.
        // The guide runs this as an EditorScript; here it runs in _Ready() and saves to user://.
        var catalog = new TextLocalizationCatalog();
        foreach (string path in ProjectSettings.GetSetting("internationalization/locale/translations").AsStringArray())
        {
            catalog.AddTranslation(GD.Load<Translation>(path));
        }
        catalog.SaveToFile("user://game.lcat");
        GD.Print(catalog.KeyCount, " keys in ", string.Join(", ", catalog.Locales));
        GetTree().Quit();
    }
}
