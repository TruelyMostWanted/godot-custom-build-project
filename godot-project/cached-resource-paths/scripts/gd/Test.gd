extends FeatureTest
## Full API test of Cached Resource Paths (the get_cached_* methods of ResourceLoader). Same tests as scripts/cs/Test.cs.

const ROOT := "user://cached-resource-paths/gd/"
const RESOURCES := "res://cached-resource-paths/resources/"
const FIRST := RESOURCES + "first.tres"
const SECOND := RESOURCES + "second.tres"
const THIRD := RESOURCES + "third.tres"
const THIRD_CHILD := THIRD + "::Resource_child"
const THREAD_COUNT := 4
const THREAD_ITERATIONS := 500

# Resources of the type, extension and directory tests, kept alive so that they stay in the cache.
var _held: Array[Resource] = []
var _curve2: Resource
var _texture_sub_path: String = ""


func _run_tests() -> void:
	_prepare()
	_test_cached_paths()
	_test_freed_paths()
	_test_type()
	_test_class_name()
	_test_extension()
	_test_directory()
	_test_counts()
	_test_kinds()
	_test_resources_and_type_counts()
	_test_freed_filters()
	_test_threads()
	_cleanup()


func _p(relative: String) -> String:
	return ROOT + relative


func _under(paths: PackedStringArray) -> Array:
	var result: Array = []
	for path: String in paths:
		if path.begins_with(ROOT):
			result.append(path)
	result.sort()
	return result


func _sorted(paths: Array) -> Array:
	var result: Array = paths.duplicate()
	result.sort()
	return result


func _save(resource: Resource, relative: String) -> void:
	resource.resource_name = relative
	ResourceSaver.save(resource, _p(relative))
	_held.append(load(_p(relative)))


func _prepare() -> void:
	DirAccess.make_dir_recursive_absolute(_p("a/b"))
	_save(Gradient.new(), "a/grad.tres")
	_save(Curve.new(), "a/curve.res")
	_save(StyleBoxFlat.new(), "a/flat.tres")
	var texture := GradientTexture1D.new()
	texture.gradient = Gradient.new()
	_save(texture, "a/tex.tres")
	_save(Widget.new(), "a/b/widget.tres")
	_save(Curve.new(), "a/b/curve2.res")
	_curve2 = _held[5]
	_held.remove_at(5)
	for path: String in ResourceLoader.get_cached_paths():
		if path.begins_with(_p("a/tex.tres::")):
			_texture_sub_path = path
	expect(not _texture_sub_path.is_empty(), "the loaded texture has a built-in sub-resource path (a/tex.tres::id)")


func _test_cached_paths() -> void:
	var first: Resource = load(FIRST)
	var second: Resource = load(SECOND)
	var third: Resource = load(THIRD)
	var paths: PackedStringArray = ResourceLoader.get_cached_paths()
	expect(paths.has(FIRST), "first.tres is in the cached paths")
	expect(paths.has(SECOND), "second.tres is in the cached paths")
	expect(paths.has(THIRD), "third.tres is in the cached paths")
	expect(paths.has(THIRD_CHILD), "the built-in sub-resource of third.tres is in the cached paths")
	expect(not paths.has(""), "no empty path in the cached paths")
	expect_equal(ResourceLoader.get_cached_paths().size(), paths.size(), "two calls without changes give the same count")
	for path: String in [FIRST, SECOND, THIRD]:
		var cached: Resource = ResourceLoader.get_cached_ref(path)
		expect(cached != null, "get_cached_ref resolves " + path)
	expect(ResourceLoader.get_cached_ref("res://no/such/file.tres") == null, "get_cached_ref of an unknown path is null")
	expect(first != null and second != null and third != null, "all three resources loaded")


func _test_freed_paths() -> void:
	var resource: Resource = load(FIRST)
	expect(ResourceLoader.get_cached_paths().has(FIRST), "first.tres is cached while referenced")
	resource = null
	expect(not ResourceLoader.get_cached_paths().has(FIRST), "first.tres is gone after its last reference is freed")

	var scratch := Resource.new()
	scratch.take_over_path(_p("scratch.tres"))
	expect(ResourceLoader.get_cached_paths().has(_p("scratch.tres")), "take_over_path adds the path")
	scratch = null
	expect(not ResourceLoader.get_cached_paths().has(_p("scratch.tres")), "a freed resource with take_over_path is gone")


