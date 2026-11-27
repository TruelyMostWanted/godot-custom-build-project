extends FeatureTest
## Full API test of PCK Packages (PCKPackageServer, PCKPackage, PCKPackageEntry, PCKExtractResult, PCKPackageExporter, PCKBuildPlan).
## Same tests as scripts/cs/Test.cs. The wrong-key and missing-source tests print engine errors on purpose.

const ROOT := "user://pck_packages_test/"
const RESOURCES := "res://pck-packages/resources/"
const KEY_A := "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff"
const KEY_B := "ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100"
const ZERO_KEY := "0000000000000000000000000000000000000000000000000000000000000000"
const MISSING_PRESET := "No Such Preset"
const PLAN_METHODS: Array[String] = [
	"export_description", "export_description_with_preset", "export_target_to_res_path", "find_preset",
	"get_export_target_paths", "get_preset_names", "is_building", "is_export_target", "is_exporting",
	"matches_encryption_filters", "plan_description", "should_encrypt",
]

var _mounted: Array[String] = []
var _unmounted: Array[String] = []


func _run_tests() -> void:
	_remove_folder(ProjectSettings.globalize_path(ROOT))
	DirAccess.make_dir_recursive_absolute(ROOT)
	PCKPackageServer.pack_mounted.connect(_on_pack_mounted)
	PCKPackageServer.pack_unmounted.connect(_on_pack_unmounted)
	_test_open_and_read()
	_test_edit_build_mount()
	_test_providing_pack_and_unmount()
	_test_encryption()
	_test_resource_loading()
	_test_description()
	_test_inspector_data()
	_test_sample_files()
	_test_extract()
	_test_exporter_bindings()
	_test_exporter_plan()
	_test_exporter_helpers()
	_remove_folder(ProjectSettings.globalize_path(ROOT))


func _write_pack(path: String, files: Dictionary, key: String = ZERO_KEY, encrypt: bool = false) -> void:
	var packer := PCKPacker.new()
	packer.pck_start(path, 32, key, encrypt)
	for target: String in files:
		var content: String = files[target]
		packer.add_file_from_buffer(target, content.to_utf8_buffer(), encrypt)
	packer.flush()


func _write_file(path: String, content: String) -> void:
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	file.store_string(content)
	file.close()


func _read(package: PCKPackage, path: String) -> String:
	return PCKPackageServer.read_file(package, path).get_string_from_utf8()


func _test_open_and_read() -> void:
	var base: String = ROOT + "base.pck"
	_write_pack(base, { "pck/a.txt": "Alpha", "pck/b.txt": "Beta" })
	var package: PCKPackage = PCKPackageServer.open(base)
	expect(package != null, "open returns a package")
	expect_equal(package.format_version, 4, "format_version is 4")
	expect(not package.engine_version.is_empty(), "engine_version is set")
	expect_equal(package.pack_path, base, "pack_path is the opened pack")
	expect_equal(package.alignment, 32, "alignment is read from the pack")
	expect(not package.encrypted_directory, "the directory is not encrypted")
	expect(not package.is_description(), "an opened pack is not a description")
	expect_equal(package.get_file_count(), 2, "get_file_count")
	expect_equal(package.get_file_paths(), PackedStringArray(["res://pck/a.txt", "res://pck/b.txt"]), "get_file_paths lists all files")
	expect(package.has_file("res://pck/a.txt"), "has_file finds a file")
	expect(not package.has_file("res://pck/none.txt"), "has_file is false for a missing file")
	var entries: Array = package.get_entries()
	var entry: PCKPackageEntry = entries[0]
	expect_equal(entry.path, "res://pck/a.txt", "PCKPackageEntry.path")
	expect_equal(entry.size, 5, "PCKPackageEntry.size")
	expect_equal(entry.md5, "Alpha".md5_text(), "PCKPackageEntry.md5")
	expect(entry.offset > 0, "PCKPackageEntry.offset")
	expect(not entry.removal, "PCKPackageEntry.removal is false")
	expect(not entry.encrypted, "PCKPackageEntry.encrypted is false")
	expect(not entry.delta, "PCKPackageEntry.delta is false")
	expect(not entry.pending, "PCKPackageEntry.pending is false for a file of the pack")
	expect_equal(entry.source_type, PCKPackage.SOURCE_PACK, "PCKPackageEntry.source_type is SOURCE_PACK")
	expect_equal(_read(package, "res://pck/a.txt"), "Alpha", "read_file without mounting")
	expect_equal(PCKPackageServer.verify(package), OK, "verify")
	expect(not PCKPackageServer.is_mounted(base), "open does not mount")
	expect(not PCKPackageServer.is_main_pack(base), "is_main_pack is false for an ordinary pack")
	expect(not FileAccess.file_exists("res://pck/a.txt"), "the file is not in res:// before mounting")
	expect(PCKPackageServer.open(ROOT + "missing.pck") == null, "open of a missing file returns null")
	expect_equal(PCKPackageServer.mount(ROOT + "missing.pck"), ERR_FILE_NOT_FOUND, "mount of a missing file returns ERR_FILE_NOT_FOUND")
	expect_equal(PCKPackageServer.get_mounted_packs().size(), 0, "no pack is mounted in the editor")


