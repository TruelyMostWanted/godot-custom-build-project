extends FeatureTest
## Full API test of Resource Database (ResourceDatabase and PreloadList). Same tests as scripts/cs/Test.cs.

const ROOT := "user://resource-database/gd/"
const TYPE := &"DatabaseItem"
const THREAD_COUNT := 4
const ITEMS_PER_THREAD := 100

var _finished: Array = []
var _sword_iron: String
var _shield: String
var _sword_gold: String
var _potion: String
var _arrow: String


func _run_tests() -> void:
	ResourceDatabase.clear()
	_prepare()
	ResourceDatabase.preload_finished.connect(_on_preload_finished)
	var list: PreloadList = _make_list()
	_test_preload_list(list)
	await _test_preload_from_list(list)
	_test_lookup()
	_test_find()
	_test_add_and_remove_errors()
	await _test_stays_in_memory()
	await _test_threaded_preload()
	ResourceDatabase.preload_finished.disconnect(_on_preload_finished)
	_test_if_missing_memory_and_remove()
	_test_parallel_add_and_find()
	_test_clear()
	_cleanup()


func _item(item_name: String) -> DatabaseItem:
	var item := DatabaseItem.new()
	item.resource_name = item_name
	return item


func _save(file: String) -> String:
	var path: String = ROOT + file
	ResourceSaver.save(_item(file.get_file().get_basename()), path)
	return path


func _prepare() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT + "manifest")
	_sword_iron = _save("sword_iron.tres")
	_shield = _save("shield.tres")
	_sword_gold = _save("manifest/sword_gold.tres")
	_save("manifest/helmet.tres")
	_potion = _save("potion.tres")
	_arrow = _save("arrow.tres")


# A list with 3 resources, 2 paths and 1 manifest of 2 files.
func _make_list() -> PreloadList:
	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([ROOT + "manifest"])
	manifest.extensions = PackedStringArray(["tres"])
	var list := PreloadList.new()
	list.resources = [_item("sword_wood"), _item("bow"), _item("ring")]
	list.paths = PackedStringArray([_sword_iron, _shield])
	list.manifests = [manifest]
	list.keys = { "hero_sword": _sword_iron }
	return list


func _on_preload_finished(list: PreloadList, error: Error) -> void:
	_finished.append([list, error])


func _test_preload_list(list: PreloadList) -> void:
	expect(not list.is_empty(), "PreloadList.is_empty is false for a filled list")
	expect(PreloadList.new().is_empty(), "PreloadList.is_empty is true for a new list")
	expect_equal(list.resources.size(), 3, "PreloadList.resources holds the three resources")
	expect_equal(list.paths.size(), 2, "PreloadList.paths holds the two paths")
	expect_equal(list.manifests.size(), 1, "PreloadList.manifests holds the manifest")
	expect_equal(list.keys.size(), 1, "PreloadList.keys holds the key")


func _test_preload_from_list(list: PreloadList) -> void:
	expect(Engine.has_singleton("ResourceDatabase"), "the ResourceDatabase singleton exists")
	expect_equal(ResourceDatabase.preload_from_list(list, false), OK, "preload_from_list without threads returns OK")
	expect_equal(ResourceDatabase.get_entry_count(), 7, "get_entry_count: 3 resources + 2 paths + 2 manifest files")
	await get_tree().process_frame
	expect_equal(_finished.size(), 1, "preload_finished is emitted once on the main thread")
	expect(_finished[0][0] == list, "preload_finished passes the list")
	expect_equal(_finished[0][1], OK, "preload_finished passes OK")


func _test_lookup() -> void:
	var by_path: Resource = ResourceDatabase.get_resource(_sword_iron)
	expect(by_path != null, "get_resource finds a resource by path")
	expect(ResourceDatabase.get_resource("hero_sword") == by_path, "get_resource finds a resource by key")
	expect(ResourceDatabase.has_resource("sword_wood"), "has_resource finds a direct resource by name")
	expect(ResourceDatabase.has_resource(_sword_gold), "has_resource finds a manifest file by path")
	expect(not ResourceDatabase.has_resource("no_such_key"), "has_resource is false for an unknown key")
	expect(ResourceDatabase.get_resource("no_such_key") == null, "get_resource of an unknown key is null")
	expect(ResourceLoader.load(_sword_iron) == by_path, "ResourceLoader.load returns the stored instance")
	expect(ResourceLoader.load("hero_sword") == null, "a key that is not a path is not found by load")


