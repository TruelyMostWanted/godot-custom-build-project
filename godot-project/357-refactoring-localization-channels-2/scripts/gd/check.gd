extends FeatureCheck
## GDScript checks of 357-refactoring-localization-channels-2. Check the same things as scripts/cs/Issue357RefactoringLocalizationChannels2Check.cs.
##
## Part 2 of the issue: locale channels in LanguageSettingsServer and translator notes in localization catalogs.
## The project has two translations (de, fr, see internationalization/locale/translations).
## Expected errors (printed, not script errors): a custom channel in set_locale_for(), the reserved note column "plural".

const DATA_DIR := "user://check_357_2"
const FORMAT_CLASSES := ["JSON", "INI", "CSV", "XML", "TOML", "YAML"]


func _get_check_name() -> String:
	return "357-refactoring-localization-channels-2"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DATA_DIR)
	var previous_locale := TranslationServer.get_locale()
	var previous_path := LanguageSettingsServer.get_file_path()
	var previous_mode := LanguageSettingsServer.apply_mode
	LanguageSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	TextLocalizationServer.clear_catalogs()

	_check_properties()
	_check_channels()
	_check_custom_channel()
	_check_save_and_load()
	_check_old_file()
	_check_availability()
	_check_notes()
	_check_note_formats()
	_check_note_files()

	# Clean up.
	TextLocalizationServer.clear_catalogs()
	LanguageSettingsServer.set_file_path(previous_path)
	LanguageSettingsServer.reset_to_defaults()
	for channel in ["interface", "audio", "subtitles"]:
		TranslationServer.clear_locale_for(channel)
	TranslationServer.set_locale(previous_locale)
	LanguageSettingsServer.apply_mode = previous_mode
	var dir := DirAccess.open(DATA_DIR)
	if dir:
		for file in dir.get_files():
			dir.remove(file)
	DirAccess.remove_absolute(DATA_DIR)


func _check_properties() -> void:
	LanguageSettingsServer.reset_to_defaults()
	var names := LanguageSettingsServer.get_setting_names()
	for setting in ["interface_locale", "audio_locale", "subtitles_locale"]:
		expect(names.has(setting), "setting " + setting)
		expect_equal(LanguageSettingsServer.get_default_value(setting), "", "default of %s is empty (same as language)" % setting)
	var settings := LanguageSettings.new()
	expect_equal(settings.interface_locale, "", "LanguageSettings: default interface_locale")
	settings.audio_locale = "de"
	settings.subtitles_locale = "fr"
	expect_equal(settings.audio_locale, "de", "LanguageSettings: audio_locale is a plain property")
	expect_equal(settings.subtitles_locale, "fr", "LanguageSettings: subtitles_locale is a plain property")


func _check_channels() -> void:
	LanguageSettingsServer.reset_to_defaults()
	LanguageSettingsServer.set_locale("fr")
	expect_equal(LanguageSettingsServer.get_locale_for(&"audio"), "", "get_locale_for: empty means the language")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "fr", "an empty channel follows the language")
	expect_equal(LanguageSettingsServer.get_locale_for(&""), "fr", "channel &\"\" is the language")
	LanguageSettingsServer.set_locale_for(&"interface", "de")
	LanguageSettingsServer.set_locale_for(&"audio", "de")
	LanguageSettingsServer.set_locale_for(&"subtitles", "fr")
	expect_equal(LanguageSettingsServer.get_locale_for(&"interface"), "de", "get_locale_for interface")
	expect_equal(LanguageSettingsServer.get_locale_for(&"audio"), "de", "get_locale_for audio")
	expect_equal(LanguageSettingsServer.get_locale_for(&"subtitles"), "fr", "get_locale_for subtitles")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "de", "TranslationServer interface follows")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "de", "TranslationServer audio follows")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "fr", "TranslationServer subtitles follows")
	expect_equal(TranslationServer.get_locale(), "fr", "the language is untouched")
	expect_equal(LanguageSettingsServer.get_value(&"audio_locale"), "de", "the setting audio_locale has the value")
	LanguageSettingsServer.set_locale_for(&"audio", "")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "fr", "an empty channel locale follows the language again")
	LanguageSettingsServer.set_locale("de")
	expect_equal(TranslationServer.get_locale_for(&"audio"), "de", "a language change moves the channels that follow it")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "fr", "a channel with its own locale stays")
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(TranslationServer.get_locale_for(&"interface"), TranslationServer.get_locale(), "reset_to_defaults() clears the channels")


func _check_custom_channel() -> void:
	# Expected error: only interface, audio and subtitles can be stored.
	LanguageSettingsServer.set_locale("fr")
	LanguageSettingsServer.set_locale_for(&"custom", "de")
	expect_equal(LanguageSettingsServer.get_locale_for(&"custom"), "", "a custom channel is refused, nothing is stored")
	expect_equal(TranslationServer.get_locale_for(&"custom"), "fr", "a custom channel follows the language")
	LanguageSettingsServer.reset_to_defaults()


