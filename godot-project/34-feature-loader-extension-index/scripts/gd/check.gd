# Written before the engine build of #34; run after the merge.
extends FeatureCheck
## GDScript checks of 34-feature-loader-extension-index. Check the same things as scripts/cs/Issue34FeatureLoaderExtensionIndexCheck.cs.
# The issue adds no API: the choice of the loader must stay as it is (registration order, first match wins).
# Assumption: .import files are not covered here, because a test needs an imported asset; the built-in formats .tres and .res stand for the binary and text loaders.
# Assumption: a loader reports its identity through resource_name (load) and through the type name it returns (get_resource_type).

const ExtLoader := preload("res://34-feature-loader-extension-index/scripts/gd/ext_loader.gd")
const PathLoader := preload("res://34-feature-loader-extension-index/scripts/gd/path_loader.gd")

const ROOT := "user://34-feature-loader-extension-index/gd/"
const EXT := "t34x"
const PERF_COUNT := 10000
const EXTRA_LOADERS := 100

var _one := ROOT + "one." + EXT
var _upper := ROOT + "UPPER." + EXT.to_upper()
var _special := ROOT + "item_special.dat"
var _other := ROOT + "other.dat"
var _plain_tres := ROOT + "plain.tres"
var _plain_res := ROOT + "plain.res"


func _get_check_name() -> String:
	return "34-feature-loader-extension-index"


func _run_checks() -> void:
	_cleanup()
	_prepare()
	_check_order()
	_check_at_front()
	_check_path_based()
	_check_mixed_order()
	_check_builtin()
	_check_performance()
	_cleanup()


func _ext_loader(tag: String, extension: String = EXT) -> ResourceFormatLoader:
	var loader: ResourceFormatLoader = ExtLoader.new()
	loader.tag = tag
	loader.extension = extension
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
	for path in [_one, _upper, _special, _other]:
		_write_file(path)
	expect_equal(ResourceSaver.save(Gradient.new(), _plain_tres), OK, "saved plain.tres")
	expect_equal(ResourceSaver.save(Curve.new(), _plain_res), OK, "saved plain.res")


func _check_order() -> void:
	expect_equal(_winner(_one), "<none>", "no loader for .t34x before a loader is registered")
	var a := _ext_loader("A")
	var b := _ext_loader("B")
	var c := _ext_loader("C")
	ResourceLoader.add_resource_format_loader(a)
	ResourceLoader.add_resource_format_loader(b)
	expect_equal(_winner(_one), "A", "two loaders for one extension: the first registered loads")
	expect_equal(_type(_one), "TypeA", "get_resource_type() answers from the first registered loader")
	ResourceLoader.add_resource_format_loader(c, true)
	expect_equal(_winner(_one), "C", "a loader registered with at_front = true loads first")
	expect_equal(_type(_one), "TypeC", "get_resource_type() answers from the at_front loader")
	expect_equal(_winner(_upper), "C", "the extension match is case-insensitive (UPPER.T34X)")
	ResourceLoader.remove_resource_format_loader(c)
	expect_equal(_winner(_one), "A", "after removing the front loader the next one loads again (A)")
	ResourceLoader.remove_resource_format_loader(a)
	expect_equal(_winner(_one), "B", "after removing A the next one loads (B)")
	ResourceLoader.remove_resource_format_loader(b)
	expect_equal(_winner(_one), "<none>", "after removing every loader no loader handles .t34x")
	expect_equal(_type(_one), "", "get_resource_type() is empty without a loader")


func _check_at_front() -> void:
	var a := _ext_loader("A")
	var d1 := _ext_loader("D1")
	var d2 := _ext_loader("D2")
	ResourceLoader.add_resource_format_loader(a)
	ResourceLoader.add_resource_format_loader(d1, true)
	ResourceLoader.add_resource_format_loader(d2, true)
	expect_equal(_winner(_one), "D2", "of two at_front loaders the later one is first")
	ResourceLoader.remove_resource_format_loader(d2)
	expect_equal(_winner(_one), "D1", "after removing D2 the earlier at_front loader loads (D1)")
	ResourceLoader.remove_resource_format_loader(d1)
	expect_equal(_winner(_one), "A", "after removing D1 the normal loader loads (A)")
	ResourceLoader.remove_resource_format_loader(a)
	expect_equal(_winner(_one), "<none>", "no loader left")


func _check_path_based() -> void:
	expect_equal(_winner(_special), "<none>", "no loader for item_special.dat before registration")
	var p := _path_loader("P", "_special.dat")
	ResourceLoader.add_resource_format_loader(p)
	expect_equal(_winner(_special), "P", "a loader that overrides _recognize_path() loads a path without its extension")
	expect_equal(_type(_special), "TypeP", "get_resource_type() answers from the path-based loader")
	expect_equal(_winner(_other), "<none>", "the path-based loader ignores other paths with the same extension")
	ResourceLoader.remove_resource_format_loader(p)
	expect_equal(_winner(_special), "<none>", "after removing the path-based loader nothing handles item_special.dat")


