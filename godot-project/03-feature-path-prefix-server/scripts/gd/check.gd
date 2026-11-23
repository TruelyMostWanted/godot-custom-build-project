extends FeatureCheck
## GDScript checks of 03-feature-path-prefix-server. Check the same things as scripts/cs/Issue03FeaturePathPrefixServerCheck.cs.

const ENGINE_PREFIXES := ["res://", "user://", "uid://", "pipe://", "exec://"]


func _get_check_name() -> String:
	return "03-feature-path-prefix-server"


func _run_checks() -> void:
	_check_engine_prefixes()
	_check_exec_prefix()
	_check_callable_prefix()
	_check_platforms()
	_check_defaults()
	_check_resolve()
	_check_resolve_safe()
	_check_performance()


func _check_engine_prefixes() -> void:
	expect(Engine.has_singleton("PathPrefixServer"), "PathPrefixServer is a singleton")
	for prefix: String in ENGINE_PREFIXES:
		var before := PathPrefixServer.globalize(prefix + "a/b.txt")
		expect(PathPrefixServer.is_prefix_locked(prefix), "%s is locked" % prefix)
		expect(PathPrefixServer.register_prefix_folder(prefix, "user://x") == ERR_LOCKED, "register_prefix_folder(%s) fails with ERR_LOCKED" % prefix)
		expect(PathPrefixServer.register_prefix_callable(prefix, func(p: String) -> String: return p) == ERR_LOCKED, "register_prefix_callable(%s) fails with ERR_LOCKED" % prefix)
		expect(PathPrefixServer.unregister_prefix(prefix) == ERR_LOCKED, "unregister_prefix(%s) fails with ERR_LOCKED" % prefix)
		expect(PathPrefixServer.globalize(prefix + "a/b.txt") == before, "%s resolves as before" % prefix)
	expect(PathPrefixServer.globalize("res://a.txt") == ProjectSettings.globalize_path("res://a.txt"), "res:// globalizes like ProjectSettings")
	expect(PathPrefixServer.globalize("user://a.txt") == ProjectSettings.globalize_path("user://a.txt"), "user:// globalizes like ProjectSettings")
	for prefix: String in ["res://", "user://"]:
		var path := prefix + "folder/file.txt"
		expect(PathPrefixServer.localize(PathPrefixServer.globalize(path)) == path, "globalize() then localize() gives %s" % path)
	# In the editor exec:// and res:// are the same folder; the earlier registration (res://) wins.
	var exec_back := PathPrefixServer.localize(PathPrefixServer.globalize("exec://folder/file.txt"))
	expect(exec_back == "exec://folder/file.txt" or exec_back == "res://folder/file.txt", "exec:// round trip gives exec:// or res:// (editor): %s" % exec_back)


func _check_exec_prefix() -> void:
	var file := FileAccess.open("exec://pp_exec_test.txt", FileAccess.WRITE)
	expect(file != null, "FileAccess.open(exec://..., WRITE) works")
	if file:
		file.store_string("exec")
		file.close()
	var real := ProjectSettings.globalize_path("res://pp_exec_test.txt")
	expect(FileAccess.file_exists(real), "exec:// writes into the project folder in the editor")
	expect(ProjectSettings.globalize_path("exec://pp_exec_test.txt") == real, "ProjectSettings.globalize_path() knows exec://")
	DirAccess.remove_absolute(real)


