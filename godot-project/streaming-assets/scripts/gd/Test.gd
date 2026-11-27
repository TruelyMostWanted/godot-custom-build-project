extends FeatureTest
## Full API test of Streaming Assets (StreamingAssetsServer, StreamingAssetsExportPlugin, StreamingAssetsPackBuilder).
## Same tests as scripts/cs/Test.cs. project.godot enables streaming assets with SOURCE as the source folder.
## The tests run from the editor binary, so streaming:// points to the source folder (LOCATION_SOURCE_FOLDER).
## The external folder layout is tested in a second process started with --streaming-assets-path=<folder>.
## An export can't run here: the plugin and the pack builder are tested through their static rules.

const SOURCE := "res://streaming-assets/resources/streaming_assets"
const EXPORT_SAMPLE := "res://streaming-assets/resources/export_sample"
const PACK_SAMPLE := "res://streaming-assets/resources/pack_sample"
const PRINT_SCRIPT := "res://streaming-assets/scripts/gd/print_location.gd"
const ROOT := "user://streaming_assets_test/"
const SETTINGS := {
	"filesystem/streaming_assets/enabled": false,
	"filesystem/streaming_assets/source_folder": "res://streaming_assets",
	"filesystem/streaming_assets/mode": 0,
	"filesystem/streaming_assets/target_folder": "{exe_name}_Data/StreamingAssets",
	"filesystem/streaming_assets/pack_file": "{exe_name}.streaming.pck",
	"filesystem/streaming_assets/fallback": 0,
}
const WINDOWS: PackedStringArray = ["pc", "windows", "x86_64"]
const LINUX: PackedStringArray = ["pc", "linux", "linuxbsd", "x86_64"]
const MACOS: PackedStringArray = ["pc", "macos", "arm64"]
const ANDROID: PackedStringArray = ["mobile", "android", "arm64"]
const WEB: PackedStringArray = ["web"]


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_enums()
	_test_settings()
	_test_state()
	_test_prefix()
	_test_reading()
	_test_os_path()
	_test_expand_template()
	_test_external_folder()
	_test_target_folder()
	_test_path_in_folder()
	_test_file_excluded()
	_test_collect_files()
	_test_placement()
	_test_pack_path()
	_test_pack_description()
	_test_pack_refusals()
	# Last: mounting a pack switches the res:// access of this run.
	_test_mount_prepared_pack()


func _test_enums() -> void:
	expect_equal(StreamingAssetsServer.MODE_RAW, 0, "MODE_RAW")
	expect_equal(StreamingAssetsServer.MODE_PACK, 1, "MODE_PACK")
	expect_equal(StreamingAssetsServer.FALLBACK_EMBED, 0, "FALLBACK_EMBED")
	expect_equal(StreamingAssetsServer.FALLBACK_NONE, 1, "FALLBACK_NONE")
	expect_equal(StreamingAssetsServer.LOCATION_SOURCE_FOLDER, 0, "LOCATION_SOURCE_FOLDER")
	expect_equal(StreamingAssetsServer.LOCATION_EXTERNAL_FOLDER, 1, "LOCATION_EXTERNAL_FOLDER")
	expect_equal(StreamingAssetsServer.LOCATION_BUNDLE, 2, "LOCATION_BUNDLE")
	expect_equal(StreamingAssetsServer.LOCATION_EMBEDDED, 3, "LOCATION_EMBEDDED")
	expect_equal(StreamingAssetsServer.LOCATION_PACK, 4, "LOCATION_PACK")
	expect_equal(StreamingAssetsServer.LOCATION_NONE, 5, "LOCATION_NONE")
	expect_equal(StreamingAssetsExportPlugin.MODE_RAW, StreamingAssetsServer.MODE_RAW, "the plugin MODE_RAW matches the server")
	expect_equal(StreamingAssetsExportPlugin.MODE_PACK, StreamingAssetsServer.MODE_PACK, "the plugin MODE_PACK matches the server")
	expect_equal(StreamingAssetsExportPlugin.FALLBACK_EMBED, StreamingAssetsServer.FALLBACK_EMBED, "the plugin FALLBACK_EMBED matches the server")
	expect_equal(StreamingAssetsExportPlugin.FALLBACK_NONE, StreamingAssetsServer.FALLBACK_NONE, "the plugin FALLBACK_NONE matches the server")
	expect_equal(StreamingAssetsExportPlugin.PLACEMENT_EXTERNAL, 0, "PLACEMENT_EXTERNAL")
	expect_equal(StreamingAssetsExportPlugin.PLACEMENT_EMBED, 1, "PLACEMENT_EMBED")
	expect_equal(StreamingAssetsExportPlugin.PLACEMENT_NONE, 2, "PLACEMENT_NONE")
	expect_equal(StreamingAssetsExportPlugin.PLACEMENT_PACK, 4, "PLACEMENT_PACK")
	expect_equal(ClassDB.get_parent_class("StreamingAssetsExportPlugin"), &"EditorExportPlugin", "StreamingAssetsExportPlugin is an EditorExportPlugin")
	expect(not ClassDB.can_instantiate("StreamingAssetsExportPlugin"), "StreamingAssetsExportPlugin cannot be instantiated")
	expect(not ClassDB.can_instantiate("StreamingAssetsPackBuilder"), "StreamingAssetsPackBuilder cannot be instantiated")


