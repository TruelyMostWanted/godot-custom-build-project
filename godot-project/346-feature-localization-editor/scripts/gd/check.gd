extends FeatureCheck
## GDScript checks of 346-feature-localization-editor. Check the same things as scripts/cs/Issue346FeatureLocalizationEditorCheck.cs.

const LOCALES: Array[String] = ["en", "de", "fr", "es", "it", "pt", "ru", "ja", "zh", "ko", "pl", "nl"]
const KEY_COUNT := 20000
const PAGE_ROWS := 500


func _get_check_name() -> String:
	return "346-feature-localization-editor"


func _run_checks() -> void:
	expect(ClassDB.class_exists("TextLocalizationCatalog"), "ClassDB knows TextLocalizationCatalog")
	if not ClassDB.class_exists("TextLocalizationCatalog"):
		return
	_check_entries()
	_check_validate()
	_check_lines()
	_check_delimiters()
	_check_po()
	_check_summary()
	_measure()


func _make_catalog() -> TextLocalizationCatalog:
	var csv := "keys,?context,?plural,_notes,en,de\n"
	csv += "HELLO,,,greeting,Hello,Hallo\n"
	csv += "HELLO,menu,,,Hello (menu),Hallo (Menü)\n"
	csv += "?pluralrule,,,,,nplurals=2; plural=(n != 1);\n"
	csv += "APPLE,,APPLES,Inventory,%d apple,%d Apfel\n"
	csv += ",,,,%d apples,%d Äpfel\n"
	csv += "BYE,,,,Bye,\n"
	var catalog := TextLocalizationCatalog.new()
	expect(catalog.load_from_text(csv, ResourceTextSerializer.FORMAT_CSV) == OK, "load_from_text(CSV) returns OK")
	return catalog


func _check_entries() -> void:
	var catalog := _make_catalog()
	var hello: Dictionary = catalog.get_entry("HELLO")
	expect_equal(hello.get("en"), "Hello", "get_entry: text")
	expect_equal(hello.get("_notes"), "greeting", "get_entry: note")
	expect(not hello.has("_key"), "get_entry: no _key without context")
	expect(not hello.has("_context"), "get_entry: no _context without context")
	var hello_menu: Dictionary = catalog.get_entry("HELLO", "menu")
	expect_equal(hello_menu.get("_context"), "menu", "get_entry: _context with context")
	expect_equal(hello_menu.get("_key"), "HELLO", "get_entry: _key with context")
	var apple: Dictionary = catalog.get_entry("APPLE")
	expect_equal(apple.get("_plural"), "APPLES", "get_entry: _plural")
	expect_equal(apple.get("_notes"), "Inventory", "get_entry: plural note")
	var forms = apple.get("de")
	expect(forms is Array, "get_entry: plural forms are an Array")
	if forms is Array:
		expect_equal(forms.size(), 2, "get_entry: two plural forms")
		if forms.size() == 2:
			expect_equal(forms[1], "%d Äpfel", "get_entry: second plural form")
	expect(catalog.get_entry("MISSING").is_empty(), "get_entry: missing key gives an empty Dictionary")

	# set_entry replaces; fields the entry lacks are cleared.
	catalog.set_entry("HELLO", { "de": "Servus", "fr": "Salut", "_notes": "informal" })
	expect_equal(catalog.get_text("HELLO", "de"), "Servus", "set_entry: text replaced")
	expect(catalog.get_text("HELLO", "en").is_empty(), "set_entry: missing field cleared")
	expect_equal(catalog.get_text("HELLO", "fr"), "Salut", "set_entry: locale added")
	expect(catalog.get_locales().has("fr"), "set_entry: locale listed")
	expect_equal(catalog.get_note("HELLO"), "informal", "set_entry: note replaced")
	# Undo: the old entry restores the key.
	catalog.set_entry("HELLO", hello)
	expect_equal(catalog.get_entry("HELLO").get("en"), "Hello", "set_entry: undo restores en")
	expect_equal(catalog.get_text("HELLO", "fr"), "", "set_entry: undo clears the added text")
	catalog.set_entry("APPLE", {})
	expect(catalog.get_plural_key("APPLE").is_empty(), "set_entry({}): plural key cleared")
	catalog.set_entry("APPLE", apple)
	expect_equal(catalog.get_plural_text("APPLE", "de", 5), "%d Äpfel", "set_entry: plural undo")
	expect_equal(catalog.get_plural_key("APPLE"), "APPLES", "set_entry: plural key restored")
	catalog.set_entry("NEW", { "fr": "Salut" }, "ctx")
	expect_equal(catalog.get_text("NEW", "fr", "ctx"), "Salut", "set_entry: new key with context")
	# Renaming = remove_key + set_entry; the key moves to the end.
	var entry: Dictionary = catalog.get_entry("BYE")
	catalog.remove_key("BYE")
	catalog.set_entry("GOODBYE", entry)
	expect(not catalog.has_key("BYE"), "rename: old key gone")
	expect_equal(catalog.get_text("GOODBYE", "en"), "Bye", "rename: texts kept")
	expect_equal(catalog.get_key_count() - 1, catalog.find_key("GOODBYE"), "rename: moved to the end")
	# note_columns
	expect(catalog.note_columns.size() > 0, "note_columns lists the note column")
	expect_equal(catalog.get_note_columns(), catalog.note_columns, "note_columns equals get_note_columns()")