func _test_edit_build_mount() -> void:
	var package: PCKPackage = PCKPackageServer.open(ROOT + "base.pck")
	package.add_file_from_buffer("res://pck/c.txt", "Gamma".to_utf8_buffer())
	expect(package.get_entries()[2].pending, "an added file is pending")
	expect_equal(package.remove_file("res://pck/a.txt"), OK, "remove_file")
	expect_equal(package.rename_file("res://pck/b.txt", "res://pck/b2.txt"), OK, "rename_file")
	expect_equal(package.remove_file("res://pck/a.txt"), ERR_DOES_NOT_EXIST, "remove_file of a missing file fails")
	var edited: String = ROOT + "edited.pck"
	expect_equal(PCKPackageServer.build(package, edited), OK, "build")
	expect_equal(PCKPackageServer.mount(edited), OK, "mount")
	expect(PCKPackageServer.is_mounted(edited), "is_mounted")
	expect(PCKPackageServer.get_mounted_packs().has(edited), "get_mounted_packs lists the pack")
	expect_equal(_mounted, [edited], "pack_mounted is emitted")
	expect_equal(FileAccess.get_file_as_string("res://pck/c.txt"), "Gamma", "the added file is in res://")
	expect_equal(FileAccess.get_file_as_string("res://pck/b2.txt"), "Beta", "the renamed file is in res://")
	expect(not FileAccess.file_exists("res://pck/a.txt"), "the removed file is not in res://")
	expect_equal(PCKPackageServer.build(package, edited), ERR_BUSY, "build onto a mounted pack returns ERR_BUSY")
	var missing_source := PCKPackage.new()
	missing_source.add_file("res://pck/x.txt", ROOT + "no_such_source.txt")
	expect_equal(PCKPackageServer.build(missing_source, ROOT + "never.pck"), ERR_FILE_NOT_FOUND, "build with a missing source returns ERR_FILE_NOT_FOUND")
	expect(not FileAccess.file_exists(ROOT + "never.pck"), "build writes nothing when a source is missing")


func _test_providing_pack_and_unmount() -> void:
	var edited: String = ROOT + "edited.pck"
	var overlay: String = ROOT + "overlay.pck"
	_write_pack(overlay, { "pck/c.txt": "Overlay", "pck/only_overlay.txt": "Only" })
	expect(ProjectSettings.load_resource_pack(overlay), "load_resource_pack still works")
	expect(PCKPackageServer.is_mounted(overlay), "load_resource_pack goes through the server")
	expect_equal(PCKPackageServer.get_providing_pack("res://pck/c.txt"), overlay, "get_providing_pack names the later pack")
	expect_equal(PCKPackageServer.get_providing_pack("res://pck/b2.txt"), edited, "get_providing_pack names the only pack")
	expect_equal(PCKPackageServer.get_providing_pack("res://project.godot"), "", "get_providing_pack is empty for a project file")
	expect_equal(FileAccess.get_file_as_string("res://pck/c.txt"), "Overlay", "the later pack replaces the file")
	expect_equal(PCKPackageServer.unmount(overlay), OK, "unmount")
	expect_equal(_unmounted, [overlay], "pack_unmounted is emitted")
	expect(not PCKPackageServer.is_mounted(overlay), "is_mounted is false after unmount")
	expect(not FileAccess.file_exists("res://pck/only_overlay.txt"), "the file of the unmounted pack is gone")
	expect_equal(PCKPackageServer.get_providing_pack("res://pck/c.txt"), edited, "get_providing_pack names the remaining pack")
	expect_equal(FileAccess.get_file_as_string("res://pck/c.txt"), "Gamma", "the replaced file is back")
	expect_equal(PCKPackageServer.unmount(overlay), ERR_DOES_NOT_EXIST, "unmount twice returns ERR_DOES_NOT_EXIST")
	expect_equal(PCKPackageServer.unmount(edited), OK, "unmount the edited pack")
	expect(not FileAccess.file_exists("res://pck/c.txt"), "all files of the packs are gone")
	PCKPackageServer.pack_mounted.disconnect(_on_pack_mounted)
	PCKPackageServer.pack_unmounted.disconnect(_on_pack_unmounted)


