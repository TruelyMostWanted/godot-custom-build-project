extends FeatureTest
## Full API test of Localization Catalogs (LocalizationCatalog, TextLocalizationCatalog, ResourceLocalizationCatalog).
## Same tests as scripts/cs/Test.cs. Notes, entries, validate() and to_po() are tested in localization-editor,
## the channel in locale-channels.

const PLURAL_RULE := "nplurals=2; plural=(n != 1);"
# The rules of Godot's CSV translation importer: ?context, ?plural with a continuation row, ?pluralrule row,
# \n unescaped, a continuation row without a plural key is ignored.
const CATALOG_CSV := """keys,?context,?plural,en,de
HELLO,,,Hello,Hallo
HELLO,menu,,Hello (menu),Hallo (Menü)
?pluralrule,,,,nplurals=2; plural=(n != 1);
APPLE,,APPLES,%d apple,%d Apfel
,,,%d apples,%d Äpfel
LINE,,,One\\nTwo,Eins\\nZwei
,,,ignored row,
BYE,,,Bye,
"""
const FORMATS: Dictionary[String, int] = {
	"JSON": ResourceTextSerializer.FORMAT_JSON,
	"INI": ResourceTextSerializer.FORMAT_INI,
	"CSV": ResourceTextSerializer.FORMAT_CSV,
	"XML": ResourceTextSerializer.FORMAT_XML,
	"TOML": ResourceTextSerializer.FORMAT_TOML,
	"YAML": ResourceTextSerializer.FORMAT_YAML,
}
const EXTENSIONS: Array[String] = ["lcat", "csv", "json", "yaml", "yml", "toml", "ini", "xml"]
const LAZY_PATH := "user://localization_catalogs_lazy.lcat"

var _loaded_locales: Array[String] = []


func _run_tests() -> void:
	_test_csv_rules()
	_test_editing()
	_test_text_formats()
	_test_files()
	_test_lazy_loading()
	_test_translations()
	_test_resource_catalog()
	_test_sample_resources()


func _make_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.load_from_text(CATALOG_CSV, ResourceTextSerializer.FORMAT_CSV)
	return catalog


func _expect_texts(catalog: TextLocalizationCatalog, label: String) -> void:
	expect_equal(catalog.get_text(&"HELLO", "de"), "Hallo", "%s: get_text" % label)
	expect_equal(catalog.get_text(&"HELLO", "de", &"menu"), "Hallo (Menü)", "%s: get_text with a context" % label)
	expect_equal(catalog.get_text(&"LINE", "en"), "One\nTwo", "%s: \\n is a line break" % label)
	expect_equal(catalog.get_plural_text(&"APPLE", "de", 1), "%d Apfel", "%s: get_plural_text for 1" % label)
	expect_equal(catalog.get_plural_text(&"APPLE", "de", 5), "%d Äpfel", "%s: get_plural_text for 5" % label)
	expect_equal(catalog.get_plural_rule("de"), PLURAL_RULE, "%s: get_plural_rule" % label)