func _check_validate() -> void:
	var catalog := _make_catalog()
	var result := catalog.validate()
	expect_equal(result.get_issue_count(), 1, "validate: one missing translation")
	if result.get_issue_count() == 1:
		expect_equal(result.get_issue_kind(0), DataValidation.ISSUE_KIND_EMPTY_REQUIRED, "validate: EMPTY_REQUIRED")
		expect_equal(result.get_issue_row(0), catalog.find_key("BYE"), "validate: row")
		expect_equal(result.get_issue_column(0), 1, "validate: column = locale index")
		expect_equal(str(result.get_issue_field(0)), "de", "validate: field = locale")
		expect_equal(result.get_issue_path(0), "BYE", "validate: path = entry name")

	expect(catalog.load_from_text("keys,en,de\nA,a,\nB,b,\nA,a2,\n", ResourceTextSerializer.FORMAT_CSV) == OK, "duplicate CSV loads")
	expect_equal(catalog.get_key_count(), 2, "duplicates are merged")
	result = catalog.validate()
	expect_equal(result.get_issue_count(), 3, "validate: duplicate + two missing")
	if result.get_issue_count() == 3:
		expect_equal(result.get_issue_kind(0), DataValidation.ISSUE_KIND_DUPLICATE_KEY, "validate: DUPLICATE_KEY first")
	catalog.clear()
	expect(catalog.validate().is_valid(), "validate: duplicates belong to the last load")

	var invalid := TextLocalizationCatalog.new()
	invalid.set_text("A", "qqq", "a")
	result = invalid.validate()
	expect_equal(result.get_issue_count(), 1, "validate: invalid locale")
	if result.get_issue_count() == 1:
		expect_equal(result.get_issue_kind(0), DataValidation.ISSUE_KIND_INVALID_LOCALE, "validate: INVALID_LOCALE")

	var options := DataValidationOptions.new()
	options.stop_on_first = true
	catalog.load_from_text("keys,en,de\nA,,\nB,,\n", ResourceTextSerializer.FORMAT_CSV)
	expect_equal(catalog.validate(options).get_issue_count(), 1, "validate: stop_on_first gives 1 issue")
	expect_equal(catalog.validate().get_issue_count(), 4, "validate: default options give all 4 issues")


func _write(path: String, text: String) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file != null:
		file.store_string(text)
		file.close()


