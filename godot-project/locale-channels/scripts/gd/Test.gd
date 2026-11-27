extends FeatureTest
## Full API test of Locale Channels: the channels of TranslationServer, the channel of catalogs, the channel locales of
## LanguageSettingsServer and LanguageSettings, and ResourceLocalizationServer. Same tests as scripts/cs/Test.cs.
## The project has two translations (de, fr, see internationalization/locale/translations).
## Expected errors in the output: a custom channel in LanguageSettingsServer.set_locale_for(), the missing file logo_fr.res.

const DATA_DIR := "user://locale_channels_gd"
const REMAPS_SETTING := "internationalization/locale/translation_remaps"
const VOICE := "res://voice/line_1.ogg"
const LOGO := "res://ui/logo.png"

var _events: Array = []
var _translation_changed: int = 0


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DATA_DIR + "/mods/polish/voice")
	var previous_locale: String = TranslationServer.get_locale()
	LanguageSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	LanguageSettingsServer.set_file_path(DATA_DIR + "/language.ini")
	TextLocalizationServer.clear_catalogs()
	ResourceLocalizationServer.clear_catalogs()
	TranslationServer.set_locale("en")
	_test_channels()
	_test_locale_changed()
	_test_custom_channel()
	_test_catalog_channel()
	_test_text_channels()
	await _test_translation_changed_notification()
	_test_available_locales()
	_test_language_settings_resource()
	_test_language_settings_channels()
	_test_language_settings_custom_channel()
	_test_save_and_load()
	_test_file_without_channels()
	_test_resource_channels()
	_test_resource_paths()
	await _test_resource_relocalize()
	await _test_resource_mod()
	_test_resource_remaps()
	TextLocalizationServer.clear_catalogs()
	ResourceLocalizationServer.clear_catalogs()
	await get_tree().process_frame
	LanguageSettingsServer.reset_to_defaults()
	TranslationServer.set_locale(previous_locale)


func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED:
		_translation_changed += 1


func _on_locale_changed(channel: StringName, locale: String, previous_locale: String) -> void:
	_events.append([channel, locale, previous_locale])


func _clear_channels() -> void:
	for channel: StringName in TranslationServer.get_locale_channels():
		TranslationServer.clear_locale_for(channel)


func _ui_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "en", "Hello")
	catalog.set_text(&"HELLO", "de", "Hallo")
	return catalog


func _subtitle_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.channel = &"subtitles"
	catalog.set_text(&"LINE", "en", "Where are we?")
	catalog.set_text(&"LINE", "de", "Wo sind wir?")
	catalog.set_text(&"LINE", "es", "¿Dónde estamos?")
	return catalog


# A voice line (a Resource named after its locale) and a logo (ImageTexture, 8 px wide in English, 16 px in German).
func _write_resource_files() -> void:
	for locale: String in ["en", "de", "pl"]:
		var voice := Resource.new()
		voice.resource_name = locale
		var path: String = DATA_DIR + "/voice_%s.tres" % locale
		if locale == "en":
			path = DATA_DIR + "/voice.tres"
		if locale == "pl":
			path = DATA_DIR + "/mods/polish/voice/voice_pl.tres"
		ResourceSaver.save(voice, path)
	var logo := ImageTexture.create_from_image(Image.create(8, 4, false, Image.FORMAT_RGBA8))
	ResourceSaver.save(logo, DATA_DIR + "/logo.res")
	var logo_de := ImageTexture.create_from_image(Image.create(16, 4, false, Image.FORMAT_RGBA8))
	ResourceSaver.save(logo_de, DATA_DIR + "/logo_de.res")


func _test_channels() -> void:
	var channels: PackedStringArray = TranslationServer.get_locale_channels()
	expect_equal(channels, PackedStringArray(["interface", "audio", "subtitles"]), "get_locale_channels lists the three default channels")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "en", "a channel without its own locale follows the general locale")
	TranslationServer.set_locale_for(&"interface", "de")
	TranslationServer.set_locale_for(&"audio", "ja")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "de", "set_locale_for sets the interface locale")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "ja", "set_locale_for sets the audio locale")
	expect_equal(TranslationServer.get_locale(), "en", "set_locale_for leaves the general locale")
	TranslationServer.set_locale("fr")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "de", "a channel locale survives a change of the general locale")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "fr", "a following channel moves with the general locale")
	TranslationServer.clear_locale_for(&"audio")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "fr", "clear_locale_for lets the channel follow again")
	TranslationServer.set_locale("en")
	_clear_channels()


