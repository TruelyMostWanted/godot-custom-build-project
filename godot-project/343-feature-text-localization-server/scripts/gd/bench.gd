extends FeatureCheck
## Before/after benchmark of the translation path (#343), GDScript side. scripts/cs/Issue343FeatureTextLocalizationServerBench.cs
## measures groups 1, 2, 4 and 5 in C#. BEFORE = Godot translations in TranslationServer (the data sets of #340),
## AFTER = the same translations as .lcat catalogs in TextLocalizationServer (adapter Translation in the main domain).
## Quick: godot --headless --path godot-project res://343-feature-text-localization-server/scenes/bench_gd.tscn -- --bench-quick
## Full:  ... -- --bench-full   Options: --bench-groups=lookup,load,parity,ui   --bench-set=<substring of the set name>
## Groups: lookup (1 code paths, 2 cases incl. mod layer), ui (3), load (4 memory + 5 startup), parity (6 correctness).
## Results: one log line per value "343 bench <set> <before|after> <metric> <value> <unit>" and user://bench_343/results_gd.csv.

const Generator := preload("res://340-task-translation-benchmark/scripts/gd/generator.gd")
const BenchData := preload("res://343-feature-text-localization-server/scripts/gd/bench_data.gd")
const OUT_DIR := "user://bench_343"
const UI_KINDS: PackedStringArray = ["label", "button", "richtext", "lineedit", "optionbutton", "itemlist", "tree", "tabs", "popupmenu", "all"]
const PARITY_LOCALES: PackedStringArray = ["de", "de_DE", "en", "fr"]
const PARITY_COUNTS: PackedInt32Array = [0, 1, 2, 5]
const MOD_PRIORITY := 100

var _args := OS.get_cmdline_user_args()
var _full := _args.has("--bench-full")
var _calls := 4000 if _full else 1000
var _runs := 5 if _full else 3
var _ui_count := 1000 if _full else 100
var _groups := PackedStringArray()
var _set_filter := ""
var _csv := PackedStringArray(["language,set,keys,locales,files,translations,variant,path,metric,value,unit"])


func _get_check_name() -> String:
	return "343-text-localization-bench"


func _arg_value(prefix: String) -> String:
	for arg in _args:
		if arg.begins_with(prefix):
			return arg.substr(prefix.length())
	return ""


func _group(group_name: String) -> bool:
	return _groups.is_empty() or _groups.has(group_name)


func _run_checks() -> void:
	var t_start := Time.get_ticks_msec()
	_set_filter = _arg_value("--bench-set=")
	var groups_arg := _arg_value("--bench-groups=")
	if groups_arg != "":
		_groups = groups_arg.split(",")
	print("  cpu: %s (%d threads), debug build: %s, editor: %s, version: %s" % [OS.get_processor_name(), OS.get_processor_count(),
			OS.is_debug_build(), OS.has_feature("editor"), Engine.get_version_info().string])
	print("  mode: %s, %d calls per run, median of %d runs after 1 warm-up run, UI nodes: %d" % ["full" if _full else "quick", _calls, _runs, _ui_count])
	_reset()
	expect_equal(ProjectSettings.get_setting("internationalization/locale/fallback", "en"), "en", "fallback locale is en")

	var sets: Array[Dictionary] = []
	for data_set in Generator.get_sets(_full):
		if _set_filter == "" or String(data_set.name).contains(_set_filter):
			sets.append(data_set)
	Generator.ensure_sets(sets)
	for data_set in sets:
		expect_equal(BenchData.ensure_lcat(data_set), 0, "%s: converted to catalogs" % data_set.name)
	for data_set in sets:
		if _group("lookup"):
			await _bench_lookup(data_set)
		if _group("load"):
			await _bench_load(data_set)
		if _group("parity"):
			_bench_parity(data_set)
		print("  %s done" % data_set.name)
	if _group("ui"):
		await _bench_ui()

	_reset()
	DirAccess.make_dir_recursive_absolute(OUT_DIR)
	var csv_path := OUT_DIR + "/results_gd.csv"
	FileAccess.open(csv_path, FileAccess.WRITE).store_string("\n".join(_csv) + "\n")
	print("  csv: %s, total %.1f s" % [ProjectSettings.globalize_path(csv_path), (Time.get_ticks_msec() - t_start) / 1000.0])


