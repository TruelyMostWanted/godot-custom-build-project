extends FeatureTest
## Full API test of Resource Format Limits (ResourceLoader and ResourceSaver registration). Same tests as scripts/cs/Test.cs.

const MyLoader := preload("res://resource-format-limits/scripts/gd/my_loader.gd")
const MySaver := preload("res://resource-format-limits/scripts/gd/my_saver.gd")

# Upstream Godot stops at 64 loaders and 64 savers, engine formats included.
const COUNT := 200


func _run_tests() -> void:
	_test_many_formats()
	_test_order()


func _loaded_id(path: String) -> int:
	var resource: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	if resource == null:
		return -1
	return resource.get_meta("loader_id", -1)


func _test_many_formats() -> void:
	var loaders: Array[MyLoader] = []
	var savers: Array[MySaver] = []
	for i: int in COUNT:
		var loader := MyLoader.new("gdlimit%d" % i, i)
		ResourceLoader.add_resource_format_loader(loader)
		loaders.append(loader)
		var saver := MySaver.new("gdlimit%d" % i)
		ResourceSaver.add_resource_format_saver(saver)
		savers.append(saver)

	var loaded: int = 0
	var saved: int = 0
	var resource := Resource.new()
	for i: int in COUNT:
		var path: String = "res://__format_limit.gdlimit%d" % i
		if _loaded_id(path) == i:
			loaded += 1
		var error: Error = ResourceSaver.save(resource, path)
		if error == OK and savers[i].saved == 1:
			saved += 1
	expect_equal(loaded, COUNT, "all 200 loaders are used")
	expect_equal(saved, COUNT, "all 200 savers are used")
	var extensions: PackedStringArray = ResourceLoader.get_recognized_extensions_for_type("Resource")
	expect(extensions.has("gdlimit199"), "extension of the 200th loader is recognized")

	for i: int in COUNT:
		ResourceLoader.remove_resource_format_loader(loaders[i])
		ResourceSaver.remove_resource_format_saver(savers[i])
	extensions = ResourceLoader.get_recognized_extensions_for_type("Resource")
	expect(not extensions.has("gdlimit0"), "removed loaders are gone")


func _test_order() -> void:
	var older := MyLoader.new("gdorder", 1)
	var front := MyLoader.new("gdorder", 2)
	ResourceLoader.add_resource_format_loader(older)
	expect_equal(_loaded_id("res://__order.gdorder"), 1, "the first registered loader wins")
	ResourceLoader.add_resource_format_loader(front, true)
	expect_equal(_loaded_id("res://__order.gdorder"), 2, "a loader added with at_front wins over older loaders")
	ResourceLoader.remove_resource_format_loader(front)
	expect_equal(_loaded_id("res://__order.gdorder"), 1, "after removing it, the older loader wins again")
	ResourceLoader.remove_resource_format_loader(older)
