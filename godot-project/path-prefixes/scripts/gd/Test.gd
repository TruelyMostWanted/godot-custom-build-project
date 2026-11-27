extends FeatureTest
## Full API test of Path Prefixes (PathPrefixServer, PathPrefix, PathInfo). Same tests as scripts/cs/Test.cs.

const ENGINE_PREFIXES: Array[String] = ["res://", "user://", "uid://", "pipe://", "exec://"]
const DEFAULT_PREFIXES: Array[String] = ["appdata://", "config://", "cache://", "temp://"]
const PACK_PATH := "user://pp_test.pck"
const PACK_FOLDER := "res://pp_test_pack"
# A feature tag that is never true.
const MISSING_TAG := "pp_no_such_feature"

const TestPrefix := preload("res://path-prefixes/scripts/gd/TestPrefix.gd")

var _registered: Array[String] = []
var _unregistered: Array[String] = []


func _run_tests() -> void:
	_test_engine_prefixes()
	_test_default_prefixes()
	_test_os_path_prefixes()
	_test_signals()
	_test_folder_prefix()
	_test_callable_prefix()
	_test_subclass_prefix()
	_test_platforms()
	_test_system_dir_prefix()
	_test_resolve()
	_test_resolve_safe()
	_test_resource_references()
	_test_engine_path()
	# Last: mounting a pack switches the res:// access of this run.
	_test_pack()


func _test_engine_prefixes() -> void:
	var prefixes: PackedStringArray = PathPrefixServer.get_prefixes()
	for prefix: String in ENGINE_PREFIXES:
		expect(prefixes.has(prefix), "get_prefixes lists %s" % prefix)
		expect(PathPrefixServer.has_prefix(prefix), "has_prefix finds %s" % prefix)
		expect(PathPrefixServer.is_prefix_locked(prefix), "is_prefix_locked: %s is locked" % prefix)
		expect_equal(PathPrefixServer.register_prefix_folder(prefix, "user://x"), ERR_LOCKED, "register_prefix_folder(%s) fails" % prefix)
		expect_equal(PathPrefixServer.unregister_prefix(prefix), ERR_LOCKED, "unregister_prefix(%s) fails" % prefix)
	expect_equal(PathPrefixServer.globalize("user://a.txt"), ProjectSettings.globalize_path("user://a.txt"), "globalize(user://) is the user folder")
	var global_path: String = PathPrefixServer.globalize("user://folder/file.txt")
	expect_equal(PathPrefixServer.localize(global_path), "user://folder/file.txt", "localize reverses globalize")
	expect_equal(PathPrefixServer.globalize("no_prefix/a.txt"), "no_prefix/a.txt", "globalize keeps a path without prefix")
	expect_equal(PathPrefixServer.localize("/nowhere/a.txt"), "", "localize returns an empty string outside all prefixes")
	expect(not PathPrefixServer.has_prefix("nope://"), "has_prefix is false for an unknown prefix")
	expect(not PathPrefixServer.is_prefix_available("nope://"), "is_prefix_available is false for an unknown prefix")
	expect(not PathPrefixServer.is_prefix_read_only("user://"), "is_prefix_read_only is false for user://")
	expect_equal(ProjectSettings.globalize_path("exec://a.txt"), PathPrefixServer.globalize("exec://a.txt"), "ProjectSettings.globalize_path knows exec://")


func _test_default_prefixes() -> void:
	expect(ProjectSettings.get_setting("filesystem/path_prefixes/defaults", false), "filesystem/path_prefixes/defaults is on")
	for prefix: String in DEFAULT_PREFIXES:
		expect(PathPrefixServer.has_prefix(prefix), "%s is registered" % prefix)
		expect(not PathPrefixServer.is_prefix_locked(prefix), "%s is not locked" % prefix)
	expect_equal(PathPrefixServer.register_prefix_folder("appdata://", "user://pp_appdata"), OK, "a default prefix can be replaced")
	expect_equal(PathPrefixServer.globalize("appdata://a"), ProjectSettings.globalize_path("user://pp_appdata/a"), "the replacement is used")


