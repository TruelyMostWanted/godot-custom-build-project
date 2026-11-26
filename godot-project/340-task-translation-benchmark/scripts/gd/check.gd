extends FeatureCheck
## Benchmark of Godot's translation system (#340), GDScript side. scripts/cs/Issue340TaskTranslationBenchmarkCheck.cs
## measures the same in C#. Default: quick run (4 small data sets, few calls) with sanity checks, as part of run-checks.ps1.
## Full run: godot --headless --path godot-project res://340-task-translation-benchmark/scenes/check_gd.tscn -- --bench-full
## Results: user://bench_340/gdscript_<quick|full>.csv and a table in the log.

const Generator := preload("res://340-task-translation-benchmark/scripts/gd/generator.gd")
const OUT_DIR := "user://bench_340"
const LABEL_COUNT := 1000

var _full := OS.get_cmdline_user_args().has("--bench-full")
var _calls := 4000 if _full else 1000
var _runs := 5 if _full else 3
var _csv := PackedStringArray(["language,set,keys,locales,files,translations,variant,metric,value,unit"])
var _table := PackedStringArray()


func _get_check_name() -> String:
	return "340-task-translation-benchmark"


func _run_checks() -> void:
	var t_start := Time.get_ticks_msec()
	print("  cpu: %s (%d threads), debug build: %s, editor: %s, version: %s" % [OS.get_processor_name(), OS.get_processor_count(),
			OS.is_debug_build(), OS.has_feature("editor"), Engine.get_version_info().string])
	print("  mode: %s, %d calls per run, median of %d runs after 1 warm-up run" % ["full" if _full else "quick", _calls, _runs])
	TranslationServer.clear() # the project's own translations (hello_de/hello_fr) would add to every loop
	expect_equal(ProjectSettings.get_setting("internationalization/locale/fallback", "en"), "en", "fallback locale is en")

	var sets := Generator.get_sets(_full)
	Generator.ensure_sets(sets)
	_bench_compare_locales()
	_check_duplicate_keys()
	_table.append("| set | translations | load ms | static MB | B/msg | hit | hit nl (last) | lang fallback | miss→fallback | miss→key | context | tr_n | switch 1k Labels ms |")
	_table.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
	for data_set in sets:
		await _bench_set(data_set)

	DirAccess.make_dir_recursive_absolute(OUT_DIR)
	var csv_path := "%s/gdscript_%s.csv" % [OUT_DIR, "full" if _full else "quick"]
	FileAccess.open(csv_path, FileAccess.WRITE).store_string("\n".join(_csv) + "\n")
	print("\n".join(_table))
	print("  csv: %s, total %.1f s" % [ProjectSettings.globalize_path(csv_path), (Time.get_ticks_msec() - t_start) / 1000.0])


## Bottleneck 2: compare_locales() builds an "a|b" String for every non-equal locale pair.
func _bench_compare_locales() -> void:
	var n := _calls * 10
	for pair in [["de", "de"], ["de", "fr"], ["de_DE", "de"]]:
		var a: String = pair[0]
		var b: String = pair[1]
		var samples: Array[float] = []
		for r in _runs + 1:
			var t0 := Time.get_ticks_usec()
			for j in n:
				TranslationServer.compare_locales(a, b)
			if r > 0:
				samples.append((Time.get_ticks_usec() - t0) * 1000.0 / n)
		var ns := _median(samples)
		_add_row({"name": "micro"}, "compare_locales_%s_%s_ns" % [a, b], ns, "ns")
		print("  compare_locales(%s, %s): %.1f ns" % [a, b, ns])


## Bottleneck 8: which translation wins when two translations of one locale have the same key.
func _check_duplicate_keys() -> void:
	var domain := TranslationServer.get_or_add_domain(&"bench_340_duplicates")
	var a := _single("A")
	var b := _single("B")
	var c := _single("C")
	var results := {}
	domain.set_locale_override("de")
	domain.add_translation(a)
	domain.add_translation(b)
	results["exact_ab"] = domain.translate(&"DUP")
	domain.clear()
	domain.add_translation(b)
	domain.add_translation(a)
	results["exact_ba"] = domain.translate(&"DUP")
	domain.set_locale_override("de_DE") # language match only: no early exit, the last match wins
	results["lang_ba"] = domain.translate(&"DUP")
	domain.set_locale_override("de")
	domain.clear()
	for t in [a, b, c]:
		domain.add_translation(t)
	domain.remove_translation(a)
	domain.add_translation(a)
	results["exact_abc_remove_a_add_a"] = domain.translate(&"DUP")
	TranslationServer.remove_domain(&"bench_340_duplicates")
	for key in results:
		_add_row({"name": "micro"}, "duplicate_" + key, results[key], "winner")
	print("  duplicate key winners: %s" % str(results))
	expect_equal(results["exact_ab"], "A", "exact locale: the first added translation wins (insertion order, not random)")
	expect_equal(results["exact_ba"], "B", "exact locale, other order: the first added translation wins")
	expect_equal(results["lang_ba"], "A", "language match (de_DE -> de): the last added translation wins")
	expect_equal(results["exact_abc_remove_a_add_a"], "C", "remove_translation() reorders: the last one moves to the front")


