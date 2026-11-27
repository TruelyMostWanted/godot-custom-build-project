extends FeatureTest
## Full API test of Resource Paths: resource_path, cache modes, take_over_path, saver flags, typed loader and saver virtuals.
## Same tests as scripts/cs/Test.cs, plus the typed pair that only GDScript has. The step "resource_path of a taken path" prints an engine error on purpose.

const NoteLoader := preload("res://resource-paths/scripts/gd/note_loader.gd")
const NoteSaver := preload("res://resource-paths/scripts/gd/note_saver.gd")

const ROOT := "user://resource-paths/gd/"
const NOTE_TEXT := "Buy milk"


# The _load() and _save() virtuals keep Godot's int arguments. This pair types them as the enum and the flags instead.
class TypedLoader extends ResourceFormatLoader:
	var last_cache_mode: int = -1

	func _get_recognized_extensions() -> PackedStringArray:
		return PackedStringArray(["rptyped"])

	func _handles_type(type: StringName) -> bool:
		return type == &"Resource"

	func _get_resource_type(path: String) -> String:
		if path.get_extension() == "rptyped":
			return "Resource"
		return ""

	func _load(path: String, _original_path: String, _use_sub_threads: bool, cache_mode: ResourceFormatLoader.CacheMode) -> Variant:
		last_cache_mode = cache_mode
		var resource := Resource.new()
		resource.resource_name = FileAccess.get_file_as_string(path)
		return resource


class TypedSaver extends ResourceFormatSaver:
	var last_flags: int = -1

	func _recognize(_resource: Resource) -> bool:
		return true

	func _get_recognized_extensions(_resource: Resource) -> PackedStringArray:
		return PackedStringArray(["rptyped"])

	func _save(resource: Resource, path: String, flags: int) -> Error:
		last_flags = flags
		var file := FileAccess.open(path, FileAccess.WRITE)
		file.store_string(resource.resource_name)
		return OK


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_cache_modes()
	_test_save_leaves_the_path()
	_test_take_over_path()
	_test_resource_path_of_a_taken_path()
	_test_loader_and_saver()
	_test_typed_loader_and_saver()
	_cleanup()


func _make(resource_name: String) -> Resource:
	var resource := Resource.new()
	resource.resource_name = resource_name
	return resource


func _test_cache_modes() -> void:
	var path: String = ROOT + "stats.tres"
	ResourceSaver.save(_make("v1"), path)
	var first: Resource = load(path)
	var second: Resource = load(path)
	expect_equal(first.resource_path, path, "REUSE sets the path")
	expect(first == second, "REUSE returns the cached instance")
	expect(ResourceLoader.has_cached(path), "REUSE caches the instance")

	var ignored: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(ignored != first, "IGNORE returns a new instance")
	expect_equal(ignored.resource_path, path, "IGNORE reports the path")
	expect(load(path) == first, "IGNORE leaves the cached instance in the cache")

	ResourceSaver.save(_make("v2"), path)
	var replaced: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REPLACE)
	expect(replaced == first, "REPLACE returns the cached instance")
	expect_equal(first.resource_name, "v2", "REPLACE updates the cached instance in place")
	expect_equal(first.resource_path, path, "REPLACE keeps the path")


func _test_save_leaves_the_path() -> void:
	var path: String = ROOT + "profile.tres"
	var profile := Resource.new()
	ResourceSaver.save(profile, path)
	expect_equal(profile.resource_path, "", "save does not set the path")
	ResourceSaver.save(profile, path, ResourceSaver.FLAG_CHANGE_PATH)
	expect_equal(profile.resource_path, "", "FLAG_CHANGE_PATH restores the previous path after saving")
	var loaded: Resource = load(path)
	expect(loaded != profile, "a saved resource without a path is not the cached one")


func _test_take_over_path() -> void:
	var path: String = ROOT + "taken.tres"
	var first := Resource.new()
	ResourceSaver.save(first, path)
	first.take_over_path(path)
	expect_equal(first.resource_path, path, "take_over_path sets the path")
	expect(load(path) == first, "load returns the resource that took over the path")
	var other := Resource.new()
	other.take_over_path(path)
	expect_equal(other.resource_path, path, "another take_over_path gets the path")
	expect_equal(first.resource_path, "", "the previous holder loses the path")
	expect(load(path) == other, "load returns the new holder")


func _test_resource_path_of_a_taken_path() -> void:
	var path: String = ROOT + "held.tres"
	var holder := Resource.new()
	holder.take_over_path(path)
	var latecomer := Resource.new()
	latecomer.resource_path = path
	expect_equal(latecomer.resource_path, "", "setting resource_path of a held path fails and leaves no path")
	expect_equal(holder.resource_path, path, "the holder keeps the path")


func _test_loader_and_saver() -> void:
	var path: String = ROOT + "shopping.note"
	var loader := NoteLoader.new()
	var saver := NoteSaver.new()
	ResourceLoader.add_resource_format_loader(loader)
	ResourceSaver.add_resource_format_saver(saver)
	var note: Resource = _make(NOTE_TEXT)

	expect_equal(ResourceSaver.save(note, path), OK, "the saver writes the file")
	expect_equal(saver.last_flags, 0, "the saver gets no flags by default")
	ResourceSaver.save(note, path, ResourceSaver.FLAG_COMPRESS)
	expect_equal(saver.last_flags & ResourceSaver.FLAG_COMPRESS, ResourceSaver.FLAG_COMPRESS, "the saver gets FLAG_COMPRESS")

	var loaded: Resource = load(path)
	expect_equal(loaded.resource_name, NOTE_TEXT, "the loader reads the file")
	expect_equal(loaded.resource_path, path, "ResourceLoader sets the path, not the loader")
	expect_equal(loader.last_cache_mode, ResourceFormatLoader.CACHE_MODE_REUSE, "the loader gets CACHE_MODE_REUSE by default")
	ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(loader.last_cache_mode, ResourceFormatLoader.CACHE_MODE_IGNORE, "the loader gets CACHE_MODE_IGNORE")

	ResourceLoader.remove_resource_format_loader(loader)
	ResourceSaver.remove_resource_format_saver(saver)


func _test_typed_loader_and_saver() -> void:
	var path: String = ROOT + "typed.rptyped"
	var loader := TypedLoader.new()
	var saver := TypedSaver.new()
	ResourceLoader.add_resource_format_loader(loader)
	ResourceSaver.add_resource_format_saver(saver)

	ResourceSaver.save(_make(NOTE_TEXT), path, ResourceSaver.FLAG_COMPRESS)
	expect_equal(saver.last_flags & ResourceSaver.FLAG_COMPRESS, ResourceSaver.FLAG_COMPRESS, "a saver that types its flags gets FLAG_COMPRESS")
	var loaded: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(loaded.resource_name, NOTE_TEXT, "a loader that types its cache mode loads the file")
	expect_equal(loader.last_cache_mode, ResourceFormatLoader.CACHE_MODE_IGNORE, "a loader that types its cache mode gets CACHE_MODE_IGNORE")

	ResourceLoader.remove_resource_format_loader(loader)
	ResourceSaver.remove_resource_format_saver(saver)


func _cleanup() -> void:
	for file: String in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT + file)
