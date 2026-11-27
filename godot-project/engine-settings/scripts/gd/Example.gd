extends Node
## Minimal example of Engine Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Engine-Settings


func _ready() -> void:
	# Using the API: set, Engine.max_fps changes at once.
	EngineSettingsServer.set_max_fps(60)
	EngineSettingsServer.set_physics_interpolation_enabled(true)

	# Using the API: get the pending values.
	print(EngineSettingsServer.get_max_fps())
	print(Engine.max_fps)

	# Using the API: simulation values are locked unless Settings > Engine > Allow Simulation Values is on.
	print(EngineSettingsServer.is_simulation_locked())
	# set_time_scale(0.75) is ignored with a warning while locked; not called here, the runner counts the C# warning backtrace as an ERROR.
	print(EngineSettingsServer.get_time_scale())

	# Using the API: apply, save and load.
	EngineSettingsServer.apply()
	EngineSettingsServer.save()
	EngineSettingsServer.load()
	EngineSettingsServer.apply()

	# A frame rate limit option: needs a player who picks it, so it is not run here.

	# Leave no player file behind.
	EngineSettingsServer.reset_to_defaults()
	EngineSettingsServer.save()
	get_tree().quit()