# ---------------------------------------------------------------------------------------------------------------------
# Set up BEFORE (Godot translations) and AFTER (catalogs). Only one path is active at a time.

func _reset() -> void:
	TextLocalizationServer.clear_catalogs() # removes the adapter translation from the main domain
	TranslationServer.clear()
	TranslationServer.set_locale("de")


func _load_res(paths: PackedStringArray) -> Array[Translation]:
	var result: Array[Translation] = []
	for path in paths:
		var translation: Translation = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
		TranslationServer.add_translation(translation)
		result.append(translation)
	return result


func _use_before(data_set: Dictionary) -> Array[Translation]:
	_reset()
	return _load_res(data_set.paths)


## Returns the number of catalogs added.
func _use_after(data_set: Dictionary) -> int:
	_reset()
	var manifest := FilesManifest.new()
	manifest.file_paths = BenchData.lcat_paths(data_set)
	return TextLocalizationServer.add_catalog_manifest(manifest)


func _set_locale(mode: String, locale: String) -> void:
	if mode == "after":
		TranslationServer.set_locale(locale)
	else:
		TranslationServer.set_locale(locale)


func _row(data_set: Dictionary, path: String, metric: String, value: Variant, unit: String) -> void:
	var text := ("%.3f" % value) if value is float else str(value)
	var paths: PackedStringArray = data_set.get("paths", PackedStringArray())
	var translations: Variant = paths.size() if data_set.has("paths") else ""
	_csv.append("gdscript,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s" % [data_set.name, data_set.get("keys", ""), data_set.get("locales", ""),
			data_set.get("files", ""), translations, data_set.get("variant", ""), path, metric, text, unit])
	print("343 bench %s %s %s %s %s" % [data_set.name, path, metric, text, unit])


func _median(values: Array[float]) -> float:
	var sorted := values.duplicate()
	sorted.sort()
	return sorted[sorted.size() / 2]


# ---------------------------------------------------------------------------------------------------------------------
# Groups 1 and 2: code paths and cases (ns per call, median of runs after one warm-up run).

