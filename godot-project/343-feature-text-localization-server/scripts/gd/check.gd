extends FeatureCheck
## GDScript checks of 343-feature-text-localization-server. Check the same things as scripts/cs/Issue343FeatureTextLocalizationServerCheck.cs.
## Benchmark (rerun of #340 with the new path): add `-- --bench-343` to the command line.

const DATA_DIR := "user://check_343"
const BENCH_DIR := "user://bench_343"
const BENCH_LOCALES := ["en", "de", "fr", "es", "it", "pt", "ru", "ja", "zh", "ko", "pl", "nl"]
const CALLS := 4000

var _locale_changes: Array = []
var _catalogs_changed := 0


func _get_check_name() -> String:
	return "343-feature-text-localization-server"


func _on_locale_changed(locale: String, previous_locale: String) -> void:
	_locale_changes.append([locale, previous_locale])


func _on_catalogs_changed() -> void:
	_catalogs_changed += 1


func _game_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "en", "Hello")
	catalog.set_text(&"HELLO", "de", "Hallo")
	catalog.set_text(&"HELLO", "de", "Hallo (Menü)", &"menu")
	catalog.set_text(&"BYE", "en", "Bye")
	catalog.set_text(&"GREET", "de", "Hallo {name}")
	catalog.set_plural_rule("de", "nplurals=2; plural=(n != 1);")
	catalog.set_plural_texts(&"APPLE", "de", PackedStringArray(["{n} Apfel", "{n} Äpfel"]))
	catalog.set_text(&"HELLO", "fr", "Bonjour")
	return catalog


func _mod_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"HELLO", "de", "Servus")
	catalog.set_text(&"MOD_ONLY", "de", "Nur im Mod")
	return catalog


