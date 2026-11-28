extends FeatureTest
## Full API test of the Locale Option Button (LocaleOptionButton): native names, project locales, fallback, channels. Same tests as scripts/cs/Test.cs.

var _selected: Array[String] = []


func _on_locale_selected(locale: String) -> void:
	_selected.append(locale)


func _run_tests() -> void:
	var original_locale := TranslationServer.get_locale()
	var translations: Array[Translation] = []
	for locale in ["fr", "ja", "de_DE", "de_AT"]:
		var translation := Translation.new()
		translation.locale = locale
		TranslationServer.add_translation(translation)
		translations.append(translation)

	# Native names.
	expect(LocaleOptionButton.get_native_name("de") == "Deutsch", "native name of de")
	expect(LocaleOptionButton.get_native_name("fr") == "Français", "native name of fr")
	expect(LocaleOptionButton.get_native_name("ja") == "日本語", "native name of ja")
	expect(LocaleOptionButton.get_native_name("pt_BR") == "Português (Brasil)", "native name of pt_BR")
	expect(LocaleOptionButton.get_native_name("de_AT", true).begins_with("Deutsch ("), "region is appended on request")

	# The list shows the locales of the project.
	var menu := LocaleOptionButton.new()
	add_child(menu)
	var locales := menu.get_locales()
	expect(locales.has("fr") and locales.has("ja"), "the list has the translations of the project")
	expect(menu.item_count == locales.size(), "one entry per locale")
	expect(menu.get_item_text(locales.find("fr")) == "Français", "entries show native names")
	expect(menu.get_item_text(locales.find("de_AT")) != menu.get_item_text(locales.find("de_DE")), "locales of one language are told apart")
	menu.show_native_names = false
	expect(menu.get_item_text(menu.get_locales().find("fr")) == TranslationServer.get_locale_name("fr"), "show_native_names off shows engine names")
	menu.show_native_names = true

	# The fallback locale.
	var fallback := TranslationServer.get_fallback_locale()
	menu.include_fallback = true
	expect(menu.get_locales().has(fallback), "include_fallback lists the fallback locale")
	menu.include_fallback = false

	# Selecting sets the locale of the target; the selection follows locale_changed.
	menu.locale_selected.connect(_on_locale_selected)
	var subtitles := LocaleOptionButton.new()
	subtitles.target = &"subtitles"
	add_child(subtitles)
	TranslationServer.set_locale("fr")
	expect(menu.get_selected_locale() == "fr", "the selection follows the language")
	menu.item_selected.emit(menu.get_locales().find("ja"))
	expect(TranslationServer.get_locale() == "ja", "selecting sets the language")
	expect(menu.get_selected_locale() == "ja", "the selection shows the language")
	expect(_selected == ["ja"], "locale_selected is emitted")
	subtitles.item_selected.emit(subtitles.get_locales().find("fr"))
	expect(TranslationServer.get_locale_for(&"subtitles") == "fr", "the subtitles channel is set alone")
	expect(TranslationServer.get_locale() == "ja", "the language stays")
	expect(menu.get_selected_locale() == "ja", "the general menu keeps its entry")
	TranslationServer.clear_locale_for(&"subtitles")
	expect(subtitles.get_selected_locale() == "ja", "a channel without own locale follows the language")

	menu.queue_free()
	subtitles.queue_free()
	for translation in translations:
		TranslationServer.remove_translation(translation)
	for channel in ["interface", "audio", "subtitles"]:
		LanguageSettingsServer.set_locale_for(channel, "")
	LanguageSettingsServer.set_locale("")
	TranslationServer.set_locale(original_locale)