func _test_type() -> void:
	var gradient: Array = _sorted([_p("a/grad.tres"), _texture_sub_path])
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Gradient")), gradient, "type Gradient: the file and the sub-resource, not the texture")
	var curves: Array = _sorted([_p("a/curve.res"), _p("a/b/curve2.res")])
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Curve")), curves, "type Curve: both curves")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Curve", true)), curves, "type Curve, exact: both curves")
	# StyleBoxFlat inherits StyleBox.
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"StyleBox")), [_p("a/flat.tres")], "type StyleBox includes the subclass StyleBoxFlat")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"StyleBox", true)), [], "type StyleBox, exact: not the subclass StyleBoxFlat")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"StyleBoxFlat", true)), [_p("a/flat.tres")], "type StyleBoxFlat, exact: the class itself")
	# GradientTexture1D inherits Texture2D.
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Texture2D")), [_p("a/tex.tres")], "type Texture2D includes GradientTexture1D")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Texture2D", true)), [], "type Texture2D, exact: nothing")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"GradientTexture1D", true)), [_p("a/tex.tres")], "type GradientTexture1D, exact: the texture")
	var everything: Array = _sorted([_p("a/grad.tres"), _p("a/curve.res"), _p("a/flat.tres"), _p("a/tex.tres"), _texture_sub_path, _p("a/b/widget.tres"), _p("a/b/curve2.res")])
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Resource")), everything, "type Resource: every loaded resource, including sub-resources and script classes")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Node")), [], "type Node: no resource")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"NoSuchClass")), [], "unknown type: nothing, no error")


func _test_class_name() -> void:
	var widget: Array = [_p("a/b/widget.tres")]
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Widget")), widget, "class_name: the script class matches")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Widget", true)), widget, "class_name, exact: the script class matches itself")
	expect(ResourceLoader.get_cached_paths_for_type(&"Resource").has(_p("a/b/widget.tres")), "a class_name resource is also found by its native base class")


func _test_extension() -> void:
	var tres: Array = _sorted([_p("a/grad.tres"), _p("a/flat.tres"), _p("a/tex.tres"), _texture_sub_path, _p("a/b/widget.tres")])
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("tres")), tres, "extension tres: files and the sub-resource of a tres file")
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("TRES")), tres, "extension TRES: case-insensitive")
	var res: Array = _sorted([_p("a/curve.res"), _p("a/b/curve2.res")])
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("res")), res, "extension res: does not match tres")
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("xyz")), [], "unknown extension: nothing")
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("")), [], "empty extension: nothing under the test directory")


func _test_directory() -> void:
	var in_b: Array = _sorted([_p("a/b/widget.tres"), _p("a/b/curve2.res")])
	var direct_a: Array = _sorted([_p("a/grad.tres"), _p("a/curve.res"), _p("a/flat.tres"), _p("a/tex.tres"), _texture_sub_path])
	var recursive_a: Array = _sorted(direct_a + in_b)
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/b/"))), in_b, "directory a/b/: its two resources")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/"))), recursive_a, "directory a/ is recursive by default")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/"), true)), recursive_a, "directory a/, recursive = true: includes a/b/")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/"), false)), direct_a, "directory a/, recursive = false: direct children only")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/b/"), false)), in_b, "directory a/b/, recursive = false: its two resources")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("none/"))), [], "unknown directory: nothing")
	expect(not ResourceLoader.get_cached_paths_in_directory("res://").has(_p("a/grad.tres")), "directory res:// does not return user:// paths")


func _test_counts() -> void:
	var directory: String = _p("a/")
	var file: int = ResourceLoader.CACHED_RESOURCE_KIND_FILE
	var built_in: int = ResourceLoader.CACHED_RESOURCE_KIND_BUILT_IN
	for kinds: int in [file, built_in, ResourceLoader.CACHED_RESOURCE_KIND_ALL]:
		var message: String = " equals the size of its path list, kinds " + str(kinds)
		expect_equal(ResourceLoader.get_cached_count(kinds), ResourceLoader.get_cached_paths(kinds).size(), "get_cached_count" + message)
		expect_equal(ResourceLoader.get_cached_count_for_type(&"Curve", false, kinds), ResourceLoader.get_cached_paths_for_type(&"Curve", false, kinds).size(), "get_cached_count_for_type" + message)
		expect_equal(ResourceLoader.get_cached_count_with_extension("tres", kinds), ResourceLoader.get_cached_paths_with_extension("tres", kinds).size(), "get_cached_count_with_extension" + message)
		expect_equal(ResourceLoader.get_cached_count_in_directory(directory, true, kinds), ResourceLoader.get_cached_paths_in_directory(directory, true, kinds).size(), "get_cached_count_in_directory" + message)
	expect_equal(ResourceLoader.get_cached_count_in_directory(directory, true, file), 6, "six resources in their own files under a/")


