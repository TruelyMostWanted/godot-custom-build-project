extends Node
## Minimal example of TOML: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/TOML
## In the editor: double-click server.toml for live errors, select it once for the data tree (no code).


func _ready() -> void:
	# Using the API
	var config := load("res://toml/resources/server.toml") as TOML
	print(config.get_value("server", "port"))
	print(config.get_value("server.limits", "players"))
	for map: Dictionary in config.data["maps"]:
		print(map["name"], ": ", map["rounds"], " rounds")

	var mine := TOML.new()
	mine.parse(config.encode_to_text())
	mine.set_value("server.limits", "players", 32)
	mine.save("user://server.toml")

	# Start a server from its file
	_start_server()

	# Save your own resource as TOML
	var settings := _make_settings()
	ResourceSaver.save(settings, "user://settings.toml")
	var loaded := load("user://settings.toml") as GameSettings
	var text := TOML.serialize(settings)
	print(loaded.title)
	print(text)
	get_tree().quit()


func _start_server() -> void:
	var config := TOML.new()
	if config.load(OS.get_executable_path().get_base_dir().path_join("server.toml")) != OK:
		config.parse(FileAccess.get_file_as_string("res://toml/resources/server.toml"))

	var peer := ENetMultiplayerPeer.new()
	var port: int = config.get_value("server", "port", 8080)
	var players: int = config.get_value("server.limits", "players", 16)
	peer.create_server(port, players)
	multiplayer.multiplayer_peer = peer
	Engine.physics_ticks_per_second = int(config.get_value("server.limits", "tick_rate", 30.0))
	print("Server on port ", port, " for ", players, " players at ", Engine.physics_ticks_per_second, " ticks")
	peer.close()
	multiplayer.multiplayer_peer = null


## The GameSettings of the guide's file.
func _make_settings() -> GameSettings:
	var settings := GameSettings.new()
	settings.title = "Space Trader"
	settings.quality = GameSettings.Quality.High
	settings.resolution = Vector2i(1920, 1080)
	settings.fullscreen = true
	settings.audio = AudioSettings.new()
	var ann := PlayerProfile.new()
	ann.name = "Ann"
	ann.level = 12
	settings.profiles = [ann]
	return settings
