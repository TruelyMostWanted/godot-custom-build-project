extends FeatureTest
## Full API test of File Manifests (FilesManifest, FilesManifestServer, FilesManifestValidation, ResourceLoader manifest calls).
## Same tests as scripts/cs/Test.cs.

const FEATURE := "res://file-manifests/resources/"
const DATA := FEATURE + "data/"
const ROOT := "user://file_manifests_test/"
const NAME := "fm_test"
const PREFIX := "fmtest://"
const THREAD_TIMEOUT_MSEC := 20000
const WATCH_TIMEOUT_MSEC := 3000

var _added: Array[String] = []
var _removed: Array[String] = []
var _changed: Array[String] = []


func _run_tests() -> void:
	_remove_folder(ProjectSettings.globalize_path(ROOT))
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_defaults()
	_test_extensions()
	_test_sample_manifest()
	_test_save_and_load()
	_test_filters()
	_test_single_files()
	_test_missing_folders()
	_test_prefixes()
	_test_order()
	_test_wildcards()
	await _test_threaded()
	_test_null()
	_test_server_edit()
	_test_server_save()
	_test_server_validate()
	await _test_server_watch()
	_remove_folder(ProjectSettings.globalize_path(ROOT))


func _data(relative: String) -> String:
	return DATA + relative


func _sample() -> FilesManifest:
	return ResourceLoader.load(FEATURE + "manifest.tres", "", ResourceLoader.CACHE_MODE_IGNORE)


func _write_curves(paths: Array[String]) -> void:
	for path: String in paths:
		DirAccess.make_dir_recursive_absolute(path.get_base_dir())
		ResourceSaver.save(Curve.new(), path)


func _test_defaults() -> void:
	var manifest := FilesManifest.new()
	expect_equal(manifest.type_hint, &"", "type_hint is empty by default")
	expect_equal(manifest.extensions, PackedStringArray(), "extensions are empty by default")
	expect_equal(manifest.folder_paths, PackedStringArray(), "folder_paths are empty by default")
	expect_equal(manifest.file_paths, PackedStringArray(), "file_paths are empty by default")
	expect(manifest.recursive, "recursive is true by default")
	expect(not manifest.create_missing_folders, "create_missing_folders is false by default")


func _test_extensions() -> void:
	expect_equal(FilesManifest.normalize_extension("png"), ".png", "normalize_extension adds the dot")
	expect_equal(FilesManifest.normalize_extension("*.PNG"), ".png", "normalize_extension removes * and lowers the case")
	expect_equal(FilesManifest.normalize_extension(" .Tres "), ".tres", "normalize_extension trims spaces")
	expect_equal(FilesManifest.normalize_extension("tres.json"), ".tres.json", "normalize_extension keeps a double extension")
	expect_equal(FilesManifest.normalize_extension(""), "", "normalize_extension of an empty string")
	var manifest := FilesManifest.new()
	manifest.extensions = PackedStringArray(["png", ".PNG", "", "*.webp"])
	expect_equal(manifest.extensions, PackedStringArray([".png", ".webp"]), "extensions are normalized and deduplicated")
	expect(manifest.matches_extension("res://x/B.PNG"), "matches_extension ignores case")
	expect(not manifest.matches_extension("res://x/b.png.txt"), "matches_extension checks the end of the name")
	manifest.extensions = PackedStringArray()
	expect(manifest.matches_extension("res://x/b.anything"), "no extensions match every file")


