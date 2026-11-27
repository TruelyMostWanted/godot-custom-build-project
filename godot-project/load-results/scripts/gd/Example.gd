extends Node
## Minimal example of Load Results: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Load-Results

const QuizLoader := preload("res://load-results/scripts/gd/quiz_loader.gd")
const LEVEL := "user://levels/custom.tscn"


func _ready() -> void:
	# Tell the player why a save game didn't load
	# (the guide loads a SaveGame; this example saves and loads a plain Resource, and prints instead of showing a dialog)
	DirAccess.make_dir_recursive_absolute("user://saves")
	ResourceSaver.save(Resource.new(), "user://saves/slot_1.tres")
	var result: ResourceLoadResult = ResourceLoader.load_result("user://saves/slot_1.tres", "Resource")
	if result.ok:
		print("Apply save: ", result.resource)
	elif result.error == ERR_FILE_NOT_FOUND:
		print("Start new game")
	else:
		print("The save game is broken:\n", result.error_message)

	# Check that a file was saved
	var settings := Resource.new()
	var saved: ResourceSaveResult = ResourceSaver.save_result(settings, "user://settings.tres")
	if not saved.ok:
		push_error(saved.error_message)
	print("Settings saved: ", saved.ok)

	# Get the error of a background load
	# (the example saves the level first)
	var level := Node.new()
	var scene := PackedScene.new()
	scene.pack(level)
	level.free()
	DirAccess.make_dir_recursive_absolute("user://levels")
	ResourceSaver.save(scene, LEVEL)
	ResourceLoader.load_threaded_request(LEVEL)


func _process(_delta: float) -> void:
	if ResourceLoader.load_threaded_get_status(LEVEL) == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		return
	set_process(false)
	var result: ResourceLoadResult = ResourceLoader.load_threaded_get_result(LEVEL)
	if result.ok:
		add_child((result.resource as PackedScene).instantiate())
		print("Level loaded")
	else:
		print(result.error_message)

	# Give callers a message from your own loader
	# (the example loads a quiz file with text; an empty file would give the message of the guide)
	var file := FileAccess.open("user://questions.lrquiz", FileAccess.WRITE)
	file.store_string("What is 6 * 7?")
	file.close()
	var loader := QuizLoader.new()
	ResourceLoader.add_resource_format_loader(loader)
	var quiz: ResourceLoadResult = ResourceLoader.load_result("user://questions.lrquiz")
	print("Quiz loaded: ", quiz.ok)
	ResourceLoader.remove_resource_format_loader(loader)
	get_tree().quit()
