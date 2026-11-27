extends FeatureCheck
## GDScript checks of 341-feature-localization-catalog. Check the same things as scripts/cs/Issue341FeatureLocalizationCatalogCheck.cs.

const PLURAL_RULE := "nplurals=2; plural=(n != 1);"
const FORMAT_CLASSES: Array[String] = ["JSON", "INI", "CSV", "XML", "TOML", "YAML"]
const LOCALES: Array[String] = ["en", "de", "fr", "es", "it", "pt", "ru", "ja", "zh", "ko", "pl", "nl"]
const KEY_COUNT := 20000
const CALLS_PER_RUN := 4000
const RUNS := 5

var _loaded_log: Array[String] = []


func _get_check_name() -> String:
	return "341-feature-localization-catalog"


func _run_checks() -> void:
	expect(ClassDB.class_exists("LocalizationCatalog"), "ClassDB knows LocalizationCatalog")
	expect(ClassDB.class_exists("TextLocalizationCatalog"), "ClassDB knows TextLocalizationCatalog")
	expect(ClassDB.class_exists("ResourceLocalizationCatalog"), "ClassDB knows ResourceLocalizationCatalog")
	expect(not ClassDB.class_exists("LocalizationCatalogList"), "LocalizationCatalogList is removed (#357): catalog sources are FilesManifest lists")
	if not ClassDB.class_exists("TextLocalizationCatalog"):
		return
	_check_csv_rules()
	_check_text_round_trips()
	_check_file_round_trips()
	_check_binary()
	_check_translations()
	_check_resource_catalog()
	_measure()


## The rules of the CSV translation importer: ?context, ?plural + continuation rows, _ columns ignored,
## ?pluralrule row, translations unescaped.
func _make_text_catalog() -> TextLocalizationCatalog:
	var csv := "keys,?context,?plural,_notes,en,de\n"
	csv += "HELLO,,,greeting,Hello,Hallo\n"
	csv += "HELLO,menu,,,Hello (menu),Hallo (Menü)\n"
	csv += "?pluralrule,,,,,nplurals=2; plural=(n != 1);\n"
	csv += "APPLE,,APPLES,,%d apple,%d Apfel\n"
	csv += ",,,,%d apples,%d Äpfel\n"
	csv += "LINE,,,,One\\nTwo,Eins\\nZwei\n"
	csv += ",,,,ignored row,\n"
	csv += "BYE,,,,Bye,\n"
	var catalog := TextLocalizationCatalog.new()
	var err := catalog.load_from_text(csv, ResourceTextSerializer.FORMAT_CSV)
	expect(err == OK, "load_from_text(CSV) returns OK")
	return catalog


func _check_values(catalog: TextLocalizationCatalog, label: String) -> void:
	expect(catalog.get_text("HELLO", "de") == "Hallo", "%s: get_text" % label)
	expect(catalog.get_text("HELLO", "de", "menu") == "Hallo (Menü)", "%s: get_text with context" % label)
	expect(catalog.get_text("LINE", "en") == "One\nTwo", "%s: \\n escape is a newline" % label)
	expect(catalog.get_plural_text("APPLE", "de", 1) == "%d Apfel", "%s: get_plural_text n=1" % label)
	expect(catalog.get_plural_text("APPLE", "de", 5) == "%d Äpfel", "%s: get_plural_text n=5" % label)
	expect(catalog.get_plural_rule("de") == PLURAL_RULE, "%s: plural rule" % label)


