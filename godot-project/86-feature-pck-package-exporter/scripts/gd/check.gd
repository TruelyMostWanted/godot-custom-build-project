extends FeatureCheck
## GDScript checks of 86-feature-pck-package-exporter. Check the same things as scripts/cs/Issue86FeaturePckPackageExporterCheck.cs.
## A full export needs the editor and an export preset: these checks cover the bound rules (plan, encryption filters,
## export targets) and the refusals. The manual export steps are in the wiki guide and in the issue.

const CLASS := "PCKPackageExporter"
const DESCRIPTION := "res://86-feature-pck-package-exporter/resources/dlc_description.tres"
const METHODS: Array[String] = [
	"export_description", "export_description_with_preset", "export_target_to_res_path", "find_preset",
	"get_export_target_paths", "get_preset_names", "is_building", "is_export_target", "is_exporting",
	"matches_encryption_filters", "plan_description", "should_encrypt",
]


func _get_check_name() -> String:
	return "86-feature-pck-package-exporter"


func _run_checks() -> void:
	_check_bindings()
	_check_plan_of_saved_description()
	_check_plan_refusals()
	_check_encryption_filters()
	_check_export_targets()
	_check_refusals()


func _check_bindings() -> void:
	expect(ClassDB.class_exists(CLASS), "%s exists in the editor build" % CLASS)
	expect_equal(ClassDB.get_parent_class(CLASS), &"Object", "%s extends Object" % CLASS)
	expect(not ClassDB.can_instantiate(CLASS), "%s can't be instantiated from scripts" % CLASS)
	for method in METHODS:
		expect(ClassDB.class_has_method(CLASS, method, true), "%s.%s() is bound" % [CLASS, method])


func _check_plan_of_saved_description() -> void:
	var description := load(DESCRIPTION) as PCKPackage
	expect(description != null, "the description .tres loads as a PCKPackage")
	if description == null:
		return
	expect(description.is_description(), "a .tres is a description")
	var plan := PCKPackageExporter.plan_description(description)
	for key in ["export_sources", "raw_files", "removals", "skipped", "errors"]:
		expect(plan.has(key), "the plan has '%s'" % key)
	expect_equal(plan["export_sources"], PackedStringArray(["res://icon.svg"]), "res:// sources go through the export pipeline")
	expect_equal(plan["raw_files"], PackedStringArray(["dlc/readme.txt", "dlc/settings.cfg"]), "data and user:// files are copied as they are")
	expect_equal(plan["removals"], PackedStringArray(["old/unused.png"]), "removals stay removals")
	expect(PackedStringArray(plan["errors"]).is_empty(), "the description can be built")


func _check_plan_refusals() -> void:
	var description := PCKPackage.new()
	description.add_file("textures/icon.svg", "res://icon.svg")
	description.add_file("icon.svg.import", "res://icon.svg.import")
	description.add_file("other.png.import", "res://other.png.import")
	var plan := PCKPackageExporter.plan_description(description)
	var errors := PackedStringArray(plan["errors"])
	expect_equal(errors.size(), 3, "a renamed res:// target and both .import files are refused")
	expect(errors.size() > 0 and errors[0].contains("textures/icon.svg"), "the error names the renamed entry")
	expect(PackedStringArray(plan["skipped"]).is_empty(), "no .import is skipped without its exported source")

	var ok := PCKPackage.new()
	ok.add_file("icon.svg", "res://icon.svg")
	ok.add_file("icon.svg.import", "res://icon.svg.import")
	var ok_plan := PCKPackageExporter.plan_description(ok)
	expect_equal(ok_plan["skipped"], PackedStringArray(["icon.svg.import"]), "the .import of an exported source is skipped")
	expect(PackedStringArray(ok_plan["errors"]).is_empty(), "a source with its .import can be built")


func _check_encryption_filters() -> void:
	expect(PCKPackageExporter.matches_encryption_filters("res://scripts/a.gd", ["*.gd"], []), "*.gd matches a script")
	expect(PCKPackageExporter.matches_encryption_filters("scripts/a.gd", ["*.gd"], []), "filters match without res://")
	expect(PCKPackageExporter.matches_encryption_filters("res://SECRET/a.png", ["secret/*"], []), "filters ignore case")
	expect(not PCKPackageExporter.matches_encryption_filters("res://dlc/a.pck", ["*"], ["*.pck"]), "an exclude filter wins")
	expect(not PCKPackageExporter.matches_encryption_filters("res://a.png", [], []), "no filter, no encryption")
	expect(not PCKPackageExporter.should_encrypt(null, "res://a.gd"), "no preset, no encryption")


func _check_export_targets() -> void:
	var project := "C:/Projects/Game"
	expect_equal(PCKPackageExporter.export_target_to_res_path("build/game.pck", project), "res://build/game.pck", "a relative target is inside the project")
	expect_equal(PCKPackageExporter.export_target_to_res_path("C:/Projects/Game/build/game.exe", project), "res://build/game.exe", "an absolute target inside the project")
	expect_equal(PCKPackageExporter.export_target_to_res_path("../builds/game.pck", project), "", "a target above the project is outside")
	expect_equal(PCKPackageExporter.export_target_to_res_path("C:/Projects/GameOther/game.pck", project), "", "a sibling folder with the same prefix is outside")
	expect(not PCKPackageExporter.is_exporting(), "no export runs during the checks")
	expect(not PCKPackageExporter.is_building(), "no description is built during the checks")


func _check_refusals() -> void:
	var description := PCKPackage.new()
	var err := PCKPackageExporter.export_description(description, "No Such Preset 86", "user://86_check.pck")
	expect_equal(err, ERR_DOES_NOT_EXIST, "an unknown preset is refused")
	expect(PCKPackageExporter.find_preset("No Such Preset 86") == null, "find_preset() returns null for an unknown preset")
	expect(not FileAccess.file_exists("user://86_check.pck"), "nothing is written when the build is refused")
