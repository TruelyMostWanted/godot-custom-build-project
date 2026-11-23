# Written before the engine build of #37; run after the merge.
extends FeatureCheck
## GDScript checks of 37-feature-cached-paths-filters. Check the same things as scripts/cs/Issue37FeatureCachedPathsFiltersCheck.cs.
# Assumption: directories are passed with a trailing slash; a directory result includes sub-resource paths (file::id) of files in it.
# Assumption: the exact flag is not checked for "Resource" itself, because the class of a script-class resource is its script class.

const ROOT := "user://37-feature-cached-paths-filters/gd/"
const PERF_COUNT := 10000
const PERF_HITS := 100
const THREAD_COUNT := 4
const THREAD_ITERATIONS := 1500

var _held: Array[Resource] = []
var _tex_sub_path := ""


func _get_check_name() -> String:
	return "37-feature-cached-paths-filters"


func _run_checks() -> void:
	_cleanup()
	_prepare()
	_check_api()
	_check_type()
	_check_class_name()
	_check_extension()
	_check_directory()
	_check_freed()
	_check_threads()
	_check_performance()
	_cleanup()


func _p(relative: String) -> String:
	return ROOT + relative


func _under(paths: PackedStringArray) -> Array:
	var out := []
	for path in paths:
		if path.begins_with(ROOT):
			out.append(path)
	out.sort()
	return out


func _sorted(list: Array) -> Array:
	var out := list.duplicate()
	out.sort()
	return out


func _save(res: Resource, relative: String) -> void:
	var err := ResourceSaver.save(res, _p(relative))
	expect_equal(err, OK, "saved %s" % relative)


func _prepare() -> void:
	DirAccess.make_dir_recursive_absolute(_p("a/b"))
	_save(Gradient.new(), "a/grad.tres")
	_save(Curve.new(), "a/curve.res")
	_save(StyleBoxFlat.new(), "a/flat.tres")
	var tex := GradientTexture1D.new()
	tex.gradient = Gradient.new()
	_save(tex, "a/tex.tres")
	_save(Issue37GdWidget.new(), "a/b/widget.tres")
	_save(Curve.new(), "a/b/curve2.res")

	for relative in ["a/grad.tres", "a/curve.res", "a/flat.tres", "a/tex.tres", "a/b/widget.tres", "a/b/curve2.res"]:
		_held.append(load(_p(relative)))
	for path in ResourceLoader.get_cached_paths():
		if path.begins_with(_p("a/tex.tres::")):
			_tex_sub_path = path
	expect(_tex_sub_path != "", "the loaded texture has a built-in sub-resource path (a/tex.tres::<id>)")


func _check_api() -> void:
	for method in ["get_cached_paths_for_type", "get_cached_paths_with_extension", "get_cached_paths_in_directory"]:
		expect(ClassDB.class_has_method("ResourceLoader", method), "ResourceLoader has %s()" % method)
	expect(typeof(ResourceLoader.get_cached_paths_for_type(&"Curve")) == TYPE_PACKED_STRING_ARRAY, "get_cached_paths_for_type() returns a PackedStringArray")


func _check_type() -> void:
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Gradient")), _sorted([_p("a/grad.tres"), _tex_sub_path]), "type Gradient: the file and the sub-resource, not the texture")
	var curves := _sorted([_p("a/curve.res"), _p("a/b/curve2.res")])
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
	var all := _sorted([_p("a/grad.tres"), _p("a/curve.res"), _p("a/flat.tres"), _p("a/tex.tres"), _tex_sub_path, _p("a/b/widget.tres"), _p("a/b/curve2.res")])
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Resource")), all, "type Resource: every loaded resource, including sub-resources and script classes")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Node")), [], "type Node: no resource")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"NoSuchClass")), [], "unknown type: nothing, no error")