func _test_sample_manifest() -> void:
	var manifest: FilesManifest = _sample()
	expect_equal(manifest.extensions, PackedStringArray([".tres", ".svg"]), "manifest.tres has two extensions")
	var expected := PackedStringArray([_data("a/curve.tres"), _data("a/icon.svg"), _data("b/gradient.tres"), _data("b/sub/deep.tres")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "get_manifest_paths: both folders, recursive, info.json left out")
	var loaded: Dictionary = ResourceLoader.load_manifest(manifest)
	expect_equal(loaded.size(), 4, "load_manifest loads every matching file")
	expect(loaded[_data("a/curve.tres")] is Curve, "a/curve.tres is a Curve")
	expect(loaded[_data("a/icon.svg")] is Texture2D, "a/icon.svg (imported) is a Texture2D")
	expect(loaded[_data("b/gradient.tres")] is Gradient, "b/gradient.tres is a Gradient")
	expect(not loaded.has(_data("b/info.json")), "b/info.json is not loaded")
	var uncached: Dictionary = ResourceLoader.load_manifest(manifest, ResourceLoader.CACHE_MODE_IGNORE)
	expect(uncached[_data("a/curve.tres")] != loaded[_data("a/curve.tres")], "load_manifest takes the cache mode")


func _test_save_and_load() -> void:
	var manifest: FilesManifest = _sample()
	manifest.type_hint = &"Curve"
	manifest.recursive = false
	manifest.file_paths = PackedStringArray([_data("b/sub/deep.tres")])
	manifest.create_missing_folders = true
	var path: String = ROOT + "copy.tres"
	expect_equal(ResourceSaver.save(manifest, path), OK, "the manifest saves as .tres")
	var copy: FilesManifest = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(copy.type_hint, &"Curve", "type_hint survives saving")
	expect_equal(copy.extensions, manifest.extensions, "extensions survive saving")
	expect_equal(copy.folder_paths, manifest.folder_paths, "folder_paths survive saving")
	expect_equal(copy.file_paths, manifest.file_paths, "file_paths survive saving")
	expect(not copy.recursive, "recursive survives saving")
	expect(copy.create_missing_folders, "create_missing_folders survives saving")


func _test_filters() -> void:
	var manifest: FilesManifest = _sample()
	manifest.recursive = false
	var flat := PackedStringArray([_data("a/curve.tres"), _data("a/icon.svg"), _data("b/gradient.tres")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), flat, "recursive = false skips b/sub")
	manifest.recursive = true
	manifest.type_hint = &"Curve"
	var curves := PackedStringArray([_data("a/curve.tres"), _data("b/sub/deep.tres")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), curves, "type_hint Curve keeps only curves")
	manifest.type_hint = &"Texture2D"
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/icon.svg")]), "type_hint Texture2D keeps the imported texture")
	manifest.type_hint = &""
	manifest.extensions = PackedStringArray()
	var all: PackedStringArray = ResourceLoader.get_manifest_paths(manifest)
	expect(all.has(_data("b/info.json")), "no extensions: every recognized file, also b/info.json")
	expect_equal(all.size(), 5, "no extensions: five files")


func _test_single_files() -> void:
	var manifest := FilesManifest.new()
	manifest.extensions = PackedStringArray(["svg"])
	manifest.folder_paths = PackedStringArray([_data("a")])
	manifest.file_paths = PackedStringArray([_data("b/info.json"), _data("a/icon.svg"), _data("b/sub/../gradient.tres")])
	var expected := PackedStringArray([_data("a/icon.svg"), _data("b/info.json"), _data("b/gradient.tres")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "single files are added once, normalized, regardless of the extensions")
	manifest.file_paths = PackedStringArray([_data("missing.tres"), _data("a/curve.tres")])
	expected = PackedStringArray([_data("a/icon.svg"), _data("a/curve.tres")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "a missing single file is skipped")
	var relative := FilesManifest.new()
	relative.folder_paths = PackedStringArray(["file-manifests/resources/data/a/"])
	relative.extensions = PackedStringArray(["tres"])
	expect_equal(ResourceLoader.get_manifest_paths(relative), PackedStringArray([_data("a/curve.tres")]), "relative folders are relative to res://")


func _test_missing_folders() -> void:
	var missing: String = ROOT + "missing/deeper"
	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([missing, _data("a")])
	manifest.extensions = PackedStringArray(["tres"])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/curve.tres")]), "a missing folder is skipped")
	expect(not DirAccess.dir_exists_absolute(missing), "the missing folder is not created by default")
	manifest.create_missing_folders = true
	expect_equal(ResourceLoader.get_manifest_paths(manifest), PackedStringArray([_data("a/curve.tres")]), "with create_missing_folders the other folders are still listed")
	expect(DirAccess.dir_exists_absolute(missing), "create_missing_folders creates the folder in user://")


func _test_prefixes() -> void:
	_write_curves([ROOT + "prefixed/data/c0.tres", ROOT + "prefixed/data/c1.tres"])
	PathPrefixServer.register_prefix_folder(PREFIX, ROOT + "prefixed")
	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([PREFIX + "data"])
	var expected := PackedStringArray([PREFIX + "data/c0.tres", PREFIX + "data/c1.tres"])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "folders with a custom prefix")
	var loaded: Dictionary = ResourceLoader.load_manifest(manifest)
	expect(loaded[PREFIX + "data/c1.tres"] is Curve, "the prefixed path is the key")
	manifest.folder_paths = PackedStringArray([ROOT + "prefixed/data/"])
	expect_equal(ResourceLoader.get_manifest_paths(manifest).size(), 2, "user:// folders work, a trailing slash is accepted")
	PathPrefixServer.unregister_prefix(PREFIX)


func _test_order() -> void:
	var manifest := FilesManifest.new()
	manifest.extensions = PackedStringArray(["tres", "svg"])
	manifest.folder_paths = PackedStringArray([_data("b"), _data("a")])
	manifest.file_paths = PackedStringArray([_data("b/info.json"), _data("a/curve.tres")])
	var expected := PackedStringArray([_data("b/gradient.tres"), _data("b/sub/deep.tres"), _data("a/curve.tres"), _data("a/icon.svg"), _data("b/info.json")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "folders in list order, then files in list order, duplicates keep their first position")
	expect_equal(PackedStringArray(ResourceLoader.load_manifest(manifest).keys()), expected, "load_manifest keeps the load order")
	manifest.folder_paths = PackedStringArray([_data("a"), _data("b")])
	expect_equal(ResourceLoader.get_manifest_paths(manifest)[0], _data("a/curve.tres"), "reordering folder_paths changes the load order")
	_write_curves([ROOT + "order/z.tres", ROOT + "order/m/x.tres"])
	var sorted := FilesManifest.new()
	sorted.folder_paths = PackedStringArray([ROOT + "order"])
	expect_equal(ResourceLoader.get_manifest_paths(sorted), PackedStringArray([ROOT + "order/m/x.tres", ROOT + "order/z.tres"]), "files of a folder are sorted by path, subfolders included")


func _test_wildcards() -> void:
	_write_curves([ROOT + "mods/b/tr/x.tres", ROOT + "mods/a/tr/y.tres", ROOT + "mods/a_extra/other/z.tres", ROOT + "mods/c/tr/sub/w.tres"])
	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([ROOT + "mods/*/tr"])
	var expected := PackedStringArray([ROOT + "mods/a/tr/y.tres", ROOT + "mods/b/tr/x.tres", ROOT + "mods/c/tr/sub/w.tres"])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "mods/*/tr: one level, sorted by name, mods without tr skipped")
	manifest.folder_paths = PackedStringArray([ROOT + "mods/a*"])
	expected = PackedStringArray([ROOT + "mods/a/tr/y.tres", ROOT + "mods/a_extra/other/z.tres"])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "a pattern inside a segment (a*)")
	manifest.folder_paths = PackedStringArray([ROOT + "mods/*/nothing"])
	expect_equal(ResourceLoader.get_manifest_paths(manifest).size(), 0, "no match is not an error")
	PathPrefixServer.register_prefix_folder(PREFIX, ROOT + "mods")
	manifest.folder_paths = PackedStringArray([PREFIX + "*/tr"])
	expected = PackedStringArray([PREFIX + "a/tr/y.tres", PREFIX + "b/tr/x.tres", PREFIX + "c/tr/sub/w.tres"])
	expect_equal(ResourceLoader.get_manifest_paths(manifest), expected, "wildcards behind a custom prefix")
	PathPrefixServer.unregister_prefix(PREFIX)


func _test_threaded() -> void:
	var manifest: FilesManifest = _sample()
	var paths: PackedStringArray = ResourceLoader.get_manifest_paths(manifest)
	expect_equal(ResourceLoader.load_threaded_request_manifest(manifest, false, ResourceLoader.CACHE_MODE_IGNORE), OK, "load_threaded_request_manifest starts every request")
	var pending: Array = Array(paths)
	var start: int = Time.get_ticks_msec()
	while not pending.is_empty() and Time.get_ticks_msec() - start < THREAD_TIMEOUT_MSEC:
		for path: String in pending.duplicate():
			var progress: Array = []
			var status: ResourceLoader.ThreadLoadStatus = ResourceLoader.load_threaded_get_status(path, progress)
			if status != ResourceLoader.THREAD_LOAD_IN_PROGRESS:
				pending.erase(path)
		await get_tree().process_frame
	expect(pending.is_empty(), "every threaded request finishes")
	var loaded: int = 0
	for path: String in paths:
		if ResourceLoader.load_threaded_get(path) != null:
			loaded += 1
	expect_equal(loaded, paths.size(), "load_threaded_get returns every resource")


func _test_null() -> void:
	expect_equal(ResourceLoader.get_manifest_paths(null), PackedStringArray(), "get_manifest_paths(null) is empty")
	expect_equal(ResourceLoader.load_manifest(null), {}, "load_manifest(null) is empty")
	expect_equal(ResourceLoader.load_threaded_request_manifest(null), ERR_INVALID_PARAMETER, "load_threaded_request_manifest(null) fails")


func _test_server_edit() -> void:
	FilesManifestServer.manifest_changed.connect(_on_manifest_changed)
	expect_equal(FilesManifestServer.register_manifest(NAME, FilesManifest.new()), OK, "register_manifest")
	expect_equal(_changed, [NAME], "manifest_changed is emitted when a manifest is registered")
	expect(FilesManifestServer.has_manifest(NAME), "has_manifest finds the manifest")
	expect(FilesManifestServer.get_manifest_names().has(NAME), "get_manifest_names lists the manifest")
	var manifest: FilesManifest = FilesManifestServer.get_manifest(NAME)
	expect(manifest != null, "get_manifest returns the manifest")
	expect_equal(FilesManifestServer.add_folder(NAME, _data("a")), OK, "add_folder")
	expect_equal(FilesManifestServer.add_folder(NAME, _data("a")), ERR_ALREADY_EXISTS, "add_folder twice: ERR_ALREADY_EXISTS")
	expect_equal(FilesManifestServer.add_extension(NAME, "TRES"), OK, "add_extension")
	expect_equal(FilesManifestServer.add_extension(NAME, ".tres"), ERR_ALREADY_EXISTS, "add_extension twice: ERR_ALREADY_EXISTS")
	expect_equal(FilesManifestServer.add_file(NAME, _data("b/gradient.tres")), OK, "add_file")
	expect_equal(FilesManifestServer.add_file(NAME, _data("b/gradient.tres")), ERR_ALREADY_EXISTS, "add_file twice: ERR_ALREADY_EXISTS")
	expect_equal(manifest.extensions, PackedStringArray([".tres"]), "the edits change the manifest")
	var expected := PackedStringArray([_data("a/curve.tres"), _data("b/gradient.tres")])
	expect_equal(FilesManifestServer.get_file_paths(NAME), expected, "get_file_paths lists folders, then files")
	expect(_changed.size() > 1, "manifest_changed is emitted for every edit")
	FilesManifestServer.manifest_changed.disconnect(_on_manifest_changed)
	expect_equal(FilesManifestServer.remove_file(NAME, _data("b/gradient.tres")), OK, "remove_file")
	expect_equal(FilesManifestServer.remove_file(NAME, _data("b/gradient.tres")), ERR_DOES_NOT_EXIST, "remove_file of a missing file fails")
	expect_equal(FilesManifestServer.remove_extension(NAME, "tres"), OK, "remove_extension")
	expect_equal(FilesManifestServer.remove_extension(NAME, "tres"), ERR_DOES_NOT_EXIST, "remove_extension of a missing extension fails")
	expect_equal(FilesManifestServer.remove_folder(NAME, _data("a")), OK, "remove_folder")
	expect_equal(FilesManifestServer.remove_folder(NAME, _data("a")), ERR_DOES_NOT_EXIST, "remove_folder of a missing folder fails")
	expect(FilesManifestServer.get_file_paths(NAME).is_empty(), "the manifest is empty again")
	expect(FilesManifestServer.get_manifest("fm_unknown") == null, "get_manifest of an unknown name is null")
	expect(not FilesManifestServer.has_manifest("fm_unknown"), "has_manifest of an unknown name is false")


func _test_server_save() -> void:
	FilesManifestServer.add_folder(NAME, _data("a"))
	FilesManifestServer.add_extension(NAME, "tres")
	var path: String = ROOT + "server.tres"
	expect_equal(FilesManifestServer.save_manifest(NAME, path), OK, "save_manifest with a path")
	var loaded: FilesManifest = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(loaded.folder_paths.size(), 1, "the saved manifest has the folder")
	FilesManifestServer.add_extension(NAME, "svg")
	expect_equal(FilesManifestServer.save_manifest(NAME), OK, "save_manifest without a path writes back to the last file")
	expect_equal(FilesManifestServer.unregister_manifest(NAME), OK, "unregister_manifest")
	expect(not FilesManifestServer.has_manifest(NAME), "the manifest is gone")
	expect_equal(FilesManifestServer.unregister_manifest(NAME), ERR_DOES_NOT_EXIST, "unregister_manifest of an unknown name fails")
	expect_equal(FilesManifestServer.register_manifest_file(NAME, path), OK, "register_manifest_file")
	var from_file: FilesManifest = FilesManifestServer.get_manifest(NAME)
	expect_equal(from_file.extensions, PackedStringArray([".tres", ".svg"]), "register_manifest_file loads the manifest")
	FilesManifestServer.unregister_manifest(NAME)


func _test_server_validate() -> void:
	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([_data("a"), ROOT + "absent/folder", ROOT + "absent/*/tr", ROOT + "mods/*/tr"])
	manifest.file_paths = PackedStringArray([_data("missing.tres")])
	manifest.extensions = PackedStringArray(["tres", "nosuchext"])
	FilesManifestServer.register_manifest(NAME, manifest)
	var result: FilesManifestValidation = FilesManifestServer.validate(NAME)
	expect(not result.valid, "validate: the manifest is not valid")
	expect_equal(result.error, ERR_FILE_NOT_FOUND, "validate: the error is ERR_FILE_NOT_FOUND")
	expect_equal(result.missing_folders, PackedStringArray([ROOT + "absent/folder", ROOT + "absent/*/tr"]), "validate: a wildcard folder is missing only if its base is")
	expect_equal(result.missing_files, PackedStringArray([_data("missing.tres")]), "validate: missing_files")
	expect_equal(result.unknown_extensions, PackedStringArray([".nosuchext"]), "validate: unknown_extensions")
	expect_equal(FilesManifestServer.create_missing_folders(NAME), OK, "create_missing_folders")
	expect(DirAccess.dir_exists_absolute(ROOT + "absent/folder"), "create_missing_folders creates a folder")
	expect(DirAccess.dir_exists_absolute(ROOT + "absent"), "create_missing_folders creates the base of a wildcard folder")
	FilesManifestServer.remove_file(NAME, _data("missing.tres"))
	FilesManifestServer.remove_extension(NAME, "nosuchext")
	expect(FilesManifestServer.validate(NAME).valid, "validate: the manifest is valid after the fixes")
	FilesManifestServer.unregister_manifest(NAME)


func _test_server_watch() -> void:
	var watched: String = ROOT + "watched/"
	_write_curves([watched + "a.tres"])
	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([watched])
	FilesManifestServer.register_manifest(NAME, manifest)
	FilesManifestServer.file_added.connect(_on_file_added)
	FilesManifestServer.file_removed.connect(_on_file_removed)
	FilesManifestServer.watch_interval = 0.1
	expect_equal(FilesManifestServer.watch_interval, 0.1, "watch_interval is set")
	expect(not FilesManifestServer.is_watching(NAME), "is_watching is false at first")
	expect_equal(FilesManifestServer.set_watching(NAME, true), OK, "set_watching")
	expect(FilesManifestServer.is_watching(NAME), "is_watching is true")
	_write_curves([watched + "b.tres"])
	var start: int = Time.get_ticks_msec()
	while _added.is_empty() and Time.get_ticks_msec() - start < WATCH_TIMEOUT_MSEC:
		await get_tree().process_frame
	expect_equal(_added, [watched + "b.tres"], "file_added is emitted by the automatic poll")
	DirAccess.remove_absolute(watched + "b.tres")
	FilesManifestServer.poll()
	expect_equal(_removed, [watched + "b.tres"], "poll emits file_removed")
	_write_curves([watched + "c.tres"])
	expect_equal(FilesManifestServer.refresh(NAME), OK, "refresh")
	expect_equal(_added, [watched + "b.tres", watched + "c.tres"], "refresh reports the differences of a watched manifest")
	expect_equal(FilesManifestServer.get_file_paths(NAME).size(), 2, "refresh updates the cached file list")
	expect_equal(FilesManifestServer.set_watching(NAME, false), OK, "set_watching(false)")
	expect(not FilesManifestServer.is_watching(NAME), "is_watching is false again")
	FilesManifestServer.file_added.disconnect(_on_file_added)
	FilesManifestServer.file_removed.disconnect(_on_file_removed)
	FilesManifestServer.unregister_manifest(NAME)


func _on_manifest_changed(manifest_name: String) -> void:
	_changed.append(manifest_name)


func _on_file_added(manifest_name: String, path: String) -> void:
	_added.append(path)


func _on_file_removed(manifest_name: String, path: String) -> void:
	_removed.append(path)


func _remove_folder(absolute: String) -> void:
	var dir: DirAccess = DirAccess.open(absolute)
	if dir == null:
		return
	for sub: String in dir.get_directories():
		_remove_folder(absolute.path_join(sub))
	for file: String in dir.get_files():
		dir.remove(file)
	DirAccess.remove_absolute(absolute)
