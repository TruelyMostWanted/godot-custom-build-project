extends Node
## Minimal example of Texture Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Texture-Settings


func _ready() -> void:
	# Using the API: set, pushed to the root viewport at once.
	TextureSettingsServer.set_anisotropic_filtering_level(Viewport.ANISOTROPY_16X)
	TextureSettingsServer.set_texture_mipmap_bias(-0.5)
	TextureSettingsServer.set_canvas_item_default_texture_filter(Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST)

	# Using the API: get the pending values.
	print(TextureSettingsServer.get_anisotropic_filtering_level())
	print(TextureSettingsServer.get_texture_mipmap_bias())

	# Using the API: apply, save and load.
	TextureSettingsServer.apply()
	TextureSettingsServer.save()
	TextureSettingsServer.load()
	TextureSettingsServer.apply()

	# An anisotropic filtering button: needs a player who clicks it, so it is not run here.

	# Leave no player file behind.
	TextureSettingsServer.reset_to_defaults()
	TextureSettingsServer.save()
	get_tree().quit()