func _check_csv_rules() -> void:
	var catalog := _make_text_catalog()
	expect_equal(catalog.get_key_count(), 5, "key_count")
	expect_equal(catalog.get_locales(), PackedStringArray(["en", "de"]), "locales")
	_check_values(catalog, "csv")
	expect_equal(catalog.get_plural_key("APPLE"), "APPLES", "plural key")
	expect_equal(catalog.get_plural_texts("APPLE", "de"), PackedStringArray(["%d Apfel", "%d Äpfel"]), "plural texts")
	expect(catalog.get_text("BYE", "de").is_empty(), "missing translation is empty")
	expect_equal(catalog.get_translated_count("de"), 4, "translated count de")
	expect_equal(catalog.get_missing_rows("de"), PackedInt32Array([4]), "missing rows de")
	expect_equal(str(catalog.get_key(1)), "HELLO", "key of row 1")
	expect_equal(str(catalog.get_key_context(1)), "menu", "context of row 1")
	expect_equal(catalog.find_key("HELLO", "menu"), 1, "find_key with context")
	expect(not catalog.has_key("HELLO", "other"), "has_key with other context is false")
	expect_equal(catalog.get_plural_form("de", 1), 0, "plural form n=1")
	expect_equal(catalog.get_plural_form("de", 5), 1, "plural form n=5")

	# Keys, locales and cells.
	var small := TextLocalizationCatalog.new()
	small.set_text("A", "en", "a")
	small.set_text("B", "en", "b")
	small.set_text("C", "de", "c")
	small.set_text("B", "en", "a much longer text")
	expect(small.get_text("A", "en") == "a" and small.get_text("B", "en") == "a much longer text" and small.get_text("C", "de") == "c", "cells grow in the middle")
	small.remove_key("A")
	expect_equal(small.get_key_count(), 2, "remove_key")
	expect_equal(small.find_key("B"), 0, "rows move up")
	small.remove_locale("en")
	expect_equal(small.get_locales(), PackedStringArray(["de"]), "remove_locale")
	for i in 1000:
		small.set_text("KEY_%d" % i, "de", str(i))
	expect(small.get_text("KEY_777", "de") == "777" and small.get_text("C", "de") == "c", "index grows with many keys")


func _check_text_round_trips() -> void:
	var catalog := _make_text_catalog()
	var data: Variant = catalog.get("data")
	expect(data is Dictionary, "data property is a Dictionary")
	for f in FORMAT_CLASSES.size():
		var format_name: String = FORMAT_CLASSES[f]
		if not ClassDB.class_exists(format_name):
			print("  NOTE  %s module is not in this build: format skipped" % format_name)
			continue
		var text := catalog.to_text(f as ResourceTextSerializer.Format)
		expect(not text.is_empty(), "%s: to_text is not empty" % format_name)
		var copy := TextLocalizationCatalog.new()
		var err := copy.load_from_text(text, f as ResourceTextSerializer.Format)
		expect(err == OK, "%s: load_from_text returns OK" % format_name)
		expect(copy.get("data") == data, "%s: data round trip is equal" % format_name)
		_check_values(copy, format_name)
		expect_equal(copy.get_plural_texts("APPLE", "en").size(), 2, "%s: plural texts of en" % format_name)

	var duplicate := catalog.duplicate() as TextLocalizationCatalog
	expect(duplicate != null and duplicate.get_text("HELLO", "de", "menu") == "Hallo (Menü)", "duplicate keeps the data")


func _check_file_round_trips() -> void:
	var catalog := _make_text_catalog()
	var data: Variant = catalog.get("data")
	var extensions: Array[String] = ["lcat"]
	for f in FORMAT_CLASSES.size():
		if ClassDB.class_exists(FORMAT_CLASSES[f]):
			extensions.append(FORMAT_CLASSES[f].to_lower())
	for extension in extensions:
		var path := "user://catalog_341_round_trip." + extension
		expect(catalog.save_to_file(path) == OK, "save_to_file .%s" % extension)
		var copy := TextLocalizationCatalog.new()
		expect(copy.load_from_file(path) == OK, "load_from_file .%s" % extension)
		expect(copy.get("data") == data if extension != "lcat" else _without_notes(copy.get("data")) == _without_notes(data), "file .%s: data is equal" % extension)
		expect(copy.get_text("HELLO", "de", "menu") == "Hallo (Menü)", "file .%s: get_text" % extension)
		DirAccess.remove_absolute(path)


func _on_locale_loaded(locale: String) -> void:
	_loaded_log.append(locale)


