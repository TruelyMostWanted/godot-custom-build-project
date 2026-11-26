using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue281BugCsvLocaleWarnings;

/// <summary>C# checks of 281-bug-csv-locale-warnings. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>
/// The editor imports resources/units.csv (data, header name,health,speed) and resources/greetings.csv
/// (translation, header keys,en,de) with the default "CSV Translation" importer before the checks run.
/// </remarks>
public partial class Issue281BugCsvLocaleWarningsCheck : FeatureCheckCs
{
    private const string Dir = "res://281-bug-csv-locale-warnings/resources/";

    protected override string CheckName => "281-bug-csv-locale-warnings";

    protected override Task RunChecks()
    {
        // Data CSV: no column is a locale, so no bogus translations are created.
        Expect(!ResourceLoader.Exists(Dir + "units.health.translation"), "data CSV creates no 'health' translation");
        Expect(!ResourceLoader.Exists(Dir + "units.speed.translation"), "data CSV creates no 'speed' translation");
        string units = FileAccess.GetFileAsString(Dir + "units.csv");
        Expect(units.StartsWith("name,health,speed"), "data CSV stays readable as a plain file");

        // Translation CSV: still imported, one file per locale.
        Expect(ResourceLoader.Exists(Dir + "greetings.en.translation"), "translation CSV creates the 'en' translation");
        Expect(ResourceLoader.Exists(Dir + "greetings.de.translation"), "translation CSV creates the 'de' translation");
        var de = GD.Load<Translation>(Dir + "greetings.de.translation");
        Expect(de != null && de.Locale == "de", "'de' translation has locale 'de'");
        Expect(de != null && de.GetMessage("HELLO") == "Hallo", "'de' translation contains HELLO");
        return Task.CompletedTask;
    }
}
