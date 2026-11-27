extends Node
## Minimal example of File Manifests: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/File-Manifests

const DATA := "res://file-manifests/resources/data/"

var manifest: FilesManifest
var paths: PackedStringArray


func _ready() -> void:
	# Load all textures of two folders (the sample data has one SVG icon instead of PNG files).
	manifest = FilesManifest.new()
	manifest.type_hint = &"Texture2D"
	manifest.extensions = PackedStringArray(["png", "webp", "svg"])
	manifest.folder_paths = PackedStringArray([DATA + "a", DATA + "b"])
	manifest.recursive = true

	var textures: Dictionary = ResourceLoader.load_manifest(manifest) # path -> resource
	for path: String in textures:
		print(path, " ", (textures[path] as Texture2D).get_size())

	# Order (the example creates a mod and a patch file first).
	DirAccess.make_dir_recursive_absolute("user://mods/mod_a/translations")
	DirAccess.make_dir_recursive_absolute("user://patch")
	ResourceSaver.save(Curve.new(), "user://mods/mod_a/translations/mod_a.tres")
	ResourceSaver.save(Curve.new(), "user://patch/fixes.tres")
	var translations := FilesManifest.new()
	# Base game first, then every mod in name order: mods override the base game.
	translations.folder_paths = PackedStringArray([DATA + "b", "user://mods/*/translations"])
	# A patch file last: it overrides everything.
	translations.file_paths = PackedStringArray(["user://patch/fixes.tres"])
	for path: String in ResourceLoader.get_manifest_paths(translations):
		print(path) # in load order

	# Show a loading bar for a manifest (the example prints the progress instead of filling a ProgressBar).
	paths = ResourceLoader.get_manifest_paths(manifest)
	ResourceLoader.load_threaded_request_manifest(manifest)


func _process(_delta: float) -> void:
	var total: float = 0.0
	var done: int = 0
	for path: String in paths:
		var progress: Array = []
		if ResourceLoader.load_threaded_get_status(path, progress) != ResourceLoader.THREAD_LOAD_IN_PROGRESS:
			done += 1
		total += progress[0]
	print("Loading: ", total / paths.size() * 100.0, " %")
	if done == paths.size():
		set_process(false)
		_after_loading()


func _after_loading() -> void:
	# List save slots and create the folder on the first start.
	var saves := FilesManifest.new()
	saves.folder_paths = PackedStringArray(["user://saves"])
	saves.extensions = PackedStringArray(["tres"])
	saves.create_missing_folders = true
	var slots: PackedStringArray = ResourceLoader.get_manifest_paths(saves) # empty on the first start
	print("Save slots: ", slots.size())

	# React when a player drops a file into the mods folder.
	var mods := FilesManifest.new()
	mods.folder_paths = PackedStringArray(["user://mods"])
	mods.extensions = PackedStringArray(["tres"])
	mods.create_missing_folders = true
	FilesManifestServer.register_manifest("mods", mods)
	FilesManifestServer.file_added.connect(_on_mod_file_added)
	FilesManifestServer.set_watching("mods", true)
	ResourceSaver.save(Curve.new(), "user://mods/dropped.tres") # the player drops a file

	var validation: FilesManifestValidation = FilesManifestServer.validate("mods")
	print("Mods valid: ", validation.valid)


func _on_mod_file_added(manifest_name: String, path: String) -> void:
	print("New mod file: ", path)
	FilesManifestServer.set_watching("mods", false)
	DirAccess.remove_absolute(path)
	get_tree().quit()
