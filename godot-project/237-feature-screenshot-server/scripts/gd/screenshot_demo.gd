extends Node2D
## Press F12 (or set take_on_start, or run with `-- --take`) to save a screenshot of the window with ScreenshotServer.
## The path is shown on screen; the folder and file name come from Project Settings > Display > Screenshots.

@export var take_on_start := false

@onready var _label: Label = $Label


func _ready() -> void:
	ScreenshotServer.screenshot_saved.connect(_on_screenshot_saved)
	var from_command_line := OS.get_cmdline_user_args().has("--take")
	if take_on_start or from_command_line:
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		_label.text = "Saved by ScreenshotServer.take_screenshot()"
		await RenderingServer.frame_post_draw
		print("SCREENSHOT ", ProjectSettings.globalize_path(ScreenshotServer.take_screenshot()))
		if from_command_line:
			get_tree().quit()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_F12:
		ScreenshotServer.take_screenshot()


func _on_screenshot_saved(path: String, _image: Image) -> void:
	_label.text = "Saved: " + ProjectSettings.globalize_path(path)