func _test_locale_changed() -> void:
	TranslationServer.locale_changed.connect(_on_locale_changed)
	TranslationServer.set_locale_for(&"interface", "de")
	TranslationServer.set_locale("fr")
	TranslationServer.clear_locale_for(&"interface")
	TranslationServer.locale_changed.disconnect(_on_locale_changed)
	expect_equal(_events[0], [&"interface", "de", "en"], "locale_changed(channel, locale, previous_locale) for a channel")
	expect_equal(_events[1], [&"", "fr", "en"], "locale_changed has the channel &\"\" for the general locale")
	expect_equal(_events.back(), [&"interface", "fr", "de"], "locale_changed for clear_locale_for")
	TranslationServer.set_locale("en")


func _test_custom_channel() -> void:
	TranslationServer.set_locale_for(&"tutorial", "pl")
	expect(TranslationServer.get_locale_channels().has("tutorial"), "a custom channel is listed once set")
	expect_equal(TranslationServer.get_locale_for(&"tutorial"), "pl", "a custom channel has its own locale")
	TranslationServer.clear_locale_for(&"tutorial")
	expect_equal(TranslationServer.get_locale_for(&"tutorial"), "en", "a cleared custom channel follows the general locale")


func _test_catalog_channel() -> void:
	var text_catalog := TextLocalizationCatalog.new()
	expect_equal(text_catalog.channel, &"interface", "a text catalog is in the interface channel by default")
	var resource_catalog := ResourceLocalizationCatalog.new()
	expect_equal(resource_catalog.channel, &"", "a resource catalog decides by key type by default")
	for extension: String in ["json", "lcat"]:
		var path: String = DATA_DIR + "/subtitles." + extension
		_subtitle_catalog().save_to_file(path)
		var loaded := TextLocalizationCatalog.new()
		loaded.load_from_file(path)
		expect_equal(loaded.channel, &"subtitles", "channel round-trips through ." + extension)
	var tres_path: String = DATA_DIR + "/subtitles.tres"
	ResourceSaver.save(_subtitle_catalog(), tres_path)
	var tres: TextLocalizationCatalog = ResourceLoader.load(tres_path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(tres.channel, &"subtitles", "channel round-trips through .tres")


func _test_text_channels() -> void:
	TextLocalizationServer.add_catalog(_ui_catalog())
	TextLocalizationServer.add_catalog(_subtitle_catalog())
	TranslationServer.set_locale_for(&"interface", "de")
	expect_equal(tr(&"HELLO"), "Hallo", "tr() follows the interface channel")
	expect_equal(TextLocalizationServer.translate(&"HELLO"), "Hallo", "translate() uses the channel of the key")
	var subtitles_domain: TranslationDomain = TranslationServer.get_or_add_domain(&"subtitles")
	expect_equal(subtitles_domain.translate(&"LINE"), &"Where are we?", "the domain subtitles follows the subtitles channel")
	expect_equal(tr(&"LINE"), "LINE", "a subtitle key is not in the main domain")
	var node := Node.new()
	node.set_translation_domain(&"subtitles")
	expect_equal(node.tr(&"LINE"), "Where are we?", "the translation domain of a node picks the channel")
	node.free()
	TranslationServer.set_locale_for(&"subtitles", "de")
	expect_equal(subtitles_domain.translate(&"LINE"), &"Wo sind wir?", "a subtitles channel change changes the subtitles")
	expect_equal(tr(&"HELLO"), "Hallo", "a subtitles channel change leaves the interface texts")
	TranslationServer.clear_locale_for(&"subtitles")
	expect_equal(TextLocalizationServer.translate(&"LINE"), "Where are we?", "a cleared channel follows the general locale")
	_clear_channels()


func _test_translation_changed_notification() -> void:
	await get_tree().process_frame
	var before: int = _translation_changed
	TranslationServer.set_locale_for(&"audio", "de")
	await get_tree().process_frame
	expect_equal(_translation_changed, before, "an audio-only change sends no NOTIFICATION_TRANSLATION_CHANGED")
	TranslationServer.set_locale_for(&"subtitles", "de")
	await get_tree().process_frame
	expect(_translation_changed > before, "a subtitles change sends NOTIFICATION_TRANSLATION_CHANGED")
	_clear_channels()


func _test_available_locales() -> void:
	expect_equal(TextLocalizationServer.get_available_locales_for(&"subtitles"), PackedStringArray(["en", "de", "es"]), "TextLocalizationServer.get_available_locales_for lists the locales of the channel in catalog order")
	expect(TextLocalizationServer.get_available_locales_for(&"audio").is_empty(), "TextLocalizationServer.get_available_locales_for a channel without catalogs is empty")
	expect_equal(LanguageSettingsServer.get_available_locales_for(&"subtitles"), PackedStringArray(["de", "en", "es"]), "LanguageSettingsServer.get_available_locales_for is sorted")
	var interface_locales: PackedStringArray = LanguageSettingsServer.get_available_locales_for(&"interface")
	expect(interface_locales.has("de"), "interface: the locales of the catalogs")
	expect(interface_locales.has("fr"), "interface: also the locales of the project translations")
	expect(not interface_locales.has("es"), "interface: not a subtitle-only locale")
	expect(LanguageSettingsServer.get_available_locales().has("es"), "get_available_locales has the locales of all catalogs")
	expect_equal(LanguageSettingsServer.get_available_locales_for(&"audio"), LanguageSettingsServer.get_available_locales(), "a channel no catalog declares offers all locales")
	TextLocalizationServer.clear_catalogs()


func _test_language_settings_resource() -> void:
	var settings := LanguageSettings.new()
	expect_equal(settings.interface_locale, "", "LanguageSettings.interface_locale is empty by default (same as language)")
	expect_equal(settings.audio_locale, "", "LanguageSettings.audio_locale is empty by default")
	expect_equal(settings.subtitles_locale, "", "LanguageSettings.subtitles_locale is empty by default")
	settings.interface_locale = "de"
	settings.audio_locale = "ja"
	settings.subtitles_locale = "fr"
	expect_equal(settings.interface_locale, "de", "LanguageSettings.interface_locale is a plain property")
	expect_equal(settings.audio_locale, "ja", "LanguageSettings.audio_locale is a plain property")
	expect_equal(settings.subtitles_locale, "fr", "LanguageSettings.subtitles_locale is a plain property")
	LanguageSettingsServer.reset_to_defaults()
	var names: PackedStringArray = LanguageSettingsServer.get_setting_names()
	for setting: String in ["interface_locale", "audio_locale", "subtitles_locale"]:
		expect(names.has(setting), "LanguageSettingsServer has the setting " + setting)
		expect_equal(LanguageSettingsServer.get_default_value(setting), "", "the default of %s is empty" % setting)


func _test_language_settings_channels() -> void:
	LanguageSettingsServer.set_locale("fr")
	expect_equal(LanguageSettingsServer.get_locale_for(&"audio"), "", "get_locale_for is empty for a channel that follows the language")
	expect_equal(LanguageSettingsServer.get_locale_for(&""), "fr", "get_locale_for(&\"\") is the language")
	LanguageSettingsServer.set_locale_for(&"interface", "de")
	LanguageSettingsServer.set_locale_for(&"audio", "de")
	LanguageSettingsServer.set_locale_for(&"subtitles", "fr")
	expect_equal(LanguageSettingsServer.get_locale_for(&"interface"), "de", "get_locale_for returns the stored interface locale")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "de", "set_locale_for applies the interface locale")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "de", "set_locale_for applies the audio locale")
	expect_equal(TranslationServer.get_locale(), "fr", "set_locale_for leaves the language")
	expect_equal(LanguageSettingsServer.get_value(&"audio_locale"), "de", "the setting audio_locale has the value")
	LanguageSettingsServer.set_locale_for(&"audio", "")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "fr", "an empty channel locale follows the language again")
	LanguageSettingsServer.set_locale("de")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "de", "a language change moves the channels that follow it")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "fr", "a channel with its own locale stays")
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(TranslationServer.get_locale_for(&"interface"), TranslationServer.get_locale(), "reset_to_defaults clears the channels")