func _check_save_and_load() -> void:
	var path := DATA_DIR + "/language.ini"
	LanguageSettingsServer.set_file_path(path)
	LanguageSettingsServer.reset_to_defaults()
	LanguageSettingsServer.set_locale("fr")
	LanguageSettingsServer.set_locale_for(&"interface", "de")
	LanguageSettingsServer.set_locale_for(&"audio", "de")
	expect_equal(LanguageSettingsServer.save(), OK, "save() writes the channels")
	var text := FileAccess.get_file_as_string(path)
	expect(text.contains("interface_locale") and text.contains("audio_locale"), "the file has the channel keys")
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(LanguageSettingsServer.get_locale_for(&"interface"), "", "after reset the channels are empty")
	expect_equal(LanguageSettingsServer.load(), OK, "load() reads the file")
	expect_equal(LanguageSettingsServer.apply(), OK, "apply() uses the loaded values")
	expect_equal(LanguageSettingsServer.get_locale(), "fr", "the language is loaded")
	expect_equal(LanguageSettingsServer.get_locale_for(&"interface"), "de", "interface_locale is loaded")
	expect_equal(LanguageSettingsServer.get_locale_for(&"audio"), "de", "audio_locale is loaded")
	expect_equal(LanguageSettingsServer.get_locale_for(&"subtitles"), "", "subtitles_locale stays empty")
	expect_equal(TranslationServer.get_locale_for(&"interface"), "de", "the loaded interface locale is applied")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "fr", "subtitles follow the language")
	LanguageSettingsServer.reset_to_defaults()


func _check_old_file() -> void:
	var path := DATA_DIR + "/before_channels.json"
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string("{ \"locale\": \"fr\" }")
	file.close()
	LanguageSettingsServer.set_file_path(path)
	LanguageSettingsServer.reset_to_defaults()
	expect_equal(LanguageSettingsServer.load(), OK, "an old file without channel keys loads")
	LanguageSettingsServer.apply()
	expect_equal(LanguageSettingsServer.get_locale(), "fr", "old file: the language is loaded")
	for channel in [&"interface", &"audio", &"subtitles"]:
		expect_equal(LanguageSettingsServer.get_locale_for(channel), "", "old file: channel %s is empty" % channel)
		expect_equal(TranslationServer.get_locale_for(channel), "fr", "old file: channel %s follows the language" % channel)
	LanguageSettingsServer.reset_to_defaults()


func _check_availability() -> void:
	var interface_texts := TextLocalizationCatalog.new()
	interface_texts.set_text(&"HELLO", "en", "Hello")
	interface_texts.set_text(&"HELLO", "de", "Hallo")
	var subtitles := TextLocalizationCatalog.new()
	subtitles.channel = &"subtitles"
	subtitles.set_text(&"LINE_1", "en", "Hi")
	subtitles.set_text(&"LINE_1", "es", "Hola")
	TextLocalizationServer.add_catalog(interface_texts)
	TextLocalizationServer.add_catalog(subtitles)

	expect_equal(TextLocalizationServer.get_available_locales_for(&"subtitles"), PackedStringArray(["en", "es"]), "TextLocalizationServer.get_available_locales_for(subtitles)")
	expect(TextLocalizationServer.get_available_locales_for(&"audio").is_empty(), "no catalog with audio keys: empty")
	expect_equal(LanguageSettingsServer.get_available_locales_for(&"subtitles"), PackedStringArray(["en", "es"]), "get_available_locales_for(subtitles) is sorted")
	var interface_locales := LanguageSettingsServer.get_available_locales_for(&"interface")
	expect(interface_locales.has("en") and interface_locales.has("de"), "interface: the catalog locales")
	expect(interface_locales.has("fr"), "interface: also the Godot translation locales")
	expect(not interface_locales.has("es"), "interface: not the subtitle-only locale")
	expect_equal(LanguageSettingsServer.get_available_locales_for(&"audio"), LanguageSettingsServer.get_available_locales(), "a channel no catalog declares: all locales")
	expect(LanguageSettingsServer.get_available_locales().has("es"), "get_available_locales() has the locales of all catalogs")

	LanguageSettingsServer.set_locale("en")
	LanguageSettingsServer.set_locale_for(&"subtitles", "es")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "es", "an available channel locale is used")
	LanguageSettingsServer.set_locale_for(&"subtitles", "de")
	expect_equal(TranslationServer.get_locale_for(&"subtitles"), "en", "an unavailable channel locale: the channel follows the language")
	expect_equal(LanguageSettingsServer.get_locale_for(&"subtitles"), "de", "the stored value is kept")
	TextLocalizationServer.clear_catalogs()
	LanguageSettingsServer.reset_to_defaults()


