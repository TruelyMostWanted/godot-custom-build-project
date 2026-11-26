extends RefCounted
## Shared data helpers of the #343 benchmark (GDScript and C#). The BEFORE data are the #340 translation resources
## (user://bench_340/data/, made by the #340 generator). The AFTER data are the same translations converted once per
## "file" into one .lcat catalog (all locales of that file merged with TextLocalizationCatalog.add_translation()),
## cached in user://bench_343/data/<set>/f<file>.lcat. Delete user://bench_343/data to rebuild.

const Generator := preload("res://340-task-translation-benchmark/scripts/gd/generator.gd")
const DATA_DIR := "user://bench_343/data"
const VERSION := "1b"


static func locales_of(data_set: Dictionary) -> PackedStringArray:
	return Generator.LOCALES_2 if data_set.locales == 2 else Generator.LOCALES_12


## One catalog path per file of the data set.
static func lcat_paths(data_set: Dictionary) -> PackedStringArray:
	var paths := PackedStringArray()
	var files: int = data_set.files
	for f in files:
		paths.append("%s/%s/f%d.lcat" % [DATA_DIR, data_set.name, f])
	return paths


## Converts the #340 translations of the set into catalogs when not cached yet. Returns the number of errors.
static func ensure_lcat(data_set: Dictionary) -> int:
	var marker := "%s/%s/done.txt" % [DATA_DIR, data_set.name]
	if FileAccess.file_exists(marker) and FileAccess.get_file_as_string(marker) == VERSION + Generator.VERSION:
		return 0
	var t0 := Time.get_ticks_msec()
	DirAccess.make_dir_recursive_absolute("%s/%s" % [DATA_DIR, data_set.name])
	var locales := locales_of(data_set)
	var source_paths: PackedStringArray = data_set.paths
	if data_set.variant == "compress":
		# OptimizedTranslation keeps only hashes of the keys and cannot be converted: the catalog of a
		# compress set is built from the plain set with the same texts (the AFTER path has no compression).
		source_paths = Generator._make_set(data_set.keys, data_set.locales, data_set.files, "plain").paths
	var targets := lcat_paths(data_set)
	var files: int = data_set.files
	var errors := 0
	for f in files:
		var catalog := TextLocalizationCatalog.new()
		for li in locales.size():
			var translation: Translation = ResourceLoader.load(source_paths[f * locales.size() + li], "", ResourceLoader.CACHE_MODE_IGNORE)
			catalog.add_translation(translation)
		var err := catalog.save_to_file(targets[f])
		if err != OK:
			errors += 1
			printerr("  save_to_file failed (%d): %s" % [err, targets[f]])
	if errors == 0:
		FileAccess.open(marker, FileAccess.WRITE).store_string(VERSION + Generator.VERSION)
	print("  converted %s to catalogs in %d ms (%d errors)" % [data_set.name, Time.get_ticks_msec() - t0, errors])
	return errors


## Same key sequence as the #340 benchmark.
static func hit_index(j: int, keys: int) -> int:
	var i := (j * 7919) % keys
	return i - 1 if Generator.is_fallback_only(i) else i


## Mod layer: overrides every 10th key (i % 10 == 3) in "de".
static func is_mod_key(i: int) -> bool:
	return i % 10 == 3


static func mod_text(i: int) -> String:
	return "MOD de text %d" % i


static func message_count(data_set: Dictionary) -> int:
	var keys: int = data_set.keys
	var locale_count: int = data_set.locales
	var count: int = keys + (keys - keys / 10) * (locale_count - 1)
	if data_set.variant == "ctx_plural":
		count = count * 2 + keys / 4 * locale_count
	return count
