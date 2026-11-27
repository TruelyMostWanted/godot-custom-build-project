extends Node
## Minimal example of Localization: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Localization


func _ready() -> void:
	# Migrate from CSV and translation files.
	# The guide runs this as an EditorScript; here it runs in _ready() and saves to user://.
	var catalog := TextLocalizationCatalog.new()
	for path: String in ProjectSettings.get_setting("internationalization/locale/translations"):
		catalog.add_translation(load(path))
	catalog.save_to_file("user://game.lcat")
	print(catalog.key_count, " keys in ", catalog.locales)
	get_tree().quit()