func _bench_lookup(data_set: Dictionary) -> void:
	var keys: int = data_set.keys
	var hit: Array[StringName] = []
	var hit_strings: Array[String] = []
	var fallback: Array[StringName] = []
	var missing: Array[StringName] = []
	var plural: Array[StringName] = []
	for j in _calls:
		var index := BenchData.hit_index(j, keys)
		hit.append(StringName(Generator.key_name(index)))
		hit_strings.append(Generator.key_name(index))
		fallback.append(StringName(Generator.key_name(((j * 7919) % (keys / 10)) * 10 + 9)))
		missing.append(StringName("NO_KEY_%05d" % j))
		plural.append(StringName(Generator.plural_key_name(((j * 7919) % (keys / 4)) * 4)))
	var ctx_plural: bool = data_set.variant == "ctx_plural"
	var ui := &"ui"
	var none := &""
	# name, locale, keys, context, code paths before, code paths added after
	var cases: Array[Dictionary] = [
		{"name": "hit", "locale": "de", "list": hit, "ctx": none, "before": ["tr", "tr_str", "atr", "ts"], "after": ["tls", "tls_str", "tls_id", "tls_en", "tls_args"]},
		{"name": "lang_fallback", "locale": "de_DE", "list": hit, "ctx": none, "before": ["tr", "atr", "ts"], "after": ["tls", "tls_id"]},
		{"name": "fallback_locale", "locale": "de", "list": fallback, "ctx": none, "before": ["tr", "atr", "ts"], "after": ["tls", "tls_id"]},
		{"name": "miss", "locale": "de", "list": missing, "ctx": none, "before": ["tr", "atr", "ts"], "after": ["tls"]},
	]
	if ctx_plural:
		cases.append({"name": "context", "locale": "de", "list": hit, "ctx": ui, "before": ["tr_ctx", "ts_ctx"], "after": ["tls_ctx", "tls_id"]})
		cases.append({"name": "plural", "locale": "de", "list": plural, "ctx": none, "before": ["tr_n", "ts_plural"], "after": ["tls_plural", "tls_id_n"]})

	for mode in ["before", "after"]:
		var after: bool = mode == "after"
		var loaded: Array[Translation] = []
		if after:
			expect_equal(_use_after(data_set), data_set.files, "%s: all catalogs added" % data_set.name)
		else:
			loaded = _use_before(data_set)
		var label := "%s %s" % [data_set.name, mode]
		_set_locale(mode, "de")
		expect_equal(tr(hit[1]), Generator.text("de", BenchData.hit_index(1, keys)), label + ": hit returns the de text")
		expect_equal(tr(fallback[1]), Generator.text("en", (7919 % (keys / 10)) * 10 + 9), label + ": key missing in de returns the en text")
		expect_equal(tr(missing[1]), "NO_KEY_00001", label + ": unknown key returns the key")
		if ctx_plural:
			expect_equal(tr(hit[1], ui), Generator.text("de", BenchData.hit_index(1, keys)) + " (ui)", label + ": context hit")
			expect_equal(tr_n(plural[0], plural[0], 5), "de plural 0 form 1", label + ": tr_n(5) returns plural form 1")
		_set_locale(mode, "de_DE")
		expect_equal(tr(hit[1]), Generator.text("de", BenchData.hit_index(1, keys)), label + ": de_DE finds the de text")
		_set_locale(mode, "de")

		# Cost of the call itself: main domain disabled (the adapter is in the main domain in the AFTER path).
		var main := TranslationServer.get_or_add_domain(&"")
		main.set_enabled(false)
		_row(data_set, mode, "call_overhead_ns", _measure("tr", hit), "ns")
		main.set_enabled(true)

		for c in cases:
			_set_locale(mode, c.locale)
			var kinds: Array = c.before.duplicate()
			var ids: Array[int] = []
			if after:
				kinds.append_array(c.after)
				ids = _ids(c["list"], c["ctx"])
			for kind in kinds:
				var ns := _measure(kind, c["list"], ids, hit_strings)
				_row(data_set, mode, "%s_%s_ns" % [c.name, kind], ns, "ns")
		_set_locale(mode, "de")

		if after:
			_bench_mod_layer(data_set, hit, hit_strings)
		loaded.clear()
		await get_tree().process_frame
	_reset()


func _ids(keys: Array, context: StringName) -> Array[int]:
	var ids: Array[int] = []
	for k in keys:
		ids.append(TextLocalizationServer.get_key_id(k, context))
	return ids


## AFTER only: a mod layer (priority 100) overrides every 10th key in "de". Hit cost and correctness with it.
func _bench_mod_layer(data_set: Dictionary, hit: Array[StringName], hit_strings: Array[String]) -> void:
	var keys: int = data_set.keys
	var mod := TextLocalizationCatalog.new()
	var mod_keys := 0
	for i in keys:
		if BenchData.is_mod_key(i):
			mod.set_text(StringName(Generator.key_name(i)), "de", BenchData.mod_text(i))
			mod_keys += 1
	TextLocalizationServer.add_catalog(mod, MOD_PRIORITY)
	TranslationServer.set_locale("de")
	var wrong_translate := 0
	var wrong_tr := 0
	var wrong_en := 0
	var examples := PackedStringArray()
	for i in keys:
		var key := StringName(Generator.key_name(i))
		var expected := Generator.text("de", i)
		if BenchData.is_mod_key(i):
			expected = BenchData.mod_text(i)
		elif Generator.is_fallback_only(i):
			expected = Generator.text("en", i)
		var got := TextLocalizationServer.translate(key)
		if got != expected:
			wrong_translate += 1
			if examples.size() < 5:
				examples.append("%s: expected '%s' got '%s'" % [key, expected, got])
		if tr(key) != expected:
			wrong_tr += 1
		if TextLocalizationServer.translate_for_locale("en", key) != Generator.text("en", i):
			wrong_en += 1
	_row(data_set, "after", "mod_keys", mod_keys, "count")
	_row(data_set, "after", "mod_wrong_translate", wrong_translate, "count")
	_row(data_set, "after", "mod_wrong_tr", wrong_tr, "count")
	_row(data_set, "after", "mod_wrong_en_untouched", wrong_en, "count")
	expect(wrong_translate == 0 and wrong_tr == 0 and wrong_en == 0, "%s after: mod layer overrides exactly its keys %s" % [data_set.name, str(examples)])
	var ids := _ids(hit, &"")
	_row(data_set, "after", "mod_hit_tr_ns", _measure("tr", hit), "ns")
	_row(data_set, "after", "mod_hit_tls_ns", _measure("tls", hit), "ns")
	_row(data_set, "after", "mod_hit_tls_id_ns", _measure("tls_id", hit, ids), "ns")
	TextLocalizationServer.remove_catalog(mod)


