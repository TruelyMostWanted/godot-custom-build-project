extends Node
## Started as a separate process by the checks: prints the engine values that the engine settings domain applied
## before the main scene was ready, then quits.


func _ready() -> void:
	var probe := {
		"ready_frame": Engine.get_process_frames(),
		"started": EngineSettingsServer.is_started(),
		"max_fps": EngineSettingsServer.get_max_fps(),
		"engine_max_fps": Engine.max_fps,
		"engine_time_scale": Engine.time_scale,
		"physics_interpolation": get_tree().physics_interpolation,
		"sleep_usec": OS.low_processor_usage_mode_sleep_usec,
		"simulation_locked": EngineSettingsServer.is_simulation_locked(),
		"max_fps_overridden": EngineSettingsServer.has_session_override(&"max_fps"),
		"time_scale_overridden": EngineSettingsServer.has_session_override(&"time_scale"),
	}
	print("CHECK26_PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
