extends FeatureCheck
## GDScript checks of 85-feature-pck-package-inspector. Check the same things as scripts/cs/Issue85FeaturePckPackageInspectorCheck.cs.
## The Inspector plugin is editor-only: these checks cover the data it edits (the storage properties `files` and
## `removals` of a pack description), saving the description as `.tres`, "Build Pack..." and "Create Description...".
## The UI itself is checked by hand (see the issue).

const ROOT := "user://85-feature-pck-package-inspector/gd/"


func _get_check_name() -> String:
	return "85-feature-pck-package-inspector"


func _write(path: String, text: String) -> void:
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var f := FileAccess.open(path, FileAccess.WRITE)
	f.store_string(text)
	f.close()


func _run_checks() -> void:
	_write(ROOT + "src/assets/a.txt", "Alpha")
	_write(ROOT + "src/assets/sub/c.txt", "Gamma")

	# A description as the Inspector edits it: a dropped folder keeps the relative paths, a removal is listed apart.
	var description := PCKPackage.new()
	expect(description.is_description(), "a new PCKPackage is a description (editable in the Inspector)")
	description.set("files", [
		{"path": "pck85gd/assets/a.txt", "source": ROOT + "src/assets/a.txt"},
		{"path": "pck85gd/assets/sub/c.txt", "source": ROOT + "src/assets/sub/c.txt", "encrypt": false},
		{"path": "pck85gd/data.txt", "data": "Data".to_utf8_buffer()},
	])
	description.set("removals", PackedStringArray(["pck85gd/old.txt"]))
	expect_equal(description.get_file_paths(), PackedStringArray(["res://pck85gd/assets/a.txt", "res://pck85gd/assets/sub/c.txt", "res://pck85gd/data.txt"]), "files set as storage property are listed in order")
	expect_equal(description.get("removals"), PackedStringArray(["pck85gd/old.txt"]), "removals set as storage property")

	# Rename a target path as the Inspector does: edit the snapshot and set it back (undo sets the old snapshot).
	var before: Array = description.get("files")
	var files: Array = description.get("files")
	var first: Dictionary = files[0]
	first["path"] = "pck85gd/renamed/a.txt"
	description.set("files", files)
	expect(description.has_file("res://pck85gd/renamed/a.txt") and not description.has_file("res://pck85gd/assets/a.txt"), "rename through the files snapshot")
	description.set("files", before)
	expect(description.has_file("res://pck85gd/assets/a.txt") and not description.has_file("res://pck85gd/renamed/a.txt"), "undo: the old snapshot restores the entry")
	description.set("files", files)

	# Save the description as text and open it again.
	var tres := ROOT + "dlc.tres"
	expect_equal(ResourceSaver.save(description, tres), OK, "save the description as .tres")
	var loaded := ResourceLoader.load(tres, "", ResourceLoader.CACHE_MODE_IGNORE) as PCKPackage
	expect(loaded != null and loaded.is_description(), "the .tres loads as a description")
	if loaded == null:
		return
	expect_equal(loaded.get_file_paths(), description.get_file_paths(), "the loaded description lists the same files")
	expect_equal(loaded.get("removals"), PackedStringArray(["pck85gd/old.txt"]), "the loaded description keeps the removals")

	# "Build Pack...": build the description, the pack opens read-only.
	var pck := ROOT + "dlc.pck"
	expect_equal(PCKPackageServer.build(loaded, pck), OK, "build the description")
	var opened := PCKPackageServer.open(pck)
	expect(opened != null and not opened.is_description(), "a built pack opens as a pack, not a description (read-only in the Inspector)")
	if opened == null:
		return
	expect_equal(opened.get_file_paths(), loaded.get_file_paths(), "the built pack has the files of the description")
	expect_equal(PCKPackageServer.read_file(opened, "res://pck85gd/renamed/a.txt").get_string_from_utf8(), "Alpha", "a renamed file is built from its source")
	expect_equal(PCKPackageServer.read_file(opened, "res://pck85gd/data.txt").get_string_from_utf8(), "Data", "a data entry is built")

	# "Create Description...": every file of the pack becomes a from_pack entry of the base pack.
	var copy := PCKPackage.new()
	copy.base_pack = pck
	copy.alignment = opened.alignment
	copy.encrypted_directory = opened.encrypted_directory
	copy.set("files", opened.get("files"))
	copy.set("removals", opened.get("removals"))
	var from_pack: Array = copy.get("files")
	expect(from_pack.size() == 3 and from_pack.all(func(f: Dictionary) -> bool: return f.has("from_pack")), "Create Description: every file is a from_pack entry")
	var copy_tres := ROOT + "from_pack.tres"
	expect_equal(ResourceSaver.save(copy, copy_tres), OK, "save the created description")
	var copy_loaded := ResourceLoader.load(copy_tres, "", ResourceLoader.CACHE_MODE_IGNORE) as PCKPackage
	expect(copy_loaded != null and copy_loaded.base_pack == pck, "the created description keeps base_pack")
	if copy_loaded == null:
		return
	var rebuilt := ROOT + "rebuilt.pck"
	expect_equal(PCKPackageServer.build(copy_loaded, rebuilt), OK, "build the created description")
	var rebuilt_pack := PCKPackageServer.open(rebuilt)
	expect(rebuilt_pack != null and rebuilt_pack.get_file_paths() == opened.get_file_paths(), "the rebuilt pack has the same files")
	if rebuilt_pack != null:
		expect_equal(PCKPackageServer.read_file(rebuilt_pack, "res://pck85gd/assets/sub/c.txt").get_string_from_utf8(), "Gamma", "a from_pack file is copied from the base pack")
