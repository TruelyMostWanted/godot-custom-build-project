extends FeatureTest
## Full API test of Localization Editor: entries, notes, validate(), CSV delimiters, POT/PO and the Inspector summary
## of LocalizationCatalog and TextLocalizationCatalog. Same tests as scripts/cs/Test.cs.
## Expected error in the output: the reserved note column "plural".

const DATA_DIR := "user://localization_editor_gd"
const CATALOG_CSV := "keys,?context,?plural,_notes,en,de\nHELLO,,,greeting,Hello,Hallo\nHELLO,menu,,,Hello (menu),Hallo (Menü)\n?pluralrule,,,,,nplurals=2; plural=(n != 1);\nAPPLE,,APPLES,Inventory,%d apple,%d Apfel\n,,,,%d apples,%d Äpfel\nBYE,,,,Bye,\n"
const DUPLICATE_CSV := "keys,en,de\nA,a,\nB,b,\nA,a2,\n"
const EMPTY_CSV := "keys,en,de\nA,,\nB,,\n"
const LINES_CSV := "keys,?plural,en,de\nA,,a,\"Zeile\nzwei\"\nAPPLE,APPLES,apple,\n,,apples,\nB,,b,\n"
const LINES_JSON := "{\n\t\"A\": {\n\t\t\"en\": \"a\",\n\t\t\"de\": \"a\"\n\t},\n\t\"B\": {\n\t\t\"en\": \"b\"\n\t}\n}\n"
const SEMICOLON_CSV := "keys;en;de\nHELLO;Hello, world;Hallo, Welt\n"
const TAB_CSV := "keys\ten\tde\nHELLO\tHello\tHallo\n"
const FORMAT_CLASSES: Array[String] = ["JSON", "INI", "CSV", "XML", "TOML", "YAML"]


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DATA_DIR)
	_test_get_entry()
	_test_set_entry()
	_test_rename()
	_test_notes()
	_test_note_formats()
	_test_note_files()
	_test_validate()
	_test_validate_options()
	_test_validate_lines()
	_test_delimiters()
	_test_po()
	_test_summary()


func _catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.load_from_text(CATALOG_CSV, ResourceTextSerializer.FORMAT_CSV)
	return catalog


func _notes_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "en", "Hello")
	catalog.set_text(&"HELLO", "de", "Hallo")
	catalog.set_text(&"HELLO", "de", "Hallo (Menü)", &"menu")
	catalog.set_text(&"APPLE", "de", "Apfel")
	catalog.set_text(&"LINE", "de", "Eins")
	catalog.set_note(&"HELLO", "greeting")
	catalog.set_note(&"HELLO", "Main menu button", &"menu")
	catalog.set_note(&"APPLE", "12", &"", &"max_length")
	catalog.set_note(&"LINE", "Two lines\nkeep it short")
	return catalog


func _write(path: String, text: String) -> void:
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text)
	file.close()


func _test_get_entry() -> void:
	var catalog: TextLocalizationCatalog = _catalog()
	var hello: Dictionary = catalog.get_entry(&"HELLO")
	expect_equal(hello.get("en"), "Hello", "get_entry has the text per locale")
	expect_equal(hello.get("_notes"), "greeting", "get_entry has the note")
	expect(not hello.has("_key"), "get_entry has no _key without a context")
	expect(not hello.has("_context"), "get_entry has no _context without a context")
	var hello_menu: Dictionary = catalog.get_entry(&"HELLO", &"menu")
	expect_equal(hello_menu.get("_context"), "menu", "get_entry with a context has _context")
	expect_equal(hello_menu.get("_key"), "HELLO", "get_entry with a context has _key")
	var apple: Dictionary = catalog.get_entry(&"APPLE")
	expect_equal(apple.get("_plural"), "APPLES", "get_entry has _plural")
	expect_equal(apple.get("de"), ["%d Apfel", "%d Äpfel"], "get_entry: plural forms are an Array")
	expect(catalog.get_entry(&"MISSING").is_empty(), "get_entry of a missing key is empty")


