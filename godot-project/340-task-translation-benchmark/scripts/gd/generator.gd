extends RefCounted
## Generates the translation data sets of the benchmark (#340) in user://bench_340/data/.
## One folder per data set, one resource file per "file" and locale ("files" split the keys into disjoint parts,
## like several CSV files of a project). Variants: "plain" (Translation), "compress" (OptimizedTranslation, like the
## CSV importer's compress option) and "ctx_plural" (Translation with an extra context message per key and plural keys).
## Shared by the GDScript and the C# benchmark: both read the data set list from get_sets().

const DATA_DIR := "user://bench_340/data"
const VERSION := "1"
const ACTIVE_LOCALE := "de"
const FALLBACK_LOCALE := "en"
const CONTEXT := "ui"
const LOCALES_12: PackedStringArray = ["en", "de", "fr", "es", "it", "pt", "ru", "ja", "zh", "ko", "pl", "nl"]
const LOCALES_2: PackedStringArray = ["en", "de"]


## Key i is missing in every locale except the fallback locale when this is true (lookup falls back to "en").
static func is_fallback_only(i: int) -> bool:
	return i % 10 == 9


## Key i also has a plural key "PL_<i>" in ctx_plural data sets.
static func has_plural(i: int) -> bool:
	return i % 4 == 0


static func key_name(i: int) -> String:
	return "UI_KEY_%05d" % i


static func plural_key_name(i: int) -> String:
	return "PL_%05d" % i


static func text(locale: String, i: int) -> String:
	return "%s text %d: the quick brown fox jumps over the lazy dog" % [locale, i]


## The data sets of a run. full = false gives the small set list of the quick check.
static func get_sets(full: bool) -> Array[Dictionary]:
	var sets: Array[Dictionary] = []
	if not full:
		sets.append(_make_set(1000, 2, 1, "plain"))
		sets.append(_make_set(1000, 12, 8, "plain"))
		sets.append(_make_set(1000, 12, 8, "compress"))
		sets.append(_make_set(1000, 12, 8, "ctx_plural"))
		return sets
	for variant in ["plain", "compress", "ctx_plural"]:
		for keys in [1000, 5000, 20000]:
			for locale_count in [2, 12]:
				for files in [1, 8]:
					sets.append(_make_set(keys, locale_count, files, variant))
	return sets


static func _make_set(keys: int, locale_count: int, files: int, variant: String) -> Dictionary:
	var set_name := "k%d_l%d_f%d_%s" % [keys, locale_count, files, variant]
	var locales := LOCALES_2 if locale_count == 2 else LOCALES_12
	# File-major order, like the project setting lists the imported CSV files: f0_en, f0_de, ..., f1_en, ...
	var paths := PackedStringArray()
	var needed_paths := PackedStringArray() # only the active and the fallback locale
	for f in files:
		for locale in locales:
			var path := "%s/%s/f%d_%s.res" % [DATA_DIR, set_name, f, locale]
			paths.append(path)
			if locale == ACTIVE_LOCALE or locale == FALLBACK_LOCALE:
				needed_paths.append(path)
	return {
		"name": set_name, "keys": keys, "locales": locale_count, "files": files, "variant": variant,
		"paths": paths, "needed_paths": needed_paths,
	}


## Creates the missing data sets. Returns the number of generated sets.
static func ensure_sets(sets: Array[Dictionary]) -> int:
	var generated := 0
	for data_set in sets:
		var marker := "%s/%s/done.txt" % [DATA_DIR, data_set.name]
		if FileAccess.file_exists(marker) and FileAccess.get_file_as_string(marker) == VERSION:
			continue
		var t0 := Time.get_ticks_msec()
		_generate(data_set)
		FileAccess.open(marker, FileAccess.WRITE).store_string(VERSION)
		generated += 1
		print("  generated %s in %d ms" % [data_set.name, Time.get_ticks_msec() - t0])
	return generated


static func _generate(data_set: Dictionary) -> void:
	DirAccess.make_dir_recursive_absolute("%s/%s" % [DATA_DIR, data_set.name])
	var keys: int = data_set.keys
	var files: int = data_set.files
	var locales := LOCALES_2 if data_set.locales == 2 else LOCALES_12
	var per_file := keys / files
	var path_index := 0
	for f in files:
		for locale in locales:
			var translation := Translation.new()
			translation.locale = locale
			var plural_forms := _plural_form_count(locale)
			for i in range(f * per_file, (f + 1) * per_file):
				if locale != FALLBACK_LOCALE and is_fallback_only(i):
					continue
				translation.add_message(key_name(i), text(locale, i))
				if data_set.variant == "ctx_plural":
					translation.add_message(key_name(i), text(locale, i) + " (ui)", CONTEXT)
					if has_plural(i):
						var forms := PackedStringArray()
						for form in plural_forms:
							forms.append("%s plural %d form %d" % [locale, i, form])
						translation.add_plural_message(plural_key_name(i), forms)
			var resource: Translation = translation
			if data_set.variant == "compress":
				var optimized := OptimizedTranslation.new()
				optimized.generate(translation)
				resource = optimized
			var err := ResourceSaver.save(resource, data_set.paths[path_index])
			assert(err == OK)
			path_index += 1


static func _plural_form_count(locale: String) -> int:
	# "nplurals=2; plural=(n != 1);"
	var rules := TranslationServer.get_plural_rules(locale)
	var start := rules.find("nplurals=")
	if start < 0:
		return 2
	return rules.substr(start + 9, rules.find(";", start) - start - 9).to_int()
