# Written before the engine build of #7; run after the merge.
extends FeatureCheck
## GDScript checks of 07-feature-files-manifest. Check the same things as scripts/cs/Issue07FeatureFilesManifestCheck.cs.

const FEATURE := "res://07-feature-files-manifest/resources/"
const DATA := FEATURE + "data/"
const ROOT := "user://07-feature-files-manifest/gd/"
const PREFIX := "fm07gd://"
const PERF_COUNT := 1000
const THREAD_TIMEOUT_MSEC := 20000


func _get_check_name() -> String:
	return "07-feature-files-manifest"


func _run_checks() -> void:
	_cleanup()
	DirAccess.make_dir_recursive_absolute(ROOT)
	_check_api()
	_check_normalization()
	_check_manifest_file()
	_check_filters()
	_check_single_files()
	_check_missing_folders()
	_check_prefixes()
	await _check_threaded()
	_check_null()
	await _check_performance()
	_cleanup()


func _data(relative: String) -> String:
	return DATA + relative


func _load_manifest_file() -> FilesManifest:
	return ResourceLoader.load(FEATURE + "manifest.tres", "", ResourceLoader.CACHE_MODE_IGNORE) as FilesManifest


func _check_api() -> void:
	expect(ClassDB.class_exists("FilesManifest"), "ClassDB knows FilesManifest")
	expect(ClassDB.is_parent_class("FilesManifest", "Resource"), "FilesManifest is a Resource")
	for method in ["get_manifest_paths", "load_manifest", "load_threaded_request_manifest"]:
		expect(ClassDB.class_has_method("ResourceLoader", method), "ResourceLoader has %s()" % method)
	var manifest := FilesManifest.new()
	expect_equal(manifest.type_hint, &"", "default type_hint is empty")
	expect_equal(manifest.extensions, PackedStringArray(), "default extensions are empty")
	expect_equal(manifest.recursive, true, "default recursive is true")
	expect_equal(manifest.create_non_existing_folders, false, "default create_non_existing_folders is false")
	var stored := []
	for property in manifest.get_property_list():
		if property.usage & PROPERTY_USAGE_STORAGE:
			stored.append(property.name)
	for property_name in ["type_hint", "extensions", "folder_paths", "file_paths", "recursive", "create_non_existing_folders"]:
		expect(property_name in stored, "%s is stored (PROPERTY_USAGE_STORAGE)" % property_name)


func _check_normalization() -> void:
	expect_equal(FilesManifest.normalize_extension("png"), ".png", "normalize_extension(png)")
	expect_equal(FilesManifest.normalize_extension("*.PNG"), ".png", "normalize_extension(*.PNG)")
	expect_equal(FilesManifest.normalize_extension(" .Tres "), ".tres", "normalize_extension( .Tres )")
	expect_equal(FilesManifest.normalize_extension("tres.json"), ".tres.json", "normalize_extension(tres.json)")
	expect_equal(FilesManifest.normalize_extension(""), "", "normalize_extension() of an empty string")

	var manifest := FilesManifest.new()
	manifest.extensions = PackedStringArray(["png", ".PNG", "", "*.webp"])
	expect_equal(manifest.extensions, PackedStringArray([".png", ".webp"]), "extensions are normalized and deduplicated")
	expect(manifest.matches_extension("res://x/B.PNG"), "matches_extension() ignores case")
	expect(not manifest.matches_extension("res://x/b.png.txt"), "matches_extension() checks the end of the name")
	manifest.extensions = PackedStringArray()
	expect(manifest.matches_extension("res://x/b.anything"), "no extensions match every file")


