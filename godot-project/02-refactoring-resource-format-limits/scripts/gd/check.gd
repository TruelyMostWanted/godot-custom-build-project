extends FeatureCheck
## GDScript checks of 02-refactoring-resource-format-limits. Check the same things as scripts/cs/Issue02RefactoringResourceFormatLimitsCheck.cs.

const LimitLoader := preload("res://02-refactoring-resource-format-limits/scripts/gd/limit_loader.gd")
const LimitSaver := preload("res://02-refactoring-resource-format-limits/scripts/gd/limit_saver.gd")
const COUNT := 200


func _get_check_name() -> String:
	return "02-refactoring-resource-format-limits"


func _run_checks() -> void:
	_check_many_formats()
	_check_order()


func _loaded_id(path: String) -> int:
	var res := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	return res.get_meta("loader_id", -1) if res else -1


func _check_many_formats() -> void:
	var loaders: Array = []
	var savers: Array = []
	for i in COUNT:
		var loader := LimitLoader.new("gdlimit%d" % i, i)
		ResourceLoader.add_resource_format_loader(loader)
		loaders.append(loader)
		var saver := LimitSaver.new("gdlimit%d" % i)
		ResourceSaver.add_resource_format_saver(saver)
		savers.append(saver)

	var loaded := 0
	var saved := 0
	var res := Resource.new()
	for i in COUNT:
		var path := "res://__format_limit.gdlimit%d" % i
		if _loaded_id(path) == i:
			loaded += 1
		if ResourceSaver.save(res, path) == OK and savers[i].saved == 1:
			saved += 1
	expect_equal(loaded, COUNT, "all 200 GDScript loaders are used (old limit: 64 incl. engine loaders)")
	expect_equal(saved, COUNT, "all 200 GDScript savers are used (old limit: 64 incl. engine savers)")
	expect(ResourceLoader.get_recognized_extensions_for_type("Resource").has("gdlimit199"), "extension of the 200th loader is recognized")

	for i in COUNT:
		ResourceLoader.remove_resource_format_loader(loaders[i])
		ResourceSaver.remove_resource_format_saver(savers[i])
	expect(not ResourceLoader.get_recognized_extensions_for_type("Resource").has("gdlimit0"), "removed loaders are gone")


func _check_order() -> void:
	var older := LimitLoader.new("gdorder", 1)
	var front := LimitLoader.new("gdorder", 2)
	ResourceLoader.add_resource_format_loader(older)
	expect_equal(_loaded_id("res://__order.gdorder"), 1, "the first registered loader wins")
	ResourceLoader.add_resource_format_loader(front, true)
	expect_equal(_loaded_id("res://__order.gdorder"), 2, "a loader added with at_front wins over older loaders")
	ResourceLoader.remove_resource_format_loader(front)
	expect_equal(_loaded_id("res://__order.gdorder"), 1, "after removing it, the older loader wins again")
	ResourceLoader.remove_resource_format_loader(older)