func _run_checks() -> void:
	var server := TextLocalizationServer
	expect(Engine.has_singleton("TextLocalizationServer"), "TextLocalizationServer is a registered singleton")
	expect(ClassDB.class_exists("LocalizationServer") and not ClassDB.can_instantiate("LocalizationServer"), "LocalizationServer is abstract")
	var previous_locale := LocalizationServer.get_locale()
	server.clear_catalogs()
	LocalizationServer.set_locale("en")
	server.locale_changed.connect(_on_locale_changed)
	server.catalogs_changed.connect(_on_catalogs_changed)

	# Locale and chain, forwarded to TranslationServer.
	var fallback := LocalizationServer.get_fallback_locale()
	expect_equal(LocalizationServer.get_locale_chain("de_DE"), PackedStringArray(["de_DE", "de", fallback]), "chain de_DE -> de -> fallback")

	# Game and mod layers: the mod (priority 100) overrides the game.
	var game := _game_catalog()
	var mod := _mod_catalog()
	server.add_catalog(game)
	server.add_catalog(mod, 100)
	expect_equal(_catalogs_changed, 2, "catalogs_changed per add_catalog")
	LocalizationServer.set_locale("de_DE")
	expect_equal(TranslationServer.get_locale(), "de_DE", "set_locale forwards to TranslationServer")
	expect_equal(_locale_changes.back(), ["de_DE", "en"], "locale_changed(locale, previous_locale)")
	expect_equal(server.translate(&"HELLO"), "Servus", "mod wins over game")
	expect_equal(server.translate(&"HELLO", &"menu"), "Hallo (Menü)", "context")
	expect_equal(server.translate(&"BYE"), "Bye", "fallback locale")
	expect_equal(server.translate(&"UNKNOWN"), "UNKNOWN", "missing key returns the key")
	expect_equal(server.translate_for_locale("fr", &"HELLO"), "Bonjour", "translate_for_locale")
	expect(server.has_translation(&"MOD_ONLY") and not server.has_translation(&"UNKNOWN"), "has_translation")

	# tr(), atr(), plurals and pseudolocalization through the adapter.
	expect_equal(tr(&"HELLO"), "Servus", "tr() through the adapter")
	expect_equal(tr(&"HELLO", &"menu"), "Hallo (Menü)", "tr() with context")
	expect_equal(atr(&"HELLO"), "Servus", "atr() through the adapter")
	expect_equal(tr_n(&"APPLE", &"APPLES", 5), "{n} Äpfel", "tr_n() through the adapter")
	TranslationServer.pseudolocalization_enabled = true
	var pseudo := tr(&"HELLO")
	TranslationServer.pseudolocalization_enabled = false
	expect(pseudo != "Servus" and pseudo.begins_with("["), "pseudolocalization still applies: " + pseudo)

	# A direct TranslationServer.set_locale() reaches the server too.
	TranslationServer.set_locale("en")
	expect_equal(server.translate(&"HELLO"), "Hello", "TranslationServer.set_locale is the locale source")
	LocalizationServer.set_locale("de")

	# Key ids, plurals and arguments.
	var hello_id := server.get_key_id(&"HELLO")
	expect(hello_id >= 0 and server.get_key_id(&"UNKNOWN") == -1, "get_key_id")
	expect_equal(server.translate_id(hello_id), "Servus", "translate_id")
	server.remove_catalog(mod)
	expect_equal(server.get_key_id(&"HELLO"), hello_id, "key ids stay valid after remove_catalog")
	expect_equal(server.translate_id(hello_id), "Hallo", "translate_id after remove_catalog")
	server.add_catalog(mod, 100)
	expect_equal(server.translate(&"APPLE", &"", 1, { "n": 1 }), "1 Apfel", "plural form 1")
	expect_equal(server.translate(&"APPLE", &"", 3, { "n": 3 }), "3 Äpfel", "plural form 3 with args")
	expect_equal(server.translate(&"GREET", &"", -1, { "name": "Ann" }), "Hallo Ann", "placeholders from args")
	expect_equal(TextInsertionServer.insert("{k:t}!", { "k": "HELLO" }), "Servus!", "t modifier uses TextLocalizationServer")

	# Reverse lookup and missing keys.
	expect_equal(server.get_translation_key("Servus"), &"HELLO", "get_translation_key")
	expect_equal(server.get_translation_keys("Bye"), PackedStringArray(["BYE"]), "get_translation_keys")
	expect_equal(server.get_translation_key_for_locale("fr", "Bonjour"), &"HELLO", "get_translation_key_for_locale")
	expect(server.get_missing_keys("de").has("BYE"), "get_missing_keys")
	expect(server.get_available_locales().has("fr"), "get_available_locales")

	# Catalog changes are applied on the next idle frame.
	game.set_text(&"BYE", "de", "Tschüss")
	await get_tree().process_frame
	expect_equal(server.translate(&"BYE"), "Tschüss", "catalog change picked up")

	# Catalog files with folder wildcards and on-demand locales.
	server.clear_catalogs()
	DirAccess.make_dir_recursive_absolute(DATA_DIR + "/mods/knights/translations")
	game.save_to_file(DATA_DIR + "/game.lcat")
	_mod_catalog().save_to_file(DATA_DIR + "/mods/knights/translations/texts.lcat")
	var list := LocalizationCatalogList.new()
	list.add_entry(DATA_DIR + "/*.lcat")
	list.add_entry(DATA_DIR + "/mods/*/translations/*.lcat", 100)
	expect_equal(server.add_catalog_list(list), 2, "add_catalog_list loads game and mod")
	var loaded: LocalizationCatalog = server.get_catalogs()[0]
	expect_equal(server.translate(&"HELLO"), "Servus", "mod from user://mods/*/translations")
	expect(loaded.is_locale_loaded("de") and not loaded.is_locale_loaded("fr"), "only the chain locales are read")

	server.locale_changed.disconnect(_on_locale_changed)
	server.catalogs_changed.disconnect(_on_catalogs_changed)
	server.clear_catalogs()
	LocalizationServer.set_locale(previous_locale)

	if OS.get_cmdline_user_args().has("--bench-343"):
		await _bench()


