extends FeatureCheck
## GDScript checks of 01-feature-resource-paths-retrieval. Check the same things as scripts/cs/Issue01FeatureResourcePathsRetrievalCheck.cs.

const DIR := "res://01-feature-resource-paths-retrieval/resources/"
const FIRST := DIR + "first.tres"
const SECOND := DIR + "second.tres"
const THIRD := DIR + "third.tres"
const THREAD_COUNT := 4
const THREAD_ITERATIONS := 2000
const PERF_COUNT := 10000

var _thread_path_regex := RegEx.create_from_string("^res://__gd_thread_\\d+/\\d+\\.tres$")


func _get_check_name() -> String:
	return "01-feature-resource-paths-retrieval"


func _run_checks() -> void:
	_check_loaded_paths()
	_check_freed_paths()
	_check_threads()
	_check_performance()


func _check_loaded_paths() -> void:
	expect(ClassDB.class_has_method("ResourceLoader", "get_cached_paths"), "ResourceLoader has get_cached_paths()")

	var first := load(FIRST)
	var second := load(SECOND)
	var third := load(THIRD)
	var paths: PackedStringArray = ResourceLoader.get_cached_paths()
	expect(paths.has(FIRST), "first.tres is in the cached paths")
	expect(paths.has(SECOND), "second.tres is in the cached paths")
	expect(paths.has(THIRD), "third.tres is in the cached paths")
	expect(paths.has(THIRD + "::Resource_child"), "built-in sub-resource third.tres::Resource_child is in the cached paths")
	expect(not paths.has(""), "no empty path in the cached paths")
	expect_equal(paths.size(), ResourceLoader.get_cached_paths().size(), "two calls without changes give the same count")
	for path in [FIRST, SECOND, THIRD]:
		expect(ResourceLoader.get_cached_ref(path) != null, "get_cached_ref() resolves %s" % path)
	expect(first != null and second != null and third != null, "all three resources loaded")


func _check_freed_paths() -> void:
	var res := load(FIRST)
	expect(ResourceLoader.get_cached_paths().has(FIRST), "first.tres is cached while referenced")
	res = null
	expect(not ResourceLoader.get_cached_paths().has(FIRST), "first.tres is gone after its last reference is freed")

	var scratch := Resource.new()
	scratch.take_over_path("res://__gd_scratch.tres")
	expect(ResourceLoader.get_cached_paths().has("res://__gd_scratch.tres"), "take_over_path() adds the path")
	scratch = null
	expect(not ResourceLoader.get_cached_paths().has("res://__gd_scratch.tres"), "freed resource with take_over_path() is gone")


func _thread_worker(p_index: int) -> void:
	for i in THREAD_ITERATIONS:
		var res := Resource.new()
		res.take_over_path("res://__gd_thread_%d/%d.tres" % [p_index, i])
		res = null


func _check_threads() -> void:
	var threads: Array[Thread] = []
	for t in THREAD_COUNT:
		var thread := Thread.new()
		thread.start(_thread_worker.bind(t))
		threads.append(thread)
	ResourceLoader.load_threaded_request(SECOND)
	ResourceLoader.load_threaded_request(THIRD)

	var calls := 0
	var bad_paths := 0
	var seen_thread_paths := 0
	var running := true
	while running:
		running = false
		for thread in threads:
			if thread.is_alive():
				running = true
		for path in ResourceLoader.get_cached_paths():
			if not path.begins_with("res://"):
				bad_paths += 1
			elif path.begins_with("res://__gd_thread_"):
				seen_thread_paths += 1
				if _thread_path_regex.search(path) == null:
					bad_paths += 1
		calls += 1
	for thread in threads:
		thread.wait_to_finish()
	ResourceLoader.load_threaded_get(SECOND)
	ResourceLoader.load_threaded_get(THIRD)

	print("  INFO  %d calls while %d threads changed the cache, %d thread paths seen" % [calls, THREAD_COUNT, seen_thread_paths])
	expect(calls > 0, "get_cached_paths() was called while threads were running")
	expect_equal(bad_paths, 0, "no partial or malformed path while threads load")
	var leftovers := 0
	for path in ResourceLoader.get_cached_paths():
		if path.begins_with("res://__gd_thread_"):
			leftovers += 1
	expect_equal(leftovers, 0, "no thread path left after the threads freed their resources")


func _check_performance() -> void:
	var held: Array[Resource] = []
	for i in PERF_COUNT:
		var res := Resource.new()
		res.take_over_path("res://__gd_perf/%d.tres" % i)
		held.append(res)

	var runs := 10
	var start := Time.get_ticks_usec()
	var paths := PackedStringArray()
	for i in runs:
		paths = ResourceLoader.get_cached_paths()
	var avg := (Time.get_ticks_usec() - start) / runs
	print("  INFO  get_cached_paths() with %d cached paths: %d usec per call (average of %d)" % [paths.size(), avg, runs])

	var perf_paths := 0
	for path in paths:
		if path.begins_with("res://__gd_perf/"):
			perf_paths += 1
	expect_equal(perf_paths, PERF_COUNT, "all 10,000 paths are returned")
	held.clear()
	paths = ResourceLoader.get_cached_paths()
	expect(not paths.has("res://__gd_perf/0.tres"), "10,000 paths are gone after release")