func _check_lines() -> void:
	var csv_path := "user://catalog_346_lines.csv"
	_write(csv_path, "keys,?plural,en,de\nA,,a,\"Zeile\nzwei\"\nAPPLE,APPLES,apple,\n,,apples,\nB,,b,\n")
	var catalog := ResourceLoader.load(csv_path, "TextLocalizationCatalog", ResourceLoader.CACHE_MODE_IGNORE) as TextLocalizationCatalog
	if expect(catalog != null, "CSV loads through ResourceLoader"):
		var result := catalog.validate()
		expect_equal(result.get_issue_count(), 2, "csv lines: two issues")
		if result.get_issue_count() == 2:
			expect_equal(result.get_issue_path(0), "APPLE", "csv lines: path 0")
			expect_equal(result.get_issue_line(0), 4, "csv lines: APPLE after a quoted line break")
			expect_equal(result.get_issue_path(1), "B", "csv lines: path 1")
			expect_equal(result.get_issue_line(1), 6, "csv lines: B after plural rows")
	var json_path := "user://catalog_346_lines.json"
	_write(json_path, "{\n\t\"A\": {\n\t\t\"en\": \"a\",\n\t\t\"de\": \"a\"\n\t},\n\t\"B\": {\n\t\t\"en\": \"b\"\n\t}\n}\n")
	var json_catalog := ResourceLoader.load(json_path, "TextLocalizationCatalog", ResourceLoader.CACHE_MODE_IGNORE) as TextLocalizationCatalog
	if expect(json_catalog != null, "JSON loads through ResourceLoader"):
		var result := json_catalog.validate()
		expect_equal(result.get_issue_count(), 1, "json lines: one issue")
		if result.get_issue_count() == 1:
			expect_equal(result.get_issue_line(0), 6, "json lines: line of entry B")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(csv_path))
	DirAccess.remove_absolute(ProjectSettings.globalize_path(json_path))


func _check_delimiters() -> void:
	var catalog := TextLocalizationCatalog.new()
	expect(catalog.load_from_text("keys;en;de\nHELLO;Hello, world;Hallo, Welt\n", ResourceTextSerializer.FORMAT_CSV) == OK, "semicolon CSV loads")
	expect_equal(catalog.get_text("HELLO", "de"), "Hallo, Welt", "semicolon CSV detected")
	expect(catalog.load_from_text("keys\ten\tde\nHELLO\tHello\tHallo\n", ResourceTextSerializer.FORMAT_CSV) == OK, "tab CSV loads")
	expect_equal(catalog.get_text("HELLO", "de"), "Hallo", "tab CSV detected")
	var options := SerializerOptions.new()
	options.csv_delimiter = ";"
	var text := catalog.to_text(ResourceTextSerializer.FORMAT_CSV, options)
	expect(text.begins_with("keys;en;de"), "to_text uses the delimiter of the options")
	var copy := TextLocalizationCatalog.new()
	expect(copy.load_from_text(text, ResourceTextSerializer.FORMAT_CSV, options) == OK, "load_from_text with options")
	expect_equal(copy.get_text("HELLO", "en"), "Hello", "round trip with csv_delimiter")
	expect(options.csv_headers, "caller options are not changed")


func _check_po() -> void:
	var catalog := _make_catalog()
	var pot := catalog.to_po()
	expect(pot.contains("msgctxt \"menu\""), "POT: msgctxt")
	expect(pot.contains("msgid \"HELLO\""), "POT: msgid")
	expect(pot.contains("msgid_plural \"APPLES\""), "POT: msgid_plural")
	expect(pot.contains("#. greeting"), "POT: translator note as #. comment")
	var path := "user://catalog_346_x.po"
	_write(path, catalog.to_po("de"))
	var translation := ResourceLoader.load(path, "Translation", ResourceLoader.CACHE_MODE_IGNORE) as Translation
	if expect(translation != null, "PO of de loads as Translation"):
		expect_equal(translation.get_locale(), "de", "PO: locale")
		expect_equal(str(translation.get_message("HELLO")), "Hallo", "PO: get_message")
		expect_equal(str(translation.get_message("HELLO", "menu")), "Hallo (Menü)", "PO: get_message with context")
		expect_equal(str(translation.get_plural_message("APPLE", "APPLES", 5)), "%d Äpfel", "PO: get_plural_message")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))