func _test_encryption() -> void:
	var secret: String = ROOT + "secret.pck"
	_write_pack(secret, { "pck/secret.txt": "Hidden" }, KEY_A, true)
	var package: PCKPackage = PCKPackageServer.open(secret, KEY_A)
	expect(package.encrypted_directory, "open with the right key: the directory is encrypted")
	expect(package.get_entries()[0].encrypted, "PCKPackageEntry.encrypted is true")
	expect_equal(_read(package, "res://pck/secret.txt"), "Hidden", "read_file decrypts")
	expect(PCKPackageServer.open(secret, KEY_B) == null, "open with a wrong key returns null")
	var rebuilt: String = ROOT + "secret_b.pck"
	expect_equal(PCKPackageServer.build(package, rebuilt, KEY_B), OK, "build encrypts again with another key")
	var again: PCKPackage = PCKPackageServer.open(rebuilt, KEY_B)
	expect_equal(_read(again, "res://pck/secret.txt"), "Hidden", "the rebuilt pack opens with the new key")


# PCKPackage is a Resource: .pck files load and save through ResourceLoader and ResourceSaver.
func _test_resource_loading() -> void:
	var pack: String = ROOT + "resource.pck"
	_write_pack(pack, { "pck/a.txt": "Alpha", "pck/b.txt": "Beta" })
	expect(ClassDB.is_parent_class("PCKPackage", "Resource"), "PCKPackage is a Resource")
	expect(ResourceLoader.exists(pack, "PCKPackage"), "ResourceLoader knows .pck files")
	var loaded: PCKPackage = ResourceLoader.load(pack)
	expect_equal(loaded.get_pack_path(), pack, "get_pack_path is the loaded pack")
	expect(not loaded.is_description(), "a loaded .pck is not a description")
	expect(not PCKPackageServer.is_mounted(pack), "load does not mount")
	expect(ResourceLoader.load(pack) == loaded, "load twice returns the same object")
	expect_equal(_read(loaded, "res://pck/b.txt"), "Beta", "read_file on a loaded pack")
	_write_pack(pack, { "pck/a.txt": "Alpha 2", "pck/c.txt": "Gamma" })
	var replaced: PCKPackage = ResourceLoader.load(pack, "", ResourceLoader.CACHE_MODE_REPLACE)
	expect(replaced == loaded, "CACHE_MODE_REPLACE keeps the cached object")
	expect_equal(loaded.get_file_paths(), PackedStringArray(["res://pck/a.txt", "res://pck/c.txt"]), "CACHE_MODE_REPLACE shows the new entries")
	expect_equal(_read(loaded, "res://pck/a.txt"), "Alpha 2", "CACHE_MODE_REPLACE shows the new data")
	var copy: PCKPackage = loaded.duplicate()
	expect(copy != loaded, "duplicate returns another object")
	expect_equal(copy.get_file_paths(), loaded.get_file_paths(), "duplicate keeps the entries")
	expect_equal(_read(copy, "res://pck/c.txt"), "Gamma", "a duplicate can read the files")


