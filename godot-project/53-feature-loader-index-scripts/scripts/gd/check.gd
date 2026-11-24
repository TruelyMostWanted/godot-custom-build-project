# Written before the engine build of #53; run after the merge.
extends FeatureCheck
## GDScript checks of 53-feature-loader-index-scripts. Check the same things as scripts/cs/Issue53FeatureLoaderIndexScriptsCheck.cs.
# Script loaders that override only _get_recognized_extensions() are now found through the extension index,
# and the binary loader too. The choice of the loader must stay as it was (registration order, first match wins).
# A loader that changes its list calls ResourceLoader.rebuild_extension_index().
# Assumption: a loader reports its identity through resource_name (load) and through the type name it returns (get_resource_type).
# Assumption: res://icon.svg is an imported asset of this project (icon.svg.import), so it stands for the .import path.

const ExtLoader := preload("res://53-feature-loader-index-scripts/scripts/gd/ext_loader.gd")
const PathLoader := preload("res://53-feature-loader-index-scripts/scripts/gd/path_loader.gd")

const ROOT := "user://53-feature-loader-index-scripts/gd/"
const PERF_COUNT := 10000
const EXTRA_LOADERS := 100

var _one := ROOT + "one.t53x"
var _upper := ROOT + "UPPER.T53X"
var _old := ROOT + "data.t53old"
var _new := ROOT + "data.t53new"
var _dotted := ROOT + "archive.pack.t53"
var _plain_dotted := ROOT + "archive.t53"
var _special := ROOT + "item_special.dat"
var _plain_tres := ROOT + "plain.tres"
var _plain_res := ROOT + "plain.res"


func _get_check_name() -> String:
	return "53-feature-loader-index-scripts"


func _run_checks() -> void:
	_cleanup()
	_prepare()
	_check_builtin()
	_check_order()
	_check_dotted()
	_check_path_based()
	_check_refresh()
	_check_performance()
	_cleanup()


func _ext_loader(tag: String, extensions: PackedStringArray) -> ResourceFormatLoader:
	var loader: ResourceFormatLoader = ExtLoader.new()
	loader.tag = tag
	loader.extensions = extensions
	loader.type_name = "Type" + tag
	return loader


func _path_loader(tag: String, ending: String) -> ResourceFormatLoader:
	var loader: ResourceFormatLoader = PathLoader.new()
	loader.tag = tag
	loader.path_ending = ending
	loader.type_name = "Type" + tag
	return loader


func _winner(path: String) -> String:
	if not ResourceLoader.exists(path):
		return "<none>"
	var res := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	return "<null>" if res == null else res.resource_name


func _type(path: String) -> String:
	return ResourceLoader.get_resource_type(path)


func _write_file(path: String) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string("x")


func _prepare() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	for path in [_one, _upper, _old, _new, _dotted, _plain_dotted, _special]:
		_write_file(path)
	expect_equal(ResourceSaver.save(Gradient.new(), _plain_tres), OK, "saved plain.tres")
	expect_equal(ResourceSaver.save(Curve.new(), _plain_res), OK, "saved plain.res")