func _test_language_settings_custom_channel() -> void:
	LanguageSettingsServer.set_locale("fr")
	LanguageSettingsServer.set_locale_for(&"tutorial", "de")
	expect_equal(LanguageSettingsServer.get_locale_for(&"tutorial"), "", "a custom channel is not stored")
	expect_equal(TranslationServer.get_locale_for(&"tutorial"), "fr", "a custom channel follows the language")
	LanguageSettingsServer.reset_to_defaults()


func _test_save_and_load() -> void:
	LanguageSettingsServer.set_locale("fr")
	LanguageSettingsServer.set_locale_for(&"interface", "de")
	LanguageSettingsServer.set_locale_for(&"audio", "de")
	expect_equal(LanguageSettingsServer.save(), OK, "save writes the channels")
	var text: String = FileAccess.get_file_as_string(DATA_DIR + "/language.ini")
	expect(text.contains("interface_locale"), "the file has interface_locale")
	expect(text.contains("audio_locale"), "the file has audio_locale")
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(LanguageSettingsServer.load(), OK, "load reads the file")
	expect_equal(LanguageSettingsServer.apply(), OK, "apply uses the loaded values")
	expect_equal(LanguageSettingsServer.get_locale_for(&"interface"), "de", "interface_locale is loaded")
	expect_equal(LanguageSettingsServer.get_locale_for(&"subtitles"), "", "subtitles_locale stays empty")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "de", "the loaded interface locale is applied")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "fr", "the subtitles follow the loaded language")
	LanguageSettingsServer.reset_to_defaults()


