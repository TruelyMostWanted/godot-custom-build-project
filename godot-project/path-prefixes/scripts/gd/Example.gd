extends Node
## Minimal example of Path Prefixes: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Path-Prefixes


func _ready() -> void:
	# Open the mods folder next to the executable (the example uses user://mods to run anywhere).
	DirAccess.make_dir_recursive_absolute("user://mods/my_mod")
	var mod_file: FileAccess = FileAccess.open("user://mods/my_mod/mod.toml", FileAccess.WRITE)
	mod_file.store_string("name = \"my_mod\"")
	mod_file.close()
	PathPrefixServer.register_prefix_folder("mods://", "user://mods")
	var mods: DirAccess = DirAccess.open("mods://")
	var manifest: String = FileAccess.get_file_as_string("mods://my_mod/mod.toml")
	print("Mod folders: ", mods.get_directories())
	print("Manifest: ", manifest)

	# Use a different folder per platform.
	PathPrefixServer.register_prefix_folder("saves://", "user://saves")
	PathPrefixServer.register_prefix_folder("saves://", "exec://saves", ["windows", "linux"])
	print("saves:// is ", PathPrefixServer.globalize("saves://slot_1.json"))

	# Compute the folder with your own logic.
	PathPrefixServer.register_prefix_callable("levels://",
			func(path: String) -> String: return "user://levels/" + path.trim_prefix("levels://"))
	print("levels:// is ", PathPrefixServer.globalize("levels://a/b.tres"))

	# Add a prefix for a system folder (saving a screenshot needs a window, so only the path is shown).
	PathPrefixServer.register_prefix("pictures://", PathPrefixServer.create_system_dir_prefix(OS.SYSTEM_DIR_PICTURES))
	print("pictures:// is ", PathPrefixServer.globalize("pictures://screenshot.png"))

	# Turn a prefixed path into an OS path and back.
	var info: PathInfo = PathPrefixServer.resolve("USER://saves\a.json")
	print(info.local_path)
	print(info.relative_path)
	var os_path: String = PathPrefixServer.globalize("appdata://saves/slot_1.json")
	print(PathPrefixServer.localize(os_path))

	# Check paths from mods or players.
	var path_from_player: String = "user://saves/slot_1.json"
	DirAccess.make_dir_recursive_absolute("user://saves")
	var safe: PathInfo = PathPrefixServer.resolve_safe(path_from_player, PathPrefixServer.ACCESS_WRITE, ["user://saves/"])
	if safe.valid:
		var file: FileAccess = FileAccess.open(safe.local_path, FileAccess.WRITE)
		print("Opened ", file.get_path())
	else:
		push_error(safe.error_message)

	# Point a prefix into res:// or user://.
	PathPrefixServer.register_prefix_folder("streaming://", "res://path-prefixes/resources")
	print(PathPrefixServer.resolve_engine_path("streaming://level.tres"))
	var level: Resource = load("streaming://level.tres")
	print("Level: ", level.resource_name)
	get_tree().quit()