func _test_set_entry() -> void:
	var catalog: TextLocalizationCatalog = _catalog()
	var hello: Dictionary = catalog.get_entry(&"HELLO")
	catalog.set_entry(&"HELLO", { "de": "Servus", "fr": "Salut", "_notes": "informal" })
	expect_equal(catalog.get_text(&"HELLO", "de"), "Servus", "set_entry replaces a text")
	expect_equal(catalog.get_text(&"HELLO", "en"), "", "set_entry clears a field the entry lacks")
	expect_equal(catalog.get_text(&"HELLO", "fr"), "Salut", "set_entry adds a text")
	expect(catalog.has_locale("fr"), "set_entry adds the locale")
	expect_equal(catalog.get_note(&"HELLO"), "informal", "set_entry replaces the note")
	catalog.set_entry(&"HELLO", hello)
	expect_equal(catalog.get_text(&"HELLO", "en"), "Hello", "set_entry with the old entry restores a text (undo)")
	expect_equal(catalog.get_text(&"HELLO", "fr"), "", "set_entry with the old entry clears the added text (undo)")
	var apple: Dictionary = catalog.get_entry(&"APPLE")
	catalog.set_entry(&"APPLE", {})
	expect_equal(catalog.get_plural_key(&"APPLE"), "", "set_entry({}) clears the plural key")
	catalog.set_entry(&"APPLE", apple)
	expect_equal(catalog.get_plural_text(&"APPLE", "de", 5), "%d Äpfel", "set_entry restores the plural forms")
	expect_equal(catalog.get_plural_key(&"APPLE"), "APPLES", "set_entry restores the plural key")
	catalog.set_entry(&"NEW", { "fr": "Salut" }, &"ctx")
	expect_equal(catalog.get_text(&"NEW", "fr", &"ctx"), "Salut", "set_entry adds a new key with a context")


func _test_rename() -> void:
	var catalog: TextLocalizationCatalog = _catalog()
	var entry: Dictionary = catalog.get_entry(&"BYE")
	catalog.remove_key(&"BYE")
	catalog.set_entry(&"GOODBYE", entry)
	expect(not catalog.has_key(&"BYE"), "rename: the old key is gone")
	expect_equal(catalog.get_text(&"GOODBYE", "en"), "Bye", "rename: the texts are kept")
	expect_equal(catalog.find_key(&"GOODBYE"), catalog.key_count - 1, "rename: the key moves to the end")


func _test_notes() -> void:
	var empty := TextLocalizationCatalog.new()
	expect(empty.note_columns.is_empty(), "a new catalog has no note columns")
	var catalog: TextLocalizationCatalog = _notes_catalog()
	expect_equal(catalog.note_columns, PackedStringArray(["notes", "max_length"]), "note_columns lists the columns without the underscore")
	expect_equal(catalog.get_note(&"HELLO"), "greeting", "get_note reads the notes column")
	expect_equal(catalog.get_note(&"HELLO", &"menu"), "Main menu button", "get_note with a context")
	expect_equal(catalog.get_note(&"APPLE", &"", &"max_length"), "12", "get_note with a column")
	expect_equal(catalog.get_note(&"MISSING"), "", "get_note of a missing key is empty")
	expect_equal(catalog.get_text(&"HELLO", "de"), "Hallo", "notes do not change the texts")
	catalog.set_note(&"HELLO", "x", &"", &"plural")
	expect_equal(catalog.get_note(&"HELLO", &"", &"plural"), "", "set_note refuses the reserved column plural")
	expect_equal(catalog.note_columns.size(), 2, "a refused column is not added")
	catalog.set_note(&"LINE", "")
	expect(not catalog.get_entry(&"LINE").has("_notes"), "an empty note removes the note")


func _test_note_formats() -> void:
	var catalog: TextLocalizationCatalog = _notes_catalog()
	for format: int in ResourceTextSerializer.FORMAT_MAX:
		var format_class: String = FORMAT_CLASSES[format]
		if not ClassDB.class_exists(format_class):
			print("  SKIP  notes in %s (module disabled)" % format_class)
			continue
		var text: String = catalog.to_text(format)
		var copy := TextLocalizationCatalog.new()
		expect_equal(copy.load_from_text(text, format), OK, "%s: load_from_text of to_text returns OK" % format_class)
		expect_equal(copy.get_note(&"HELLO"), "greeting", "%s: the note round-trips" % format_class)
		expect_equal(copy.get_note(&"HELLO", &"menu"), "Main menu button", "%s: the note with a context round-trips" % format_class)
		expect_equal(copy.get_note(&"APPLE", &"", &"max_length"), "12", "%s: the max_length column round-trips" % format_class)
		expect_equal(copy.get_note(&"LINE"), "Two lines\nkeep it short", "%s: a note with a line break round-trips" % format_class)
		expect_equal(copy.get_text(&"HELLO", "de", &"menu"), "Hallo (Menü)", "%s: the texts are kept" % format_class)
	var csv: String = catalog.to_text(ResourceTextSerializer.FORMAT_CSV)
	var header: PackedStringArray = csv.get_slice("\n", 0).strip_edges().split(",")
	expect(header.find("_notes") > header.find("de"), "CSV header: the notes column comes after the locales")
	expect(header.find("_max_length") > header.find("_notes"), "CSV header: max_length comes after notes")