func _test_find() -> void:
	expect_equal(ResourceDatabase.find_resources("", TYPE).size(), 7, "find_resources with an empty pattern and the class_name finds all 7")
	expect_equal(ResourceDatabase.find_resources("", &"Resource").size(), 7, "find_resources with the base class finds all 7")
	ResourceDatabase.add_resource("sword_texture", Resource.new())
	expect_equal(ResourceDatabase.find_resources("%sword%", TYPE).size(), 3, "find_resources %sword% with the type finds 3 swords, not the plain Resource")
	expect_equal(ResourceDatabase.find_resources("%sword%").size(), 4, "find_resources %sword% without a type finds 4")
	var keys: PackedStringArray = ResourceDatabase.find_keys("%SWORD%", TYPE)
	expect(keys.has("sword_wood"), "find_keys is case-insensitive: sword_wood")
	expect(keys.has(_sword_iron), "find_keys is case-insensitive: sword_iron path")
	expect(keys.has(_sword_gold), "find_keys is case-insensitive: sword_gold path")
	expect(not keys.has("sword_texture"), "find_keys with the type skips the plain Resource")


func _test_add_and_remove_errors() -> void:
	expect_equal(ResourceDatabase.add_resource("", Resource.new()), ERR_INVALID_PARAMETER, "add_resource with an empty key is rejected")
	expect_equal(ResourceDatabase.add_resource("null_item", null), ERR_INVALID_PARAMETER, "add_resource with a null resource is rejected")
	expect_equal(ResourceDatabase.remove_matching("no_such_%"), 0, "remove_matching of an unknown pattern removes nothing")


# Resources stay in memory after the scene that preloaded them is freed.
func _test_stays_in_memory() -> void:
	var scene := Node.new()
	add_child(scene)
	expect_equal(ResourceDatabase.preload_resource(_potion, "potion_key"), OK, "preload_resource from a child scene")
	scene.queue_free()
	await get_tree().process_frame
	await get_tree().process_frame
	expect(ResourceLoader.has_cached(_potion), "the resource stays cached after the scene is freed")
	expect(ResourceDatabase.get_resource("potion_key") != null, "the resource is still found by key")


func _test_threaded_preload() -> void:
	var list := PreloadList.new()
	list.paths = PackedStringArray([_arrow])
	_finished.clear()
	expect_equal(ResourceDatabase.preload_from_list(list), OK, "preload_from_list with threads returns OK")
	var end: int = Time.get_ticks_msec() + 5000
	while _finished.is_empty() and Time.get_ticks_msec() < end:
		await get_tree().process_frame
	expect_equal(_finished.size(), 1, "preload_finished is emitted for the threaded list")
	expect(_finished[0][0] == list, "preload_finished passes the threaded list")
	expect(ResourceDatabase.has_resource(_arrow), "the threaded preload stored the resource")
	expect(not ResourceDatabase.is_preloading(), "is_preloading is false afterwards")


func _test_if_missing_memory_and_remove() -> void:
	expect(not ResourceDatabase.add_resource_if_missing("hero_sword", _item("other")), "add_resource_if_missing keeps an existing key")
	expect(ResourceDatabase.add_resource_if_missing("new_key", _item("new")), "add_resource_if_missing stores a new key")
	expect(ResourceDatabase.get_memory_usage() > 0, "get_memory_usage is above 0")
	expect_equal(ResourceDatabase.remove_matching("%sword%"), 4, "remove_matching %sword% removes 4 entries")
	expect(not ResourceDatabase.has_resource("hero_sword"), "the key is removed with its entry")


func _add_many(index: int, failures: Array) -> void:
	for i: int in ITEMS_PER_THREAD:
		var key: String = "thread%d/item%d" % [index, i]
		if ResourceDatabase.add_resource(key, _item(key)) != OK:
			failures.append(key)
		if ResourceDatabase.find_resources("thread%d/%%" % index).size() != i + 1:
			failures.append("find " + key)


func _test_parallel_add_and_find() -> void:
	ResourceDatabase.clear()
	var failures: Array = []
	var threads: Array[Thread] = []
	for index: int in THREAD_COUNT:
		var thread := Thread.new()
		thread.start(_add_many.bind(index, failures))
		threads.append(thread)
	for thread: Thread in threads:
		thread.wait_to_finish()
	expect(failures.is_empty(), "4 threads: no failed add or find")
	expect_equal(ResourceDatabase.get_entry_count(), THREAD_COUNT * ITEMS_PER_THREAD, "4 threads: no lost entry")


func _test_clear() -> void:
	ResourceDatabase.clear()
	expect_equal(ResourceDatabase.get_entry_count(), 0, "clear removes every entry")


func _cleanup() -> void:
	for file: String in ["sword_iron.tres", "shield.tres", "manifest/sword_gold.tres", "manifest/helmet.tres", "potion.tres", "arrow.tres"]:
		DirAccess.remove_absolute(ROOT + file)
	DirAccess.remove_absolute(ROOT + "manifest")
	DirAccess.remove_absolute(ROOT)