func _single(value: String) -> Translation:
	var t := Translation.new()
	t.locale = "de"
	t.add_message(&"DUP", value)
	return t


func _bench_set(data_set: Dictionary) -> void:
	var keys: int = data_set.keys
	var set_name: String = data_set.name
	TranslationServer.set_locale("de")

	# Bottleneck 5: load time and memory for all locales vs only the needed ones (active + fallback).
	if data_set.locales > 2:
		var mem_needed := Performance.get_monitor(Performance.MEMORY_STATIC)
		var t_needed := Time.get_ticks_usec()
		var needed := _load(data_set.needed_paths)
		_add_row(data_set, "load_needed_ms", (Time.get_ticks_usec() - t_needed) / 1000.0, "ms")
		_add_row(data_set, "static_needed_mb", (Performance.get_monitor(Performance.MEMORY_STATIC) - mem_needed) / 1048576.0, "MB")
		for t in needed:
			TranslationServer.remove_translation(t)
		needed.clear()
	var mem0 := Performance.get_monitor(Performance.MEMORY_STATIC)
	var t0 := Time.get_ticks_usec()
	var loaded := _load(data_set.paths)
	var load_ms := (Time.get_ticks_usec() - t0) / 1000.0
	var mem_mb := (Performance.get_monitor(Performance.MEMORY_STATIC) - mem0) / 1048576.0
	var messages := _message_count(data_set)
	_add_row(data_set, "load_ms", load_ms, "ms")
	_add_row(data_set, "static_mb", mem_mb, "MB")
	_add_row(data_set, "messages", messages, "count")
	_add_row(data_set, "bytes_per_message", mem_mb * 1048576.0 / messages, "B")

	# Lookup keys spread over the whole key range, the same sequence in GDScript and C#.
	var hit: Array[StringName] = []
	var hit_strings: Array[String] = []
	var fallback: Array[StringName] = []
	var missing: Array[StringName] = []
	var plural: Array[StringName] = []
	for j in _calls:
		hit.append(StringName(Generator.key_name(_hit_index(j, keys))))
		hit_strings.append(Generator.key_name(_hit_index(j, keys)))
		fallback.append(StringName(Generator.key_name(((j * 7919) % (keys / 10)) * 10 + 9)))
		missing.append(StringName("NO_KEY_%05d" % j))
		plural.append(StringName(Generator.plural_key_name(((j * 7919) % (keys / 4)) * 4)))

	expect_equal(tr(hit[1]), Generator.text("de", _hit_index(1, keys)), set_name + ": hit returns the de text")
	expect_equal(tr(fallback[1]), Generator.text("en", (7919 % (keys / 10)) * 10 + 9), set_name + ": key missing in de returns the en text")
	expect_equal(tr(missing[1]), "NO_KEY_00001", set_name + ": unknown key returns the key")

	var r := {}
	TranslationServer.get_or_add_domain(&"").set_enabled(false)
	r.baseline_disabled = _measure("tr", hit)
	TranslationServer.get_or_add_domain(&"").set_enabled(true)
	r.hit_active = _measure("tr", hit)
	r.hit_string_key = _measure("tr", hit_strings)
	r.hit_server_translate = _measure("server", hit)
	r.miss_fallback_locale = _measure("tr", fallback)
	r.miss_key = _measure("tr", missing)
	if data_set.variant == "ctx_plural":
		expect_equal(tr(hit[1], &"ui"), Generator.text("de", _hit_index(1, keys)) + " (ui)", set_name + ": context hit")
		expect_equal(tr_n(plural[0], plural[0], 5), "de plural 0 form 1", set_name + ": tr_n(5) returns plural form 1")
		r.hit_context = _measure("tr_ctx", hit)
		r.tr_n = _measure("tr_n", plural)
	TranslationServer.set_locale("de_DE")
	expect_equal(tr(hit[1]), Generator.text("de", _hit_index(1, keys)), set_name + ": de_DE finds the de text")
	r.hit_lang_fallback = _measure("tr", hit)
	if data_set.locales > 2:
		# Bottleneck 1: the cost of a hit depends on where the locale's translations are in the domain's list ("nl" is added last).
		TranslationServer.set_locale("nl")
		expect_equal(tr(hit[1]), Generator.text("nl", _hit_index(1, keys)), set_name + ": nl (added last) finds the nl text")
		r.hit_last_locale = _measure("tr", hit)
	TranslationServer.set_locale("de")
	for metric in r:
		_add_row(data_set, metric + "_ns", r[metric], "ns")

	# Re-translation of 1k Labels (auto_translate) on a locale switch: the set_locale() call itself, and with the next frame.
	var box := VBoxContainer.new()
	add_child(box)
	for j in LABEL_COUNT:
		var label := Label.new()
		label.text = hit[j % hit.size()]
		box.add_child(label)
	await get_tree().process_frame
	var switch_samples: Array[float] = []
	var frame_samples: Array[float] = []
	for s in _runs + 1:
		var ts := Time.get_ticks_usec()
		TranslationServer.set_locale("en" if s % 2 == 0 else "de")
		var ts1 := Time.get_ticks_usec()
		await get_tree().process_frame
		if s > 0:
			switch_samples.append((ts1 - ts) / 1000.0)
			frame_samples.append((Time.get_ticks_usec() - ts) / 1000.0)
	box.free()
	var switch_ms := _median(switch_samples)
	_add_row(data_set, "locale_switch_1k_labels_ms", switch_ms, "ms")
	_add_row(data_set, "locale_switch_1k_labels_frame_ms", _median(frame_samples), "ms")

	for t in loaded:
		TranslationServer.remove_translation(t)
	loaded.clear()

	_table.append("| %s | %d | %.1f | %.2f | %.0f | %.0f | %s | %.0f | %.0f | %.0f | %s | %s | %.2f |" % [set_name, data_set.paths.size(), load_ms,
			mem_mb, mem_mb * 1048576.0 / messages, r.hit_active, ("%.0f" % r.hit_last_locale) if r.has("hit_last_locale") else "-", r.hit_lang_fallback, r.miss_fallback_locale, r.miss_key,
			("%.0f" % r.hit_context) if r.has("hit_context") else "-", ("%.0f" % r.tr_n) if r.has("tr_n") else "-", switch_ms])
	print("  %s done" % set_name)