func _test_note_files() -> void:
	var catalog: TextLocalizationCatalog = _notes_catalog()
	var lcat_path: String = DATA_DIR + "/notes.lcat"
	expect_equal(catalog.save_to_file(lcat_path), OK, "save_to_file(.lcat) returns OK")
	var from_lcat := TextLocalizationCatalog.new()
	expect_equal(from_lcat.load_from_file(lcat_path), OK, "load_from_file(.lcat) returns OK")
	expect(from_lcat.note_columns.is_empty(), ".lcat stores no notes")
	expect_equal(from_lcat.get_text(&"HELLO", "de"), "Hallo", ".lcat keeps the texts")
	var csv_path: String = DATA_DIR + "/notes.csv"
	expect_equal(catalog.save_to_file(csv_path), OK, "save_to_file(.csv) returns OK")
	var loaded: TextLocalizationCatalog = ResourceLoader.load(csv_path, "TextLocalizationCatalog", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(loaded.get_text(&"HELLO", "de", &"menu"), "Hallo (Menü)", "ResourceLoader loads the texts")
	expect(loaded.note_columns.is_empty(), "ResourceLoader outside the editor skips the notes")
	var tool_catalog := TextLocalizationCatalog.new()
	tool_catalog.load_from_file(csv_path)
	expect_equal(tool_catalog.get_note(&"HELLO", &"menu"), "Main menu button", "load_from_file keeps the notes")


func _test_validate() -> void:
	var catalog: TextLocalizationCatalog = _catalog()
	var result: DataValidation = catalog.validate()
	expect_equal(result.get_issue_count(), 1, "validate finds the one missing text")
	expect_equal(result.get_issue_kind(0), DataValidation.ISSUE_KIND_EMPTY_REQUIRED, "a missing text is ISSUE_KIND_EMPTY_REQUIRED")
	expect_equal(result.get_issue_row(0), catalog.find_key(&"BYE"), "the issue row is the row of the key")
	expect_equal(result.get_issue_column(0), 1, "the issue column is the locale index")
	expect_equal(result.get_issue_field(0), &"de", "the issue field is the locale")
	expect_equal(result.get_issue_path(0), "BYE", "the issue path is the key")
	expect_equal(catalog.load_from_text(DUPLICATE_CSV, ResourceTextSerializer.FORMAT_CSV), OK, "a CSV with a duplicate key loads")
	expect_equal(catalog.key_count, 2, "duplicate keys are merged")
	result = catalog.validate()
	expect_equal(result.get_issue_count(), 3, "validate finds the duplicate and two missing texts")
	expect_equal(result.get_issue_kind(0), DataValidation.ISSUE_KIND_DUPLICATE_KEY, "the duplicate comes first as ISSUE_KIND_DUPLICATE_KEY")
	catalog.clear()
	expect(catalog.validate().is_valid(), "clear forgets the duplicates of the last load")
	var invalid := TextLocalizationCatalog.new()
	invalid.set_text(&"A", "qqq", "a")
	result = invalid.validate()
	expect_equal(result.get_issue_kind(0), DataValidation.ISSUE_KIND_INVALID_LOCALE, "an unknown locale is ISSUE_KIND_INVALID_LOCALE")


func _test_validate_options() -> void:
	var catalog := TextLocalizationCatalog.new()
	catalog.load_from_text(EMPTY_CSV, ResourceTextSerializer.FORMAT_CSV)
	var options := DataValidationOptions.new()
	options.stop_on_first = true
	expect_equal(catalog.validate(options).get_issue_count(), 1, "stop_on_first gives one issue")
	expect_equal(catalog.validate().get_issue_count(), 4, "the default options give all 4 issues")
	var limited := DataValidationOptions.new()
	limited.max_issues = 2
	expect_equal(catalog.validate(limited).get_issue_count(), 2, "max_issues limits the issues")


func _test_validate_lines() -> void:
	var csv_path: String = DATA_DIR + "/lines.csv"
	_write(csv_path, LINES_CSV)
	var catalog: TextLocalizationCatalog = ResourceLoader.load(csv_path, "TextLocalizationCatalog", ResourceLoader.CACHE_MODE_IGNORE)
	var result: DataValidation = catalog.validate()
	expect_equal(result.get_issue_count(), 2, "CSV lines: two issues")
	expect_equal(result.get_issue_path(0), "APPLE", "CSV lines: the first issue is APPLE")
	expect_equal(result.get_issue_line(0), 4, "CSV lines: APPLE is on line 4, after a quoted line break")
	expect_equal(result.get_issue_line(1), 6, "CSV lines: B is on line 6, after the plural row")
	var json_path: String = DATA_DIR + "/lines.json"
	_write(json_path, LINES_JSON)
	var json_catalog: TextLocalizationCatalog = ResourceLoader.load(json_path, "TextLocalizationCatalog", ResourceLoader.CACHE_MODE_IGNORE)
	result = json_catalog.validate()
	expect_equal(result.get_issue_line(0), 6, "JSON lines: B is on line 6")
	var text_catalog := TextLocalizationCatalog.new()
	text_catalog.load_from_text(LINES_CSV, ResourceTextSerializer.FORMAT_CSV)
	expect_equal(text_catalog.validate().get_issue_line(0), DataValidation.POSITION_NONE, "a catalog from load_from_text has no lines")


func _test_delimiters() -> void:
	var catalog := TextLocalizationCatalog.new()
	expect_equal(catalog.load_from_text(SEMICOLON_CSV, ResourceTextSerializer.FORMAT_CSV), OK, "a semicolon CSV loads")
	expect_equal(catalog.get_text(&"HELLO", "de"), "Hallo, Welt", "the semicolon is detected")
	expect_equal(catalog.load_from_text(TAB_CSV, ResourceTextSerializer.FORMAT_CSV), OK, "a tab CSV loads")
	expect_equal(catalog.get_text(&"HELLO", "de"), "Hallo", "the tab is detected")
	var options := SerializerOptions.new()
	options.csv_delimiter = ";"
	var text: String = catalog.to_text(ResourceTextSerializer.FORMAT_CSV, options)
	expect(text.begins_with("keys;en;de"), "to_text uses csv_delimiter of the options")
	var copy := TextLocalizationCatalog.new()
	expect_equal(copy.load_from_text(text, ResourceTextSerializer.FORMAT_CSV, options), OK, "load_from_text with options returns OK")
	expect_equal(copy.get_text(&"HELLO", "en"), "Hello", "the text round-trips with csv_delimiter")
	expect(options.csv_headers, "the options of the caller are not changed")


func _test_po() -> void:
	var catalog: TextLocalizationCatalog = _catalog()
	var pot: String = catalog.to_po()
	expect(pot.contains("msgctxt \"menu\""), "POT has msgctxt for the context")
	expect(pot.contains("msgid \"HELLO\""), "POT has msgid for the key")
	expect(pot.contains("msgid_plural \"APPLES\""), "POT has msgid_plural for the plural key")
	expect(pot.contains("#. greeting"), "POT has the note as a #. comment")
	expect(pot.contains("msgstr \"\""), "POT has empty msgstr")
	var po_path: String = DATA_DIR + "/de.po"
	_write(po_path, catalog.to_po("de"))
	var translation: Translation = ResourceLoader.load(po_path, "Translation", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(translation.locale, "de", "the PO file has the locale")
	expect_equal(translation.get_message(&"HELLO"), &"Hallo", "the PO file has the text")
	expect_equal(translation.get_message(&"HELLO", &"menu"), &"Hallo (Menü)", "the PO file has the text with a context")
	expect_equal(translation.get_plural_message(&"APPLE", &"APPLES", 5), &"%d Äpfel", "the PO file has the plural forms")


func _test_summary() -> void:
	var catalog: TextLocalizationCatalog = _catalog()
	var summary: String = str(catalog.get("summary/de"))
	expect(summary.contains("%)"), "the Inspector property summary/de has a percentage: " + summary)
	expect(summary.contains("missing"), "the Inspector property summary/de names the missing keys")
