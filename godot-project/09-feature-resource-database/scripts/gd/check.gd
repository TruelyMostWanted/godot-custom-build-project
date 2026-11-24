extends FeatureCheck
## GDScript checks of 09-feature-resource-database. Check the same things as scripts/cs/Issue09FeatureResourceDatabaseCheck.cs.

const ROOT := "user://09-feature-resource-database/gd/"
const TYPE := &"Issue09GdItem"


func _get_check_name() -> String:
	return "09-feature-resource-database"


func _item(item_name: String) -> Issue09GdItem:
	var item := Issue09GdItem.new()
	item.resource_name = item_name
	return item


func _save(file: String) -> String:
	var path := ROOT + file
	ResourceSaver.save(_item(file.get_file().get_basename()), path)
	return path


func _insert_many(index: int, failures: Array) -> void:
	for i in 100:
		var key := "thread%d/item%d" % [index, i]
		if ResourceDatabase.insert(key, _item(key)) != OK:
			failures.append(key)
		if ResourceDatabase.select("thread%d/%%" % index).size() != i + 1:
			failures.append("select " + key)


func _run_checks() -> void:
	var db := ResourceDatabase
	expect(Engine.has_singleton("ResourceDatabase"), "ResourceDatabase singleton exists")
	db.clear()
	DirAccess.make_dir_recursive_absolute(ROOT + "manifest")
	var sword_iron := _save("sword_iron.tres")
	var shield := _save("shield.tres")
	var sword_gold := _save("manifest/sword_gold.tres")
	_save("manifest/helmet.tres")
	var potion := _save("potion.tres")
	var arrow := _save("arrow.tres")

	# A list with 3 resources, 2 paths and 1 manifest.
	var manifest := FilesManifest.new()
	manifest.folder_paths = PackedStringArray([ROOT + "manifest"])
	manifest.extensions = PackedStringArray(["tres"])
	var list := PreloadList.new()
	list.resources = [_item("sword_wood"), _item("bow"), _item("ring")]
	list.paths = PackedStringArray([sword_iron, shield])
	list.manifests = [manifest]
	list.keys = { "hero_sword": sword_iron }
	expect(not list.is_empty(), "PreloadList.is_empty() is false")

	var finished: Array = []
	var on_finished := func(l: PreloadList, error: int): finished.append([l, error])
	db.preload_finished.connect(on_finished)
	expect_equal(db.preload_list(list, false), OK, "preload_list() synchronous")
	expect_equal(db.count(), 7, "count(): 3 resources + 2 paths + 2 manifest files")
	await get_tree().process_frame
	expect(finished.size() == 1 and finished[0][0] == list and finished[0][1] == OK, "preload_finished(list, OK) on the main thread")

	# Found by path, key, name and type.
	expect(db.get_resource(sword_iron) != null, "found by path")
	expect(db.get_resource("hero_sword") == db.get_resource(sword_iron), "found by key")
	expect(db.has_resource("sword_wood"), "direct resource found by name")
	expect(db.has_resource(sword_gold), "manifest file found by path")
	expect_equal(db.select("", TYPE).size(), 7, "select('', Issue09GdItem): all 7")
	expect_equal(db.select("", &"Resource").size(), 7, "select('', Resource): inherited classes match")

	# load() returns the stored instance.
	expect(ResourceLoader.load(sword_iron) == db.get_resource(sword_iron), "ResourceLoader.load() returns the stored instance")

	# select("%sword%", type) returns the matching resources only.
	db.insert("sword_texture", Resource.new())
	var swords := db.select("%sword%", TYPE)
	expect_equal(swords.size(), 3, "select('%sword%', Issue09GdItem): 3 swords, not the plain Resource")
	var keys := db.select_keys("%SWORD%", TYPE)
	expect(keys.has("sword_wood") and keys.has(sword_iron) and keys.has(sword_gold), "select_keys() is case-insensitive")
	expect_equal(db.select("%sword%").size(), 4, "select('%sword%') without type: 4")

	# Resources stay in memory after the scene that preloaded them is freed.
	var scene := Node.new()
	add_child(scene)
	expect_equal(db.preload_path(potion, "potion_key"), OK, "preload_path() from a child scene")
	scene.queue_free()
	await get_tree().process_frame
	await get_tree().process_frame
	expect(ResourceLoader.has_cached(potion), "resource stays cached after the scene is freed")
	expect(db.get_resource("potion_key") != null, "resource still found by key")

	# Threaded preload.
	var list2 := PreloadList.new()
	list2.paths = PackedStringArray([arrow])
	finished.clear()
	expect_equal(db.preload_list(list2), OK, "preload_list() with threads")
	var end := Time.get_ticks_msec() + 5000
	while finished.is_empty() and Time.get_ticks_msec() < end:
		await get_tree().process_frame
	expect(finished.size() == 1 and finished[0][0] == list2, "preload_finished for the threaded list")
	expect(db.has_resource(arrow), "threaded preload stored the resource")
	expect(not db.is_preloading(), "is_preloading() false afterwards")
	db.preload_finished.disconnect(on_finished)

	# insert_if_absent, memory, remove.
	expect(not db.insert_if_absent("hero_sword", _item("other")), "insert_if_absent() keeps an existing key")
	expect(db.get_memory_usage() > 0, "get_memory_usage() > 0")
	expect_equal(db.remove("%sword%"), 4, "remove('%sword%') removes 4 entries")
	expect(not db.has_resource("hero_sword"), "key removed with its entry")

	# Parallel insert and select from 4 threads.
	db.clear()
	var failures: Array = []
	var threads: Array[Thread] = []
	for t in 4:
		var thread := Thread.new()
		thread.start(_insert_many.bind(t, failures))
		threads.append(thread)
	for thread in threads:
		thread.wait_to_finish()
	expect(failures.is_empty(), "4 threads: no failed insert or select")
	expect_equal(db.count(), 400, "4 threads: no lost entry")

	db.clear()
	expect_equal(db.count(), 0, "clear()")
	for f in ["sword_iron.tres", "shield.tres", "manifest/sword_gold.tres", "manifest/helmet.tres", "potion.tres", "arrow.tres"]:
		DirAccess.remove_absolute(ROOT + f)
	for d in ["manifest", ""]:
		DirAccess.remove_absolute(ROOT + d)
