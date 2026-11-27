extends Node
## Minimal example of Locale Channels: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Locale-Channels

const LanguageMenu := preload("res://locale-channels/scripts/gd/LanguageMenu.gd")
const MENU_LISTS: Array[String] = ["LanguageList", "InterfaceList", "AudioList", "SubtitlesList"]


func _ready() -> void:
	# Set a channel.
	TranslationServer.locale_changed.connect(func(channel: StringName, locale: String, previous: String) -> void:
		print("[", channel, "] ", previous, " -> ", locale))

	TranslationServer.set_locale("en")
	TranslationServer.set_locale_for(&"interface", "de")
	TranslationServer.set_locale_for(&"audio", "ja")
	print(TranslationServer.get_locale_for(&"subtitles"))
	print(TranslationServer.get_locale_channels())

	TranslationServer.clear_locale_for(&"audio")

	# Choose the channel of a control. The labels are added here; in a game they are in the scene.
	var subtitle_label := Label.new()
	subtitle_label.name = "SubtitleLabel"
	add_child(subtitle_label)
	var tutorial_label := Label.new()
	tutorial_label.name = "TutorialLabel"
	add_child(tutorial_label)
	$SubtitleLabel.set_translation_domain(&"subtitles")
	$TutorialLabel.set_translation_domain(&"tutorial")
	TranslationServer.set_locale_for(&"tutorial", "fr")
	print("Tutorial label locale: ", TranslationServer.get_locale_for($TutorialLabel.get_translation_domain()))

	# Store the player's choice. Another file than the game's user://settings/language.ini, so other scenes start clean.
	LanguageSettingsServer.set_file_path("user://locale_channels/language.ini")
	LanguageSettingsServer.set_locale("en")
	LanguageSettingsServer.set_locale_for(&"interface", "de")
	LanguageSettingsServer.set_locale_for(&"audio", "ja")
	LanguageSettingsServer.set_locale_for(&"subtitles", "fr")
	print(LanguageSettingsServer.get_locale_for(&"audio"))
	print(TranslationServer.get_locale_for(&"audio"))
	LanguageSettingsServer.set_locale_for(&"audio", "")
	LanguageSettingsServer.save()

	# A complete example: a language menu. Picking an entry needs user input; the filled lists are printed.
	var menu: Control = LanguageMenu.new()
	for list_name: String in MENU_LISTS:
		var list := OptionButton.new()
		list.name = list_name
		menu.add_child(list)
	add_child(menu)
	for list_name: String in MENU_LISTS:
		var list: OptionButton = menu.get_node(list_name)
		print(list_name, ": ", list.get_item_text(list.selected), " of ", list.item_count, " entries")

	LanguageSettingsServer.reset_to_defaults()
	TranslationServer.clear_locale_for(&"tutorial")
	get_tree().quit()
