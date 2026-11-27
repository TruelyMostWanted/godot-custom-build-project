extends Node
## Minimal example of Screenshots: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Screenshots
## The input handler is left out (scenes/screenshot_demo.tscn shows it: press F12). Capturing needs a window.


func _ready() -> void:
	# Project settings.
	var folder: String = ProjectSettings.get_setting("display/screenshots/folder")
	var format: String = ProjectSettings.get_setting("display/screenshots/format")
	print("Folder: ", folder, ", format: ", format)

	# Taking a screenshot (a viewport has no image in headless runs).
	if DisplayServer.get_name() != "headless":
		var path: String = ScreenshotServer.take_screenshot()
		print("Saved ", ProjectSettings.globalize_path(path))

	# Capturing without saving.
	ScreenshotServer.screenshot_saved.connect(func(saved_path: String, _image: Image) -> void: print("screenshot_saved: ", saved_path))
	var minimap := Image.create_empty(64, 64, false, Image.FORMAT_RGBA8)
	minimap.fill(Color.CORNFLOWER_BLUE)
	ScreenshotServer.save_screenshot(minimap, "user://maps/minimap.webp")
	get_tree().quit()
