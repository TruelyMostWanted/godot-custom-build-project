extends Node
## Started as a separate process by the tests: prints the bus values that the domain applied before the main scene was ready, then quits.


func _ready() -> void:
	var music: int = AudioServer.get_bus_index(&"Music")
	var probe: Dictionary = {
		"ready_frame": Engine.get_process_frames(),
		"started": AudioBusSettingsServer.is_started(),
		"music_volume_db": AudioServer.get_bus_volume_db(music),
		"music_mute": AudioServer.is_bus_mute(music),
	}
	print("PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