func _check_class_name() -> void:
	var widget := [_p("a/b/widget.tres")]
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Issue37GdWidget")), widget, "class_name: the script class matches")
	expect_equal(_under(ResourceLoader.get_cached_paths_for_type(&"Issue37GdWidget", true)), widget, "class_name, exact: the script class matches itself")
	expect(ResourceLoader.get_cached_paths_for_type(&"Resource").has(_p("a/b/widget.tres")), "class_name resource is also found by its native base class")


func _check_extension() -> void:
	var tres := _sorted([_p("a/grad.tres"), _p("a/flat.tres"), _p("a/tex.tres"), _tex_sub_path, _p("a/b/widget.tres")])
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("tres")), tres, "extension tres: files and the sub-resource of a tres file")
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("TRES")), tres, "extension TRES: case-insensitive")
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("res")), _sorted([_p("a/curve.res"), _p("a/b/curve2.res")]), "extension res: does not match tres")
	expect(ResourceLoader.get_cached_paths_with_extension("tres").has(_tex_sub_path), "a sub-resource counts as the extension of its file")
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("xyz")), [], "unknown extension: nothing")
	expect_equal(_under(ResourceLoader.get_cached_paths_with_extension("")), [], "empty extension: nothing under the test directory")


func _check_directory() -> void:
	var in_b := _sorted([_p("a/b/widget.tres"), _p("a/b/curve2.res")])
	var direct_a := _sorted([_p("a/grad.tres"), _p("a/curve.res"), _p("a/flat.tres"), _p("a/tex.tres"), _tex_sub_path])
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/b/"))), in_b, "directory a/b/: its two resources")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/"))), _sorted(direct_a + in_b), "directory a/ (recursive by default): includes a/b/")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/"), true)), _sorted(direct_a + in_b), "directory a/, recursive = true: includes a/b/")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/"), false)), direct_a, "directory a/, recursive = false: direct children only")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("a/b/"), false)), in_b, "directory a/b/, recursive = false: its two resources")
	expect_equal(_under(ResourceLoader.get_cached_paths_in_directory(_p("none/"))), [], "unknown directory: nothing")
	expect(not ResourceLoader.get_cached_paths_in_directory("res://").has(_p("a/grad.tres")), "directory res:// does not return user:// paths")


func _check_freed() -> void:
	var curve2 := _p("a/b/curve2.res")
	_held[5] = null
	expect(not ResourceLoader.get_cached_paths_for_type(&"Curve").has(curve2), "freed resource is gone from the type filter")
	expect(not ResourceLoader.get_cached_paths_with_extension("res").has(curve2), "freed resource is gone from the extension filter")
	expect(not ResourceLoader.get_cached_paths_in_directory(_p("a/b/")).has(curve2), "freed resource is gone from the directory filter")
	expect(ResourceLoader.get_cached_paths_in_directory(_p("a/b/")).has(_p("a/b/widget.tres")), "the other resource in the directory stays")


func _thread_worker(p_index: int) -> void:
	for i in THREAD_ITERATIONS:
		var res := Curve.new()
		res.take_over_path("res://__gd_filter_thread_%d/%d.curvex" % [p_index, i])
		res = null


func _check_threads() -> void:
	var threads: Array[Thread] = []
	for t in THREAD_COUNT:
		var thread := Thread.new()
		thread.start(_thread_worker.bind(t))
		threads.append(thread)
	var regex := RegEx.create_from_string("^res://__gd_filter_thread_\\d+/\\d+\\.curvex$")
	var calls := 0
	var bad := 0
	var running := true
	while running:
		running = false
		for thread in threads:
			if thread.is_alive():
				running = true
		var lists: Array[PackedStringArray] = [
			ResourceLoader.get_cached_paths_for_type(&"Curve", true),
			ResourceLoader.get_cached_paths_with_extension("curvex"),
			ResourceLoader.get_cached_paths_in_directory("res://__gd_filter_thread_0/"),
		]
		for list in lists:
			for path in list:
				if path.begins_with("res://__gd_filter_thread_") and regex.search(path) == null:
					bad += 1
		calls += 1
	for thread in threads:
		thread.wait_to_finish()
	print("  INFO  %d rounds of the three filters while %d threads changed the cache" % [calls, THREAD_COUNT])
	expect(calls > 0, "the filters were called while threads were running")
	expect_equal(bad, 0, "no partial or malformed path while threads create and free resources")
	expect_equal(ResourceLoader.get_cached_paths_with_extension("curvex").size(), 0, "no thread path left after the threads freed their resources")