func _check_binary() -> void:
	var catalog := _make_text_catalog()
	var path := "user://catalog_341_binary.lcat"
	expect(catalog.save_to_file(path) == OK, "save .lcat")
	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as TextLocalizationCatalog
	expect(loaded != null, "load() returns a TextLocalizationCatalog")
	if loaded == null:
		return
	expect_equal(loaded.get_key_count(), 5, "binary: key_count")
	expect_equal(loaded.get_locales(), PackedStringArray(["en", "de"]), "binary: locales")
	expect(not loaded.is_locale_loaded("en") and not loaded.is_locale_loaded("de"), "binary: no locale loaded after load()")
	expect_equal(loaded.get_translated_count("de"), 4, "binary: translated count without loading")
	expect(not loaded.is_locale_loaded("de"), "binary: get_translated_count does not load")

	_loaded_log.clear()
	loaded.locale_loaded.connect(_on_locale_loaded)
	expect(loaded.get_text("HELLO", "de", "menu") == "Hallo (Menü)", "binary: get_text loads on first use")
	expect(_loaded_log == ["de"], "binary: locale_loaded emitted once with de")
	expect(loaded.is_locale_loaded("de") and not loaded.is_locale_loaded("en"), "binary: only de is loaded")
	expect_equal(loaded.get_plural_texts("APPLE", "de").size(), 2, "binary: plural forms of de")
	loaded.get_text("HELLO", "de")
	expect_equal(_loaded_log.size(), 1, "binary: no second signal for a loaded locale")

	loaded.unload_locale("de")
	expect(not loaded.is_locale_loaded("de"), "binary: unload_locale")
	expect(loaded.load_locale("de") == OK, "binary: load_locale returns OK")
	expect(loaded.is_locale_loaded("de"), "binary: load_locale loads")
	expect(_without_notes(loaded.get("data")) == _without_notes(catalog.get("data")), "binary: data equals the source (notes are not stored in .lcat)")
	loaded.locale_loaded.disconnect(_on_locale_loaded)

	# A changed catalog no longer reads from the file.
	loaded.set_text("NEW", "en", "new")
	expect(loaded.is_locale_loaded("en"), "binary: a change loads every locale")
	expect(loaded.get_text("BYE", "en") == "Bye", "binary: other cells are kept")

	# The wrong kind is refused (the engine prints an error).
	var wrong := ResourceLocalizationCatalog.new()
	expect(wrong.load_from_file(path) != OK, "binary: ResourceLocalizationCatalog refuses a text catalog")
	DirAccess.remove_absolute(path)


func _check_translations() -> void:
	var catalog := _make_text_catalog()
	var translations := catalog.to_translations()
	expect_equal(translations.size(), 2, "to_translations: one per locale")
	if translations.size() != 2:
		return
	var german := translations[1] as Translation
	expect_equal(german.locale, "de", "translation locale")
	expect_equal(str(german.get_message("HELLO")), "Hallo", "translation message")
	expect_equal(str(german.get_message("HELLO", "menu")), "Hallo (Menü)", "translation message with context")
	expect_equal(str(german.get_plural_message("APPLE", "APPLES", 5)), "%d Äpfel", "translation plural message")
	expect_equal(german.plural_rules_override, PLURAL_RULE, "translation plural rule override")

	var back := TextLocalizationCatalog.new()
	for translation in translations:
		back.add_translation(translation as Translation)
	expect(back.get_text("HELLO", "de", "menu") == "Hallo (Menü)", "add_translation: context")
	expect_equal(back.get_plural_texts("APPLE", "de").size(), 2, "add_translation: plural forms")
	expect_equal(back.get_plural_rule("de"), PLURAL_RULE, "add_translation: plural rule")
	expect(back.get_text("LINE", "en") == "One\nTwo", "add_translation: newline")