func _test_csv_rules() -> void:
	var catalog := TextLocalizationCatalog.new()
	var error: Error = catalog.load_from_text(CATALOG_CSV, ResourceTextSerializer.FORMAT_CSV)
	expect_equal(error, OK, "load_from_text(CSV) returns OK")
	expect_equal(catalog.key_count, 5, "key_count counts every key and context")
	expect_equal(catalog.locales, PackedStringArray(["en", "de"]), "locales keep the column order")
	_expect_texts(catalog, "CSV")
	expect_equal(catalog.get_plural_key(&"APPLE"), "APPLES", "get_plural_key")
	expect_equal(catalog.get_plural_texts(&"APPLE", "de"), PackedStringArray(["%d Apfel", "%d Äpfel"]), "get_plural_texts")
	expect_equal(catalog.get_plural_form("de", 1), 0, "get_plural_form for 1 is form 0")
	expect_equal(catalog.get_plural_form("de", 5), 1, "get_plural_form for 5 is form 1")
	expect_equal(catalog.get_text(&"BYE", "de"), "", "a missing translation is empty")
	expect_equal(catalog.get_translated_count("de"), 4, "get_translated_count")
	expect_equal(catalog.get_missing_rows("de"), PackedInt32Array([4]), "get_missing_rows")
	expect_equal(catalog.get_key(1), &"HELLO", "get_key of row 1")
	expect_equal(catalog.get_key_context(1), &"menu", "get_key_context of row 1")
	expect_equal(catalog.find_key(&"HELLO", &"menu"), 1, "find_key with a context")
	expect_equal(catalog.find_key(&"NOBODY"), -1, "find_key of an unknown key is -1")
	expect(catalog.has_key(&"HELLO", &"menu"), "has_key with a context")
	expect(not catalog.has_key(&"HELLO", &"other"), "has_key with another context is false")
	expect(catalog.has_locale("de"), "has_locale finds de")
	expect(not catalog.has_locale("fr"), "has_locale does not find fr")
	expect_equal(catalog.get_text(&"HELLO", "de_DE"), "", "a catalog does not fall back from de_DE to de")


func _test_editing() -> void:
	var catalog := TextLocalizationCatalog.new()
	catalog.add_locale("en")
	expect_equal(catalog.locales, PackedStringArray(["en"]), "add_locale adds a column")
	var row: int = catalog.add_key(&"A")
	expect_equal(row, 0, "add_key returns the new row")
	expect_equal(catalog.add_key(&"A"), 0, "add_key of an existing key returns its row")
	catalog.set_text(&"A", "en", "a")
	catalog.set_text(&"B", "en", "b")
	catalog.set_text(&"C", "de", "c")
	catalog.set_text(&"B", "en", "a much longer text")
	expect_equal(catalog.get_text(&"A", "en"), "a", "set_text: the cell before a grown cell is kept")
	expect_equal(catalog.get_text(&"B", "en"), "a much longer text", "set_text replaces a cell")
	expect_equal(catalog.get_text(&"C", "de"), "c", "set_text adds the locale de")
	catalog.remove_key(&"A")
	expect_equal(catalog.key_count, 2, "remove_key removes a key")
	expect_equal(catalog.find_key(&"B"), 0, "remove_key: the rows below move up")
	catalog.remove_locale("en")
	expect_equal(catalog.locales, PackedStringArray(["de"]), "remove_locale removes a column")
	for i: int in 1000:
		catalog.set_text("KEY_%d" % i, "de", str(i))
	expect_equal(catalog.get_text(&"KEY_777", "de"), "777", "the key index grows with many keys")
	expect_equal(catalog.get_text(&"C", "de"), "c", "the key index keeps the old keys")

	catalog.set_plural_key(&"ENEMY", "ENEMIES")
	catalog.set_plural_rule("de", PLURAL_RULE)
	catalog.set_plural_texts(&"ENEMY", "de", PackedStringArray(["%d Gegner", "%d Gegner (viele)"]))
	expect_equal(catalog.get_plural_key(&"ENEMY"), "ENEMIES", "set_plural_key")
	expect_equal(catalog.get_plural_rule("de"), PLURAL_RULE, "set_plural_rule")
	expect_equal(catalog.get_plural_text(&"ENEMY", "de", 2), "%d Gegner (viele)", "set_plural_texts")

	catalog.clear()
	expect_equal(catalog.key_count, 0, "clear removes every key")
	expect(catalog.locales.is_empty(), "clear removes every locale")


