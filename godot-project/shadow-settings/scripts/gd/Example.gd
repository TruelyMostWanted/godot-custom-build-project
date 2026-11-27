extends Node
## Minimal example of Shadow Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Shadow-Settings


func _ready() -> void:
	# Using the API: set, pushed to the renderer and the root viewport at once.
	ShadowSettingsServer.set_directional_shadow_atlas_size(8192)
	ShadowSettingsServer.set_directional_soft_shadow_filter_quality(RenderingServer.SHADOW_QUALITY_SOFT_HIGH)
	ShadowSettingsServer.set_positional_shadow_atlas_size(2048)

	# Using the API: get the pending values.
	print(ShadowSettingsServer.get_directional_shadow_atlas_size())
	print(ShadowSettingsServer.get_directional_soft_shadow_filter_quality())

	# Using the API: apply, save and load.
	ShadowSettingsServer.apply()
	ShadowSettingsServer.save()
	ShadowSettingsServer.load()
	ShadowSettingsServer.apply()

	# A shadow quality button: needs a player who clicks it, so it is not run here.

	# Leave no player file behind.
	ShadowSettingsServer.reset_to_defaults()
	ShadowSettingsServer.save()
	get_tree().quit()
