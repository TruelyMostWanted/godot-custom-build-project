extends Node
## Started as a separate process by the tests: prints what the domains applied before the main scene was ready, then quits.


func _ready() -> void:
	var probe: Dictionary = {"ready_frame": Engine.get_process_frames()}
	for domain: String in ["test_gd", "test_cs"]:
		var server: SettingsServer = SettingsServerManager.get_server(domain)
		probe[domain] = {
			"started": server != null and server.is_started(),
			"applied": Engine.get_meta(domain + "_applied", {}),
		}
	print("PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