func _check_performance() -> void:
	# Release the resources of the earlier checks, so that only the 100 Curves below match the type filter.
	_held.clear()
	var held: Array[Resource] = []
	var step := PERF_COUNT / PERF_HITS
	for i in PERF_COUNT:
		var res: Resource
		var path: String
		if i % step == 0:
			res = Curve.new()
			path = "res://__gd_filter_perf/hit/%d.perfx" % i
		else:
			res = Resource.new()
			path = "res://__gd_filter_perf/miss/%d.tres" % i
		res.take_over_path(path)
		held.append(res)

	var runs := 10
	# By type: the script alternative has to fetch every resource.
	var start := Time.get_ticks_usec()
	var by_type := PackedStringArray()
	for i in runs:
		by_type = ResourceLoader.get_cached_paths_for_type(&"Curve", true)
	var filtered_type := (Time.get_ticks_usec() - start) / runs
	start = Time.get_ticks_usec()
	var script_type := 0
	for path in ResourceLoader.get_cached_paths():
		var ref := ResourceLoader.get_cached_ref(path)
		if ref != null and ref.get_class() == "Curve":
			script_type += 1
	var script_type_usec := Time.get_ticks_usec() - start

	start = Time.get_ticks_usec()
	var by_ext := PackedStringArray()
	for i in runs:
		by_ext = ResourceLoader.get_cached_paths_with_extension("perfx")
	var filtered_ext := (Time.get_ticks_usec() - start) / runs
	start = Time.get_ticks_usec()
	var script_ext := 0
	for i in runs:
		script_ext = 0
		for path in ResourceLoader.get_cached_paths():
			if path.get_extension().to_lower() == "perfx":
				script_ext += 1
	var script_ext_usec := (Time.get_ticks_usec() - start) / runs

	start = Time.get_ticks_usec()
	var by_dir := PackedStringArray()
	for i in runs:
		by_dir = ResourceLoader.get_cached_paths_in_directory("res://__gd_filter_perf/hit/")
	var filtered_dir := (Time.get_ticks_usec() - start) / runs
	start = Time.get_ticks_usec()
	var script_dir := 0
	for i in runs:
		script_dir = 0
		for path in ResourceLoader.get_cached_paths():
			if path.begins_with("res://__gd_filter_perf/hit/"):
				script_dir += 1
	var script_dir_usec := (Time.get_ticks_usec() - start) / runs

	print("  INFO  %d cached paths, %d matches" % [ResourceLoader.get_cached_paths().size(), PERF_HITS])
	print("  INFO  by type:      filtered %d usec, get_cached_paths() + get_cached_ref() in script %d usec (one run)" % [filtered_type, script_type_usec])
	print("  INFO  by extension: filtered %d usec, get_cached_paths() + script filter %d usec" % [filtered_ext, script_ext_usec])
	print("  INFO  by directory: filtered %d usec, get_cached_paths() + script filter %d usec" % [filtered_dir, script_dir_usec])
	expect_equal(by_type.size(), PERF_HITS, "type filter returns the 100 matches of 10,000 paths")
	expect_equal(by_ext.size(), PERF_HITS, "extension filter returns the 100 matches of 10,000 paths")
	expect_equal(by_dir.size(), PERF_HITS, "directory filter returns the 100 matches of 10,000 paths")
	expect_equal([script_type, script_ext, script_dir], [PERF_HITS, PERF_HITS, PERF_HITS], "the script filters find the same 100 matches")
	held.clear()
	expect_equal(ResourceLoader.get_cached_paths_in_directory("res://__gd_filter_perf/").size(), 0, "10,000 paths are gone after release")


func _cleanup() -> void:
	_held.clear()
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
