extends Node
## Started as a separate process by the checks: prints the bus values that the audio settings domains
## applied before the main scene was ready, then quits.


func _ready() -> void:
	var music := AudioServer.get_bus_index(&"Music")
	var probe := {
		"ready_frame": Engine.get_process_frames(),
		"buses_started": AudioBusSettingsServer.is_started(),
		"devices_started": AudioSettingsServer.is_started(),
		"music_volume_db": AudioServer.get_bus_volume_db(music) if music >= 0 else 1000.0,
		"music_mute": AudioServer.is_bus_mute(music) if music >= 0 else false,
		"output_device": AudioServer.output_device,
	}
	print("CHECK16_PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