## ns per call. kind: tr, tr_str (String key), atr, ts (TranslationServer.translate), tr_ctx, ts_ctx, tr_n, ts_plural (BEFORE and AFTER);
## tls* = TextLocalizationServer (AFTER only): tls, tls_str, tls_ctx, tls_plural (count 5), tls_args (args dictionary),
## tls_en (translate_for_locale), tls_id / tls_id_n (translate_id with key ids).
func _measure(kind: String, keys: Array, ids: Array = [], strings: Array = []) -> float:
	var samples: Array[float] = []
	var ui := &"ui"
	var none := &""
	var args := {"count": 3, "name": "x"}
	var calls := keys.size()
	for r in _runs + 1:
		var t0 := Time.get_ticks_usec()
		match kind:
			"tr":
				for k in keys:
					tr(k)
			"tr_str":
				for k in strings:
					tr(k)
			"atr":
				for k in keys:
					atr(k)
			"ts":
				for k in keys:
					TranslationServer.translate(k)
			"tr_ctx":
				for k in keys:
					tr(k, ui)
			"ts_ctx":
				for k in keys:
					TranslationServer.translate(k, ui)
			"tr_n":
				for k in keys:
					tr_n(k, k, 5)
			"ts_plural":
				for k in keys:
					TranslationServer.translate_plural(k, k, 5)
			"tls":
				for k in keys:
					TextLocalizationServer.translate(k)
			"tls_str":
				for k in strings:
					TextLocalizationServer.translate(k)
			"tls_ctx":
				for k in keys:
					TextLocalizationServer.translate(k, ui)
			"tls_plural":
				for k in keys:
					TextLocalizationServer.translate(k, none, 5)
			"tls_args":
				for k in keys:
					TextLocalizationServer.translate(k, none, -1, args)
			"tls_en":
				for k in keys:
					TextLocalizationServer.translate_for_locale("en", k)
			"tls_id":
				for id in ids:
					TextLocalizationServer.translate_id(id)
			"tls_id_n":
				for id in ids:
					TextLocalizationServer.translate_id(id, 5)
		if r > 0:
			samples.append((Time.get_ticks_usec() - t0) * 1000.0 / calls)
	return _median(samples)


# ---------------------------------------------------------------------------------------------------------------------
# Groups 4 and 5: load time, memory, references, locales loaded.

func _snap() -> Dictionary:
	return {"mem": Performance.get_monitor(Performance.MEMORY_STATIC), "peak": float(OS.get_static_memory_peak_usage()),
			"obj": Performance.get_monitor(Performance.OBJECT_COUNT), "res": Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT)}


## Rows for the delta since snapshot s0. tag "" = after load, else a suffix (_after_switch, _all_locales).
func _emit_delta(data_set: Dictionary, mode: String, tag: String, s0: Dictionary, messages: int) -> void:
	var now := _snap()
	var delta_mem: float = now["mem"] - s0["mem"]
	_row(data_set, mode, "static_mb" + tag, delta_mem / 1048576.0, "MB")
	_row(data_set, mode, "bytes_per_message" + tag, delta_mem / messages, "B")
	_row(data_set, mode, "peak_mb" + tag, (now["peak"] - s0["peak"]) / 1048576.0, "MB")
	_row(data_set, mode, "objects" + tag, int(now["obj"] - s0["obj"]), "count")
	_row(data_set, mode, "resources" + tag, int(now["res"] - s0["res"]), "count")
	_row(data_set, mode, "translation_objects" + tag, TranslationServer.get_or_add_domain(&"").get_translations().size(), "count")
	_row(data_set, mode, "catalog_objects" + tag, TextLocalizationServer.get_catalogs().size(), "count")


