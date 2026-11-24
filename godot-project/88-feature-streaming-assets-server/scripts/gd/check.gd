extends FeatureCheck
## GDScript checks of 88-feature-streaming-assets-server. Check the same things as scripts/cs/Issue88FeatureStreamingAssetsServerCheck.cs.
## project.godot enables streaming assets with the source folder below. The checks run from the editor
## binary, so streaming:// points to the source folder (LOCATION_SOURCE_FOLDER). The external folder
## layout is checked in a second process started with --streaming-assets-path=<folder>.

const CLASS := "StreamingAssetsServer"
const SOURCE := "res://88-feature-streaming-assets-server/resources/streaming_assets"
const PRINT_SCRIPT := "res://88-feature-streaming-assets-server/scripts/gd/print_location.gd"
const METHODS: Array[String] = [
	"expand_template", "get_exe_name", "get_fallback", "get_location", "get_mode",
	"get_os_path", "get_root", "get_source_folder", "is_enabled",
]
const SETTINGS := {
	"filesystem/streaming_assets/enabled": false,
	"filesystem/streaming_assets/source_folder": "res://streaming_assets",
	"filesystem/streaming_assets/mode": 0,
	"filesystem/streaming_assets/target_folder": "{exe_name}_Data/StreamingAssets",
	"filesystem/streaming_assets/pack_file": "{exe_name}.streaming.pck",
	"filesystem/streaming_assets/fallback": 0,
}


func _get_check_name() -> String:
	return "88-feature-streaming-assets-server"


func _run_checks() -> void:
	_check_bindings()
	_check_settings()
	_check_state()
	_check_prefix()
	_check_reading()
	_check_os_path()
	_check_expand_template()
	_check_external_folder_override()


func _check_bindings() -> void:
	expect(Engine.has_singleton(CLASS), "%s is a singleton" % CLASS)
	for method in METHODS:
		expect(ClassDB.class_has_method(CLASS, method, true), "%s.%s() is bound" % [CLASS, method])
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
	# The enums of the server and of the export plugin (#89) must match: both read the same settings.
	if ClassDB.class_exists("StreamingAssetsExportPlugin"):
		expect_equal(StreamingAssetsServer.MODE_PACK, StreamingAssetsExportPlugin.MODE_PACK, "Mode matches the export plugin")
		expect_equal(StreamingAssetsServer.FALLBACK_NONE, StreamingAssetsExportPlugin.FALLBACK_NONE, "Fallback matches the export plugin")


func _check_settings() -> void:
	for setting: String in SETTINGS:
		expect(ProjectSettings.has_setting(setting), "%s is registered" % setting)
		expect_equal(ProjectSettings.property_get_revert(setting), SETTINGS[setting], "%s default" % setting)
	var mode_hint := _get_property_hint("filesystem/streaming_assets/mode")
	expect_equal(mode_hint, "Raw,Pack", "mode is an enum in the Inspector")
	expect_equal(_get_property_hint("filesystem/streaming_assets/fallback"), "Embed,None", "fallback is an enum in the Inspector")
	expect_equal(ProjectSettings.get_setting("filesystem/streaming_assets/enabled"), true, "project.godot enables streaming assets")


func _check_state() -> void:
	expect(StreamingAssetsServer.is_enabled(), "is_enabled()")
	expect_equal(StreamingAssetsServer.get_mode(), StreamingAssetsServer.MODE_RAW, "get_mode()")
	expect_equal(StreamingAssetsServer.get_fallback(), StreamingAssetsServer.FALLBACK_EMBED, "get_fallback()")
	expect_equal(StreamingAssetsServer.get_location(), StreamingAssetsServer.LOCATION_SOURCE_FOLDER, "a game run from the editor reads the source folder")
	expect_equal(StreamingAssetsServer.get_source_folder(), SOURCE, "get_source_folder()")
	expect_equal(StreamingAssetsServer.get_root(), SOURCE, "get_root() is the source folder")


func _check_prefix() -> void:
	expect(PathPrefixServer.has_prefix("streaming://"), "streaming:// is registered")
	expect(PathPrefixServer.is_prefix_locked("streaming://"), "streaming:// is locked")
	expect(PathPrefixServer.is_prefix_available("streaming://"), "streaming:// is available")
	expect(not PathPrefixServer.is_prefix_read_only("streaming://"), "the source folder is writable in the editor")
	expect_equal(PathPrefixServer.register_prefix_folder("streaming://", "user://elsewhere"), ERR_LOCKED, "streaming:// can't be replaced")
	expect_equal(PathPrefixServer.unregister_prefix("streaming://"), ERR_LOCKED, "streaming:// can't be removed")
	expect_equal(PathPrefixServer.globalize("streaming://readme.txt"), ProjectSettings.globalize_path(SOURCE + "/readme.txt"), "globalize() resolves to the source folder")


