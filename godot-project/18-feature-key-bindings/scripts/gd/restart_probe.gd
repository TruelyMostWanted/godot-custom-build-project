extends Node
## Started as a separate process by the checks: prints what the input_map domain loaded and applied to
## InputMap before the main scene was ready, then quits.


func _ready() -> void:
	var w := InputEventKey.new()
	w.keycode = KEY_W
	w.physical_keycode = KEY_W
	var space := InputEventKey.new()
	space.keycode = KEY_SPACE
	space.physical_keycode = KEY_SPACE
	var pad_a := InputEventJoypadButton.new()
	pad_a.button_index = JOY_BUTTON_A
	var probe := {
		"started": InputMapSettingsServer.is_started(),
		"w_jumps": InputMap.event_is_action(w, &"kb18_jump"),
		"space_jumps": InputMap.event_is_action(space, &"kb18_jump"),
		"pad_a_jumps": InputMap.event_is_action(pad_a, &"kb18_jump"),
		"has_removed": InputMapSettingsServer.get_action_names().has("kb18_removed"),
	}
	print("CHECK18_PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
