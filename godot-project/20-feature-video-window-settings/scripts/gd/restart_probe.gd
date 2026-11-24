extends Node
## Started as a separate process by the checks: prints the window values that the video window settings domain
## loaded before the main scene was ready and the state a second window gets when it enters the tree, then quits.


func _ready() -> void:
	var inventory := Window.new()
	inventory.name = "Inventory"
	inventory.visible = false
	add_child(inventory) # Path /root/RestartProbe/Inventory: the stored state is applied here.
	var probe := {
		"ready_frame": Engine.get_process_frames(),
		"started": VideoWindowSettingsServer.is_started(),
		"window_mode": VideoWindowSettingsServer.get_window_mode(),
		"window_size": str(VideoWindowSettingsServer.get_window_size()),
		"screen": VideoWindowSettingsServer.get_screen(),
		"borderless": VideoWindowSettingsServer.is_borderless(),
		"command_line_options": VideoWindowSettingsServer.get_command_line_options(),
		"window_mode_overridden": VideoWindowSettingsServer.has_session_override(&"window_mode"),
		"window_size_overridden": VideoWindowSettingsServer.has_session_override(&"window_size"),
		"inventory_size": str(inventory.size),
		"inventory_position": str(inventory.position),
	}
	print("CHECK20_PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
