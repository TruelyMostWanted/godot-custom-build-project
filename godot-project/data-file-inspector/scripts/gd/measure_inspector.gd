@tool
extends EditorScript
## Measures how long the Inspector takes to show a data file (#361). Editor only: open this script in the script
## editor and run it with File > Run (Ctrl+Shift+X). The first run writes resources/big/units_20000.csv (20 000 rows)
## and resources/big/deep.json (20 000 objects, each nested 10 levels deep) and starts a scan; run it again after the
## import. Prints per file the load time and, as the best of RUNS, the time of inspect_object() (the Inspector is
## built synchronously) and the time until the next frame. Works on builds with and without the data file view, for
## a before/after comparison.

const RES := "res://data-file-inspector/resources/"
const BIG := RES + "big/"
const RUNS := 5


func _run() -> void:
	if not FileAccess.file_exists(BIG + "units_20000.csv") or not FileAccess.file_exists(BIG + "deep.json"):
		_write_files()
		EditorInterface.get_resource_filesystem().scan()
		print("Wrote the measurement files. Run the script again after the import.")
		return
	print("file | load ms | inspect_object() ms | until next frame ms")
	for path in [BIG + "units_20000.csv", BIG + "deep.json", "res://csv/resources/units.csv", "res://json/resources/enemies.json", "res://xml/resources/level.xml"]:
		await _measure(path)


func _write_files() -> void:
	DirAccess.make_dir_recursive_absolute(BIG)
	var classes := ["warrior", "ranger", "caster"]
	var csv := FileAccess.open(BIG + "units_20000.csv", FileAccess.WRITE)
	csv.store_line("name,class,hp,ranged")
	for i in 20000:
		csv.store_line("unit_%d,%s,%d,%s" % [i, classes[i % 3], i % 200 + 1, "true" if i % 2 else "false"])
	csv.close()
	# A data CSV, not a translation: keep the file as it is.
	var import_file := FileAccess.open(BIG + "units_20000.csv.import", FileAccess.WRITE)
	import_file.store_string("[remap]\n\nimporter=\"keep\"\n")
	import_file.close()

	var objects := []
	for i in 20000:
		var value: Variant = {"id": i, "name": "unit_%d" % i, "tags": ["a", "b"]}
		for depth in 10:
			value = {"level": depth, "child": value}
		objects.push_back(value)
	var json := FileAccess.open(BIG + "deep.json", FileAccess.WRITE)
	json.store_string(JSON.stringify({"objects": objects}, "\t"))
	json.close()


func _measure(path: String) -> void:
	var start := Time.get_ticks_usec()
	var resource := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REUSE)
	var load_ms := (Time.get_ticks_usec() - start) / 1000.0
	if resource == null:
		print("%s | could not load" % path.get_file())
		return

	var best_sync := INF
	var best_frame := INF
	for _run_index in RUNS:
		EditorInterface.inspect_object(null)
		await Engine.get_main_loop().process_frame
		start = Time.get_ticks_usec()
		EditorInterface.inspect_object(resource, "", true)
		best_sync = minf(best_sync, (Time.get_ticks_usec() - start) / 1000.0)
		await Engine.get_main_loop().process_frame
		best_frame = minf(best_frame, (Time.get_ticks_usec() - start) / 1000.0)
	print("%s | %.1f | %.1f | %.1f" % [path.get_file(), load_ms, best_sync, best_frame])
