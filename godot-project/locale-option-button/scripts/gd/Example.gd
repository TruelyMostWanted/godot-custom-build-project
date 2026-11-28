extends Node
## Minimal example of Locale Option Button: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Locale-Option-Button


func _ready() -> void:
	# Where the list comes from.
	var locales := LanguageSettingsServer.get_available_locales_for(&"subtitles")
	print("Subtitle locales: ", locales)

	# Adding the menu.
	var menu := LocaleOptionButton.new()
	menu.locale_selected.connect(func(locale: String) -> void: print("Language: ", locale))
	add_child(menu)

	# Channels.
	var subtitles := LocaleOptionButton.new()
	subtitles.target = &"subtitles"
	add_child(subtitles)

	get_tree().quit()
