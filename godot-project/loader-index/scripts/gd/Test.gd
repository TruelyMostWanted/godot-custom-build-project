extends FeatureTest
## Full API test of Loader Index (ResourceLoader extension index). Same tests as scripts/cs/Test.cs.

const MyLoader := preload("res://loader-index/scripts/gd/my_loader.gd")
const PathLoader := preload("res://loader-index/scripts/gd/path_loader.gd")

const ROOT := "user://loader-index/gd/"
const ONE := ROOT + "one.lix"
const UPPER := ROOT + "UPPER.LIX"
const OLD := ROOT + "data.lixold"
const NEW := ROOT + "data.lixnew"
const DOTTED := ROOT + "archive.pack.lix"
const PLAIN_DOTTED := ROOT + "archive.lix"
const SPECIAL := ROOT + "item_special.dat"
const OTHER := ROOT + "other.dat"
const PLAIN_TRES := ROOT + "plain.tres"
const PLAIN_RES := ROOT + "plain.res"
const NONE := "<none>"
const SAMPLE_PATH_COUNT := 1000
const EXTRA_LOADERS := 100


func _run_tests() -> void:
	_prepare()
	_test_builtin_formats()
	_test_order()
	_test_at_front()
	_test_dotted_extension()
	_test_path_based_loader()
	_test_rebuild_extension_index()
	_test_same_results_with_many_loaders()
	_cleanup()


func _prepare() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	for path: String in [ONE, UPPER, OLD, NEW, DOTTED, PLAIN_DOTTED, SPECIAL, OTHER]:
		var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
		file.store_string("x")
	expect_equal(ResourceSaver.save(Gradient.new(), PLAIN_TRES), OK, "saved plain.tres")
	expect_equal(ResourceSaver.save(Curve.new(), PLAIN_RES), OK, "saved plain.res")


func _cleanup() -> void:
	var dir: DirAccess = DirAccess.open(ROOT)
	for file: String in dir.get_files():
		dir.remove(file)
	DirAccess.remove_absolute(ROOT)


func _ext_loader(tag: String, extensions: PackedStringArray) -> MyLoader:
	var loader := MyLoader.new()
	loader.tag = tag
	loader.extensions = extensions
	loader.type_name = "Type" + tag
	return loader


func _path_loader(tag: String, ending: String) -> PathLoader:
	var loader := PathLoader.new()
	loader.tag = tag
	loader.path_ending = ending
	loader.type_name = "Type" + tag
	return loader


func _winner(path: String) -> String:
	if not ResourceLoader.exists(path):
		return NONE
	var resource: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	return resource.resource_name


func _type(path: String) -> String:
	return ResourceLoader.get_resource_type(path)