func _loaded_locales() -> int:
	var count := 0
	for catalog: LocalizationCatalog in TextLocalizationServer.get_catalogs():
		for locale in catalog.locales:
			if catalog.is_locale_loaded(locale):
				count += 1
	return count


func _bench_load(data_set: Dictionary) -> void:
	var messages := BenchData.message_count(data_set)
	var files: int = data_set.files
	var total_locales: int = files * int(data_set.locales)
	var keys: int = data_set.keys

	# BEFORE: only the needed files (en + de), then all files (what load_project_translations() does).
	if data_set.locales > 2:
		_reset()
		var s_needed := _snap()
		var t_needed := Time.get_ticks_usec()
		_load_res(data_set.needed_paths)
		_row(data_set, "before", "load_needed_ms", (Time.get_ticks_usec() - t_needed) / 1000.0, "ms")
		_row(data_set, "before", "static_needed_mb", (_snap()["mem"] - s_needed["mem"]) / 1048576.0, "MB")
	_reset()
	var s0 := _snap()
	var t0 := Time.get_ticks_usec()
	var loaded := _load_res(data_set.paths)
	_row(data_set, "before", "load_ms", (Time.get_ticks_usec() - t0) / 1000.0, "ms")
	_row(data_set, "before", "messages", messages, "count")
	_row(data_set, "before", "locales_loaded", total_locales, "count")
	_row(data_set, "before", "locales_total", total_locales, "count")
	_emit_delta(data_set, "before", "", s0, messages)
	# Estimate: a Translation holds StringName key and StringName value per message (OptimizedTranslation: none).
	_row(data_set, "before", "stringname_est", 0 if data_set.variant == "compress" else keys + messages, "count")
	var t_switch := Time.get_ticks_usec()
	TranslationServer.set_locale("fr")
	_row(data_set, "before", "switch_fr_sync_ms", (Time.get_ticks_usec() - t_switch) / 1000.0, "ms")
	await get_tree().process_frame
	_emit_delta(data_set, "before", "_after_switch", s0, messages)
	loaded.clear()

	# AFTER: add_catalog_manifest reads the directory of every catalog; only the chain locales (en + de) are loaded.
	_reset()
	var a0 := _snap()
	var ta := Time.get_ticks_usec()
	var manifest := FilesManifest.new()
	manifest.file_paths = BenchData.lcat_paths(data_set)
	var added := TextLocalizationServer.add_catalog_manifest(manifest)
	_row(data_set, "after", "load_ms", (Time.get_ticks_usec() - ta) / 1000.0, "ms")
	expect_equal(added, files, "%s: add_catalog_manifest added all catalogs" % data_set.name)
	_row(data_set, "after", "messages", messages, "count")
	_row(data_set, "after", "locales_loaded", _loaded_locales(), "count")
	_row(data_set, "after", "locales_total", total_locales, "count")
	_row(data_set, "after", "key_count", TextLocalizationServer.get_key_count(), "count")
	_row(data_set, "after", "stringname_est", TextLocalizationServer.get_key_count(), "count")
	_emit_delta(data_set, "after", "", a0, messages)
	var ts := Time.get_ticks_usec()
	TranslationServer.set_locale("fr")
	_row(data_set, "after", "switch_fr_sync_ms", (Time.get_ticks_usec() - ts) / 1000.0, "ms")
	await get_tree().process_frame
	_row(data_set, "after", "locales_loaded_after_switch", _loaded_locales(), "count")
	_emit_delta(data_set, "after", "_after_switch", a0, messages)
	TranslationServer.set_locale("de")
	await get_tree().process_frame
	# Force every locale (load_locale), the worst case equal to the BEFORE path.
	var tf := Time.get_ticks_usec()
	for catalog: LocalizationCatalog in TextLocalizationServer.get_catalogs():
		for locale in catalog.locales:
			catalog.load_locale(locale)
	_row(data_set, "after", "load_all_locales_ms", (Time.get_ticks_usec() - tf) / 1000.0, "ms")
	_row(data_set, "after", "locales_loaded_all", _loaded_locales(), "count")
	_emit_delta(data_set, "after", "_all_locales", a0, messages)
	_reset()


