extends Node
## Minimal example of Settings Servers: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Settings-Servers

const GameSettings := preload("res://settings-servers/scripts/gd/game_settings.gd")
const ListDomain := preload("res://settings-servers/scripts/gd/test_list_domain.gd")
const FILE := "user://settings-servers-example/game.ini"

var game: SettingsServer


func _ready() -> void:
	# Write a settings domain for your game (a game lists the script in settings/startup/custom_servers).
	DirAccess.make_dir_recursive_absolute(FILE.get_base_dir())
	DirAccess.remove_absolute(FILE)
	game = GameSettings.new()
	game.file_path = FILE
	SettingsServerManager.register_server(game)
	print("Settings: ", game.get_setting_names())

	# Change a value from the options menu.
	game = SettingsServerManager.get_server(&"game")
	game.settings_changed.connect(_refresh)
	game.set_value("difficulty", 2)
	print("Difficulty: ", game.get_value("difficulty"))

	# React when a value reaches the engine.
	game.difficulty_changed.connect(_on_difficulty_changed)
	game.set_value("difficulty", 0)

	# Add Apply and Cancel buttons.
	game.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	game.set_value("difficulty", 2)
	print("Dirty: ", game.is_dirty())
	game.revert()
	print("Dirty after revert: ", game.is_dirty())
	game.set_value("difficulty", 1)
	game.apply()

	# Ask the player to keep a risky change (the dialog is left out).
	game.set_value("show_hints", false)
	game.apply_with_confirmation(10.0)
	print("Confirmation pending: ", game.is_confirmation_pending())
	game.confirm_changes()
	print("Confirmation pending after confirm: ", game.is_confirmation_pending())

	# Push values to the engine with bindings.
	game.set_value("max_fps", 60)
	game.apply()
	print("Engine.max_fps: ", Engine.max_fps)
	game.reset_value("max_fps")
	game.apply()

	# Float values have three decimals.
	print(SettingsServer.snap_float(0.30000000000000004)) # 0.3
	print(SettingsServer.snap_float(-6.0206, 0.1)) # -6.0

	# Choose where and in which format settings are stored.
	game.file_path = "user://settings-servers-example/game.json"
	game.save()
	game.load()
	game.apply()

	# React when a file can't be loaded or saved.
	for server: SettingsServer in SettingsServerManager.get_servers():
		server.load_failed.connect(func(_err: int, path: String) -> void: print("Settings reset: %s is broken" % path))
		server.save_failed.connect(func(err: int, path: String) -> void: print("Could not save %s (%s)" % [path, error_string(err)]))

	# Override a value for one run (the snippet reads the command line argument --hard).
	game.set_session_override("difficulty", 2)
	print("Session override: ", game.get_value("difficulty"))
	game.clear_session_override("difficulty")

	# Write a list domain.
	var voices: ListSettingsServer = ListDomain.new()
	voices.file_path = "user://settings-servers-example/voices.ini"
	SettingsServerManager.register_server(voices)
	var guard := SettingsServersTestEntry.new()
	guard.entry_name = "guard"
	voices.add_entry(guard)
	var index: int = voices.find_entry("guard")
	voices.set_entry_value(index, "level", 7)
	print("Guard level: ", voices.get_entry_value(index, "level"))

	voices.free()
	game.free()
	get_tree().quit()


func _refresh(names: PackedStringArray) -> void:
	print("Refresh: ", names)


func _on_difficulty_changed(difficulty: int) -> void:
	print("Difficulty reached the engine: ", difficulty)
