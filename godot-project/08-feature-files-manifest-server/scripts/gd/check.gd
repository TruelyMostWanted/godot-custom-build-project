extends FeatureCheck
## GDScript checks of 08-feature-files-manifest-server. Check the same things as scripts/cs/Issue08FeatureFilesManifestServerCheck.cs.

const ROOT := "user://08-feature-files-manifest-server/gd/"
const NAME := "fms08gd"

var _added: Array[String] = []
var _removed: Array[String] = []


func _get_check_name() -> String:
	return "08-feature-files-manifest-server"


func _write(path: String) -> void:
	var f := FileAccess.open(path, FileAccess.WRITE)
	f.store_string("[gd_resource type=\"Resource\" format=3]\n\n[resource]\n")
	f.close()


func _wait_for(list: Array[String], seconds: float) -> bool:
	var end := Time.get_ticks_msec() + int(seconds * 1000.0)
	while list.is_empty() and Time.get_ticks_msec() < end:
		await get_tree().process_frame
	return not list.is_empty()


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT + "mods")
	_write(ROOT + "mods/a.tres")
	var server := FilesManifestServer
	expect(Engine.has_singleton("FilesManifestServer"), "FilesManifestServer singleton exists")

	# Register, edit, save, load again.
	expect_equal(server.register_manifest(NAME, FilesManifest.new()), OK, "register_manifest()")
	expect(server.get_manifest_names().has(NAME), "get_manifest_names() lists the manifest")
	expect_equal(server.add_folder(NAME, ROOT + "mods"), OK, "add_folder()")
	expect_equal(server.add_folder(NAME, ROOT + "mods"), ERR_ALREADY_EXISTS, "add_folder() twice: ERR_ALREADY_EXISTS")
	expect_equal(server.add_extension(NAME, "TRES"), OK, "add_extension()")
	expect_equal(server.get_paths(NAME).size(), 1, "get_paths(): one file")
	expect_equal(server.save_manifest(NAME, ROOT + "manifest.tres"), OK, "save_manifest()")
	var loaded := ResourceLoader.load(ROOT + "manifest.tres", "", ResourceLoader.CACHE_MODE_IGNORE) as FilesManifest
	expect(loaded != null and loaded.extensions == PackedStringArray([".tres"]) and loaded.folder_paths.size() == 1, "saved manifest has the changes")

	# Validate.
	server.add_folder(NAME, ROOT + "missing")
	server.add_extension(NAME, "nosuchext")
	var result: Dictionary = server.validate(NAME)
	expect(not result.valid and result.missing_folders.size() == 1, "validate() reports a missing folder")
	expect(result.unknown_extensions == PackedStringArray([".nosuchext"]), "validate() reports an unknown extension")
	expect_equal(server.create_missing_folders(NAME), OK, "create_missing_folders()")
	expect(DirAccess.dir_exists_absolute(ROOT + "missing"), "missing folder created")

	# Watch: signals within one second.
	server.file_added.connect(func(n: String, p: String): if n == NAME: _added.append(p))
	server.file_removed.connect(func(n: String, p: String): if n == NAME: _removed.append(p))
	server.watch_interval = 0.1
	expect_equal(server.set_watching(NAME, true), OK, "set_watching()")
	_write(ROOT + "mods/b.tres")
	expect(await _wait_for(_added, 1.0), "file_added within one second")
	DirAccess.remove_absolute(ROOT + "mods/b.tres")
	expect(await _wait_for(_removed, 1.0), "file_removed within one second")

	expect_equal(server.unregister_manifest(NAME), OK, "unregister_manifest()")
	expect(not server.has_manifest(NAME), "manifest is gone")
	for f in ["mods/a.tres", "manifest.tres"]:
		DirAccess.remove_absolute(ROOT + f)
	for d in ["mods", "missing", ""]:
		DirAccess.remove_absolute(ROOT + d)