func _test_settings() -> void:
	for setting: String in SETTINGS:
		expect(ProjectSettings.has_setting(setting), "%s is registered" % setting)
		expect_equal(ProjectSettings.property_get_revert(setting), SETTINGS[setting], "%s default" % setting)
	expect_equal(_get_property_hint("filesystem/streaming_assets/mode"), "Raw,Pack", "mode is an enum in the Inspector")
	expect_equal(_get_property_hint("filesystem/streaming_assets/fallback"), "Embed,None", "fallback is an enum in the Inspector")
	expect(ProjectSettings.get_setting("filesystem/streaming_assets/enabled"), "project.godot enables streaming assets")


func _test_state() -> void:
	expect(StreamingAssetsServer.is_enabled(), "is_enabled")
	expect_equal(StreamingAssetsServer.get_mode(), StreamingAssetsServer.MODE_RAW, "get_mode")
	expect_equal(StreamingAssetsServer.get_fallback(), StreamingAssetsServer.FALLBACK_EMBED, "get_fallback")
	expect_equal(StreamingAssetsServer.get_location(), StreamingAssetsServer.LOCATION_SOURCE_FOLDER, "a game run from the editor reads the source folder")
	expect_equal(StreamingAssetsServer.get_source_folder(), SOURCE, "get_source_folder")
	expect_equal(StreamingAssetsServer.get_root(), SOURCE, "get_root is the source folder")


func _test_prefix() -> void:
	expect(PathPrefixServer.has_prefix("streaming://"), "streaming:// is registered")
	expect(PathPrefixServer.is_prefix_locked("streaming://"), "streaming:// is locked")
	expect(PathPrefixServer.is_prefix_available("streaming://"), "streaming:// is available")
	expect(not PathPrefixServer.is_prefix_read_only("streaming://"), "the source folder is writable in the editor")
	expect_equal(PathPrefixServer.register_prefix_folder("streaming://", "user://elsewhere"), ERR_LOCKED, "streaming:// cannot be replaced")
	expect_equal(PathPrefixServer.unregister_prefix("streaming://"), ERR_LOCKED, "streaming:// cannot be removed")
	expect_equal(PathPrefixServer.globalize("streaming://readme.txt"), ProjectSettings.globalize_path(SOURCE + "/readme.txt"), "globalize resolves to the source folder")


func _test_reading() -> void:
	var file: FileAccess = FileAccess.open("streaming://config/levels.json", FileAccess.READ)
	var data: Dictionary = JSON.parse_string(file.get_as_text())
	expect_equal(data["levels"].size(), 3, "levels.json is read through streaming://")
	expect(FileAccess.file_exists("streaming://readme.txt"), "FileAccess.file_exists finds streaming://readme.txt")
	expect(not FileAccess.file_exists("streaming://missing.txt"), "a missing file does not exist")
	expect(DirAccess.get_files_at("streaming://config").has("levels.json"), "DirAccess lists streaming://config")