func _check_manifest_file() -> void:
	var manifest := _load_manifest_file()
	expect(manifest != null, "manifest.tres loads as FilesManifest")
	if manifest == null:
		return
	expect_equal(manifest.extensions, PackedStringArray([".tres", ".svg"]), "manifest.tres has two extensions")
	expect_equal(manifest.folder_paths.size(), 2, "manifest.tres has two folders")

	var expected := PackedStringArray([_data("a/curve.tres"), _data("a/icon.svg"), _data("b/gradient.tres"), _data("b/sub/deep.tres")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "get_manifest_paths(): both folders, both extensions, recursive, info.json left out")

	var loaded := ResourceLoader.load_manifest(manifest)
	expect_equal(loaded.size(), 4, "load_manifest() loads every matching file")
	expect(loaded.get(_data("a/curve.tres")) is Curve, "a/curve.tres is a Curve")
	expect(loaded.get(_data("a/icon.svg")) is Texture2D, "a/icon.svg (imported) is a Texture2D")
	expect(loaded.get(_data("b/gradient.tres")) is Gradient, "b/gradient.tres is a Gradient")
	expect(loaded.get(_data("b/sub/deep.tres")) is Curve, "b/sub/deep.tres is a Curve")
	expect(not loaded.has(_data("b/info.json")), "b/info.json is not loaded")

	# Saved and loaded again, the manifest keeps all properties.
	manifest.type_hint = &"Curve"
	manifest.recursive = false
	manifest.file_paths = PackedStringArray([_data("b/sub/deep.tres")])
	manifest.create_non_existing_folders = true
	var copy_path := ROOT + "copy.tres"
	expect_equal(ResourceSaver.save(manifest, copy_path), OK, "the manifest saves as .tres")
	var copy := ResourceLoader.load(copy_path, "", ResourceLoader.CACHE_MODE_IGNORE) as FilesManifest
	expect(copy != null, "the saved manifest loads")
	if copy != null:
		expect_equal(copy.type_hint, &"Curve", "type_hint survives saving")
		expect_equal(copy.extensions, manifest.extensions, "extensions survive saving")
		expect_equal(copy.folder_paths, manifest.folder_paths, "folder_paths survive saving")
		expect_equal(copy.file_paths, manifest.file_paths, "file_paths survive saving")
		expect_equal(copy.recursive, false, "recursive survives saving")
		expect_equal(copy.create_non_existing_folders, true, "create_non_existing_folders survives saving")


func _check_filters() -> void:
	var manifest := _load_manifest_file()
	manifest.recursive = false
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/curve.tres"), _data("a/icon.svg"), _data("b/gradient.tres")]), "recursive = false skips b/sub")

	manifest.recursive = true
	manifest.type_hint = &"Curve"
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/curve.tres"), _data("b/sub/deep.tres")]), "type_hint Curve keeps only curves")
	var curves := ResourceLoader.load_manifest(manifest)
	expect_equal(curves.size(), 2, "load_manifest() with type_hint Curve loads two curves")

	manifest.type_hint = &"Texture2D"
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/icon.svg")]), "type_hint Texture2D keeps the imported texture")

	manifest.type_hint = &""
	manifest.extensions = PackedStringArray()
	var all := ResourceLoader.get_manifest_paths(manifest)
	expect(_data("b/info.json") in all, "no extensions: every recognized file, also b/info.json")
	expect_equal(all.size(), 5, "no extensions: five files")


func _check_single_files() -> void:
	var manifest := FilesManifest.new()
	manifest.extensions = PackedStringArray(["svg"])
	manifest.folder_paths = PackedStringArray([_data("a")])
	manifest.file_paths = PackedStringArray([_data("b/info.json"), _data("a/icon.svg"), _data("b/sub/../gradient.tres")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/icon.svg"), _data("b/info.json"), _data("b/gradient.tres")]), "single files are added once, normalized, regardless of the extensions")

	manifest.file_paths = PackedStringArray([_data("missing.tres"), _data("a/curve.tres")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/icon.svg"), _data("a/curve.tres")]), "a missing single file is skipped with an error")

	var relative := FilesManifest.new()
	relative.folder_paths = PackedStringArray(["07-feature-files-manifest/resources/data/a/"])
	relative.extensions = PackedStringArray(["tres"])
	expect_equal(ResourceLoader.get_manifest_paths(relative), PackedStringArray([_data("a/curve.tres")]), "relative folders are relative to res://")


func _check_missing_folders() -> void:
	var missing := ROOT + "missing/deeper"
	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([missing, _data("a")])
	manifest.extensions = PackedStringArray(["tres"])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/curve.tres")]), "a missing folder is skipped with an error")
	expect(not DirAccess.dir_exists_absolute(missing), "the missing folder is not created by default")

	manifest.create_non_existing_folders = true
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/curve.tres")]), "with create_non_existing_folders the other folders are still listed")
	expect(DirAccess.dir_exists_absolute(missing), "create_non_existing_folders creates the folder in user://")
	expect_equal(ResourceLoader.load_manifest(manifest).size(), 1, "the new folder is empty")

	if OS.has_feature("template"):
		# Exported build: res:// is read-only and cannot get new folders.
		var res_missing := FEATURE + "not_exported_folder"
		var res_manifest := FilesManifest.new()
		res_manifest.folder_paths = PackedStringArray([res_missing])
		res_manifest.create_non_existing_folders = true
		expect_equal(ResourceLoader.get_manifest_paths(res_manifest).size(), 0, "exported: a missing res:// folder gives no paths")
		expect(not DirAccess.dir_exists_absolute(res_missing), "exported: res:// folders are not created")


func _check_prefixes() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT + "prefixed/data")
	for i in 3:
		ResourceSaver.save(Curve.new(), ROOT + "prefixed/data/c%d.tres" % i)
	expect_equal(PathPrefixServer.register_prefix_folder(PREFIX, ROOT + "prefixed"), OK, "register_prefix_folder(%s)" % PREFIX)

	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([PREFIX + "data"])
	var expected := PackedStringArray([PREFIX + "data/c0.tres", PREFIX + "data/c1.tres", PREFIX + "data/c2.tres"])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "folders with a custom prefix")
	var loaded := ResourceLoader.load_manifest(manifest, ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(loaded.size(), 3, "load_manifest() with a custom prefix")
	expect(loaded.get(PREFIX + "data/c1.tres") is Curve, "the prefixed path is the key")

	# user:// itself works too.
	var user_manifest := FilesManifest.new()
	user_manifest.folder_paths = PackedStringArray([ROOT + "prefixed/data/"])
	expect_equal(ResourceLoader.get_manifest_paths(user_manifest).size(), 3, "user:// folders (trailing slash is accepted)")

	if PathPrefixServer.has_prefix("exec://") and PathPrefixServer.is_prefix_available("exec://"):
		var exec_manifest := FilesManifest.new()
		exec_manifest.folder_paths = PackedStringArray(["exec://"])
		exec_manifest.recursive = false
		exec_manifest.extensions = PackedStringArray(["fm07_no_such_extension"])
		expect_equal(ResourceLoader.get_manifest_paths(exec_manifest).size(), 0, "exec:// folders resolve (no matching files)")

	PathPrefixServer.unregister_prefix(PREFIX)


func _check_threaded() -> void:
	var manifest := _load_manifest_file()
	var paths := ResourceLoader.get_manifest_paths(manifest)
	expect_equal(ResourceLoader.load_threaded_request_manifest(manifest, false, ResourceLoader.CACHE_MODE_IGNORE), OK, "load_threaded_request_manifest() starts every request")

	var known := true
	for path in paths:
		var status := ResourceLoader.load_threaded_get_status(path)
		if status == ResourceLoader.THREAD_LOAD_INVALID_RESOURCE or status == ResourceLoader.THREAD_LOAD_FAILED:
			known = false
	expect(known, "load_threaded_get_status() knows every path right after the request")

	var start := Time.get_ticks_msec()
	var pending := paths.duplicate()
	var progress_seen := true
	while not pending.is_empty() and Time.get_ticks_msec() - start < THREAD_TIMEOUT_MSEC:
		for path in pending.duplicate():
			var progress := []
			var status := ResourceLoader.load_threaded_get_status(path, progress)
			if progress.size() != 1:
				progress_seen = false
			if status != ResourceLoader.THREAD_LOAD_IN_PROGRESS:
				pending.remove_at(pending.find(path))
		await get_tree().process_frame
	expect(pending.is_empty(), "every threaded request finishes")
	expect(progress_seen, "load_threaded_get_status() reports progress for every path")

	var valid := 0
	for path in paths:
		if ResourceLoader.load_threaded_get(path) != null:
			valid += 1
	expect_equal(valid, paths.size(), "load_threaded_get() returns every resource")


func _check_null() -> void:
	expect_equal(ResourceLoader.get_manifest_paths(null), PackedStringArray(), "get_manifest_paths(null) is empty")
	expect_equal(ResourceLoader.load_manifest(null), {}, "load_manifest(null) is empty")
	expect_equal(ResourceLoader.load_threaded_request_manifest(null), ERR_INVALID_PARAMETER, "load_threaded_request_manifest(null) fails")


func _check_performance() -> void:
	var folder := ROOT + "perf"
	DirAccess.make_dir_recursive_absolute(folder)
	var curve := Curve.new()
	for i in PERF_COUNT:
		ResourceSaver.save(curve, folder.path_join("c%04d.tres" % i))

	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([folder])
	manifest.extensions = PackedStringArray(["tres"])

	var t0 := Time.get_ticks_usec()
	var paths := ResourceLoader.get_manifest_paths(manifest)
	var t1 := Time.get_ticks_usec()
	var loaded := ResourceLoader.load_manifest(manifest, ResourceLoader.CACHE_MODE_IGNORE)
	var t2 := Time.get_ticks_usec()
	expect_equal(paths.size(), PERF_COUNT, "%d files listed" % PERF_COUNT)
	expect_equal(loaded.size(), PERF_COUNT, "%d files loaded blocking" % PERF_COUNT)

	ResourceLoader.load_threaded_request_manifest(manifest, false, ResourceLoader.CACHE_MODE_IGNORE)
	var done := 0
	for path in paths:
		if ResourceLoader.load_threaded_get(path) != null:
			done += 1
	var t3 := Time.get_ticks_usec()
	expect_equal(done, PERF_COUNT, "%d files loaded threaded" % PERF_COUNT)
	print("  INFO  %d files: list %.1f ms, load_manifest %.1f ms, threaded (one request per file) %.1f ms" % [PERF_COUNT, (t1 - t0) / 1000.0, (t2 - t1) / 1000.0, (t3 - t2) / 1000.0])
	await get_tree().process_frame


func _cleanup() -> void:
	if PathPrefixServer.has_prefix(PREFIX):
		PathPrefixServer.unregister_prefix(PREFIX)
	_remove_recursive(ProjectSettings.globalize_path(ROOT))


func _remove_recursive(absolute: String) -> void:
	var dir := DirAccess.open(absolute)
	if dir == null:
		return
	for sub in dir.get_directories():
		_remove_recursive(absolute.path_join(sub))
	for file in dir.get_files():
		dir.remove(file)
	DirAccess.remove_absolute(absolute)
