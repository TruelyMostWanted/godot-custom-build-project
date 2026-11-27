extends Node
## Minimal example of Texture Streaming Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Texture-Streaming-Settings


func _ready() -> void:
	# Using the API: builds without texture streaming (the Web export) don't have this server.
	if not Engine.has_singleton(&"TextureStreamingSettingsServer"):
		get_tree().quit()
		return
	var streaming: Object = Engine.get_singleton(&"TextureStreamingSettingsServer")

	# Using the API: set, budget and mip levels apply at once, "enabled" at the next start.
	streaming.set_enabled(true)
	streaming.set_memory_budget_enabled(true)
	streaming.set_memory_budget_mb(1024)
	streaming.set_min_lod(1)

	# Using the API: get the pending values.
	print(streaming.get_memory_budget_mb())
	print(streaming.get_restart_required_names())

	# Using the API: apply, save and load.
	streaming.apply()
	streaming.save()
	streaming.load()
	streaming.apply()

	# A texture streaming check box: needs a player who clicks it, so it is not run here.

	# Leave no player file behind.
	streaming.reset_to_defaults()
	streaming.save()
	get_tree().quit()