func _test_os_path() -> void:
	var expected: String = ProjectSettings.globalize_path(SOURCE + "/config/levels.json")
	expect_equal(StreamingAssetsServer.get_os_path("streaming://config/levels.json"), expected, "get_os_path of a streaming:// path")
	expect_equal(StreamingAssetsServer.get_os_path("config/levels.json"), expected, "get_os_path of a relative path")
	expect_equal(StreamingAssetsServer.get_os_path("streaming://config/../config/levels.json"), expected, "get_os_path normalizes ..")
	expect_equal(StreamingAssetsServer.get_os_path("streaming://../project.godot"), "", "get_os_path refuses paths that leave the folder")
	expect_equal(StreamingAssetsServer.get_os_path("user://a.txt"), "", "get_os_path refuses other prefixes")
	expect(FileAccess.file_exists(expected), "the OS path exists")


func _test_expand_template() -> void:
	expect_equal(StreamingAssetsServer.expand_template("{exe_name}_Data/StreamingAssets", "MyGame"), "MyGame_Data/StreamingAssets", "{exe_name}")
	expect_equal(StreamingAssetsServer.expand_template("{exe_name}/{exe_name}.data", "a"), "a/a.data", "every {exe_name} is replaced")
	var app_name: String = String(ProjectSettings.get_setting("application/config/name")).strip_edges().validate_filename()
	expect_equal(StreamingAssetsServer.expand_template("{app_name}", "x"), app_name, "{app_name} is the project name")
	var version: String = String(ProjectSettings.get_setting("application/config/version", "")).strip_edges().validate_filename()
	expect_equal(StreamingAssetsServer.expand_template("v{version}", "x"), "v" + version, "{version} is the project version")
	expect_equal(StreamingAssetsServer.expand_template("{unknown}/x", "x"), "{unknown}/x", "unknown placeholders stay")
	expect_equal(StreamingAssetsExportPlugin.expand_template("{app_name}/{exe_name}", "MyGame"), StreamingAssetsServer.expand_template("{app_name}/{exe_name}", "MyGame"), "the export plugin uses the same rules")
	var exe_name: String = StreamingAssetsServer.get_exe_name()
	expect_equal(exe_name, OS.get_executable_path().get_file().get_basename(), "get_exe_name is the executable name")


func _test_external_folder() -> void:
	var folder: String = ProjectSettings.globalize_path(ROOT + "external")
	DirAccess.make_dir_recursive_absolute(folder)
	var file: FileAccess = FileAccess.open(folder.path_join("hello.txt"), FileAccess.WRITE)
	file.store_string("Hello from outside")
	file.close()
	var output: Array = []
	var arguments: PackedStringArray = ["--headless", "--path", ProjectSettings.globalize_path("res://"), "--streaming-assets-path=" + folder, "--script", PRINT_SCRIPT]
	var code: int = OS.execute(OS.get_executable_path(), arguments, output, true)
	var text: String = "\n".join(output)
	expect_equal(code, 0, "the second process exits with 0")
	expect(text.contains("STREAMING_LOCATION=%d" % StreamingAssetsServer.LOCATION_EXTERNAL_FOLDER), "--streaming-assets-path gives LOCATION_EXTERNAL_FOLDER")
	expect(text.contains("STREAMING_ROOT=%s" % folder.simplify_path()), "the root is the given folder")
	expect(text.contains("STREAMING_TEXT=Hello from outside"), "streaming:// reads the external folder")
	expect(text.contains("STREAMING_OS_PATH=%s" % folder.simplify_path().path_join("hello.txt")), "get_os_path points into the external folder")


func _test_target_folder() -> void:
	for folder: String in ["MyGame_Data/StreamingAssets", "StreamingAssets", "StreamingAssets/", "a\\b"]:
		expect(StreamingAssetsExportPlugin.is_valid_target_folder(folder), "'%s' is a valid target folder" % folder)
	for folder: String in ["", ".", "./a", "..", "a/../b", "a//b", "/abs", "C:/abs", "res://a", "{app_name}/x"]:
		expect(not StreamingAssetsExportPlugin.is_valid_target_folder(folder), "'%s' is not a valid target folder" % folder)