func _test_os_path_prefixes() -> void:
	var folders: Dictionary = {
		PathPrefixServer.OS_PATH_DATA: OS.get_data_dir(),
		PathPrefixServer.OS_PATH_CONFIG: OS.get_config_dir(),
		PathPrefixServer.OS_PATH_CACHE: OS.get_cache_dir(),
		PathPrefixServer.OS_PATH_TEMP: OS.get_temp_dir(),
	}
	for os_path: int in folders:
		var prefix: PathPrefix = PathPrefixServer.create_os_path_prefix(os_path as PathPrefixServer.OSPath)
		expect_equal(PathPrefixServer.register_prefix("ppos://", prefix), OK, "register_prefix with create_os_path_prefix(%d)" % os_path)
		var folder: String = folders[os_path]
		var global_path: String = PathPrefixServer.globalize("ppos://x")
		expect_equal(global_path, folder.replace("\\", "/").path_join("x"), "OS path %d maps to its OS folder" % os_path)
	PathPrefixServer.unregister_prefix("ppos://")


func _test_signals() -> void:
	PathPrefixServer.prefix_registered.connect(_on_prefix_registered)
	PathPrefixServer.prefix_unregistered.connect(_on_prefix_unregistered)
	PathPrefixServer.register_prefix_folder("ppsig://", "user://pp_sig")
	PathPrefixServer.unregister_prefix("ppsig://")
	PathPrefixServer.prefix_registered.disconnect(_on_prefix_registered)
	PathPrefixServer.prefix_unregistered.disconnect(_on_prefix_unregistered)
	expect_equal(_registered, ["ppsig://"], "prefix_registered is emitted with the prefix")
	expect_equal(_unregistered, ["ppsig://"], "prefix_unregistered is emitted with the prefix")
	expect_equal(PathPrefixServer.unregister_prefix("ppsig://"), ERR_DOES_NOT_EXIST, "unregister_prefix of an unknown prefix fails")
	expect(PathPrefixServer.register_prefix_folder("Bad://", "user://x") != OK, "register_prefix_folder rejects uppercase letters")
	expect(PathPrefixServer.register_prefix_folder("http://", "user://x") != OK, "register_prefix_folder rejects URL schemes")


func _test_folder_prefix() -> void:
	var folder_prefix: PathPrefix = PathPrefixServer.create_folder_prefix("user://pp_folder")
	expect_equal(PathPrefixServer.register_prefix("ppfolder://", folder_prefix), OK, "register_prefix with create_folder_prefix")
	expect_equal(DirAccess.make_dir_recursive_absolute("ppfolder://sub/dir"), OK, "DirAccess creates folders through the prefix")
	var file: FileAccess = FileAccess.open("ppfolder://sub/a.txt", FileAccess.WRITE)
	file.store_string("hello")
	file.close()
	expect_equal(FileAccess.get_file_as_string("ppfolder://sub/a.txt"), "hello", "FileAccess writes and reads through the prefix")
	var dir: DirAccess = DirAccess.open("ppfolder://sub")
	expect(dir.file_exists("a.txt"), "DirAccess.open lists the file")
	var resource := Resource.new()
	resource.resource_name = "from_prefix"
	expect_equal(ResourceSaver.save(resource, "ppfolder://r.tres"), OK, "ResourceSaver saves through the prefix")
	var loaded: Resource = ResourceLoader.load("ppfolder://r.tres")
	expect_equal(loaded.resource_name, "from_prefix", "ResourceLoader loads through the prefix")
	expect_equal(loaded.resource_path, "ppfolder://r.tres", "resource_path keeps the prefixed path")
	expect(ResourceLoader.exists("ppfolder://r.tres"), "ResourceLoader.exists finds the resource")
	expect(not ResourceLoader.exists("ppfolder://missing.tres"), "ResourceLoader.exists is false for a missing resource")
	expect(ResourceLoader.load("ppfolder://r.tres") == loaded, "a second load returns the cached resource")
	PathPrefixServer.register_prefix_folder("pp_under://", "user://pp_folder")
	var underscore: Resource = ResourceLoader.load("pp_under://r.tres")
	expect(underscore != null, "ResourceLoader loads through a prefix with an underscore")
	PathPrefixServer.unregister_prefix("pp_under://")
	PathPrefixServer.unregister_prefix("ppfolder://")