func _check_callable_prefix() -> void:
	var err := PathPrefixServer.register_prefix_callable("mods://",
			func(path: String) -> String: return "user://pp_gd_mods/" + path.trim_prefix("mods://"),
			[],
			func(global_path: String) -> String:
				var root := ProjectSettings.globalize_path("user://pp_gd_mods")
				return "mods://" + global_path.substr(root.length() + 1) if global_path.begins_with(root + "/") else "")
	expect(err == OK, "register_prefix_callable(mods://) works")
	expect(DirAccess.make_dir_recursive_absolute("mods://sub/dir") == OK, "DirAccess.make_dir_recursive_absolute(mods://sub/dir)")
	expect(DirAccess.dir_exists_absolute(ProjectSettings.globalize_path("user://pp_gd_mods/sub/dir")), "the folder exists below user://pp_gd_mods")

	var file := FileAccess.open("mods://sub/a.txt", FileAccess.WRITE)
	expect(file != null, "FileAccess.open(mods://sub/a.txt, WRITE)")
	if file:
		file.store_string("hello")
		file.close()
	expect(FileAccess.get_file_as_string("mods://sub/a.txt") == "hello", "FileAccess reads mods://sub/a.txt")
	var dir := DirAccess.open("mods://sub")
	expect(dir != null and dir.file_exists("a.txt"), "DirAccess.open(mods://sub) lists a.txt")

	var res := Resource.new()
	res.resource_name = "from_mods"
	expect(ResourceSaver.save(res, "mods://r.tres") == OK, "ResourceSaver.save(mods://r.tres)")
	var loaded := ResourceLoader.load("mods://r.tres", "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(loaded != null and loaded.resource_name == "from_mods", "ResourceLoader.load(mods://r.tres)")

	_check_resource_io()

	var info := PathPrefixServer.resolve(ProjectSettings.globalize_path("user://pp_gd_mods/sub/a.txt"))
	expect(info.local_path == "user://pp_gd_mods/sub/a.txt" or info.local_path == "mods://sub/a.txt", "a global path below two prefixes resolves to one of them: %s" % info.local_path)
	expect(PathPrefixServer.unregister_prefix("mods://") == OK, "unregister_prefix(mods://)")
	expect(not PathPrefixServer.has_prefix("mods://"), "mods:// is gone")


func _check_resource_io() -> void:
	# exists(), default cache mode, has_cached()
	expect(ResourceLoader.exists("mods://r.tres"), "ResourceLoader.exists(mods://r.tres) is true")
	expect(not ResourceLoader.exists("mods://missing.tres"), "ResourceLoader.exists(mods://missing.tres) is false")
	var first := ResourceLoader.load("mods://r.tres")
	var second := ResourceLoader.load("mods://r.tres")
	expect(first != null and first == second, "load(mods://r.tres) twice gives the same object (cache)")
	expect(first != null and first.resource_path == "mods://r.tres", "resource_path is mods://r.tres")
	expect(ResourceLoader.has_cached("mods://r.tres"), "ResourceLoader.has_cached(mods://r.tres)")

	# threaded loading
	var res2 := Resource.new()
	res2.resource_name = "threaded"
	expect(ResourceSaver.save(res2, "mods://r2.tres") == OK, "ResourceSaver.save(mods://r2.tres)")
	expect(ResourceLoader.load_threaded_request("mods://r2.tres") == OK, "load_threaded_request(mods://r2.tres)")
	var deadline := Time.get_ticks_msec() + 10000
	var status := ResourceLoader.load_threaded_get_status("mods://r2.tres")
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and Time.get_ticks_msec() < deadline:
		OS.delay_msec(10)
		status = ResourceLoader.load_threaded_get_status("mods://r2.tres")
	expect(status == ResourceLoader.THREAD_LOAD_LOADED, "load_threaded_get_status(mods://r2.tres) is LOADED: %d" % status)
	var threaded := ResourceLoader.load_threaded_get("mods://r2.tres")
	expect(threaded != null and threaded.resource_name == "threaded", "load_threaded_get(mods://r2.tres) is not null")

	# references between files
	var b := Gradient.new()
	b.set_color(0, Color.RED)
	b.take_over_path("mods://b.tres")
	expect(ResourceSaver.save(b, "mods://b.tres") == OK, "ResourceSaver.save(mods://b.tres)")
	expect(b.resource_path == "mods://b.tres", "take_over_path() sets resource_path to mods://b.tres")
	var a := GradientTexture1D.new()
	a.gradient = b
	expect(ResourceSaver.save(a, "mods://a.tres") == OK, "ResourceSaver.save(mods://a.tres)")
	var text := FileAccess.get_file_as_string("mods://a.tres")
	expect(text.contains("path=\"mods://b.tres\""), "a.tres has an ext_resource with path mods://b.tres")
	var a_loaded := ResourceLoader.load("mods://a.tres", "", ResourceLoader.CACHE_MODE_IGNORE_DEEP) as GradientTexture1D
	expect(a_loaded != null and a_loaded.gradient != null, "a.tres loads with its gradient")
	if a_loaded and a_loaded.gradient:
		expect(a_loaded.gradient.resource_path == "mods://b.tres", "the loaded gradient has resource_path mods://b.tres: %s" % a_loaded.gradient.resource_path)
	var found := false
	for dependency: String in ResourceLoader.get_dependencies("mods://a.tres"):
		if dependency.contains("mods://b.tres"):
			found = true
	expect(found, "get_dependencies(mods://a.tres) contains mods://b.tres")
	print("INFO gd: uid of mods://b.tres = %d, .uid file exists = %s" % [ResourceLoader.get_resource_uid("mods://b.tres"), FileAccess.file_exists("mods://b.tres.uid")])


func _check_platforms() -> void:
	PathPrefixServer.register_prefix_folder("pp_win://", "user://pp_win", ["windows"])
	PathPrefixServer.register_prefix_folder("pp_linux://", "user://pp_linux", ["linux"])
	PathPrefixServer.register_prefix_folder("pp_web://", "user://pp_web", ["web"])
	expect(PathPrefixServer.is_prefix_available("pp_win://") == OS.has_feature("windows"), "a windows-only prefix is available only on Windows")
	expect(PathPrefixServer.is_prefix_available("pp_linux://") == OS.has_feature("linux"), "a linux-only prefix is available only on Linux")
	expect(PathPrefixServer.is_prefix_available("pp_web://") == OS.has_feature("web"), "a web-only prefix is available only on the Web")
	if not OS.has_feature("linux"):
		expect(PathPrefixServer.resolve("pp_linux://a").error == ERR_UNAVAILABLE, "resolve() of an unavailable prefix gives ERR_UNAVAILABLE")
	for prefix: String in ["pp_win://", "pp_linux://", "pp_web://"]:
		PathPrefixServer.unregister_prefix(prefix)


func _check_defaults() -> void:
	expect(ProjectSettings.get_setting("filesystem/path_prefixes/defaults", false) == true, "filesystem/path_prefixes/defaults is on by default")
	for prefix: String in ["appdata://", "config://", "cache://", "temp://"]:
		expect(PathPrefixServer.has_prefix(prefix), "%s is registered" % prefix)
		expect(not PathPrefixServer.is_prefix_locked(prefix), "%s is not locked" % prefix)
	expect(PathPrefixServer.globalize("temp://x") == OS.get_temp_dir().replace("\\", "/").path_join("x"), "temp:// maps to OS.get_temp_path()")
	expect(PathPrefixServer.register_prefix_folder("appdata://", "user://pp_appdata") == OK, "appdata:// can be replaced")
	expect(PathPrefixServer.globalize("appdata://a") == ProjectSettings.globalize_path("user://pp_appdata/a"), "the replacement is used")
	PathPrefixServer.register_prefix("appdata://", PathPrefixServer.create_os_path_logic(PathPrefixServer.OS_PATH_DATA))


func _check_resolve() -> void:
	var info := PathPrefixServer.resolve("USER://saves\\a.json")
	expect(info.valid, "resolve(USER://saves\\a.json) is valid")
	expect(info.local_path == "user://saves/a.json", "local_path == user://saves/a.json")
	expect(info.global_path == ProjectSettings.globalize_path("user://saves/a.json"), "global_path is the user folder")
	expect(info.prefix == "user://", "prefix == user://")
	expect(info.relative_path == "saves/a.json", "relative_path == saves/a.json")
	expect(info.file_name == "a.json" and info.extension == "json" and info.base_dir == "user://saves", "file_name, extension, base_dir")
	var from_global := PathPrefixServer.resolve(OS.get_user_data_dir() + "/saves/a.json")
	expect(from_global.local_path == "user://saves/a.json", "resolve(user data dir + /saves/a.json) gives the same local_path")
	var nope := PathPrefixServer.resolve("nope://x")
	expect(not nope.valid and nope.error == ERR_FILE_BAD_PATH and nope.error_message != "", "resolve(nope://x) is invalid with an error")
	expect(PathPrefixServer.resolve("res://project.godot").exists(), "PathInfo.exists() for res://project.godot")
	expect(PathPrefixServer.resolve("res://").is_dir(), "PathInfo.is_dir() for res://")


func _check_resolve_safe() -> void:
	expect(PathPrefixServer.resolve_safe("user://saves/a.json", true).valid, "resolve_safe(user://saves/a.json) is valid")
	expect(PathPrefixServer.resolve_safe("user://saves/../../x", false).error == ERR_FILE_BAD_PATH, "resolve_safe rejects user://saves/../../x")
	expect(PathPrefixServer.resolve_safe("/etc/passwd", false).error == ERR_FILE_BAD_PATH, "resolve_safe rejects /etc/passwd")
	expect(PathPrefixServer.resolve_safe("C:\\Windows\\win.ini", false).error == ERR_FILE_BAD_PATH, "resolve_safe rejects C:\\Windows\\win.ini")
	expect(PathPrefixServer.resolve_safe("user://other/a.json", true, ["user://saves/"]).error == ERR_FILE_BAD_PATH, "resolve_safe rejects paths outside allowed_roots")
	expect(PathPrefixServer.resolve_safe("user://saves/a.json", true, ["user://saves/"]).valid, "resolve_safe accepts paths inside allowed_roots")
	var res_write := PathPrefixServer.resolve_safe("res://a.tres", true)
	if OS.has_feature("template"):
		expect(res_write.error == ERR_FILE_NO_PERMISSION, "resolve_safe rejects res:// for writing in an exported build")
	else:
		expect(res_write.valid, "resolve_safe accepts res:// for writing in the editor")


func _check_performance() -> void:
	var start := Time.get_ticks_usec()
	for i in 100000:
		PathPrefixServer.resolve("user://saves/a.json")
	var resolve_ms := (Time.get_ticks_usec() - start) / 1000.0
	start = Time.get_ticks_usec()
	for i in 10000:
		FileAccess.file_exists("res://project.godot")
	var exists_ms := (Time.get_ticks_usec() - start) / 1000.0
	print("PERF gd: 100000 x resolve() = %.1f ms, 10000 x FileAccess.file_exists(res://) = %.1f ms" % [resolve_ms, exists_ms])
	expect(resolve_ms < 10000.0, "100000 resolve() calls take less than 10 s")
