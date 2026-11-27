extends Node
## Minimal example of Language Settings: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Language-Settings


func _ready() -> void:
	# Using the API
	var language := LanguageSettingsServer

	# The locales of the loaded translations, for a language menu.
	print(language.get_available_locales()) # ["de", "fr"]

	# Set: applied to the TranslationServer at once (APPLY_MODE_IMMEDIATE, the default).
	language.set_locale("fr")

	# Get: the player's choice, and the locale in use. An empty locale follows the system.
	print(language.get_locale()) # fr
	print(TranslationServer.get_locale()) # fr

	# In APPLY_MODE_MANUAL, changes wait for apply().
	language.apply()
	# Autosave writes the file at the end of the frame; save() writes it now.
	language.save()
	# load() reads the file into the pending values; apply() pushes them to the TranslationServer.
	language.load()
	language.apply()

	# A language button
	var button := LanguageButton.new()
	add_child(button)
	print("Items: ", button.item_count)
	print("Selected: ", button.get_item_metadata(button.selected))

	language.reset_to_defaults()
	language.save()
	get_tree().quit()
