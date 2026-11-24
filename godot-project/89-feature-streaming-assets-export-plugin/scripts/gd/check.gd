extends FeatureCheck
## GDScript checks of 89-feature-streaming-assets-export-plugin. Check the same things as scripts/cs/Issue89FeatureStreamingAssetsExportPluginCheck.cs.
## Exports can't run here: these checks cover the bound rules of the plugin (file selection, target folder, placement).
## The manual export steps are in the wiki guide and in the issue.

const CLASS := "StreamingAssetsExportPlugin"
const SAMPLE := "res://89-feature-streaming-assets-export-plugin/resources/streaming_sample"
const METHODS: Array[String] = [
	"collect_files", "expand_template", "is_file_excluded",
	"is_path_in_folder", "is_valid_target_folder", "resolve_placement",
]


func _get_check_name() -> String:
	return "89-feature-streaming-assets-export-plugin"


func _run_checks() -> void:
	_check_bindings()
	_check_expand_template()
	_check_target_folder()
	_check_path_in_folder()
	_check_file_excluded()
	_check_collect_files()
	_check_placement()


func _check_bindings() -> void:
	expect(ClassDB.class_exists(CLASS), "%s exists in the editor build" % CLASS)
	expect_equal(ClassDB.get_parent_class(CLASS), &"EditorExportPlugin", "%s extends EditorExportPlugin" % CLASS)
	expect(not ClassDB.can_instantiate(CLASS), "%s can't be instantiated from scripts" % CLASS)
	for method in METHODS:
		expect(ClassDB.class_has_method(CLASS, method, true), "%s.%s() is bound" % [CLASS, method])
	expect_equal(StreamingAssetsExportPlugin.MODE_RAW, 0, "MODE_RAW")
	expect_equal(StreamingAssetsExportPlugin.MODE_PACK, 1, "MODE_PACK")
	expect_equal(StreamingAssetsExportPlugin.FALLBACK_EMBED, 0, "FALLBACK_EMBED")
	expect_equal(StreamingAssetsExportPlugin.FALLBACK_NONE, 1, "FALLBACK_NONE")
	expect_equal(StreamingAssetsExportPlugin.PLACEMENT_EXTERNAL, 0, "PLACEMENT_EXTERNAL")
	expect_equal(StreamingAssetsExportPlugin.PLACEMENT_EMBED, 1, "PLACEMENT_EMBED")
	expect_equal(StreamingAssetsExportPlugin.PLACEMENT_NONE, 2, "PLACEMENT_NONE")


func _check_expand_template() -> void:
	expect_equal(StreamingAssetsExportPlugin.expand_template("{exe_name}_Data/StreamingAssets", "MyGame"), "MyGame_Data/StreamingAssets", "default target expands {exe_name}")
	expect_equal(StreamingAssetsExportPlugin.expand_template("{exe_name}/{exe_name}.data", "a"), "a/a.data", "every {exe_name} is replaced")
	expect_equal(StreamingAssetsExportPlugin.expand_template("{unknown}/x", "MyGame"), "{unknown}/x", "unknown placeholders stay")


func _check_target_folder() -> void:
	for folder in ["MyGame_Data/StreamingAssets", "StreamingAssets", "StreamingAssets/", "a\\b"]:
		expect(StreamingAssetsExportPlugin.is_valid_target_folder(folder), "'%s' is a valid target folder" % folder)
	for folder in ["", ".", "./a", "..", "a/../b", "a//b", "/abs", "C:/abs", "res://a", "{app_name}/x"]:
		expect(not StreamingAssetsExportPlugin.is_valid_target_folder(folder), "'%s' is not a valid target folder" % folder)


