using System.Collections.Generic;
using Godot;

namespace TruelyInteractive.LocalizationCatalogs;

/// <summary>
/// Minimal example of Localization Catalogs: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.
/// The guide's res://localization/ui.csv is resources/ui.csv here; files are written to user:// instead of res://.
/// </summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Using the API
        var catalog = new TextLocalizationCatalog();
        catalog.LoadFromFile("res://localization-catalogs/resources/ui.csv");
        GD.Print(catalog.KeyCount, " ", string.Join(", ", catalog.Locales));
        GD.Print(catalog.GetText("menu.play", "de"));
        GD.Print(catalog.GetPluralText("enemy.count", "en", 3));
        catalog.SetText("menu.options", "en", "Options");
        GD.Print(catalog.GetTranslatedCount("de"));
        catalog.SaveToFile("user://ui.lcat");

        // A translation report (an EditorScript in the guide: the body of _Run()).
        var report = new TextLocalizationCatalog();
        report.LoadFromFile("user://ui.lcat");
        foreach (string locale in report.Locales)
        {
            var missing = new List<string>();
            foreach (int row in report.GetMissingRows(locale))
            {
                missing.Add(report.GetKey(row));
            }
            GD.Print($"{locale}: {report.GetTranslatedCount(locale)} of {report.KeyCount} translated, missing [{string.Join(", ", missing)}]");
        }

        GetTree().Quit();
    }
}
