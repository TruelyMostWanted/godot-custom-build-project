using Godot;
using Godot.Collections;

namespace TruelyInteractive.LocalizationEditor;

/// <summary>Minimal example of Localization Editor: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string UiCsv = "res://localization-editor/resources/ui.csv";
    private const string UiMissingCsv = "res://localization-editor/resources/ui_missing.csv";

    public override void _Ready()
    {
        UsingTheApi();
        FillMissingFrench();
        GetTree().Quit();
    }

    private static void UsingTheApi()
    {
        // Using the API: read a key, change it, undo the change, check the catalog, write the template.
        var catalog = new TextLocalizationCatalog();
        catalog.LoadFromFile(UiCsv);

        Dictionary before = catalog.GetEntry("menu.play");
        GD.Print(before);
        catalog.SetEntry("menu.play", new Dictionary { { "en", "Start" }, { "de", "Starten" } });
        catalog.SetEntry("menu.play", before);

        var forms = new Array { "{count} enemy", "{count} enemies" };
        catalog.SetEntry("enemy.count", new Dictionary { { "_plural", "enemy.count.plural" }, { "en", forms } });

        Dictionary entry = catalog.GetEntry("menu.quit");
        catalog.RemoveKey("menu.quit");
        catalog.SetEntry("menu.exit", entry);

        catalog.SetNote("menu.play", "Main menu button");
        catalog.SetNote("menu.play", "12", "", "max_length");
        GD.Print(catalog.GetNote("menu.play"));

        var options = new DataValidationOptions { MaxIssues = -1 };
        DataValidation result = catalog.Validate(options);
        for (int i = 0; i < result.GetIssueCount(); i++)
        {
            GD.Print(result.GetIssuePath(i), " ", result.GetIssueField(i), " line ", result.GetIssueLine(i));
        }

        var csvOptions = new SerializerOptions { CsvDelimiter = ";" };
        GD.Print(catalog.ToText(ResourceTextSerializer.Format.Csv, csvOptions));
        using FileAccess potFile = FileAccess.Open("user://ui.pot", FileAccess.ModeFlags.Write);
        potFile.StoreString(catalog.ToPo());
    }

    private static void FillMissingFrench()
    {
        // A complete example: fill the missing French texts.
        var catalog = new TextLocalizationCatalog();
        catalog.LoadFromFile(UiMissingCsv);
        GD.Print($"fr before: {catalog.GetTranslatedCount("fr")} of {catalog.KeyCount} translated");

        foreach (int row in catalog.GetMissingRows("fr"))
        {
            StringName key = catalog.GetKey(row);
            string english = catalog.GetText(key, "en");
            catalog.SetText(key, "fr", "[TODO] " + english);
            GD.Print("filled ", key);
        }

        GD.Print($"fr after: {catalog.GetTranslatedCount("fr")} of {catalog.KeyCount} translated");
        catalog.SaveToFile("user://ui_filled.csv");
        using FileAccess potFile = FileAccess.Open("user://ui.pot", FileAccess.ModeFlags.Write);
        potFile.StoreString(catalog.ToPo());
    }
}