func _check_summary() -> void:
	var catalog := _make_catalog()
	var summary := str(catalog.get("summary/de"))
	expect("(" in summary and "%)" in summary, "inspector summary/de has a percentage: %s" % summary)
	expect(summary.contains("missing"), "inspector summary/de names the missing keys")


func _ms(from_usec: int) -> float:
	return float(Time.get_ticks_usec() - from_usec) / 1000.0


func _measure() -> void:
	print("  -- measurement: %d keys x %d locales (about 90 %% filled)" % [KEY_COUNT, LOCALES.size()])
	var catalog := TextLocalizationCatalog.new()
	for locale in LOCALES:
		catalog.add_locale(locale)
	var t_gen := Time.get_ticks_usec()
	for i in KEY_COUNT:
		var key := "UI_KEY_%05d" % i
		for l in LOCALES.size():
			if (i * 7 + l * 3) % 10 == 0 and l > 0:
				catalog.set_text(key, LOCALES[l], "")
				catalog.set_text(key, LOCALES[0], "%s text %d" % [LOCALES[0], i])
			else:
				catalog.set_text(key, LOCALES[l], "%s text %d: the quick brown fox" % [LOCALES[l], i])
	var gen_ms := _ms(t_gen)
	expect_equal(catalog.get_key_count(), KEY_COUNT, "bench: key count")

	var t0 := Time.get_ticks_usec()
	var result := catalog.validate()
	var validate_ms := _ms(t0)
	var issues := result.get_issue_count()
	expect(issues > 0, "bench: validate() finds issues (%d)" % issues)

	var all_options := DataValidationOptions.new()
	all_options.max_issues = -1
	t0 = Time.get_ticks_usec()
	var all_result := catalog.validate(all_options)
	var validate_all_ms := _ms(t0)
	var all_issues := all_result.get_issue_count()
	expect(all_issues >= issues, "bench: validate(max_issues = -1) finds at least as many issues (%d)" % all_issues)

	t0 = Time.get_ticks_usec()
	var entry_total := 0
	for i in PAGE_ROWS:
		entry_total += catalog.get_entry("UI_KEY_%05d" % (i * 7)).size()
	var entries_ms := _ms(t0)
	expect(entry_total > 0, "bench: get_entry returns data")

	t0 = Time.get_ticks_usec()
	var po := catalog.to_po("de")
	var po_ms := _ms(t0)
	expect(not po.is_empty(), "bench: to_po(de) is not empty")

	t0 = Time.get_ticks_usec()
	var missing_total := 0
	for locale in LOCALES:
		missing_total += catalog.get_missing_rows(locale).size()
	var missing_ms := _ms(t0)
	expect(missing_total > 0, "bench: missing rows found (%d)" % missing_total)

	print("  BENCH | %-34s | %10s | %s" % ["operation", "ms", "result"])
	print("  BENCH | %-34s | %10.1f | %d keys" % ["generate catalog", gen_ms, KEY_COUNT])
	print("  BENCH | %-34s | %10.1f | %d issues" % ["validate() default options", validate_ms, issues])
	print("  BENCH | %-34s | %10.1f | %d issues" % ["validate() max_issues = -1", validate_all_ms, all_issues])
	print("  BENCH | %-34s | %10.1f | %d entries" % ["%d x get_entry() (one page)" % PAGE_ROWS, entries_ms, PAGE_ROWS])
	print("  BENCH | %-34s | %10.1f | %.2f MB" % ["to_po(\"de\")", po_ms, po.length() / 1048576.0])
	print("  BENCH | %-34s | %10.1f | %d missing rows" % ["get_missing_rows x %d locales" % LOCALES.size(), missing_ms, missing_total])