func _check_resource_catalog() -> void:
	var remaps: Dictionary = {
		"res://icon.png": PackedStringArray(["res://icon_de.png:de", "res://icon_fr.png:fr"]),
		"res://music.ogg": PackedStringArray(["res://music_de.ogg:de"]),
	}
	var catalog := ResourceLocalizationCatalog.new()
	catalog.add_translation_remaps(remaps)
	expect_equal(catalog.get_localized_path("res://icon.png", "fr"), "res://icon_fr.png", "localized path")
	expect(catalog.get_localized_path("res://music.ogg", "fr").is_empty(), "missing localized path is empty")
	expect(catalog.to_translation_remaps() == remaps, "to_translation_remaps equals the added remaps")

	catalog.set_expected_type("res://icon.png", "Texture2D")
	expect_equal(str(catalog.get_expected_type("res://icon.png")), "Texture2D", "expected type")
	var formats: Array[int] = [ResourceTextSerializer.FORMAT_CSV, ResourceTextSerializer.FORMAT_JSON, ResourceTextSerializer.FORMAT_XML]
	for f in formats:
		var format := f as ResourceTextSerializer.Format
		var copy := ResourceLocalizationCatalog.new()
		var err := copy.load_from_text(catalog.to_text(format), format)
		expect(err == OK, "resource catalog: load_from_text format %d" % f)
		expect(copy.get("data") == catalog.get("data"), "resource catalog: data round trip format %d" % f)
		expect_equal(str(copy.get_expected_type("res://icon.png")), "Texture2D", "resource catalog: expected type format %d" % f)


func _median(values: Array[float]) -> float:
	var sorted: Array[float] = values.duplicate()
	sorted.sort()
	return sorted[sorted.size() >> 1]


func _bench_get_text(catalog: TextLocalizationCatalog, keys: Array[StringName], locale: String) -> float:
	var samples: Array[float] = []
	for run in RUNS + 1:
		var t0 := Time.get_ticks_usec()
		for key in keys:
			var text := catalog.get_text(key, locale)
			if text.is_empty():
				return -1.0
		var elapsed := Time.get_ticks_usec() - t0
		if run > 0: # Run 0 is the warm-up.
			samples.append(float(elapsed) * 1000.0 / float(keys.size()))
	return _median(samples)


## The cost of the script call itself: a cheap bound method.
func _bench_overhead(catalog: TextLocalizationCatalog, keys: Array[StringName]) -> float:
	var samples: Array[float] = []
	for run in RUNS + 1:
		var t0 := Time.get_ticks_usec()
		for key in keys:
			var count := catalog.get_key_count()
			if count < 0:
				return -1.0
		var elapsed := Time.get_ticks_usec() - t0
		if run > 0:
			samples.append(float(elapsed) * 1000.0 / float(keys.size()))
	return _median(samples)


func _static_memory() -> float:
	return Performance.get_monitor(Performance.MEMORY_STATIC)