func _test_file_without_channels() -> void:
	var path: String = DATA_DIR + "/before_channels.json"
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	file.store_string("{ \"locale\": \"fr\" }")
	file.close()
	LanguageSettingsServer.set_file_path(path)
	expect_equal(LanguageSettingsServer.load(), OK, "a file written before the channels loads")
	LanguageSettingsServer.apply()
	expect_equal(LanguageSettingsServer.get_locale(), "fr", "old file: the language is loaded")
	for channel: StringName in [&"interface", &"audio", &"subtitles"]:
		expect_equal(TranslationServer.get_locale_for(channel), "fr", "old file: %s follows the language" % channel)
	LanguageSettingsServer.set_file_path(DATA_DIR + "/language.ini")
	LanguageSettingsServer.reset_to_defaults()


func _test_resource_channels() -> void:
	TranslationServer.set_locale("en")
	TranslationServer.set_locale_for(&"interface", "de")
	TranslationServer.set_locale_for(&"audio", "ja")
	var catalog := ResourceLocalizationCatalog.new()
	catalog.set_expected_type(VOICE, "AudioStream")
	catalog.set_localized_path(VOICE, "de", "res://voice/line_1_de.ogg")
	catalog.set_localized_path(VOICE, "ja", "res://voice/line_1_ja.ogg")
	catalog.set_localized_path(LOGO, "de", "res://ui/logo_de.png")
	catalog.set_localized_path(LOGO, "ja", "res://ui/logo_ja.png")
	ResourceLocalizationServer.add_catalog(catalog)
	expect_equal(ResourceLocalizationServer.get_localized_path(VOICE), "res://voice/line_1_ja.ogg", "an AudioStream follows the audio channel")
	expect_equal(ResourceLocalizationServer.get_localized_path(LOGO), "res://ui/logo_de.png", "other resources follow the interface channel")
	TranslationServer.clear_locale_for(&"audio")
	expect_equal(ResourceLocalizationServer.get_localized_path(VOICE), VOICE, "a cleared audio channel follows the general locale (en: the original)")
	ResourceLocalizationServer.clear_catalogs()
	_clear_channels()