func _check_reading() -> void:
	var file := FileAccess.open("streaming://config/levels.json", FileAccess.READ)
	expect(file != null, "FileAccess opens streaming://config/levels.json")
	if file:
		var data = JSON.parse_string(file.get_as_text())
		expect(data is Dictionary and data.get("levels", []).size() == 3, "levels.json is read through streaming://")
	expect(FileAccess.file_exists("streaming://readme.txt"), "FileAccess.file_exists(streaming://readme.txt)")
	expect(not FileAccess.file_exists("streaming://missing.txt"), "a missing file does not exist")
	expect(DirAccess.get_files_at("streaming://config").has("levels.json"), "DirAccess lists streaming://config")


func _check_os_path() -> void:
	var expected := ProjectSettings.globalize_path(SOURCE + "/config/levels.json")
	expect_equal(StreamingAssetsServer.get_os_path("streaming://config/levels.json"), expected, "get_os_path() of a streaming:// path")
	expect_equal(StreamingAssetsServer.get_os_path("config/levels.json"), expected, "get_os_path() of a relative path")
	expect_equal(StreamingAssetsServer.get_os_path("streaming://config/../config/levels.json"), expected, "get_os_path() normalizes ..")
	expect_equal(StreamingAssetsServer.get_os_path("streaming://../project.godot"), "", "get_os_path() refuses paths that leave the folder")
	expect_equal(StreamingAssetsServer.get_os_path("user://a.txt"), "", "get_os_path() refuses other prefixes")
	expect(FileAccess.file_exists(expected), "the OS path exists")


func _check_expand_template() -> void:
	expect_equal(StreamingAssetsServer.expand_template("{exe_name}_Data/StreamingAssets", "MyGame"), "MyGame_Data/StreamingAssets", "{exe_name}")
	expect_equal(StreamingAssetsServer.expand_template("{exe_name}.streaming.pck", "MyGame"), "MyGame.streaming.pck", "pack file template")
	var app_name := String(ProjectSettings.get_setting("application/config/name")).strip_edges().validate_filename()
	expect_equal(StreamingAssetsServer.expand_template("{app_name}", "x"), app_name, "{app_name} is the project name")
	var version := String(ProjectSettings.get_setting("application/config/version", "")).strip_edges().validate_filename()
	expect_equal(StreamingAssetsServer.expand_template("v{version}", "x"), "v" + version, "{version} is the project version")
	expect_equal(StreamingAssetsServer.expand_template("{unknown}", "x"), "{unknown}", "unknown placeholders stay")
	if ClassDB.class_exists("StreamingAssetsExportPlugin"):
		expect_equal(StreamingAssetsExportPlugin.expand_template("{app_name}/{exe_name}", "MyGame"), StreamingAssetsServer.expand_template("{app_name}/{exe_name}", "MyGame"), "the export plugin uses the same helper")
	var exe_name := StreamingAssetsServer.get_exe_name()
	print("  exe_name: %s" % exe_name)
	expect(not exe_name.is_empty(), "get_exe_name() is not empty")
	if OS.get_name() == "Windows" or OS.get_name() == "Linux":
		expect_equal(exe_name, OS.get_executable_path().get_file().get_basename(), "get_exe_name() is the executable name")


func _check_external_folder_override() -> void:
	if OS.has_feature("web") or OS.has_feature("android") or OS.has_feature("ios"):
		print("  skipped: no second process on this platform")
		return
	var folder := ProjectSettings.globalize_path("user://88_streaming_external")
	DirAccess.make_dir_recursive_absolute(folder)
	var file := FileAccess.open(folder.path_join("hello.txt"), FileAccess.WRITE)
	file.store_string("Hello from outside")
	file.close()

	var output: Array = []
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), "--streaming-assets-path=" + folder, "--script", PRINT_SCRIPT])
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	var text := "\n".join(output)
	expect_equal(code, 0, "the second process exits with 0")
	expect(text.contains("STREAMING_LOCATION=%d" % StreamingAssetsServer.LOCATION_EXTERNAL_FOLDER), "--streaming-assets-path gives LOCATION_EXTERNAL_FOLDER")
	expect(text.contains("STREAMING_ROOT=%s" % folder.simplify_path()), "the root is the given folder")
	expect(text.contains("STREAMING_TEXT=Hello from outside"), "streaming:// reads the external folder")
	expect(text.contains("STREAMING_OS_PATH=%s" % folder.simplify_path().path_join("hello.txt")), "get_os_path() points into the external folder")


func _get_property_hint(setting: String) -> String:
	for property in ProjectSettings.get_property_list():
		if property["name"] == setting:
			return property["hint_string"]
	return ""