func _test_callable_prefix() -> void:
	var globalize := func(path: String) -> String: return ProjectSettings.globalize_path("user://pp_call/" + path.trim_prefix("ppcall://"))
	var localize := func(global_path: String) -> String:
		if global_path.ends_with("/pp_call/found"):
			return "ppcall://found"
		return ""
	var is_available := func() -> bool: return false
	expect_equal(PathPrefixServer.register_prefix_callable("ppcall://", globalize, [], localize, is_available), OK, "register_prefix_callable with all callables")
	expect(not PathPrefixServer.is_prefix_available("ppcall://"), "is_prefix_available uses the is_available callable")
	expect_equal(PathPrefixServer.globalize("ppcall://a"), "ppcall://a", "globalize keeps the path of an unavailable prefix")
	expect_equal(PathPrefixServer.resolve("ppcall://a").error, ERR_UNAVAILABLE, "resolve of an unavailable prefix gives ERR_UNAVAILABLE")
	PathPrefixServer.register_prefix_callable("ppcall://", globalize, [], localize)
	expect(PathPrefixServer.is_prefix_available("ppcall://"), "without is_available the prefix is available")
	expect_equal(PathPrefixServer.globalize("ppcall://a"), ProjectSettings.globalize_path("user://pp_call/a"), "globalize calls the globalize callable")
	var found: String = PathPrefixServer.localize(ProjectSettings.globalize_path("user://pp_call/found"))
	expect_equal(found, "ppcall://found", "localize calls the localize callable")
	PathPrefixServer.unregister_prefix("ppcall://")


func _test_subclass_prefix() -> void:
	var prefix := TestPrefix.new()
	expect_equal(PathPrefixServer.register_prefix("test://", prefix), OK, "register_prefix with a PathPrefix subclass")
	expect_equal(PathPrefixServer.globalize("test://a/b.txt"), ProjectSettings.globalize_path("user://pp_test/a/b.txt"), "_globalize is called")
	var local_path: String = PathPrefixServer.localize(ProjectSettings.globalize_path("user://pp_test/a/b.txt"))
	expect_equal(local_path, "test://a/b.txt", "_localize is called")
	expect(not PathPrefixServer.is_prefix_read_only("test://"), "_is_read_only is false")
	prefix.read_only = true
	expect(PathPrefixServer.is_prefix_read_only("test://"), "_is_read_only is true")
	var write: PathInfo = PathPrefixServer.resolve_safe("test://a.txt", PathPrefixServer.ACCESS_WRITE)
	expect_equal(write.error, ERR_FILE_NO_PERMISSION, "resolve_safe rejects writing to a read-only prefix")
	expect(write.read_only, "PathInfo.read_only is true")
	var read: PathInfo = PathPrefixServer.resolve_safe("test://a.txt", PathPrefixServer.ACCESS_READ)
	expect(read.valid, "resolve_safe accepts reading from a read-only prefix")
	prefix.available = false
	expect(not PathPrefixServer.is_prefix_available("test://"), "_is_available is false")
	expect(not PathPrefixServer.resolve("test://a.txt").available, "PathInfo.available is false")
	PathPrefixServer.unregister_prefix("test://")


