extends Node
## Started as a separate process by the tests: prints what the device domain applied before the main scene was ready, then quits.


func _ready() -> void:
	var probe: Dictionary = {
		"ready_frame": Engine.get_process_frames(),
		"started": AudioDeviceSettingsServer.is_started(),
		"output_device": AudioServer.output_device,
		"input_device": AudioServer.input_device,
	}
	print("PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