func _test_path_in_folder() -> void:
	var folder := "res://streaming_assets"
	expect(StreamingAssetsExportPlugin.is_path_in_folder("res://streaming_assets/a.png", folder), "a file in the folder matches")
	expect(StreamingAssetsExportPlugin.is_path_in_folder("res://streaming_assets/sub/b.json", folder + "/"), "a file in a subfolder matches (trailing slash)")
	expect(not StreamingAssetsExportPlugin.is_path_in_folder("res://streaming_assets_other/a.png", folder), "a sibling folder with the same prefix does not match")
	expect(not StreamingAssetsExportPlugin.is_path_in_folder("res://other/streaming_assets/a.png", folder), "a nested folder with the same name does not match")
	expect(not StreamingAssetsExportPlugin.is_path_in_folder("res://a.png", "res://"), "a bare prefix never matches")


func _test_file_excluded() -> void:
	for entry: String in [".gdignore", ".DS_Store", "icon.png.import", "script.gd.uid"]:
		expect(StreamingAssetsExportPlugin.is_file_excluded(entry), "'%s' is not shipped" % entry)
	for entry: String in ["icon.png", "level.json", "importer.txt"]:
		expect(not StreamingAssetsExportPlugin.is_file_excluded(entry), "'%s' is shipped" % entry)


func _test_collect_files() -> void:
	var files: PackedStringArray = StreamingAssetsExportPlugin.collect_files(EXPORT_SAMPLE)
	var expected: PackedStringArray = ["config/levels.json", "readme.txt", "sub/deeper/blob.bin"]
	expect_equal(files, expected, "collect_files skips dot entries, .import and .uid and sorts")
	expect_equal(StreamingAssetsExportPlugin.collect_files(EXPORT_SAMPLE + "/"), files, "a trailing slash does not change the result")
	expect_equal(StreamingAssetsExportPlugin.collect_files(ProjectSettings.globalize_path(EXPORT_SAMPLE)), files, "an absolute OS path gives the same result")
	expect(StreamingAssetsExportPlugin.collect_files(EXPORT_SAMPLE + "/missing").is_empty(), "a missing folder gives an empty list")


func _test_placement() -> void:
	var embed: int = StreamingAssetsExportPlugin.FALLBACK_EMBED
	var none: int = StreamingAssetsExportPlugin.FALLBACK_NONE
	var raw: int = StreamingAssetsExportPlugin.MODE_RAW
	var pack: int = StreamingAssetsExportPlugin.MODE_PACK
	var external: int = StreamingAssetsExportPlugin.PLACEMENT_EXTERNAL
	var embedded: int = StreamingAssetsExportPlugin.PLACEMENT_EMBED
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(WINDOWS, "C:/build/Game.exe", embed), external, "raw, Windows executable: external")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(WINDOWS, "C:/build/Game.pck", none), external, "raw, Windows .pck only: external")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(LINUX, "/build/Game.x86_64", embed, raw), external, "raw, Linux executable: external")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(WINDOWS, "C:/build/Game.zip", embed), embedded, "raw, .zip export: the fallback embeds")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(MACOS, "/build/Game.app", embed), StreamingAssetsExportPlugin.PLACEMENT_BUNDLE, "raw, macOS: bundle resources")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(ANDROID, "/build/Game.apk", embed), embedded, "raw, Android: the fallback embeds")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(WEB, "/build/index.html", embed), embedded, "raw, Web: the fallback embeds")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(WEB, "/build/index.html", none), StreamingAssetsExportPlugin.PLACEMENT_NONE, "raw, Web: the fallback none")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(WINDOWS, "C:/build/Game.exe", embed, pack), StreamingAssetsExportPlugin.PLACEMENT_PACK, "pack, Windows: a pack next to the executable")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(LINUX, "/build/Game.pck", embed, pack), StreamingAssetsExportPlugin.PLACEMENT_PACK, "pack, Linux: a pack next to the main pack")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(WINDOWS, "C:/build/Game.zip", embed, pack), embedded, "pack, .zip: the fallback")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(MACOS, "/build/Game.app", embed, pack), embedded, "pack, macOS: the fallback")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(ANDROID, "/build/Game.apk", embed, pack), embedded, "pack, Android: the fallback")
	expect_equal(StreamingAssetsExportPlugin.resolve_placement(WEB, "/build/index.html", none, pack), StreamingAssetsExportPlugin.PLACEMENT_NONE, "pack, Web: the fallback none")


