using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue86FeaturePckPackageExporter;

/// <summary>C# checks of 86-feature-pck-package-exporter. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>
/// A full export needs the editor and an export preset: these checks cover the bound rules (plan, encryption filters,
/// export targets) and the refusals. PCKPackageExporter is an editor class (GodotSharpEditor), available with the
/// TOOLS define of the Debug configuration.
/// </remarks>
public partial class Issue86FeaturePckPackageExporterCheck : FeatureCheckCs
{
    private const string Class = "PCKPackageExporter";
    private const string Description = "res://86-feature-pck-package-exporter/resources/dlc_description.tres";

    private static readonly string[] Methods =
    [
        "export_description", "export_description_with_preset", "export_target_to_res_path", "find_preset",
        "get_export_target_paths", "get_preset_names", "is_building", "is_export_target", "is_exporting",
        "matches_encryption_filters", "plan_description", "should_encrypt",
    ];

    protected override string CheckName => "86-feature-pck-package-exporter";

    protected override Task RunChecks()
    {
        CheckBindings();
#if TOOLS
        CheckPlanOfSavedDescription();
        CheckPlanRefusals();
        CheckEncryptionFilters();
        CheckExportTargets();
        CheckRefusals();
#else
        Expect(false, "the C# checks need the TOOLS define (Debug configuration with GodotSharpEditor)");
#endif
        return Task.CompletedTask;
    }

    private void CheckBindings()
    {
        Expect(ClassDB.ClassExists(Class), $"{Class} exists in the editor build");
        ExpectEqual(ClassDB.GetParentClass(Class).ToString(), "Object", $"{Class} extends Object");
        Expect(!ClassDB.CanInstantiate(Class), $"{Class} can't be instantiated from scripts");
        foreach (string method in Methods)
        {
            Expect(ClassDB.ClassHasMethod(Class, method, true), $"{Class}.{method}() is bound");
        }
    }

#if TOOLS
    private static string[] Strings(Variant value) => value.AsStringArray();

    private void CheckPlanOfSavedDescription()
    {
        var description = GD.Load<PckPackage>(Description);
        Expect(description != null, "the description .tres loads as a PCKPackage");
        if (description == null)
        {
            return;
        }
        Expect(description.IsDescription(), "a .tres is a description");
        var plan = PckPackageExporter.PlanDescription(description);
        foreach (string key in new[] { "export_sources", "raw_files", "removals", "skipped", "errors" })
        {
            Expect(plan.ContainsKey(key), $"the plan has '{key}'");
        }
        Expect(Strings(plan["export_sources"]).SequenceEqual(new[] { "res://icon.svg" }), "res:// sources go through the export pipeline");
        Expect(Strings(plan["raw_files"]).SequenceEqual(new[] { "dlc/readme.txt", "dlc/settings.cfg" }), "data and user:// files are copied as they are");
        Expect(Strings(plan["removals"]).SequenceEqual(new[] { "old/unused.png" }), "removals stay removals");
        Expect(Strings(plan["errors"]).Length == 0, "the description can be built");
    }

    private void CheckPlanRefusals()
    {
        var description = new PckPackage();
        description.AddFile("textures/icon.svg", "res://icon.svg");
        description.AddFile("icon.svg.import", "res://icon.svg.import");
        description.AddFile("other.png.import", "res://other.png.import");
        var plan = PckPackageExporter.PlanDescription(description);
        string[] errors = Strings(plan["errors"]);
        ExpectEqual(errors.Length, 3, "a renamed res:// target and both .import files are refused");
        Expect(errors.Length > 0 && errors[0].Contains("textures/icon.svg"), "the error names the renamed entry");
        Expect(Strings(plan["skipped"]).Length == 0, "no .import is skipped without its exported source");

        var ok = new PckPackage();
        ok.AddFile("icon.svg", "res://icon.svg");
        ok.AddFile("icon.svg.import", "res://icon.svg.import");
        var okPlan = PckPackageExporter.PlanDescription(ok);
        Expect(Strings(okPlan["skipped"]).SequenceEqual(new[] { "icon.svg.import" }), "the .import of an exported source is skipped");
        Expect(Strings(okPlan["errors"]).Length == 0, "a source with its .import can be built");
    }

    private void CheckEncryptionFilters()
    {
        Expect(PckPackageExporter.MatchesEncryptionFilters("res://scripts/a.gd", ["*.gd"], []), "*.gd matches a script");
        Expect(PckPackageExporter.MatchesEncryptionFilters("scripts/a.gd", ["*.gd"], []), "filters match without res://");
        Expect(PckPackageExporter.MatchesEncryptionFilters("res://SECRET/a.png", ["secret/*"], []), "filters ignore case");
        Expect(!PckPackageExporter.MatchesEncryptionFilters("res://dlc/a.pck", ["*"], ["*.pck"]), "an exclude filter wins");
        Expect(!PckPackageExporter.MatchesEncryptionFilters("res://a.png", [], []), "no filter, no encryption");
        Expect(!PckPackageExporter.ShouldEncrypt(null, "res://a.gd"), "no preset, no encryption");
    }

    private void CheckExportTargets()
    {
        const string project = "C:/Projects/Game";
        ExpectEqual(PckPackageExporter.ExportTargetToResPath("build/game.pck", project), "res://build/game.pck", "a relative target is inside the project");
        ExpectEqual(PckPackageExporter.ExportTargetToResPath("C:/Projects/Game/build/game.exe", project), "res://build/game.exe", "an absolute target inside the project");
        ExpectEqual(PckPackageExporter.ExportTargetToResPath("../builds/game.pck", project), "", "a target above the project is outside");
        ExpectEqual(PckPackageExporter.ExportTargetToResPath("C:/Projects/GameOther/game.pck", project), "", "a sibling folder with the same prefix is outside");
        Expect(!PckPackageExporter.IsExporting(), "no export runs during the checks");
        Expect(!PckPackageExporter.IsBuilding(), "no description is built during the checks");
    }

    private void CheckRefusals()
    {
        var description = new PckPackage();
        Error err = PckPackageExporter.ExportDescription(description, "No Such Preset 86", "user://86_check_cs.pck");
        ExpectEqual(err, Error.DoesNotExist, "an unknown preset is refused");
        Expect(PckPackageExporter.FindPreset("No Such Preset 86") == null, "FindPreset() returns null for an unknown preset");
        Expect(!FileAccess.FileExists("user://86_check_cs.pck"), "nothing is written when the build is refused");
    }
#endif
}