func _test_builtin_formats() -> void:
	var tres: Resource = ResourceLoader.load(PLAIN_TRES, "", ResourceLoader.CACHE_MODE_IGNORE)
	var res: Resource = ResourceLoader.load(PLAIN_RES, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(tres is Gradient, "plain.tres loads as Gradient with the built-in text loader")
	expect(res is Curve, "plain.res loads as Curve with the binary loader")
	expect_equal(_type(PLAIN_TRES), "Gradient", "get_resource_type(plain.tres) is Gradient")
	expect_equal(_type(PLAIN_RES), "Curve", "get_resource_type(plain.res) is Curve")
	expect(ResourceLoader.exists(PLAIN_RES, "Curve"), "exists(plain.res, Curve) is true with a type hint")
	expect(ResourceLoader.exists("res://icon.svg"), "exists() is true for the imported res://icon.svg")
	expect_equal(_type("res://icon.svg"), "CompressedTexture2D", "get_resource_type(res://icon.svg) comes from the importer")
	var icon: Resource = ResourceLoader.load("res://icon.svg")
	expect(icon is Texture2D, "res://icon.svg loads as Texture2D")
	expect(not ResourceLoader.exists(ROOT + "missing.unknownext"), "exists() is false for an unknown extension")
	expect_equal(_type(ROOT + "missing.unknownext"), "", "get_resource_type() is empty for an unknown extension")


func _test_order() -> void:
	expect_equal(_winner(ONE), NONE, "no loader for .lix before a loader is registered")
	var a: MyLoader = _ext_loader("A", ["lix"])
	var b: MyLoader = _ext_loader("B", ["LIX"])
	var c: MyLoader = _ext_loader("C", ["lix"])
	ResourceLoader.add_resource_format_loader(a)
	ResourceLoader.add_resource_format_loader(b)
	expect_equal(_winner(ONE), "A", "two loaders for one extension: the first registered loads")
	expect_equal(_type(ONE), "TypeA", "get_resource_type() answers from the first registered loader")
	ResourceLoader.add_resource_format_loader(c, true)
	expect_equal(_winner(ONE), "C", "a loader registered with at_front = true loads first")
	expect_equal(_winner(UPPER), "C", "the extension match is case-insensitive (UPPER.LIX)")
	ResourceLoader.remove_resource_format_loader(c)
	ResourceLoader.remove_resource_format_loader(a)
	expect_equal(_winner(ONE), "B", "after removing C and A the loader declaring LIX loads (B)")
	expect_equal(_type(ONE), "TypeB", "get_resource_type() answers from B")
	ResourceLoader.remove_resource_format_loader(b)
	expect_equal(_winner(ONE), NONE, "after removing every loader no loader handles .lix")
	expect_equal(_type(ONE), "", "get_resource_type() is empty without a loader")


func _test_at_front() -> void:
	var a: MyLoader = _ext_loader("A", ["lix"])
	var d1: MyLoader = _ext_loader("D1", ["lix"])
	var d2: MyLoader = _ext_loader("D2", ["lix"])
	ResourceLoader.add_resource_format_loader(a)
	ResourceLoader.add_resource_format_loader(d1, true)
	ResourceLoader.add_resource_format_loader(d2, true)
	expect_equal(_winner(ONE), "D2", "of two at_front loaders the later one is first")
	ResourceLoader.remove_resource_format_loader(d2)
	expect_equal(_winner(ONE), "D1", "after removing D2 the earlier at_front loader loads (D1)")
	ResourceLoader.remove_resource_format_loader(d1)
	expect_equal(_winner(ONE), "A", "after removing D1 the normal loader loads (A)")
	ResourceLoader.remove_resource_format_loader(a)


func _test_dotted_extension() -> void:
	var d: MyLoader = _ext_loader("D", ["pack.lix"])
	ResourceLoader.add_resource_format_loader(d)
	expect_equal(_winner(DOTTED), "D", "an extension with a dot (pack.lix) is found for archive.pack.lix")
	expect_equal(_winner(PLAIN_DOTTED), NONE, "archive.lix does not match the extension pack.lix")
	ResourceLoader.remove_resource_format_loader(d)


func _test_path_based_loader() -> void:
	var a: MyLoader = _ext_loader("A", ["dat"])
	var p: PathLoader = _path_loader("P", "_special.dat")
	var b: MyLoader = _ext_loader("B", ["dat"])
	ResourceLoader.add_resource_format_loader(p, true)
	expect_equal(_winner(SPECIAL), "P", "a loader that overrides _recognize_path() is still asked for its paths")
	expect_equal(_type(SPECIAL), "TypeP", "get_resource_type() still asks the path-based loader")
	expect_equal(_winner(OTHER), NONE, "the path-based loader ignores other paths with the same extension")
	ResourceLoader.remove_resource_format_loader(p)
	ResourceLoader.add_resource_format_loader(a)
	ResourceLoader.add_resource_format_loader(p)
	ResourceLoader.add_resource_format_loader(b)
	expect_equal(_winner(SPECIAL), "A", "order A, P, B: A loads")
	ResourceLoader.remove_resource_format_loader(a)
	expect_equal(_winner(SPECIAL), "P", "after removing A the path-based loader P loads, not the later indexed loader B")
	ResourceLoader.remove_resource_format_loader(p)
	expect_equal(_winner(SPECIAL), "B", "after removing P the indexed loader B loads")
	ResourceLoader.remove_resource_format_loader(b)


func _test_rebuild_extension_index() -> void:
	var loader: MyLoader = _ext_loader("L", ["lixold"])
	ResourceLoader.add_resource_format_loader(loader)
	expect_equal(_winner(OLD), "L", "the loader is found for its first extension")
	loader.extensions = PackedStringArray(["lixnew"])
	expect_equal(_winner(NEW), NONE, "a changed list is not seen before rebuild_extension_index()")
	expect_equal(_winner(OLD), NONE, "the old extension is no longer recognized")
	ResourceLoader.rebuild_extension_index()
	expect_equal(_winner(NEW), "L", "after rebuild_extension_index() the new extension is found")
	expect_equal(_type(NEW), "TypeL", "after rebuild_extension_index() get_resource_type() asks the loader")
	expect_equal(_winner(OLD), NONE, "after rebuild_extension_index() the old extension stays unrecognized")
	loader.extensions = PackedStringArray(["lixold", "lixnew"])
	ResourceLoader.remove_resource_format_loader(loader)
	ResourceLoader.add_resource_format_loader(loader)
	expect_equal(_winner(OLD), "L", "adding the loader again reads its list again (old)")
	expect_equal(_winner(NEW), "L", "adding the loader again reads its list again (new)")
	ResourceLoader.remove_resource_format_loader(loader)
	expect_equal(_winner(NEW), NONE, "after removing the loader nothing handles .lixnew")


func _sample_types() -> PackedStringArray:
	var extensions: Array[String] = ["tres", "res", "scn", "tscn", "png", "gd", "wav", "ogg", "glb", "unknown"]
	var types := PackedStringArray()
	for i: int in SAMPLE_PATH_COUNT:
		var path: String = "res://__loader_index/%d.%s" % [i, extensions[i % extensions.size()]]
		types.append(ResourceLoader.get_resource_type(path))
	return types


func _test_same_results_with_many_loaders() -> void:
	var before: PackedStringArray = _sample_types()
	var loaders: Array[ResourceFormatLoader] = []
	for i: int in EXTRA_LOADERS:
		loaders.append(_ext_loader("E%d" % i, ["lixext%d" % i]))
		loaders.append(_path_loader("P%d" % i, ".lixpath%d" % i))
	for loader: ResourceFormatLoader in loaders:
		ResourceLoader.add_resource_format_loader(loader)
	var after: PackedStringArray = _sample_types()
	for loader: ResourceFormatLoader in loaders:
		ResourceLoader.remove_resource_format_loader(loader)
	expect_equal(after, before, "200 extra loaders do not change get_resource_type() of 1000 paths")