func _test_text_formats() -> void:
	var catalog := _make_catalog()
	var data: Dictionary = catalog.get("data")
	for label: String in FORMATS:
		var format: ResourceTextSerializer.Format = FORMATS[label]
		var text: String = catalog.to_text(format)
		var copy := TextLocalizationCatalog.new()
		var error: Error = copy.load_from_text(text, format)
		expect_equal(error, OK, "%s: load_from_text of to_text returns OK" % label)
		expect_equal(copy.get("data"), data, "%s: the data round-trips" % label)
		_expect_texts(copy, label)
	var duplicate: TextLocalizationCatalog = catalog.duplicate()
	expect_equal(duplicate.get_text(&"HELLO", "de", &"menu"), "Hallo (Menü)", "duplicate keeps the data")


func _test_files() -> void:
	expect_equal(LocalizationCatalog.get_file_extensions(), PackedStringArray(EXTENSIONS), "get_file_extensions")
	var catalog := _make_catalog()
	var data: Dictionary = catalog.get("data")
	for extension: String in EXTENSIONS:
		var path: String = "user://localization_catalogs_round_trip." + extension
		var saved: Error = catalog.save_to_file(path)
		expect_equal(saved, OK, ".%s: save_to_file returns OK" % extension)
		var copy := TextLocalizationCatalog.new()
		var loaded: Error = copy.load_from_file(path)
		expect_equal(loaded, OK, ".%s: load_from_file returns OK" % extension)
		expect_equal(copy.get("data"), data, ".%s: the data round-trips" % extension)
		DirAccess.remove_absolute(path)
	var json_path := "user://localization_catalogs_hint.json"
	catalog.save_to_file(json_path)
	var hinted: TextLocalizationCatalog = ResourceLoader.load(json_path, "TextLocalizationCatalog", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(hinted.get_text(&"HELLO", "de"), "Hallo", "load() of a text format with the catalog type hint")
	DirAccess.remove_absolute(json_path)


func _test_lazy_loading() -> void:
	var source := _make_catalog()
	source.save_to_file(LAZY_PATH)
	var catalog: TextLocalizationCatalog = ResourceLoader.load(LAZY_PATH, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(catalog.key_count, 5, ".lcat: load() reads the keys")
	expect_equal(catalog.locales, PackedStringArray(["en", "de"]), ".lcat: load() reads the locale table")
	expect(not catalog.is_locale_loaded("en"), ".lcat: en is not loaded after load()")
	expect(not catalog.is_locale_loaded("de"), ".lcat: de is not loaded after load()")
	expect_equal(catalog.get_translated_count("de"), 4, ".lcat: get_translated_count comes from the locale table")
	expect(not catalog.is_locale_loaded("de"), ".lcat: get_translated_count does not load de")

	catalog.locale_loaded.connect(_on_locale_loaded)
	expect_equal(catalog.get_text(&"HELLO", "de", &"menu"), "Hallo (Menü)", ".lcat: get_text loads the locale on first use")
	expect_equal(_loaded_locales, ["de"], ".lcat: locale_loaded is emitted with de")
	expect(not catalog.is_locale_loaded("en"), ".lcat: en stays unloaded")
	catalog.get_text(&"HELLO", "de")
	expect_equal(_loaded_locales.size(), 1, ".lcat: no second locale_loaded for a loaded locale")
	catalog.unload_locale("de")
	expect(not catalog.is_locale_loaded("de"), "unload_locale frees the locale")
	var error: Error = catalog.load_locale("de")
	expect_equal(error, OK, "load_locale returns OK")
	expect(catalog.is_locale_loaded("de"), "load_locale loads the locale")
	expect_equal(_loaded_locales, ["de", "de"], "load_locale emits locale_loaded")
	catalog.locale_loaded.disconnect(_on_locale_loaded)

	catalog.set_text(&"NEW", "en", "new")
	expect(catalog.is_locale_loaded("en"), ".lcat: a change loads every locale")
	expect_equal(catalog.get_text(&"BYE", "en"), "Bye", ".lcat: a change keeps the other cells")

	# The engine prints an error: the file holds a text catalog.
	var wrong := ResourceLocalizationCatalog.new()
	var refused: Error = wrong.load_from_file(LAZY_PATH)
	expect(refused != OK, "a ResourceLocalizationCatalog refuses a text catalog file")
	DirAccess.remove_absolute(LAZY_PATH)


func _test_translations() -> void:
	var catalog := _make_catalog()
	var translations: Array[Translation] = catalog.to_translations()
	expect_equal(translations.size(), 2, "to_translations returns one Translation per locale")
	var german: Translation = translations[1]
	expect_equal(german.locale, "de", "to_translations: the locale")
	expect_equal(german.get_message(&"HELLO"), &"Hallo", "to_translations: a message")
	expect_equal(german.get_message(&"HELLO", &"menu"), &"Hallo (Menü)", "to_translations: a message with a context")
	expect_equal(german.get_plural_message(&"APPLE", &"APPLES", 5), &"%d Äpfel", "to_translations: a plural message")
	expect_equal(german.plural_rules_override, PLURAL_RULE, "to_translations: the plural rule")

	var back := TextLocalizationCatalog.new()
	for translation: Translation in translations:
		back.add_translation(translation)
	expect_equal(back.get_text(&"HELLO", "de", &"menu"), "Hallo (Menü)", "add_translation keeps contexts")
	expect_equal(back.get_plural_texts(&"APPLE", "de").size(), 2, "add_translation keeps the plural forms")
	expect_equal(back.get_plural_rule("de"), PLURAL_RULE, "add_translation keeps the plural rule")
	expect_equal(back.get_text(&"LINE", "en"), "One\nTwo", "add_translation keeps line breaks")


func _test_resource_catalog() -> void:
	var remaps: Dictionary = {
		"res://icon.png": PackedStringArray(["res://icon_de.png:de", "res://icon_fr.png:fr"]),
		"res://music.ogg": PackedStringArray(["res://music_de.ogg:de"]),
	}
	var catalog := ResourceLocalizationCatalog.new()
	catalog.add_translation_remaps(remaps)
	expect_equal(catalog.get_localized_path(&"res://icon.png", "fr"), "res://icon_fr.png", "add_translation_remaps: a localized path")
	expect_equal(catalog.get_localized_path(&"res://music.ogg", "fr"), "", "a missing localized path is empty")
	expect_equal(catalog.to_translation_remaps(), remaps, "to_translation_remaps returns the added remaps")
	catalog.set_localized_path(&"res://music.ogg", "fr", "res://music_fr.ogg")
	expect_equal(catalog.get_localized_path(&"res://music.ogg", "fr"), "res://music_fr.ogg", "set_localized_path")
	catalog.set_expected_type(&"res://icon.png", &"Texture2D")
	expect_equal(catalog.get_expected_type(&"res://icon.png"), &"Texture2D", "set_expected_type")

	var data: Dictionary = catalog.get("data")
	for label: String in FORMATS:
		var format: ResourceTextSerializer.Format = FORMATS[label]
		var copy := ResourceLocalizationCatalog.new()
		var error: Error = copy.load_from_text(catalog.to_text(format), format)
		expect_equal(error, OK, "resource catalog %s: load_from_text returns OK" % label)
		expect_equal(copy.get("data"), data, "resource catalog %s: the data round-trips" % label)
		expect_equal(copy.get_expected_type(&"res://icon.png"), &"Texture2D", "resource catalog %s: the expected type" % label)


func _test_sample_resources() -> void:
	var texts: TextLocalizationCatalog = load("res://localization-catalogs/resources/ui_texts.tres")
	expect_equal(texts.get_text(&"menu.play", "de"), "Spielen", "the sample ui_texts.tres loads")
	var files: ResourceLocalizationCatalog = load("res://localization-catalogs/resources/voice_files.tres")
	expect_equal(files.get_localized_path(&"res://voice/intro.ogg", "de"), "res://voice/de/intro.ogg", "the sample voice_files.tres loads")


func _on_locale_loaded(locale: String) -> void:
	_loaded_locales.append(locale)
