extends Node
## Started as a separate process by the checks: prints the window values that the window settings domain
## loaded before the main scene was ready and the state a second window gets when it enters the tree, then quits.


func _ready() -> void:
	var inventory := Window.new()
	inventory.name = "Inventory"
	inventory.visible = false
	add_child(inventory) # Path /root/RestartProbe/Inventory: the stored state is applied here.
	var probe := {
		"ready_frame": Engine.get_process_frames(),
		"started": WindowSettingsServer.is_started(),
		"window_mode": WindowSettingsServer.get_window_mode(),
		"window_size": str(WindowSettingsServer.get_window_size()),
		"screen": WindowSettingsServer.get_screen(),
		"borderless": WindowSettingsServer.is_borderless(),
		"command_line_options": WindowSettingsServer.get_command_line_options(),
		"window_mode_overridden": WindowSettingsServer.has_session_override(&"window_mode"),
		"window_size_overridden": WindowSettingsServer.has_session_override(&"window_size"),
		"inventory_size": str(inventory.size),
		"inventory_position": str(inventory.position),
	}
	print("CHECK20_PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