func _test_platforms() -> void:
	var platform: String = OS.get_name().to_lower()
	PathPrefixServer.register_prefix_folder("ppplat://", "user://pp_all")
	PathPrefixServer.register_prefix_folder("ppplat://", "user://pp_named", [MISSING_TAG])
	var global_path: String = PathPrefixServer.globalize("ppplat://a")
	expect_equal(global_path, ProjectSettings.globalize_path("user://pp_all/a"), "a registration for a feature that does not match is ignored")
	PathPrefixServer.register_prefix_folder("ppplat://", "user://pp_named", [platform])
	global_path = PathPrefixServer.globalize("ppplat://a")
	expect_equal(global_path, ProjectSettings.globalize_path("user://pp_named/a"), "a registration for this platform wins over the one for all")
	expect_equal(PathPrefixServer.unregister_prefix("ppplat://", [platform]), OK, "unregister_prefix removes one platform registration")
	global_path = PathPrefixServer.globalize("ppplat://a")
	expect_equal(global_path, ProjectSettings.globalize_path("user://pp_all/a"), "the registration for all platforms is used again")
	PathPrefixServer.unregister_prefix("ppplat://")
	expect(not PathPrefixServer.has_prefix("ppplat://"), "unregister_prefix without platforms removes all registrations")
	PathPrefixServer.register_prefix_folder("pponly://", "user://pp_only", [MISSING_TAG])
	expect(PathPrefixServer.has_prefix("pponly://"), "has_prefix is true for any platform")
	expect(not PathPrefixServer.is_prefix_available("pponly://"), "is_prefix_available is false on other platforms")
	PathPrefixServer.unregister_prefix("pponly://")


func _test_system_dir_prefix() -> void:
	var prefix: PathPrefix = PathPrefixServer.create_system_dir_prefix(OS.SYSTEM_DIR_PICTURES)
	expect_equal(PathPrefixServer.register_prefix("pppictures://", prefix), OK, "register_prefix with create_system_dir_prefix")
	var expected: String = OS.get_system_dir(OS.SYSTEM_DIR_PICTURES).path_join("a.png")
	expect_equal(PathPrefixServer.globalize("pppictures://a.png"), expected, "the prefix maps to the system folder")
	var private_prefix: PathPrefix = PathPrefixServer.create_system_dir_prefix(OS.SYSTEM_DIR_PICTURES, false)
	expect(private_prefix != null, "create_system_dir_prefix accepts shared_storage = false")
	PathPrefixServer.unregister_prefix("pppictures://")


func _test_resolve() -> void:
	var info: PathInfo = PathPrefixServer.resolve("USER://saves\\a.json")
	expect(info.valid, "resolve: the path is valid")
	expect_equal(info.error, OK, "PathInfo.error is OK")
	expect_equal(info.error_message, "", "PathInfo.error_message is empty")
	expect_equal(info.source_path, "USER://saves\\a.json", "source_path is the given path")
	expect_equal(info.local_path, "user://saves/a.json", "local_path is canonical")
	expect_equal(info.global_path, ProjectSettings.globalize_path("user://saves/a.json"), "global_path is the OS path")
	expect_equal(info.prefix, "user://", "prefix is canonical")
	expect_equal(info.relative_path, "saves/a.json", "relative_path is the part after the prefix")
	expect_equal(info.file_name, "a.json", "file_name")
	expect_equal(info.extension, "json", "extension")
	expect_equal(info.base_dir, "user://saves", "base_dir")
	expect(info.available, "PathInfo.available is true")
	expect(info.locked, "PathInfo.locked is true for user://")
	expect(not info.read_only, "PathInfo.read_only is false for user://")
	var dots: PathInfo = PathPrefixServer.resolve("user://a/./b/../c.txt")
	expect_equal(dots.local_path, "user://a/c.txt", "resolve normalizes . and ..")
	var from_global: PathInfo = PathPrefixServer.resolve(OS.get_user_data_dir() + "/saves/a.json")
	expect_equal(from_global.local_path, "user://saves/a.json", "resolve accepts an OS path below a prefix")
	var relative: PathInfo = PathPrefixServer.resolve("project.godot")
	expect_equal(relative.local_path, "res://project.godot", "resolve treats a relative path as res://")
	expect(relative.exists(), "PathInfo.exists is true for an existing file")
	expect(not relative.is_dir(), "PathInfo.is_dir is false for a file")
	expect(PathPrefixServer.resolve("res://").is_dir(), "PathInfo.is_dir is true for res://")
	var unknown: PathInfo = PathPrefixServer.resolve("nope://x")
	expect(not unknown.valid, "resolve of an unknown prefix is invalid")
	expect_equal(unknown.error, ERR_FILE_BAD_PATH, "the error is ERR_FILE_BAD_PATH")
	expect(unknown.error_message != "", "error_message describes the error")
	expect(not PathPrefixServer.resolve("temp://x").locked, "PathInfo.locked is false for temp://")


