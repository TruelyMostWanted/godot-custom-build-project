extends Node
## Minimal example of PCK Packages: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/PCK-Packages

const BASE_PACK := "res://pck-packages/resources/sample_pack.pck"
const ZERO_KEY := "0000000000000000000000000000000000000000000000000000000000000000"
const SECRET_KEY := "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff"


func _write_pack(path: String, files: Dictionary, key: String = ZERO_KEY, encrypt: bool = false) -> void:
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var packer := PCKPacker.new()
	packer.pck_start(path, 32, key, encrypt)
	for target: String in files:
		var content: String = files[target]
		packer.add_file_from_buffer(target, content.to_utf8_buffer(), encrypt)
	packer.flush()


func _ready() -> void:
	# Setup: the packs and files the guide talks about.
	_write_pack("user://mods/level_pack.pck", {
		"levels/readme.txt": "Ten levels.",
		"levels/old.tscn": "old",
		"levels/from_base_game.tscn": "base",
	})
	_write_pack("user://dlc/forest.pck", { "dlc/forest/level.tscn": "forest" })
	_write_pack("user://downloads/mod.pck", { "levels/a.tscn": "a", "data/info.json": "{}", "docs/notes.txt": "notes" })
	_write_pack("user://secret.pck", { "secret/hidden.txt": "hidden" }, SECRET_KEY, true)
	var icon: FileAccess = FileAccess.open("user://export_icon.png", FileAccess.WRITE)
	icon.store_string("png")
	icon.close()

	# Describe a pack as a .tres file (the guide's res://art/a.png and res://base.pck are the sample files).
	var desc := PCKPackage.new()
	desc.base_pack = BASE_PACK
	desc.add_file("textures/a.png", "res://icon.svg")
	desc.add_file_from_buffer("config.json", "{}".to_utf8_buffer())
	desc.add_removal("old/c.png")
	ResourceSaver.save(desc, "user://dlc.tres")
	ResourceSaver.save(desc, "user://dlc.pck") # builds the pack
	print("Description: ", desc.get_file_paths())

	# List the files of a mod pack.
	var package: PCKPackage = PCKPackageServer.open("user://mods/level_pack.pck")
	if package == null:
		return
	for entry: PCKPackageEntry in package.get_entries():
		print(entry.path, " ", entry.size)
	var readme: String = PCKPackageServer.read_file(package, "res://levels/readme.txt").get_string_from_utf8()
	var intact: bool = PCKPackageServer.verify(package) == OK # checks the MD5 of every file
	print(readme, " ", intact)

	# Mount a DLC and unmount it again (the guide changes to the scene of the DLC; the example prints the file).
	if PCKPackageServer.mount("user://dlc/forest.pck") == OK:
		print("Mounted: ", FileAccess.file_exists("res://dlc/forest/level.tscn"))

	# Back in the main menu:
	PCKPackageServer.unmount("user://dlc/forest.pck")

	# Add a file to a pack.
	package.add_file_from_buffer("res://levels/extra.txt", "Hello".to_utf8_buffer())
	package.add_file("res://levels/icon.png", "user://export_icon.png")
	package.remove_file("res://levels/old.tscn")
	package.add_removal("res://levels/from_base_game.tscn") # hides this file of earlier packs when mounted
	PCKPackageServer.build(package, "user://mods/level_pack_v2.pck")

	# Build a DLC pack with imported resources and Keep DLC content out of the main export:
	# both need the editor and an export preset (PCKPackageExporter, Project > Tools), so the example skips them.

	# Unpack a mod into user://.
	var mod: PCKPackage = PCKPackageServer.open("user://downloads/mod.pck")
	PCKPackageServer.extract_progress.connect(func(path: String, done: int, total: int) -> void:
		print(100.0 * done / total, " % ", path))
	var result: PCKExtractResult = PCKPackageServer.extract(mod, "user://mods/my_mod", ["res://levels/", "*.json"], PCKPackageServer.EXTRACT_RENAME)
	if not result.is_ok():
		push_error("\n".join(result.errors))
	print("Written: ", result.written)

	# Open an encrypted pack.
	var secret: PCKPackage = PCKPackageServer.open("user://secret.pck", SECRET_KEY)
	if secret == null:
		push_error("Wrong key or not a pack.")
	print("Secret: ", secret.get_file_paths())
	get_tree().quit()