func _check_path_in_folder() -> void:
	var folder := "res://streaming_assets"
	expect(StreamingAssetsExportPlugin.is_path_in_folder("res://streaming_assets/a.png", folder), "a file in the folder matches")
	expect(StreamingAssetsExportPlugin.is_path_in_folder("res://streaming_assets/sub/b.json", folder + "/"), "a file in a subfolder matches (trailing slash)")
	expect(not StreamingAssetsExportPlugin.is_path_in_folder("res://streaming_assets_other/a.png", folder), "a sibling folder with the same prefix does not match")
	expect(not StreamingAssetsExportPlugin.is_path_in_folder("res://other/streaming_assets/a.png", folder), "a nested folder with the same name does not match")
	expect(not StreamingAssetsExportPlugin.is_path_in_folder("res://a.png", "res://"), "a bare prefix never matches")


func _check_file_excluded() -> void:
	for entry in [".gdignore", ".DS_Store", "icon.png.import", "script.gd.uid"]:
		expect(StreamingAssetsExportPlugin.is_file_excluded(entry), "'%s' is not shipped" % entry)
	for entry in ["icon.png", "level.json", "importer.txt"]:
		expect(not StreamingAssetsExportPlugin.is_file_excluded(entry), "'%s' is shipped" % entry)


func _check_collect_files() -> void:
	var files := StreamingAssetsExportPlugin.collect_files(SAMPLE)
	print("  files in the sample folder: %s" % files)
	expect_equal(files, PackedStringArray(["config/levels.json", "readme.txt", "sub/deeper/blob.bin"]), "collect_files() skips dot entries, .import and .uid and sorts")
	expect_equal(StreamingAssetsExportPlugin.collect_files(SAMPLE + "/"), files, "a trailing slash does not change the result")
	var absolute := StreamingAssetsExportPlugin.collect_files(ProjectSettings.globalize_path(SAMPLE))
	expect_equal(absolute, files, "an absolute OS path gives the same result")
	expect(StreamingAssetsExportPlugin.collect_files(SAMPLE + "/missing").is_empty(), "a missing folder gives an empty list")


func _check_placement() -> void:
	var windows := PackedStringArray(["pc", "windows", "x86_64"])
	var linux := PackedStringArray(["pc", "linux", "linuxbsd", "x86_64"])
	var macos := PackedStringArray(["pc", "macos", "arm64"])
	var android := PackedStringArray(["mobile", "android", "arm64"])
	var web := PackedStringArray(["web"])
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(windows, "C:/build/Game.exe", StreamingAssetsExportPlugin.FALLBACK_EMBED), StreamingAssetsExportPlugin.PLACEMENT_EXTERNAL, "Windows executable: external")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(windows, "C:/build/Game.pck", StreamingAssetsExportPlugin.FALLBACK_NONE), StreamingAssetsExportPlugin.PLACEMENT_EXTERNAL, "Windows .pck only: external")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(linux, "/build/Game.x86_64", StreamingAssetsExportPlugin.FALLBACK_EMBED), StreamingAssetsExportPlugin.PLACEMENT_EXTERNAL, "Linux executable: external")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(windows, "C:/build/Game.zip", StreamingAssetsExportPlugin.FALLBACK_EMBED), StreamingAssetsExportPlugin.PLACEMENT_EMBED, ".zip export: fallback embed")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(macos, "/build/Game.app", StreamingAssetsExportPlugin.FALLBACK_EMBED), StreamingAssetsExportPlugin.PLACEMENT_EMBED, "macOS: fallback until #90")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(android, "/build/Game.apk", StreamingAssetsExportPlugin.FALLBACK_EMBED), StreamingAssetsExportPlugin.PLACEMENT_EMBED, "Android: fallback embed")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(web, "/build/index.html", StreamingAssetsExportPlugin.FALLBACK_EMBED), StreamingAssetsExportPlugin.PLACEMENT_EMBED, "Web: fallback embed")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(web, "/build/index.html", StreamingAssetsExportPlugin.FALLBACK_NONE), StreamingAssetsExportPlugin.PLACEMENT_NONE, "Web: fallback none")