func _check_mixed_order() -> void:
	# A and B go by extension, Q by path. The registration order A, Q, B decides, not the kind of loader.
	var a := _ext_loader("A")
	var q := _path_loader("Q", "one." + EXT)
	var b := _ext_loader("B")
	ResourceLoader.add_resource_format_loader(a)
	ResourceLoader.add_resource_format_loader(q)
	ResourceLoader.add_resource_format_loader(b)
	expect_equal(_winner(_one), "A", "order A, Q, B: A loads")
	ResourceLoader.remove_resource_format_loader(a)
	expect_equal(_winner(_one), "Q", "after removing A the path-based loader Q loads, not the later extension loader B")
	expect_equal(_winner(_upper), "B", "Q does not recognize UPPER.T34X (case differs), so B loads it")
	ResourceLoader.remove_resource_format_loader(q)
	expect_equal(_winner(_one), "B", "after removing Q the extension loader B loads")
	ResourceLoader.remove_resource_format_loader(b)

	var front_q := _path_loader("FrontQ", "one." + EXT)
	var late_a := _ext_loader("LateA")
	ResourceLoader.add_resource_format_loader(late_a)
	ResourceLoader.add_resource_format_loader(front_q, true)
	expect_equal(_winner(_one), "FrontQ", "a path-based loader at the front loads before an extension loader")
	ResourceLoader.remove_resource_format_loader(front_q)
	ResourceLoader.remove_resource_format_loader(late_a)


func _check_builtin() -> void:
	var tres := ResourceLoader.load(_plain_tres, "", ResourceLoader.CACHE_MODE_IGNORE)
	var res := ResourceLoader.load(_plain_res, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(tres is Gradient, "plain.tres loads as Gradient with the built-in text loader")
	expect(res is Curve, "plain.res loads as Curve with the built-in binary loader")
	expect_equal(_type(_plain_tres), "Gradient", "get_resource_type(plain.tres) is Gradient")
	expect_equal(_type(_plain_res), "Curve", "get_resource_type(plain.res) is Curve")
	expect(ResourceLoader.exists(_plain_tres) and ResourceLoader.exists(_plain_res), "exists() is true for .tres and .res")
	expect(not ResourceLoader.exists(ROOT + "missing.unknownext"), "exists() is false for an unknown extension")

	# Script loaders in front, for other extensions and for .t34x: the built-in formats still use their own loaders.
	var loaders: Array[ResourceFormatLoader] = [_ext_loader("X1", "xa"), _ext_loader("X2", EXT), _path_loader("X3", ".nothing")]
	for loader in loaders:
		ResourceLoader.add_resource_format_loader(loader, true)
	var tres2 := ResourceLoader.load(_plain_tres, "", ResourceLoader.CACHE_MODE_IGNORE)
	var res2 := ResourceLoader.load(_plain_res, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(tres2 is Gradient and tres2.resource_name == "", "with script loaders in front plain.tres still loads with the built-in loader")
	expect(res2 is Curve and res2.resource_name == "", "with script loaders in front plain.res still loads with the built-in loader")
	expect_equal(_winner(_one), "X2", "the script loader for .t34x still loads .t34x")
	for loader in loaders:
		ResourceLoader.remove_resource_format_loader(loader)


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


func _check_performance() -> void:
	var paths := _perf_paths()
	_measure(paths) # warm-up
	var base := _measure(paths)

	var extra: Array[ResourceFormatLoader] = []
	for i in EXTRA_LOADERS:
		var loader := _ext_loader("E%d" % i, "x34_%d" % i)
		ResourceLoader.add_resource_format_loader(loader)
		extra.append(loader)
	_measure(paths) # warm-up
	var with_extra := _measure(paths)
	for loader in extra:
		ResourceLoader.remove_resource_format_loader(loader)

	print("  INFO  %d paths, built-in loaders:  get_resource_type() %d usec, exists() %d usec" % [paths.size(), base["type_usec"], base["exists_usec"]])
	print("  INFO  %d paths, +%d script loaders: get_resource_type() %d usec, exists() %d usec" % [paths.size(), EXTRA_LOADERS, with_extra["type_usec"], with_extra["exists_usec"]])
	expect_equal(with_extra["types"], base["types"], "the 100 extra loaders do not change get_resource_type() of any of the 10,000 paths")
	expect_equal(with_extra["found"], base["found"], "the 100 extra loaders do not change exists() of any of the 10,000 paths")
	expect_equal(base["found"], PERF_COUNT / 10, "exists() finds exactly the 1,000 paths of the two real files (every 20th and 21st path)")
	expect_equal(base["types"][0], "Gradient", "the first path is plain.tres: type Gradient")
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