func _notes_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "en", "Hello")
	catalog.set_text(&"HELLO", "de", "Hallo")
	catalog.set_text(&"HELLO", "en", "Hello (menu)", &"menu")
	catalog.set_text(&"HELLO", "de", "Hallo (Menü)", &"menu")
	catalog.set_text(&"APPLE", "en", "Apple")
	catalog.set_text(&"APPLE", "de", "Apfel")
	catalog.set_text(&"LINE", "en", "One")
	catalog.set_text(&"LINE", "de", "Eins")
	catalog.set_note(&"HELLO", "greeting")
	catalog.set_note(&"HELLO", "Main menu button", &"menu")
	catalog.set_note(&"APPLE", "12", &"", &"max_length")
	catalog.set_note(&"LINE", "Two lines\nkeep it short")
	return catalog


func _check_notes() -> void:
	var empty := TextLocalizationCatalog.new()
	expect(empty.get_note_columns().is_empty(), "a new catalog has no note columns")
	var catalog := _notes_catalog()
	expect_equal(catalog.get_note_columns(), PackedStringArray(["notes", "max_length"]), "get_note_columns() without the underscore")
	expect_equal(catalog.get_note(&"HELLO"), "greeting", "get_note()")
	expect_equal(catalog.get_note(&"HELLO", &"menu"), "Main menu button", "get_note() with a context")
	expect_equal(catalog.get_note(&"APPLE", &"", &"max_length"), "12", "get_note() with a column")
	expect_equal(catalog.get_note(&"MISSING"), "", "no note: empty string")
	expect_equal(catalog.get_text(&"HELLO", "de"), "Hallo", "notes do not change the texts")
	# Expected error: the reserved column.
	catalog.set_note(&"HELLO", "x", &"", &"plural")
	expect_equal(catalog.get_note(&"HELLO", &"", &"plural"), "", "the reserved column plural is refused")
	expect_equal(catalog.get_note_columns().size(), 2, "the refused column is not added")


func _check_note_formats() -> void:
	var catalog := _notes_catalog()
	for format in ResourceTextSerializer.FORMAT_MAX:
		var format_class: String = FORMAT_CLASSES[format]
		if not ClassDB.class_exists(format_class):
			print("  SKIP  notes in %s (module disabled)" % format_class)
			continue
		var copy := TextLocalizationCatalog.new()
		expect_equal(copy.load_from_text(catalog.to_text(format), format), OK, "notes round trip: load_from_text " + format_class)
		expect_equal(copy.get_note(&"HELLO"), "greeting", "%s: note" % format_class)
		expect_equal(copy.get_note(&"HELLO", &"menu"), "Main menu button", "%s: note with a context" % format_class)
		expect_equal(copy.get_note(&"APPLE", &"", &"max_length"), "12", "%s: max_length column" % format_class)
		expect_equal(copy.get_note(&"LINE"), "Two lines\nkeep it short", "%s: note with a line break" % format_class)
		expect_equal(copy.get_text(&"HELLO", "de", &"menu"), "Hallo (Menü)", "%s: texts are kept" % format_class)
	var header := catalog.to_text(ResourceTextSerializer.FORMAT_CSV).get_slice("\n", 0).strip_edges().split(",")
	expect(header.find("_notes") > header.find("de") and header.find("_max_length") > header.find("_notes"), "CSV header: note columns after the locales: " + str(header))


func _check_note_files() -> void:
	var catalog := _notes_catalog()
	var lcat_path := DATA_DIR + "/notes.lcat"
	expect_equal(catalog.save_to_file(lcat_path), OK, "save .lcat")
	var from_lcat := TextLocalizationCatalog.new()
	expect_equal(from_lcat.load_from_file(lcat_path), OK, "load .lcat")
	expect(from_lcat.get_note_columns().is_empty(), ".lcat has no notes")
	expect_equal(from_lcat.get_note(&"HELLO"), "", ".lcat: get_note() is empty")
	expect_equal(from_lcat.get_text(&"HELLO", "de"), "Hallo", ".lcat: texts are kept")

	var csv_path := DATA_DIR + "/notes.csv"
	expect_equal(catalog.save_to_file(csv_path), OK, "save .csv")
	var loaded := ResourceLoader.load(csv_path, "TextLocalizationCatalog", ResourceLoader.CACHE_MODE_IGNORE) as TextLocalizationCatalog
	expect(loaded != null, "ResourceLoader loads the csv catalog")
	if loaded != null:
		expect_equal(loaded.get_text(&"HELLO", "de", &"menu"), "Hallo (Menü)", "ResourceLoader: texts are loaded")
		expect(loaded.get_note_columns().is_empty(), "ResourceLoader outside the editor skips the notes")
	var tool_catalog := TextLocalizationCatalog.new()
	expect_equal(tool_catalog.load_from_file(csv_path), OK, "load_from_file(csv)")
	expect_equal(tool_catalog.get_note(&"HELLO", &"menu"), "Main menu button", "load_from_file() keeps the notes")