func _test_pack_path() -> void:
	expect_equal(StreamingAssetsPackBuilder.get_pack_path("C:/build/Game.exe", "Game.streaming.pck"), "C:/build/Game.streaming.pck", "the pack is next to the executable")
	var pack_file: String = StreamingAssetsServer.expand_template("{exe_name}.streaming.pck", "Game")
	expect_equal(StreamingAssetsPackBuilder.get_pack_path("/build/Game.pck", pack_file), "/build/Game.streaming.pck", "the default pack file")
	expect_equal(StreamingAssetsPackBuilder.get_pack_path("/build/Game.pck", "sub/a.pck"), "", "a pack file with a folder is refused")
	expect_equal(StreamingAssetsPackBuilder.get_pack_path("", "a.pck"), "", "an empty export path is refused")


func _test_pack_description() -> void:
	var description: PCKPackage = StreamingAssetsPackBuilder.make_description(PACK_SAMPLE)
	var paths: PackedStringArray = description.get_file_paths()
	paths.sort()
	var expected: PackedStringArray = [PACK_SAMPLE + "/data/levels.json", PACK_SAMPLE + "/icon.svg"]
	expect_equal(paths, expected, "make_description: one entry per file, at its res:// path; .import files are left out")
	var plan: PCKBuildPlan = PCKPackageExporter.plan_description(description)
	expect(plan.valid, "the streaming pack can be built")
	var sources: PackedStringArray = plan.export_sources
	sources.sort()
	expect_equal(sources, expected, "every file goes through the export pipeline")
	expect(plan.raw_files.is_empty(), "nothing is copied as it is")


func _test_pack_refusals() -> void:
	expect(StreamingAssetsPackBuilder.make_description("user://streaming") == null, "a folder outside res:// is refused")
	expect_equal(StreamingAssetsPackBuilder.build(null, PACK_SAMPLE, ROOT + "a.pck"), ERR_INVALID_PARAMETER, "build without a preset is refused")


func _test_mount_prepared_pack() -> void:
	# A prepared streaming pack: a file that exists only in the pack, below the sample folder.
	var pack: String = ROOT + "Game.streaming.pck"
	var source: String = ROOT + "pack_only.txt"
	var file: FileAccess = FileAccess.open(source, FileAccess.WRITE)
	file.store_string("Hello from the streaming pack")
	file.close()
	var packer := PCKPacker.new()
	expect_equal(packer.pck_start(pack), OK, "pck_start")
	expect_equal(packer.add_file(PACK_SAMPLE + "/pack_only.txt", source), OK, "add_file")
	expect_equal(packer.flush(), OK, "flush")
	# StreamingAssetsServer mounts the streaming pack like this in MODE_PACK.
	var pack_path: String = ProjectSettings.globalize_path(pack)
	expect_equal(PCKPackageServer.mount(pack_path, true), OK, "the streaming pack mounts")
	expect_equal(FileAccess.get_file_as_string(PACK_SAMPLE + "/pack_only.txt"), "Hello from the streaming pack", "the file loads by its res:// path")
	expect_equal(PCKPackageServer.unmount(pack_path), OK, "unmount")
	expect(not FileAccess.file_exists(PACK_SAMPLE + "/pack_only.txt"), "the file is gone after unmount")


func _get_property_hint(setting: String) -> String:
	for property: Dictionary in ProjectSettings.get_property_list():
		if property["name"] == setting:
			return property["hint_string"]
	return ""
