extends Node
## Started as a separate process by the tests: prints the input values that the input settings domain
## loaded and applied before the main scene was ready, then quits.


func _ready() -> void:
	var look := InputSettingsServer.apply_to_mouse_motion(Vector2(2, 4))
	var probe := {
		"started": InputSettingsServer.is_started(),
		"y_inverted": InputSettingsServer.is_y_inverted(InputSettingsServer.DEVICE_MOUSE),
		"sensitivity": InputSettingsServer.get_sensitivity(InputSettingsServer.DEVICE_MOUSE),
		"look_x": look.x,
		"look_y": look.y,
		"use_accumulated_input": Input.use_accumulated_input,
	}
	print("PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