func _check_builtin() -> void:
	var tres := ResourceLoader.load(_plain_tres, "", ResourceLoader.CACHE_MODE_IGNORE)
	var res := ResourceLoader.load(_plain_res, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(tres is Gradient, "plain.tres loads as Gradient with the built-in text loader")
	expect(res is Curve, "plain.res loads as Curve with the binary loader, which is now indexed")
	expect_equal(_type(_plain_tres), "Gradient", "get_resource_type(plain.tres) is Gradient")
	expect_equal(_type(_plain_res), "Curve", "get_resource_type(plain.res) is Curve")
	expect(ResourceLoader.exists(_plain_res, "Curve"), "exists(plain.res, \"Curve\") is true with a type hint")
	expect(ResourceLoader.exists("res://icon.svg"), "exists() is true for the imported res://icon.svg (.import file)")
	expect_equal(_type("res://icon.svg"), "CompressedTexture2D", "get_resource_type(res://icon.svg) comes from the importer")
	expect(ResourceLoader.load("res://icon.svg") is Texture2D, "res://icon.svg loads as Texture2D")
	expect(not ResourceLoader.exists(ROOT + "missing.unknownext"), "exists() is false for an unknown extension")
	expect_equal(_type(ROOT + "missing.unknownext"), "", "get_resource_type() is empty for an unknown extension")


func _check_order() -> void:
	expect_equal(_winner(_one), "<none>", "no loader for .t53x before a loader is registered")
	var a := _ext_loader("A", ["t53x"])
	var b := _ext_loader("B", ["T53X"])
	var c := _ext_loader("C", ["t53x"])
	ResourceLoader.add_resource_format_loader(a)
	ResourceLoader.add_resource_format_loader(b)
	expect_equal(_winner(_one), "A", "two indexed script loaders for one extension: the first registered loads")
	expect_equal(_type(_one), "TypeA", "get_resource_type() answers from the first registered loader")
	ResourceLoader.add_resource_format_loader(c, true)
	expect_equal(_winner(_one), "C", "a script loader registered with at_front = true loads first")
	expect_equal(_winner(_upper), "C", "the extension match is case-insensitive (UPPER.T53X)")
	ResourceLoader.remove_resource_format_loader(c)
	ResourceLoader.remove_resource_format_loader(a)
	expect_equal(_winner(_one), "B", "after removing C and A the loader declaring T53X loads (B)")
	expect_equal(_type(_one), "TypeB", "get_resource_type() answers from B")
	ResourceLoader.remove_resource_format_loader(b)
	expect_equal(_winner(_one), "<none>", "after removing every loader no loader handles .t53x")
	expect_equal(_type(_one), "", "get_resource_type() is empty without a loader")


func _check_dotted() -> void:
	var d := _ext_loader("D", ["pack.t53"])
	ResourceLoader.add_resource_format_loader(d)
	expect_equal(_winner(_dotted), "D", "an extension with a dot (pack.t53) is found for archive.pack.t53")
	expect_equal(_winner(_plain_dotted), "<none>", "archive.t53 does not match the extension pack.t53")
	ResourceLoader.remove_resource_format_loader(d)


func _check_path_based() -> void:
	# A (index), P (path based), B (index): registration order decides, not the kind of loader.
	var a := _ext_loader("A", ["dat"])
	var p := _path_loader("P", "_special.dat")
	var b := _ext_loader("B", ["dat"])
	ResourceLoader.add_resource_format_loader(p, true)
	expect_equal(_winner(_special), "P", "a loader that overrides _recognize_path() is still asked for its paths")
	expect_equal(_type(_special), "TypeP", "get_resource_type() still asks the path-based loader")
	ResourceLoader.remove_resource_format_loader(p)
	ResourceLoader.add_resource_format_loader(a)
	ResourceLoader.add_resource_format_loader(p)
	ResourceLoader.add_resource_format_loader(b)
	expect_equal(_winner(_special), "A", "order A, P, B: A loads")
	ResourceLoader.remove_resource_format_loader(a)
	expect_equal(_winner(_special), "P", "after removing A the path-based loader P loads, not the later indexed loader B")
	ResourceLoader.remove_resource_format_loader(p)
	expect_equal(_winner(_special), "B", "after removing P the indexed loader B loads")
	ResourceLoader.remove_resource_format_loader(b)


func _check_refresh() -> void:
	var l := _ext_loader("L", ["t53old"])
	ResourceLoader.add_resource_format_loader(l)
	expect_equal(_winner(_old), "L", "the loader is found for its first extension")
	l.extensions = PackedStringArray(["t53new"])
	expect_equal(_winner(_new), "<none>", "a changed list is not seen before rebuild_extension_index() (documented)")
	expect_equal(_winner(_old), "<none>", "the old extension is no longer recognized: recognize_path() reads the current list")
	ResourceLoader.rebuild_extension_index()
	expect_equal(_winner(_new), "L", "after rebuild_extension_index() the new extension is found")
	expect_equal(_type(_new), "TypeL", "after rebuild_extension_index() get_resource_type() asks the loader for the new extension")
	expect_equal(_winner(_old), "<none>", "after rebuild_extension_index() the old extension stays unrecognized")
	l.extensions = PackedStringArray(["t53old", "t53new"])
	ResourceLoader.remove_resource_format_loader(l)
	ResourceLoader.add_resource_format_loader(l)
	expect(_winner(_old) == "L" and _winner(_new) == "L", "removing and adding the loader again reads its list again")
	ResourceLoader.remove_resource_format_loader(l)
	expect_equal(_winner(_new), "<none>", "after removing the loader nothing handles .t53new")


func _perf_paths() -> PackedStringArray:
	var exts := ["tres", "res", "scn", "tscn", "png", "gd", "wav", "ogg", "glb", "unknown"]
	var paths := PackedStringArray()
	for i in PERF_COUNT:
		if i % 20 == 0:
			paths.append(_plain_tres)
		elif i % 20 == 1:
			paths.append(_plain_res)
		else:
			paths.append("res://__gd_loader_perf/%d.%s" % [i, exts[i % exts.size()]])
	return paths


func _measure(paths: PackedStringArray) -> Dictionary:
	var types := PackedStringArray()
	types.resize(paths.size())
	var start := Time.get_ticks_usec()
	for i in paths.size():
		types[i] = ResourceLoader.get_resource_type(paths[i])
	var type_usec := Time.get_ticks_usec() - start
	var found := 0
	start = Time.get_ticks_usec()
	for path in paths:
		if ResourceLoader.exists(path):
			found += 1
	var exists_usec := Time.get_ticks_usec() - start
	return {"types": types, "found": found, "type_usec": type_usec, "exists_usec": exists_usec}


func _measure_with(paths: PackedStringArray, loaders: Array[ResourceFormatLoader]) -> Dictionary:
	for loader in loaders:
		ResourceLoader.add_resource_format_loader(loader)
	_measure(paths) # warm-up
	var result := _measure(paths)
	for loader in loaders:
		ResourceLoader.remove_resource_format_loader(loader)
	return result


func _check_performance() -> void:
	var paths := _perf_paths()
	_measure(paths) # warm-up
	var base := _measure(paths)

	# Indexed: override only _get_recognized_extensions() (new in #53).
	var indexed: Array[ResourceFormatLoader] = []
	# Asked for every path: override _recognize_path(), the way every script loader was asked before #53.
	var asked: Array[ResourceFormatLoader] = []
	for i in EXTRA_LOADERS:
		indexed.append(_ext_loader("E%d" % i, ["x53_%d" % i]))
		asked.append(_path_loader("P%d" % i, ".x53p_%d" % i))
	var with_indexed := _measure_with(paths, indexed)
	var with_asked := _measure_with(paths, asked)

	var per_path := func(usec: int) -> String: return "%.1f usec/path" % (float(usec) / paths.size())
	print("  INFO  %d paths, built-in loaders only:         get_resource_type() %s, exists() %s" % [paths.size(), per_path.call(base["type_usec"]), per_path.call(base["exists_usec"])])
	print("  INFO  %d paths, +%d indexed script loaders:   get_resource_type() %s, exists() %s" % [paths.size(), EXTRA_LOADERS, per_path.call(with_indexed["type_usec"]), per_path.call(with_indexed["exists_usec"])])
	print("  INFO  %d paths, +%d always-asked loaders:     get_resource_type() %s, exists() %s" % [paths.size(), EXTRA_LOADERS, per_path.call(with_asked["type_usec"]), per_path.call(with_asked["exists_usec"])])
	expect_equal(with_indexed["types"], base["types"], "100 indexed script loaders do not change get_resource_type() of any of the 10,000 paths")
	expect_equal(with_indexed["found"], base["found"], "100 indexed script loaders do not change exists() of any of the 10,000 paths")
	expect_equal(with_asked["types"], base["types"], "100 path-based script loaders do not change get_resource_type() of any path")
	expect_equal(base["found"], PERF_COUNT / 10, "exists() finds exactly the 1,000 paths of the two real files (every 20th and 21st path)")
	expect_equal(base["types"][1], "Curve", "the second path is plain.res: type Curve")


func _cleanup() -> void:
	_remove_recursive(ProjectSettings.globalize_path(ROOT))


func _remove_recursive(absolute: String) -> void:
	var dir := DirAccess.open(absolute)
	if dir == null:
		return
	for sub in dir.get_directories():
		_remove_recursive(absolute.path_join(sub))
	for file in dir.get_files():
		dir.remove(file)
	DirAccess.remove_absolute(absolute)