func _test_resolve_safe() -> void:
	var good: PathInfo = PathPrefixServer.resolve_safe("user://saves/a.json", PathPrefixServer.ACCESS_WRITE)
	expect(good.valid, "resolve_safe accepts a prefixed path")
	var rejected: Array[String] = ["user://saves/../../x", "/etc/passwd", "C:\\Windows\\win.ini", "nope://x", "relative.txt"]
	for path: String in rejected:
		var info: PathInfo = PathPrefixServer.resolve_safe(path, PathPrefixServer.ACCESS_READ)
		expect_equal(info.error, ERR_FILE_BAD_PATH, "resolve_safe rejects %s" % path)
	var roots: Array[String] = ["user://saves/"]
	var outside: PathInfo = PathPrefixServer.resolve_safe("user://other/a.json", PathPrefixServer.ACCESS_WRITE, roots)
	expect_equal(outside.error, ERR_FILE_BAD_PATH, "resolve_safe rejects a path outside allowed_roots")
	var inside: PathInfo = PathPrefixServer.resolve_safe("user://saves/a.json", PathPrefixServer.ACCESS_WRITE, roots)
	expect(inside.valid, "resolve_safe accepts a path inside allowed_roots")


func _test_resource_references() -> void:
	PathPrefixServer.register_prefix_folder("ppref://", "user://pp_ref")
	DirAccess.make_dir_recursive_absolute("ppref://")
	var gradient := Gradient.new()
	gradient.take_over_path("ppref://b.tres")
	ResourceSaver.save(gradient, "ppref://b.tres")
	var texture := GradientTexture1D.new()
	texture.gradient = gradient
	ResourceSaver.save(texture, "ppref://a.tres")
	var text: String = FileAccess.get_file_as_string("ppref://a.tres")
	expect(text.contains("path=\"ppref://b.tres\""), "a saved resource stores the prefixed path of its reference")
	var loaded: GradientTexture1D = ResourceLoader.load("ppref://a.tres", "", ResourceLoader.CACHE_MODE_IGNORE_DEEP)
	expect_equal(loaded.gradient.resource_path, "ppref://b.tres", "the loaded reference keeps the prefixed path")
	var dependencies: PackedStringArray = ResourceLoader.get_dependencies("ppref://a.tres")
	expect(dependencies[0].contains("ppref://b.tres"), "get_dependencies lists the prefixed path")
	PathPrefixServer.unregister_prefix("ppref://")