# Benchmark: 20k keys x 12 locales in 1 and 8 files, active locale de, fallback en (as #340).
func _bench() -> void:
	for files in [1, 8]:
		_generate(20000, files)
		var list := LocalizationCatalogList.new()
		list.add_entry("%s/f%d_*.lcat" % [BENCH_DIR, files])
		LocalizationServer.set_locale("de")
		var memory := Performance.get_monitor(Performance.MEMORY_STATIC)
		var start := Time.get_ticks_usec()
		TextLocalizationServer.add_catalog_list(list)
		var load_ms := (Time.get_ticks_usec() - start) / 1000.0
		var mb := (Performance.get_monitor(Performance.MEMORY_STATIC) - memory) / 1048576.0
		var hit: Array[StringName] = []
		var fallback: Array[StringName] = []
		var miss: Array[StringName] = []
		var ids: Array[int] = []
		for i in CALLS:
			var k := (i * 7919) % 20000
			if k % 10 == 0:
				k += 1
			hit.append(StringName("UI_KEY_%05d" % k))
			fallback.append(StringName("UI_KEY_%05d" % ((i * 10) % 20000)))
			miss.append(StringName("NO_KEY_%05d" % i))
			ids.append(TextLocalizationServer.get_key_id(hit[i]))
		var rows := {
			"translate hit": _median(_run_translate.bind(hit)),
			"translate miss->fallback": _median(_run_translate.bind(fallback)),
			"translate miss->key": _median(_run_translate.bind(miss)),
			"translate_id": _median(_run_translate_id.bind(ids)),
			"tr() hit (adapter)": _median(_run_tr.bind(hit)),
			"tr() miss->fallback": _median(_run_tr.bind(fallback)),
			"tr() miss->key": _median(_run_tr.bind(miss)),
			"translate_for_locale en": _median(_run_translate_en.bind(hit)),
		}
		var switch_start := Time.get_ticks_usec()
		LocalizationServer.set_locale("fr")
		var switch_ms := (Time.get_ticks_usec() - switch_start) / 1000.0
		LocalizationServer.set_locale("de")
		print("343 bench k20000_l12_f%d: load %.1f ms, static %.2f MB, locale switch (rebuild) %.2f ms" % [files, load_ms, mb, switch_ms])
		for row_name in rows:
			print("343 bench k20000_l12_f%d: %s %.0f ns" % [files, row_name, rows[row_name]])
		TextLocalizationServer.clear_catalogs()
		await get_tree().process_frame


func _run_translate(keys: Array[StringName]) -> void:
	for key in keys:
		TextLocalizationServer.translate(key)


func _run_translate_en(keys: Array[StringName]) -> void:
	for key in keys:
		TextLocalizationServer.translate_for_locale("en", key)


func _run_translate_id(ids: Array[int]) -> void:
	for id in ids:
		TextLocalizationServer.translate_id(id)


func _run_tr(keys: Array[StringName]) -> void:
	for key in keys:
		tr(key)


func _median(work: Callable) -> float:
	work.call()
	var runs: Array[float] = []
	for run in 5:
		var start := Time.get_ticks_usec()
		work.call()
		runs.append((Time.get_ticks_usec() - start) * 1000.0 / CALLS)
	runs.sort()
	return runs[2]


func _generate(keys: int, files: int) -> void:
	DirAccess.make_dir_recursive_absolute(BENCH_DIR)
	var per_file := keys / files
	for f in files:
		var path := "%s/f%d_%d.lcat" % [BENCH_DIR, files, f]
		if FileAccess.file_exists(path):
			continue
		var catalog := TextLocalizationCatalog.new()
		for i in range(f * per_file, (f + 1) * per_file):
			var key := StringName("UI_KEY_%05d" % i)
			for locale in BENCH_LOCALES:
				if i % 10 == 0 and locale != "en":
					continue # Only in the fallback locale.
				catalog.set_text(key, locale, "%s text %d: the quick brown fox jumps over the lazy dog" % [locale, i])
		catalog.save_to_file(path)
