extends Node
## Started as a separate process by the checks: prints what the settings domains applied before the
## main scene was ready, then quits.


func _ready() -> void:
	var gd_server: SettingsServer = SettingsServerManager.get_server(&"check15_gd")
	var cs_server: SettingsServer = SettingsServerManager.get_server(&"check15_cs")
	var probe := {
		"ready_frame": Engine.get_process_frames(),
		"gd_started": gd_server != null and gd_server.is_started(),
		"cs_started": cs_server != null and cs_server.is_started(),
		"gd_applied": Engine.get_meta(&"check15_gd_applied", {}),
		"cs_applied": Engine.get_meta(&"check15_cs_applied", {}),
	}
	print("CHECK15_PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