func _test_engine_path() -> void:
	PathPrefixServer.register_prefix_folder("pppack://", PACK_FOLDER)
	PathPrefixServer.register_prefix_folder("ppchain://", "pppack://sub")
	PathPrefixServer.register_prefix_folder("ppuser://", "user://pp_user")
	var to_pack := func(path: String) -> String: return PACK_FOLDER + "/" + path.trim_prefix("ppcall://")
	PathPrefixServer.register_prefix_callable("ppcall://", to_pack)
	expect_equal(PathPrefixServer.resolve_engine_path("pppack://data.txt"), PACK_FOLDER + "/data.txt", "resolve_engine_path of a prefix into res://")
	expect_equal(PathPrefixServer.resolve_engine_path("pppack://x/../data.txt"), PACK_FOLDER + "/data.txt", "resolve_engine_path resolves ..")
	expect_equal(PathPrefixServer.resolve_engine_path("ppchain://b.txt"), PACK_FOLDER + "/sub/b.txt", "resolve_engine_path follows a chain of prefixes")
	expect_equal(PathPrefixServer.resolve_engine_path("ppcall://data.txt"), PACK_FOLDER + "/data.txt", "resolve_engine_path of a callable prefix")
	expect_equal(PathPrefixServer.resolve_engine_path("ppuser://a.txt"), "user://pp_user/a.txt", "resolve_engine_path of a prefix into user://")
	expect_equal(PathPrefixServer.resolve_engine_path("temp://a.txt"), "temp://a.txt", "a prefix that ends in an OS path stays unchanged")
	expect_equal(PathPrefixServer.resolve_engine_path("res://a.txt"), "res://a.txt", "an engine path stays unchanged")
	DirAccess.make_dir_recursive_absolute("ppuser://saves")
	var file: FileAccess = FileAccess.open("ppuser://saves/a.txt", FileAccess.WRITE)
	expect_equal(file.get_path(), "user://pp_user/saves/a.txt", "FileAccess.get_path is the user:// path")
	file.close()
	var dir: DirAccess = DirAccess.open("ppuser://saves")
	expect(dir.get_current_dir().begins_with("user://"), "DirAccess.open is a user:// access")
	PathPrefixServer.unregister_prefix("ppuser://")


func _test_pack() -> void:
	var packer := PCKPacker.new()
	packer.pck_start(PACK_PATH)
	packer.add_file_from_buffer("pp_test_pack/data.txt", "Packed".to_utf8_buffer())
	packer.add_file_from_buffer("pp_test_pack/sub/b.txt", "Nested".to_utf8_buffer())
	var tres: String = "[gd_resource type=\"Resource\" format=3]\n\n[resource]\nresource_name = \"packed\"\n"
	packer.add_file_from_buffer("pp_test_pack/res.tres", tres.to_utf8_buffer())
	expect_equal(packer.flush(), OK, "PCKPacker writes the pack")
	expect(not FileAccess.file_exists(PACK_FOLDER + "/data.txt"), "the file is not on disk before the pack is loaded")
	expect(ProjectSettings.load_resource_pack(ProjectSettings.globalize_path(PACK_PATH)), "load_resource_pack mounts the pack")
	expect(FileAccess.file_exists("pppack://data.txt"), "FileAccess.file_exists finds the packed file")
	expect(not FileAccess.file_exists("pppack://missing.txt"), "FileAccess.file_exists is false for a missing file")
	expect_equal(FileAccess.get_file_as_string("pppack://data.txt"), "Packed", "FileAccess reads a packed file through the prefix")
	expect_equal(FileAccess.get_file_as_string("ppchain://b.txt"), "Nested", "FileAccess reads through a chain of prefixes")
	expect_equal(FileAccess.get_file_as_string("ppcall://data.txt"), "Packed", "FileAccess reads through a callable prefix")
	expect(DirAccess.dir_exists_absolute("pppack://sub"), "DirAccess finds a packed folder")
	var dir: DirAccess = DirAccess.open("pppack://")
	expect(dir.file_exists("data.txt"), "DirAccess.open lists the packed files")
	var by_prefix: Resource = ResourceLoader.load("pppack://res.tres")
	expect_equal(by_prefix.resource_name, "packed", "ResourceLoader loads a packed resource through the prefix")
	expect_equal(by_prefix.resource_path, PACK_FOLDER + "/res.tres", "resource_path is the res:// path")
	expect(ResourceLoader.load(PACK_FOLDER + "/res.tres") == by_prefix, "the prefix and the res:// path share the cache")
	for prefix: String in ["pppack://", "ppchain://", "ppcall://"]:
		PathPrefixServer.unregister_prefix(prefix)


func _on_prefix_registered(prefix: String) -> void:
	_registered.append(prefix)


func _on_prefix_unregistered(prefix: String) -> void:
	_unregistered.append(prefix)