func _measure() -> void:
	print("  -- measurement: %d keys x %d locales" % [KEY_COUNT, LOCALES.size()])
	var path := "user://catalog_341_bench.lcat"
	var message_count := KEY_COUNT * LOCALES.size()

	var t_gen := Time.get_ticks_usec()
	var source := TextLocalizationCatalog.new()
	for locale in LOCALES:
		source.add_locale(locale)
	for i in KEY_COUNT:
		var key := "UI_KEY_%05d" % i
		for locale in LOCALES:
			source.set_text(key, locale, "%s text %d: the quick brown fox jumps over the lazy dog" % [locale, i])
	var gen_ms := float(Time.get_ticks_usec() - t_gen) / 1000.0
	var t_save := Time.get_ticks_usec()
	var save_err := source.save_to_file(path)
	var save_ms := float(Time.get_ticks_usec() - t_save) / 1000.0
	expect(save_err == OK, "bench: save %d messages as .lcat" % message_count)
	source = null
	var file_size := 0
	var file := FileAccess.open(path, FileAccess.READ)
	if file != null:
		file_size = file.get_length()
		file.close()
	print("  INFO  generated in %.0f ms, saved in %.0f ms, file %.2f MB (%.1f bytes per message)" % [gen_ms, save_ms, file_size / 1048576.0, float(file_size) / float(message_count)])

	# (a) load() + every locale.
	var mem0 := _static_memory()
	var t0 := Time.get_ticks_usec()
	var all_loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as TextLocalizationCatalog
	if not expect(all_loaded != null, "bench: load() the .lcat"):
		return
	var load_only_ms := float(Time.get_ticks_usec() - t0) / 1000.0
	var mem_keys := _static_memory() - mem0
	for locale in LOCALES:
		all_loaded.load_locale(locale)
	var all_ms := float(Time.get_ticks_usec() - t0) / 1000.0
	var all_mem := _static_memory() - mem0
	expect_equal(all_loaded.get_key_count(), KEY_COUNT, "bench: key_count after load")
	expect(all_loaded.get_text("UI_KEY_19999", "nl") == "nl text 19999: the quick brown fox jumps over the lazy dog", "bench: last cell is correct")

	# (b) en + de only.
	var mem1 := _static_memory()
	var t1 := Time.get_ticks_usec()
	var two_loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as TextLocalizationCatalog
	if not expect(two_loaded != null, "bench: load() a second time"):
		return
	two_loaded.load_locale("en")
	two_loaded.load_locale("de")
	var two_ms := float(Time.get_ticks_usec() - t1) / 1000.0
	var two_mem := _static_memory() - mem1
	expect(not two_loaded.is_locale_loaded("nl"), "bench: nl stays unloaded")

	print("  INFO  load() only: %.0f ms, %.2f MB (keys, locale table)" % [load_only_ms, mem_keys / 1048576.0])
	print("  INFO  load() + 12 locales: %.0f ms, %.2f MB, %.1f bytes per message" % [all_ms, all_mem / 1048576.0, all_mem / float(message_count)])
	print("  INFO  load() + en + de: %.0f ms, %.2f MB, %.1f bytes per message" % [two_ms, two_mem / 1048576.0, two_mem / float(KEY_COUNT * 2)])

	# (d) ns per call.
	var keys: Array[StringName] = []
	for i in CALLS_PER_RUN:
		keys.append(StringName("UI_KEY_%05d" % ((i * 5) % KEY_COUNT)))
	var ns_de := _bench_get_text(all_loaded, keys, "de")
	var ns_nl := _bench_get_text(all_loaded, keys, "nl")
	var overhead := _bench_overhead(all_loaded, keys)
	print("  INFO  get_text: de %.0f ns, nl %.0f ns per call (script call overhead, key_count: %.0f ns)" % [ns_de, ns_nl, overhead])
	print("  INFO  vs #340 Translation (12 locales): 2484 ms / 80 MB / ~380 B per message; en+de only: 424 ms / 13.9 MB; OptimizedTranslation ~72 B per message")
	print("  INFO  now: 12 locales %.0f ms / %.1f MB / %.0f B per message; en+de %.0f ms / %.1f MB" % [all_ms, all_mem / 1048576.0, all_mem / float(message_count), two_ms, two_mem / 1048576.0])

	expect(ns_de > 0.0 and ns_de < 20000.0, "bench: get_text takes less than 20 us per call (sanity)")
	expect(all_ms < 30000.0, "bench: load of 12 locales takes less than 30 s (sanity)")
	if all_mem > 0.0: # MEMORY_STATIC is 0 in release builds.
		var bytes_per_message := all_mem / float(message_count)
		expect(bytes_per_message < 120.0, "bench: less than 120 bytes per message (%.1f)" % bytes_per_message)
	else:
		print("  NOTE  MEMORY_STATIC is 0 (release build): memory check skipped")
	DirAccess.remove_absolute(path)


# Translator notes (_ columns, #357) and the list of note columns (?catalog "notes", #403) are never stored in .lcat.
func _without_notes(value: Variant) -> Variant:
	if value is Dictionary:
		var result := {}
		for key in value:
			if not (key is String and ((key.begins_with("_") and key not in ["_key", "_context"]) or key == "notes")):
				result[key] = _without_notes(value[key])
		return result
	if value is Array:
		var result := []
		for item in value:
			result.append(_without_notes(item))
		return result
	return value