func _test_kinds() -> void:
	var directory: String = _p("a/")
	var file: int = ResourceLoader.CACHED_RESOURCE_KIND_FILE
	var built_in: int = ResourceLoader.CACHED_RESOURCE_KIND_BUILT_IN
	var imported: int = ResourceLoader.CACHED_RESOURCE_KIND_IMPORTED
	expect(ResourceLoader.get_cached_paths_in_directory(directory, true, built_in).has(_texture_sub_path), "the built-in sub-resource of tex.tres has the kind BUILT_IN")
	expect(not ResourceLoader.get_cached_paths_in_directory(directory, true, file).has(_texture_sub_path), "the built-in sub-resource does not have the kind FILE")
	var icon: Resource = load("res://icon.svg")
	expect(ResourceLoader.get_cached_paths(imported).has("res://icon.svg"), "res://icon.svg has the kind IMPORTED")
	expect(not ResourceLoader.get_cached_paths(file).has("res://icon.svg"), "res://icon.svg does not have the kind FILE")
	icon = null


func _test_resources_and_type_counts() -> void:
	var file: int = ResourceLoader.CACHED_RESOURCE_KIND_FILE
	var curves: Array = []
	for resource: Resource in ResourceLoader.get_cached_resources_for_type(&"Curve", true, file):
		if resource.resource_path.begins_with(ROOT):
			curves.append(resource)
	expect_equal(curves.size(), 2, "get_cached_resources_for_type returns the two curves themselves")
	var type_counts: Dictionary = ResourceLoader.get_cached_type_counts(file)
	expect(type_counts.get(&"Curve", 0) >= 2, "get_cached_type_counts counts the curves")
	expect(type_counts.get(&"Widget", 0) >= 1, "get_cached_type_counts uses the script class name")


func _test_freed_filters() -> void:
	var curve2: String = _p("a/b/curve2.res")
	_curve2 = null
	expect(not ResourceLoader.get_cached_paths_for_type(&"Curve").has(curve2), "a freed resource is gone from the type filter")
	expect(not ResourceLoader.get_cached_paths_with_extension("res").has(curve2), "a freed resource is gone from the extension filter")
	expect(not ResourceLoader.get_cached_paths_in_directory(_p("a/b/")).has(curve2), "a freed resource is gone from the directory filter")
	expect(ResourceLoader.get_cached_paths_in_directory(_p("a/b/")).has(_p("a/b/widget.tres")), "the other resource in the directory stays")


func _thread_worker(index: int) -> void:
	for i: int in THREAD_ITERATIONS:
		var resource := Resource.new()
		resource.take_over_path("res://__cached_paths_thread_%d/%d.tres" % [index, i])
		resource = null


func _test_threads() -> void:
	var threads: Array[Thread] = []
	for index: int in THREAD_COUNT:
		var thread := Thread.new()
		thread.start(_thread_worker.bind(index))
		threads.append(thread)
	var regex: RegEx = RegEx.create_from_string("^res://__cached_paths_thread_\\d+/\\d+\\.tres$")
	var calls: int = 0
	var bad_paths: int = 0
	var running: bool = true
	while running:
		running = false
		for thread: Thread in threads:
			if thread.is_alive():
				running = true
		for path: String in ResourceLoader.get_cached_paths():
			if path.begins_with("res://__cached_paths_thread_") and regex.search(path) == null:
				bad_paths += 1
		calls += 1
	for thread: Thread in threads:
		thread.wait_to_finish()
	expect(calls > 0, "get_cached_paths was called while threads were running")
	expect_equal(bad_paths, 0, "no partial or malformed path while threads create and free resources")
	expect_equal(ResourceLoader.get_cached_paths_in_directory("res://__cached_paths_thread_0/").size(), 0, "no thread path is left after the threads freed their resources")


func _cleanup() -> void:
	_held.clear()
	for directory: String in ["a/b/", "a/", ""]:
		for file: String in DirAccess.get_files_at(_p(directory)):
			DirAccess.remove_absolute(_p(directory + file))
	DirAccess.remove_absolute(_p("a/b"))
	DirAccess.remove_absolute(_p("a"))
