extends Node
## Minimal example of Streaming Assets: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Streaming-Assets
## project.godot enables streaming assets with res://streaming-assets/resources/streaming_assets as the source folder.


func _ready() -> void:
	# Ship a folder next to the game: an editor script and an export (skipped, both need the editor).

	# Read a streaming file at runtime.
	var file := FileAccess.open("streaming://config/levels.json", FileAccess.READ)
	if file:
		var levels: Variant = JSON.parse_string(file.get_as_text())
		print("Levels: ", levels)
	print("Location: ", StreamingAssetsServer.get_location())

	# Give a native library the OS path (the example prints it instead of calling a native player).
	var video_path: String = StreamingAssetsServer.get_os_path("streaming://config/levels.json")
	if video_path.is_empty():
		print("Play through FileAccess")
	else:
		print("Native player opens ", video_path)

	# Ship the folder as a second pack (the guide loads a texture of an exported pack; the example prints the location).
	print(StreamingAssetsServer.get_location() == StreamingAssetsServer.LOCATION_PACK) # true in a Pack-mode export

	# Test the desktop layout without exporting: start the game with --streaming-assets-path=<folder> (command line, skipped).

	# See what an export will ship (editor builds only).
	var files: PackedStringArray = StreamingAssetsExportPlugin.collect_files("res://streaming-assets/resources/streaming_assets")
	var target: String = StreamingAssetsExportPlugin.expand_template("{exe_name}_Data/StreamingAssets", "MyGame")
	print("%d files go to %s" % [files.size(), target])

	# Add your own data to a macOS or iOS bundle: an EditorExportPlugin that runs during an export (skipped).
	get_tree().quit()
