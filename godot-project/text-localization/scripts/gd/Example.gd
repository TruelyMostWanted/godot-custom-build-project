extends Node
## Minimal example of Text Localization: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Text-Localization


func _ready() -> void:
	# Setup: a game lists ui.csv under Catalog Manifests and player_name in text_values.tres; here both come from code.
	var game := TextLocalizationCatalog.new()
	game.load_from_file("res://text-localization/resources/ui.csv")
	TextLocalizationServer.add_catalog(game)
	TextInsertionServer.set_value(&"player_name", "Traveler")

	# Using the API
	TranslationServer.set_locale("de")
	print(TextLocalizationServer.get_locale_chain("de_AT"))
	print(TextLocalizationServer.translate(&"menu.play"))
	print(tr("menu.quit"))
	print(TextLocalizationServer.translate(&"unknown.key"))
	print(TextLocalizationServer.translate(&"enemy.count", &"", 3, { "count": 3 }))
	print(TextInsertionServer.insert(TextLocalizationServer.translate(&"npc.greeting")))
	print(TextLocalizationServer.translate_for_locale("en", &"menu.play"))
	var quit_id: int = TextLocalizationServer.get_key_id(&"menu.quit")
	print(TextLocalizationServer.translate_id(quit_id))

	# The greeting in the player's language (GreetingLabel and LanguageMenu are nodes; their core lines)
	var greeting := Label.new()
	greeting.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	greeting.text = TextInsertionServer.insert(TextLocalizationServer.translate(&"npc.greeting"))
	print(greeting.text)
	for locale: String in TextLocalizationServer.get_available_locales():
		print(locale, ": ", TranslationServer.get_locale_name(locale))
	greeting.free()

	# A French mod (the wiki loads it from user://mods/french/translations/ui.csv)
	var french := TextLocalizationCatalog.new()
	french.load_from_file("res://text-localization/resources/french_ui.csv")
	TextLocalizationServer.add_catalog(french, 100)
	TranslationServer.set_locale("fr")
	print(TextLocalizationServer.translate(&"enemy.count", &"", 3, { "count": 3 }))
	TextLocalizationServer.remove_catalog(french)

	TextLocalizationServer.clear_catalogs()
	TextInsertionServer.remove_value(&"player_name")
	TranslationServer.set_locale("en")
	get_tree().quit()
