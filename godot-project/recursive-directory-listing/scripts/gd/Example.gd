extends Node
## Minimal example of Recursive Directory Listing: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Recursive-Directory-Listing


func _create_file(path: String) -> void:
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	file.store_string("x")
	file.close()


func _ready() -> void:
	# Setup: the files the guide talks about.
	_create_file("user://saves/slot_1.tres")
	_create_file("user://saves/backup/slot_1.tres")
	_create_file("user://mods/my_mod/icon.png")
	_create_file("user://mods/my_mod/art/banner.webp")
	_create_file("user://mods/my_mod/mod.json")
	DirAccess.remove_absolute("user://config/.secret") # a hidden file on Windows cannot be overwritten
	_create_file("user://config/settings.cfg")
	_create_file("user://config/.secret")
	FileAccess.set_hidden_attribute("user://config/.secret", true)

	# List all files below a folder.
	for path: String in DirAccess.get_file_paths_at("user://saves", true):
		print(path)
	print(DirAccess.get_directory_paths_at("user://saves", true))

	# Find the images of a mod.
	var images: PackedStringArray = DirAccess.get_file_paths_at("user://mods/my_mod", true, ["png", "webp"])
	var kinds: PackedStringArray = DirAccess.get_file_extensions_at("user://mods/my_mod", true) # for example ["json", "png", "tres"]
	print(images)
	print(kinds)

	# Include hidden files.
	var dir: DirAccess = DirAccess.open("user://config")
	dir.include_hidden = true
	var all_files: PackedStringArray = dir.get_file_paths(true)
	print(all_files)
	get_tree().quit()