func _test_resource_paths() -> void:
	_write_resource_files()
	var voice_path: String = DATA_DIR + "/voice.tres"
	var logo_path: String = DATA_DIR + "/logo.res"
	var other_path: String = DATA_DIR + "/other.tres"
	var game := ResourceLocalizationCatalog.new()
	game.set_localized_path(voice_path, "de", DATA_DIR + "/voice_de.tres")
	game.set_localized_path(logo_path, "de", DATA_DIR + "/logo_de.res")
	game.set_localized_path(logo_path, "fr", DATA_DIR + "/logo_fr.res")
	ResourceLocalizationServer.add_catalog(game)
	expect(ResourceLocalizationServer.has_localization(voice_path), "has_localization is true for a key")
	expect(not ResourceLocalizationServer.has_localization(other_path), "has_localization is false for another path")
	expect_equal(ResourceLocalizationServer.get_localized_path(voice_path), voice_path, "get_localized_path without an English entry is the original")
	expect_equal(ResourceLocalizationServer.get_localized_path(voice_path, "de_DE"), DATA_DIR + "/voice_de.tres", "get_localized_path for de_DE takes de")
	expect_equal(ResourceLocalizationServer.get_localized_path(other_path, "de"), other_path, "get_localized_path of an unknown path is the path")
	var paths: Dictionary = ResourceLocalizationServer.get_localized_paths(logo_path)
	expect_equal(paths.size(), 2, "get_localized_paths lists two locales")
	expect_equal(paths["de"], DATA_DIR + "/logo_de.res", "get_localized_paths has the de path")
	expect_equal(paths["fr"], DATA_DIR + "/logo_fr.res", "get_localized_paths has the fr path")
	expect_equal(ResourceLocalizationServer.get_missing_locales(voice_path), PackedStringArray(["fr"]), "get_missing_locales lists the locales without a path")


func _test_resource_relocalize() -> void:
	var voice_path: String = DATA_DIR + "/voice.tres"
	var logo_path: String = DATA_DIR + "/logo.res"
	var voice: Resource = load(voice_path)
	var logo: Texture2D = load(logo_path)
	expect_equal(voice.resource_name, "en", "load() gives the English voice")
	expect_equal(logo.get_width(), 8, "load() gives the English logo")
	TranslationServer.set_locale("de")
	var paths: PackedStringArray = await ResourceLocalizationServer.resources_relocalized
	expect(paths.has(voice_path), "resources_relocalized lists the voice")
	expect(paths.has(logo_path), "resources_relocalized lists the logo")
	expect(not ResourceLocalizationServer.is_relocalizing(), "is_relocalizing is false after the signal")
	expect_equal(voice.resource_name, "de", "the cached voice is updated in place")
	expect_equal(logo.get_width(), 16, "the cached logo is updated in place")
	expect(load(voice_path) == voice, "load() returns the same instance")
	TranslationServer.set_locale("fr")
	await ResourceLocalizationServer.resources_relocalized
	expect_equal(logo.get_width(), 8, "a missing localized file falls back to the original")


func _test_resource_mod() -> void:
	var voice: Resource = load(DATA_DIR + "/voice.tres")
	var mod := ResourceLocalizationCatalog.new()
	mod.set_localized_path(DATA_DIR + "/voice.tres", "pl", DATA_DIR + "/mods/polish/voice/voice_pl.tres")
	expect_equal(mod.save_to_file(DATA_DIR + "/mods/polish/voice/voice.lcat"), OK, "the mod catalog is saved as .lcat")
	var manifest := FilesManifest.new()
	manifest.type_hint = &"ResourceLocalizationCatalog"
	manifest.folder_paths = PackedStringArray([DATA_DIR + "/mods/*/voice"])
	expect_equal(ResourceLocalizationServer.add_catalog_manifest(manifest, 100), 1, "add_catalog_manifest adds the mod catalog")
	TranslationServer.set_locale("pl")
	await ResourceLocalizationServer.resources_relocalized
	expect_equal(voice.resource_name, "pl", "the Polish voice comes from the mod")
	expect(ResourceLocalizationServer.get_available_locales().has("pl"), "get_available_locales has the locale of the mod")


func _test_resource_remaps() -> void:
	ResourceLocalizationServer.clear_catalogs()
	var voice_path: String = DATA_DIR + "/voice.tres"
	var previous_remaps: Variant = ProjectSettings.get_setting(REMAPS_SETTING, {})
	ProjectSettings.set_setting(REMAPS_SETTING, { voice_path: PackedStringArray([DATA_DIR + "/voice_de.tres:de"]) })
	expect_equal(ResourceLocalizationServer.load_project_catalogs(), 1, "load_project_catalogs imports translation_remaps")
	expect_equal(ResourceLocalizationServer.get_localized_path(voice_path, "de"), DATA_DIR + "/voice_de.tres", "a translation_remaps entry is resolved")
	ProjectSettings.set_setting(REMAPS_SETTING, previous_remaps)