static func _hit_index(j: int, keys: int) -> int:
	var i := (j * 7919) % keys
	return i - 1 if Generator.is_fallback_only(i) else i


func _load(paths: PackedStringArray) -> Array[Translation]:
	var result: Array[Translation] = []
	for path in paths:
		var t: Translation = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
		TranslationServer.add_translation(t)
		result.append(t)
	return result


func _message_count(data_set: Dictionary) -> int:
	var keys: int = data_set.keys
	var count: int = keys + (keys - keys / 10) * (data_set.locales - 1)
	if data_set.variant == "ctx_plural":
		count = count * 2 + keys / 4 * data_set.locales
	return count


## ns per call: median over _runs runs after one warm-up run.
func _measure(kind: String, keys: Array) -> float:
	var samples: Array[float] = []
	for r in _runs + 1:
		var t0 := Time.get_ticks_usec()
		match kind:
			"tr":
				for k in keys:
					tr(k)
			"tr_ctx":
				for k in keys:
					tr(k, &"ui")
			"tr_n":
				for k in keys:
					tr_n(k, k, 5)
			"server":
				for k in keys:
					TranslationServer.translate(k)
		if r > 0:
			samples.append((Time.get_ticks_usec() - t0) * 1000.0 / keys.size())
	return _median(samples)


func _median(values: Array[float]) -> float:
	var sorted := values.duplicate()
	sorted.sort()
	return sorted[sorted.size() / 2]


func _add_row(data_set: Dictionary, metric: String, value: Variant, unit: String) -> void:
	var paths: PackedStringArray = data_set.get("paths", PackedStringArray())
	_csv.append("gdscript,%s,%s,%s,%s,%s,%s,%s,%s,%s" % [data_set.name, data_set.get("keys", ""), data_set.get("locales", ""),
			data_set.get("files", ""), paths.size() if data_set.has("paths") else "", data_set.get("variant", ""),
			metric, ("%.3f" % value) if value is float else str(value), unit])