# ---------------------------------------------------------------------------------------------------------------------
# Group 6: correctness parity BEFORE (Godot translations) vs AFTER (server, translate_for_locale, tr() through the adapter).

func _lookup(mode: String, locale: String, key: StringName, context: StringName, n: int, plural_key: StringName) -> String:
	match mode:
		"before":
			if n < 0:
				return TranslationServer.translate(key, context)
			return TranslationServer.translate_plural(key, plural_key, n, context)
		"server":
			return TextLocalizationServer.translate(key, context, n)
		"for_locale":
			return TextLocalizationServer.translate_for_locale(locale, key, context, n)
		_:
			if n < 0:
				return tr(key, context)
			return tr_n(key, plural_key, n, context)


## All lookups of the set in a fixed order. build = true also fills descs (one description per entry).
func _parity_collect(data_set: Dictionary, mode: String, descs: PackedStringArray, build: bool) -> PackedStringArray:
	var out := PackedStringArray()
	var keys: int = data_set.keys
	var ctx_plural: bool = data_set.variant == "ctx_plural"
	var ui := &"ui"
	var none := &""
	for locale in PARITY_LOCALES:
		TranslationServer.set_locale(locale)
		var entries: Array[Array] = []
		for i in keys:
			entries.append([StringName(Generator.key_name(i)), none, -1, none])
			if ctx_plural:
				entries.append([StringName(Generator.key_name(i)), ui, -1, none])
				if Generator.has_plural(i):
					for n in PARITY_COUNTS:
						entries.append([StringName(Generator.plural_key_name(i)), none, n, StringName(Generator.plural_key_name(i))])
		for m in 3:
			var miss := StringName("NO_KEY_PARITY_%d" % m)
			entries.append([miss, none, -1, none])
			entries.append([miss, ui, -1, none])
			for n in PARITY_COUNTS:
				entries.append([miss, none, n, StringName("NO_KEY_PARITY_%d_PL" % m)])
		for e in entries:
			out.append(_lookup(mode, locale, e[0], e[1], e[2], e[3]))
			if build:
				descs.append("%s|%s|ctx=%s|n=%d" % [locale, e[0], e[1], e[2]])
	return out


func _bench_parity(data_set: Dictionary) -> void:
	var descs := PackedStringArray()
	_use_before(data_set)
	var before := _parity_collect(data_set, "before", descs, true)
	expect_equal(_use_after(data_set), data_set.files, "%s: catalogs added for parity" % data_set.name)
	for mode in ["server", "for_locale", "tr"]:
		var after := _parity_collect(data_set, mode, descs, false)
		var mismatches := 0
		var documented := 0
		var shown := 0
		for idx in before.size():
			if before[idx] == after[idx]:
				continue
			# Documented: Godot returns the plural key for a plural miss with n != 1, the catalog returns the key.
			if descs[idx].contains("NO_KEY_PARITY") and not descs[idx].ends_with("n=-1") and before[idx].ends_with("_PL"):
				documented += 1
				continue
			mismatches += 1
			if shown < 10:
				shown += 1
				print("343 bench %s parity example %s %s before='%s' after='%s'" % [data_set.name, mode, descs[idx], before[idx], after[idx]])
		_row(data_set, "after", "parity_entries_" + mode, before.size(), "count")
		_row(data_set, "after", "parity_mismatch_" + mode, mismatches, "count")
		_row(data_set, "after", "parity_documented_plural_miss_" + mode, documented, "count")
		expect_equal(mismatches, 0, "%s: parity before/after (%s) over %d lookups, %d documented plural-miss differences" % [data_set.name, mode, before.size(), documented])
	TranslationServer.set_locale("de")
	_reset()


