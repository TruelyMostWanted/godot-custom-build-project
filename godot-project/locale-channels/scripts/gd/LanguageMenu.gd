extends Control
## The language menu of the wiki guide Locale Channels: one OptionButton per channel.
## Without class_name: the example preloads it, so the name stays free for games.


func _ready() -> void:
	_fill($LanguageList, &"")
	_fill($InterfaceList, &"interface")
	_fill($AudioList, &"audio")
	_fill($SubtitlesList, &"subtitles")


func _fill(list: OptionButton, channel: StringName) -> void:
	list.clear()
	list.add_item("System language" if channel == &"" else "Same as language")
	list.set_item_metadata(0, "")
	for locale: String in LanguageSettingsServer.get_available_locales_for(channel):
		list.add_item(TranslationServer.get_locale_name(locale))
		list.set_item_metadata(list.item_count - 1, locale)
		if locale == LanguageSettingsServer.get_locale_for(channel):
			list.select(list.item_count - 1)
	list.item_selected.connect(func(index: int) -> void:
		LanguageSettingsServer.set_locale_for(channel, list.get_item_metadata(index))
		LanguageSettingsServer.save())
