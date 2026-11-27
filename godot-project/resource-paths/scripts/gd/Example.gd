extends Node
## Minimal example of Resource Paths: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Resource-Paths

const NoteLoader := preload("res://resource-paths/scripts/gd/note_loader.gd")
const NoteSaver := preload("res://resource-paths/scripts/gd/note_saver.gd")


func _ready() -> void:
	DirAccess.make_dir_recursive_absolute("user://balance")
	DirAccess.make_dir_recursive_absolute("user://levels")

	# Try it
	_try_it()

	# Reload a resource that is already in use
	_reload_resource()

	# Load a private copy
	_load_private_copy()

	# Save to a new path and keep using it
	_save_to_new_path()

	# Write a loader and a saver
	_write_loader_and_saver()
	get_tree().quit()


func _try_it() -> void:
	# A resource file to play with.
	var stats := Resource.new()
	stats.resource_name = "stats"
	ResourceSaver.save(stats, "user://stats.tres")

	var a: Resource = load("user://stats.tres")
	var b: Resource = load("user://stats.tres")
	print("load twice:   same instance = ", a == b, ", path = ", a.resource_path)

	var copy: Resource = ResourceLoader.load("user://stats.tres", "", ResourceLoader.CACHE_MODE_IGNORE)
	print("IGNORE:       same instance = ", copy == a, ", path = ", copy.resource_path)
	print("              load() still returns the first: ", load("user://stats.tres") == a)

	var again: Resource = ResourceLoader.load("user://stats.tres", "", ResourceLoader.CACHE_MODE_REPLACE)
	print("REPLACE:      same instance = ", again == a)

	var profile := Resource.new()
	ResourceSaver.save(profile, "user://profile.tres")
	print("save:         path = '", profile.resource_path, "'")
	ResourceSaver.save(profile, "user://profile.tres", ResourceSaver.FLAG_CHANGE_PATH)
	print("CHANGE_PATH:  path = '", profile.resource_path, "'")
	profile.take_over_path("user://profile.tres")
	print("take over:    path = '", profile.resource_path, "', load() returns it: ", load("user://profile.tres") == profile)

	var other := Resource.new()
	other.take_over_path("user://profile.tres")
	print("another takes it: first path = '", profile.resource_path, "', other path = '", other.resource_path, "'")


func _reload_resource() -> void:
	var first := Resource.new()
	first.resource_name = "old values"
	ResourceSaver.save(first, "user://balance/stats.tres")
	var stats: Resource = load("user://balance/stats.tres")
	# ... the file is rewritten ...
	var rewritten := Resource.new()
	rewritten.resource_name = "new values"
	ResourceSaver.save(rewritten, "user://balance/stats.tres")
	var same: Resource = ResourceLoader.load("user://balance/stats.tres", "", ResourceLoader.CACHE_MODE_REPLACE)
	print("Same instance, new values: ", same == stats, ", ", stats.resource_name)


func _load_private_copy() -> void:
	var level := Resource.new()
	ResourceSaver.save(level, "user://levels/level_1.tres")
	var running: Resource = load("user://levels/level_1.tres")
	var copy: Resource = ResourceLoader.load("user://levels/level_1.tres", "", ResourceLoader.CACHE_MODE_IGNORE)
	print("Copy is another instance: ", copy != running)
	print("Same path: ", copy.resource_path == running.resource_path)


func _save_to_new_path() -> void:
	# The guide saves a PlayerProfile; a plain Resource does the same here.
	var profile := Resource.new()
	if ResourceSaver.save(profile, "user://new_profile.tres") == OK:
		profile.take_over_path("user://new_profile.tres")
		# load("user://new_profile.tres") now returns this instance.
		print("load() returns the profile: ", load("user://new_profile.tres") == profile)


func _write_loader_and_saver() -> void:
	var loader := NoteLoader.new()
	var saver := NoteSaver.new()
	ResourceLoader.add_resource_format_loader(loader)
	ResourceSaver.add_resource_format_saver(saver)
	var note := Resource.new()
	note.resource_name = "Buy milk"
	ResourceSaver.save(note, "user://shopping.note")
	var loaded: Resource = ResourceLoader.load("user://shopping.note", "", ResourceLoader.CACHE_MODE_IGNORE)
	print("Note: ", loaded.resource_name)
	ResourceLoader.remove_resource_format_loader(loader)
	ResourceSaver.remove_resource_format_saver(saver)