# ---------------------------------------------------------------------------------------------------------------------
# Group 3: UI. Scene tree based: translated controls with key texts. Smaller set: 12 locales, 1 file, plain
# (k5000 in the full run, k1000 in the quick run).

func _bench_ui() -> void:
	var data_set: Dictionary = Generator._make_set(5000 if _full else 1000, 12, 1, "plain")
	var one: Array[Dictionary] = [data_set]
	Generator.ensure_sets(one)
	expect_equal(BenchData.ensure_lcat(data_set), 0, "%s: converted to catalogs" % data_set.name)
	var texts := PackedStringArray()
	for j in _ui_count:
		texts.append(Generator.key_name(BenchData.hit_index(j, data_set.keys)))
	for mode in ["before", "after"]:
		var loaded: Array[Translation] = []
		if mode == "before":
			loaded = _use_before(data_set)
		else:
			_use_after(data_set)
		await get_tree().process_frame
		for kind in UI_KINDS:
			# Initial translation: time from add_child until the end of the next frame, fresh nodes each run.
			var initial: Array[float] = []
			var box: Node = null
			for r in _runs + 1:
				if box != null:
					box.free()
				box = _ui_build(kind, texts)
				var t0 := Time.get_ticks_usec()
				add_child(box)
				await get_tree().process_frame
				if r > 0:
					initial.append((Time.get_ticks_usec() - t0) / 1000.0)
			_row(data_set, mode, "ui_%s_initial_ms" % kind, _median(initial), "ms")
			# Locale switch: the synchronous part (set_locale call) and including two frames.
			for api in ["ts", "ls"]:
				var sync: Array[float] = []
				var frames: Array[float] = []
				for s in _runs + 1:
					var locale := "en" if s % 2 == 0 else "de"
					var ts0 := Time.get_ticks_usec()
					if api == "ts":
						TranslationServer.set_locale(locale)
					else:
						TranslationServer.set_locale(locale)
					var ts1 := Time.get_ticks_usec()
					await get_tree().process_frame
					await get_tree().process_frame
					if s > 0:
						sync.append((ts1 - ts0) / 1000.0)
						frames.append((Time.get_ticks_usec() - ts0) / 1000.0)
				_row(data_set, mode, "ui_%s_switch_%s_sync_ms" % [kind, api], _median(sync), "ms")
				_row(data_set, mode, "ui_%s_switch_%s_2frames_ms" % [kind, api], _median(frames), "ms")
			box.free()
			await get_tree().process_frame
		loaded.clear()
	_reset()


func _ui_build(kind: String, texts: PackedStringArray) -> Node:
	var box := VBoxContainer.new()
	if kind == "all":
		for k in UI_KINDS.size() - 1:
			_ui_fill(box, UI_KINDS[k], texts)
	else:
		_ui_fill(box, kind, texts)
	return box


func _ui_fill(box: Node, kind: String, texts: PackedStringArray) -> void:
	match kind:
		"label":
			for text in texts:
				var node := Label.new()
				node.text = text
				box.add_child(node)
		"button":
			for text in texts:
				var node := Button.new()
				node.text = text
				box.add_child(node)
		"richtext":
			for text in texts:
				var node := RichTextLabel.new()
				node.fit_content = true
				node.text = text
				box.add_child(node)
		"lineedit":
			for text in texts:
				var node := LineEdit.new()
				node.placeholder_text = text
				box.add_child(node)
		"optionbutton":
			var node := OptionButton.new()
			for text in texts:
				node.add_item(text)
			box.add_child(node)
		"itemlist":
			var node := ItemList.new()
			for text in texts:
				node.add_item(text)
			box.add_child(node)
		"tree":
			var node := Tree.new()
			var root := node.create_item()
			for text in texts:
				node.create_item(root).set_text(0, text)
			box.add_child(node)
		"tabs":
			var node := TabContainer.new()
			box.add_child(node)
			for text in texts:
				var page := Control.new()
				node.add_child(page)
				node.set_tab_title(node.get_tab_count() - 1, text)
		"popupmenu":
			var node := PopupMenu.new()
			for text in texts:
				node.add_item(text)
			box.add_child(node)
