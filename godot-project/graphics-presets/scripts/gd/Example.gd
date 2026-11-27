extends Node
## Minimal example of Graphics Presets: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Graphics-Presets


func _ready() -> void:
	# Using the API: the built-in presets, then the ones from settings/graphics_preset/presets.
	print(GraphicsPresetSettingsServer.get_preset_names())

	# Using the API: sets the values of the quality, texture, shadow and effect domains in one call.
	# The wiki applies "Cinematic", a project preset; this project has none, so the built-in "Ultra" is used.
	GraphicsPresetSettingsServer.apply_preset("Ultra")
	print(GraphicsPresetSettingsServer.get_current_preset())

	# Using the API: one change by hand and the values no longer match.
	ShadowSettingsServer.set_directional_shadow_atlas_size(1024)
	print(GraphicsPresetSettingsServer.get_current_preset())

	# Using the API: save, load and apply as usual.
	GraphicsPresetSettingsServer.save()
	GraphicsPresetSettingsServer.load()
	GraphicsPresetSettingsServer.apply()

	# A preset button: needs a player who clicks it, so it is not run here.

	# Leave no player files behind.
	for domain: String in GraphicsPreset.get_domains():
		SettingsServerManager.get_server(domain).reset_to_defaults()
		SettingsServerManager.get_server(domain).save()
	GraphicsPresetSettingsServer.reset_to_defaults()
	GraphicsPresetSettingsServer.save()
	get_tree().quit()