func _test_description() -> void:
	var base: String = ROOT + "resource.pck"
	var source: String = ROOT + "source.txt"
	var later: String = ROOT + "later.txt"
	_write_file(source, "Source")
	DirAccess.remove_absolute(later)
	var description := PCKPackage.new()
	expect(description.is_description(), "PCKPackage.new is a description")
	expect_equal(description.set_key(KEY_A), OK, "set_key")
	description.base_pack = base
	description.alignment = 64
	expect_equal(description.add_file("res://pck/source.txt", source, true), OK, "add_file")
	expect_equal(description.add_file("res://pck/later.txt", later), OK, "add_file with a missing source")
	description.add_file_from_buffer("res://pck/data.txt", "Data".to_utf8_buffer())
	var files: Array = description.files
	files.append({ "path": "pck/from_base.txt", "from_pack": "pck/c.txt" })
	description.files = files
	description.add_removal("res://pck/old.txt")
	var entries: Array = description.get_entries()
	expect_equal(entries.size(), 5, "get_entries lists files and removals")
	expect_equal(entries[0].source_type, PCKPackage.SOURCE_FILE, "SOURCE_FILE")
	expect_equal(entries[0].source, source, "PCKPackageEntry.source of a file")
	expect(entries[0].encrypted, "PCKPackageEntry.encrypted for add_file(encrypt = true)")
	expect_equal(entries[2].source_type, PCKPackage.SOURCE_DATA, "SOURCE_DATA")
	expect_equal(entries[3].source_type, PCKPackage.SOURCE_PACK, "SOURCE_PACK")
	expect_equal(entries[3].source, "res://pck/c.txt", "PCKPackageEntry.source of a pack file")
	expect(entries[4].removal, "add_removal adds a removal entry")
	expect_equal(entries[4].path, "res://pck/old.txt", "the removal path")
	var tres: String = ROOT + "description.tres"
	expect_equal(ResourceSaver.save(description, tres), OK, "ResourceSaver saves a description as .tres")
	var text: String = FileAccess.get_file_as_string(tres)
	expect(not text.contains(KEY_A), "the key is not in the .tres")
	expect(text.contains("base_pack") and text.contains("from_pack") and text.contains("removals"), "the .tres has base_pack, files and removals")
	var reloaded: PCKPackage = ResourceLoader.load(tres, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(reloaded.is_description(), "the .tres loads as a description")
	expect_equal(reloaded.files, description.files, "files are identical after the round trip")
	expect_equal(reloaded.removals, PackedStringArray(["pck/old.txt"]), "removals are identical after the round trip")
	expect_equal(reloaded.base_pack, base, "base_pack after the round trip")
	expect_equal(reloaded.alignment, 64, "alignment after the round trip")
	expect_equal(_read(reloaded, "res://pck/from_base.txt"), "Gamma", "from_pack reads the base pack")
	var built: String = ROOT + "built.pck"
	expect_equal(ResourceSaver.save(reloaded, built), ERR_FILE_NOT_FOUND, "ResourceSaver saves .pck: a missing source fails")
	_write_file(later, "Later")
	expect_equal(ResourceSaver.save(reloaded, built), OK, "ResourceSaver saves .pck: the pack is built")
	var opened: PCKPackage = PCKPackageServer.open(built)
	var expected_paths := PackedStringArray(["res://pck/source.txt", "res://pck/later.txt", "res://pck/data.txt", "res://pck/from_base.txt"])
	expect_equal(opened.get_file_paths(), expected_paths, "the built pack has all files")
	expect(opened.has_file("res://pck/old.txt"), "the built pack has the removal entry")
	expect_equal(_read(opened, "res://pck/source.txt"), "Source", "a file source is built")
	expect_equal(PCKPackageServer.verify(opened), OK, "verify of the built pack")


# The Inspector edits the storage properties `files` and `removals` as snapshots.
func _test_inspector_data() -> void:
	_write_file(ROOT + "inspector_source.txt", "Alpha")
	var description := PCKPackage.new()
	description.set("files", [
		{ "path": "pck/a.txt", "source": ROOT + "inspector_source.txt" },
		{ "path": "pck/data.txt", "data": "Data".to_utf8_buffer() },
	])
	description.set("removals", PackedStringArray(["pck/old.txt"]))
	expect_equal(description.get_file_paths(), PackedStringArray(["res://pck/a.txt", "res://pck/data.txt"]), "files set as a property are listed in order")
	var before: Array = description.get("files").duplicate(true)
	var files: Array = description.get("files")
	var first: Dictionary = files[0]
	first["path"] = "pck/renamed/a.txt"
	description.set("files", files)
	expect(description.has_file("res://pck/renamed/a.txt"), "a rename through the snapshot")
	description.set("files", before)
	expect(description.has_file("res://pck/a.txt"), "undo: the old snapshot restores the entry")
	var pack: String = ROOT + "inspector.pck"
	expect_equal(PCKPackageServer.build(description, pack), OK, "Build Pack: the description is built")
	var opened: PCKPackage = PCKPackageServer.open(pack)
	var copy := PCKPackage.new()
	copy.base_pack = pack
	copy.alignment = opened.alignment
	copy.set("files", opened.get("files"))
	copy.set("removals", opened.get("removals"))
	var from_pack: Array = copy.get("files")
	expect_equal(from_pack.size(), 2, "Create Description: one entry per file")
	var all_from_pack: bool = from_pack.all(func(entry: Dictionary) -> bool: return entry.has("from_pack"))
	expect(all_from_pack, "Create Description: every file is a from_pack entry")
	var rebuilt: String = ROOT + "inspector_rebuilt.pck"
	expect_equal(PCKPackageServer.build(copy, rebuilt), OK, "the created description builds")
	expect_equal(_read(PCKPackageServer.open(rebuilt), "res://pck/data.txt"), "Data", "a from_pack file is copied from the base pack")


func _test_sample_files() -> void:
	var sample: PCKPackage = load(RESOURCES + "sample_pack.tres")
	expect(sample.is_description(), "sample_pack.tres is a description")
	expect_equal(sample.get_file_count(), 3, "sample_pack.tres has three files")
	var pack: PCKPackage = load(RESOURCES + "sample_pack.pck")
	expect(not pack.is_description(), "sample_pack.pck is a pack")
	expect_equal(pack.get_file_count(), 3, "sample_pack.pck has three files")
	expect_equal(PCKPackageServer.verify(pack), OK, "sample_pack.pck verifies")


func _test_extract() -> void:
	var pack: String = ROOT + "extract.pck"
	var uid: int = ResourceUID.create_id()
	var uid_text: String = ResourceUID.id_to_text(uid)
	_write_pack(pack, {
		"x/a.txt": "Alpha",
		"x/sub/b.txt": "Beta",
		"x/icon.png": "PNG",
		"x/icon.png.import": "[remap]\n",
		"x/orphan.png.import": "[remap]\n",
		".godot/imported/x-icon.ctex": "CTEX",
		"x/level.tscn.remap": "[remap]\n",
		"x/tool.gd": "extends Node\n",
		"x/tool.gd.uid": uid_text + "\n",
	})
	var out: String = ROOT + "extract"
	var package: PCKPackage = PCKPackageServer.open(pack)
	var progress: Array = []
	var on_progress := func(path: String, done: int, total: int) -> void: progress.append([path, done, total])
	PCKPackageServer.extract_progress.connect(on_progress)
	var result: PCKExtractResult = PCKPackageServer.extract(package, out)
	PCKPackageServer.extract_progress.disconnect(on_progress)
	expect(result.is_ok(), "extract: is_ok")
	expect_equal(result.error, OK, "PCKExtractResult.error is OK")
	expect(result.errors.is_empty(), "PCKExtractResult.errors is empty")
	var written := PackedStringArray([out.path_join("x/a.txt"), out.path_join("x/sub/b.txt"), out.path_join("x/icon.png"), out.path_join("x/icon.png.import"), out.path_join("x/tool.gd"), out.path_join("x/tool.gd.uid")])
	expect_equal(result.written, written, "written lists the extracted files in pack order")
	expect_equal(result.skipped, PackedStringArray(["res://x/orphan.png.import", "res://.godot/imported/x-icon.ctex"]), "skipped: .import without a source and .godot/")
	expect_equal(result.not_editable, PackedStringArray(["res://x/level.tscn"]), "not_editable lists remapped resources by their original path")
	expect(result.renamed.is_empty(), "nothing is renamed")
	expect(result.dropped_uids.is_empty(), "no UID is dropped")
	expect_equal(FileAccess.get_file_as_string(out.path_join("x/sub/b.txt")), "Beta", "an extracted file has the pack data")
	expect_equal(progress.size(), 9, "extract_progress once per selected file")
	expect_equal(progress[8], ["res://x/tool.gd.uid", 9, 9], "extract_progress reports path, done and total")
	expect(ResourceUID.has_id(uid), "a free UID is registered at once")
	expect_equal(ResourceUID.get_id_path(uid), out.path_join("x/tool.gd"), "the UID points to the extracted file")
	_write_file(out.path_join("x/a.txt"), "Edited")
	var kept: PCKExtractResult = PCKPackageServer.extract(package, out, ["res://x/a.txt"])
	expect(kept.written.is_empty(), "EXTRACT_SKIP writes nothing")
	expect_equal(kept.skipped, PackedStringArray(["res://x/a.txt"]), "EXTRACT_SKIP lists the file as skipped")
	expect_equal(FileAccess.get_file_as_string(out.path_join("x/a.txt")), "Edited", "EXTRACT_SKIP keeps the existing file")
	var overwritten: PCKExtractResult = PCKPackageServer.extract(package, out, ["x/a.txt"], PCKPackageServer.EXTRACT_OVERWRITE)
	expect_equal(overwritten.written, PackedStringArray([out.path_join("x/a.txt")]), "EXTRACT_OVERWRITE writes the file")
	expect_equal(FileAccess.get_file_as_string(out.path_join("x/a.txt")), "Alpha", "EXTRACT_OVERWRITE replaces the content")
	var renamed: PCKExtractResult = PCKPackageServer.extract(package, out, ["x/icon.png"], PCKPackageServer.EXTRACT_RENAME)
	expect_equal(renamed.written, PackedStringArray([out.path_join("x/icon_2.png"), out.path_join("x/icon_2.png.import")]), "EXTRACT_RENAME renames the file and its .import")
	expect_equal(renamed.renamed["res://x/icon.png"], out.path_join("x/icon_2.png"), "renamed maps the pack path to the new path")
	var by_folder: PCKExtractResult = PCKPackageServer.extract(package, out.path_join("folder"), ["res://x/sub/"])
	expect_equal(by_folder.written, PackedStringArray([out.path_join("folder/x/sub/b.txt")]), "a folder filter extracts the files below it")
	var by_pattern: PCKExtractResult = PCKPackageServer.extract(package, out.path_join("pattern"), ["*.txt"])
	expect_equal(by_pattern.written.size(), 2, "a pattern filter extracts the matching files")
	var with_engine: PCKExtractResult = PCKPackageServer.extract(package, out.path_join("engine"), [], PCKPackageServer.EXTRACT_SKIP, false)
	expect(DirAccess.dir_exists_absolute(out.path_join("engine/.godot")), "skip_engine_files = false extracts engine files")
	expect(with_engine.skipped.is_empty() or not with_engine.skipped.has("res://.godot/imported/x-icon.ctex"), "skip_engine_files = false does not skip .godot/")
	var second: PCKExtractResult = PCKPackageServer.extract(package, out.path_join("second"), ["x/tool.gd"])
	expect_equal(second.dropped_uids, PackedStringArray([uid_text]), "a colliding UID is dropped")
	expect(not FileAccess.file_exists(out.path_join("second/x/tool.gd.uid")), "the .uid file of a dropped UID is not written")
	expect_equal(PCKPackageServer.extract(null, out).error, ERR_INVALID_PARAMETER, "extract of null returns ERR_INVALID_PARAMETER")
	ResourceUID.remove_id(uid)


func _test_exporter_bindings() -> void:
	expect(ClassDB.class_exists("PCKPackageExporter"), "PCKPackageExporter exists in the editor build")
	expect(not ClassDB.can_instantiate("PCKPackageExporter"), "PCKPackageExporter cannot be instantiated")
	for method: String in PLAN_METHODS:
		expect(ClassDB.class_has_method("PCKPackageExporter", method, true), "PCKPackageExporter.%s is bound" % method)
	expect_equal(ClassDB.get_parent_class("PCKBuildPlan"), &"RefCounted", "PCKBuildPlan is a RefCounted")
	for property: String in ["valid", "export_sources", "raw_files", "removals", "skipped", "errors"]:
		expect_equal(ClassDB.class_get_property_setter("PCKBuildPlan", property), &"", "PCKBuildPlan.%s is read-only" % property)
	expect(not PCKPackageExporter.is_exporting(), "no export runs during the test")
	expect(not PCKPackageExporter.is_building(), "no description is built during the test")


func _test_exporter_plan() -> void:
	var description: PCKPackage = load(RESOURCES + "dlc_description.tres")
	var plan: PCKBuildPlan = PCKPackageExporter.plan_description(description)
	expect(plan.valid, "the saved description can be built")
	expect_equal(plan.export_sources, PackedStringArray(["res://icon.svg"]), "res:// sources go through the export pipeline")
	expect_equal(plan.raw_files, PackedStringArray(["dlc/readme.txt", "dlc/settings.cfg"]), "data and user:// files are copied as they are")
	expect_equal(plan.removals, PackedStringArray(["old/unused.png"]), "removals stay removals")
	expect(plan.errors.is_empty(), "the plan has no errors")
	var null_plan: PCKBuildPlan = PCKPackageExporter.plan_description(null)
	expect(not null_plan.valid, "a null description gives an invalid plan")
	expect_equal(null_plan.errors.size(), 1, "a null description gives one error")
	var refused := PCKPackage.new()
	refused.add_file("textures/icon.svg", "res://icon.svg")
	refused.add_file("icon.svg.import", "res://icon.svg.import")
	refused.add_file("other.png.import", "res://other.png.import")
	var refused_plan: PCKBuildPlan = PCKPackageExporter.plan_description(refused)
	expect(not refused_plan.valid, "a plan with errors is not valid")
	expect_equal(refused_plan.errors.size(), 3, "a renamed res:// target and both .import files are refused")
	expect(refused_plan.errors[0].contains("textures/icon.svg"), "the error names the renamed entry")
	var accepted := PCKPackage.new()
	accepted.add_file("icon.svg", "res://icon.svg")
	accepted.add_file("icon.svg.import", "res://icon.svg.import")
	var accepted_plan: PCKBuildPlan = PCKPackageExporter.plan_description(accepted)
	expect_equal(accepted_plan.skipped, PackedStringArray(["icon.svg.import"]), "the .import of an exported source is skipped")
	expect(accepted_plan.errors.is_empty(), "a source with its .import can be built")


func _test_exporter_helpers() -> void:
	expect(PCKPackageExporter.matches_encryption_filters("res://scripts/a.gd", ["*.gd"], []), "*.gd matches a script")
	expect(PCKPackageExporter.matches_encryption_filters("scripts/a.gd", ["*.gd"], []), "filters match without res://")
	expect(PCKPackageExporter.matches_encryption_filters("res://SECRET/a.png", ["secret/*"], []), "filters ignore case")
	expect(not PCKPackageExporter.matches_encryption_filters("res://dlc/a.pck", ["*"], ["*.pck"]), "an exclude filter wins")
	expect(not PCKPackageExporter.matches_encryption_filters("res://a.png", [], []), "no filter, no encryption")
	expect(not PCKPackageExporter.should_encrypt(null, "res://a.gd"), "no preset, no encryption")
	var project: String = "C:/Projects/Game"
	expect_equal(PCKPackageExporter.export_target_to_res_path("build/game.pck", project), "res://build/game.pck", "a relative target is inside the project")
	expect_equal(PCKPackageExporter.export_target_to_res_path("C:/Projects/Game/build/game.exe", project), "res://build/game.exe", "an absolute target inside the project")
	expect_equal(PCKPackageExporter.export_target_to_res_path("../builds/game.pck", project), "", "a target above the project is outside")
	expect_equal(PCKPackageExporter.export_target_to_res_path("C:/Projects/GameOther/game.pck", project), "", "a sibling folder with the same prefix is outside")
	var names: PackedStringArray = PCKPackageExporter.get_preset_names()
	expect(not names.has(MISSING_PRESET), "get_preset_names does not list an unknown preset")
	expect(PCKPackageExporter.find_preset(MISSING_PRESET) == null, "find_preset returns null for an unknown preset")
	expect(not PCKPackageExporter.is_export_target("res://pck-packages/none.pck"), "is_export_target is false for an ordinary file")
	var targets: PackedStringArray = PCKPackageExporter.get_export_target_paths()
	expect(not targets.has("res://pck-packages/none.pck"), "get_export_target_paths does not list an ordinary file")
	var description := PCKPackage.new()
	var target: String = ROOT + "refused.pck"
	expect_equal(PCKPackageExporter.export_description(description, MISSING_PRESET, target), ERR_DOES_NOT_EXIST, "an unknown preset is refused")
	expect_equal(PCKPackageExporter.export_description_with_preset(description, null, target), ERR_INVALID_PARAMETER, "a null preset is refused")
	expect(not FileAccess.file_exists(target), "nothing is written when the build is refused")


func _on_pack_mounted(path: String) -> void:
	_mounted.append(path)


func _on_pack_unmounted(path: String) -> void:
	_unmounted.append(path)


func _remove_folder(absolute: String) -> void:
	var dir: DirAccess = DirAccess.open(absolute)
	if dir == null:
		return
	for sub: String in dir.get_directories():
		_remove_folder(absolute.path_join(sub))
	for file: String in dir.get_files():
		dir.remove(file)
	DirAccess.remove_absolute(absolute)
