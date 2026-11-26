extends FeatureCheck
## GDScript checks of 237-feature-screenshot-server. Check the same things as scripts/cs/Issue237FeatureScreenshotServerCheck.cs.
## The checks run headless, where viewports render nothing: they save a generated image.
## packed-scenes/screenshot_demo.tscn shows the viewport screenshot in a window (F12).

const FOLDER := "user://237-feature-screenshot-server/gd"

var _saved: Array[String] = []


func _get_check_name() -> String:
	return "237-feature-screenshot-server"


func _run_checks() -> void:
	expect(Engine.has_singleton("ScreenshotServer"), "ScreenshotServer is a singleton")
	expect_equal(ProjectSettings.get_setting("display/screenshots/folder"), "user://screenshots", "default folder")
	expect_equal(ProjectSettings.get_setting("display/screenshots/format"), "png", "default format")

	var image := Image.create_empty(32, 16, false, Image.FORMAT_RGBA8)
	image.fill(Color.CORNFLOWER_BLUE)
	ScreenshotServer.screenshot_saved.connect(_on_saved)

	for extension: String in ["png", "jpg", "webp"]:
		var path := FOLDER.path_join("explicit." + extension)
		expect_equal(ScreenshotServer.save_screenshot(image, path), path, "save_screenshot() writes .%s" % extension)
		var loaded := Image.load_from_file(path)
		expect(loaded != null and loaded.get_size() == Vector2i(32, 16), ".%s loads again" % extension)

	ProjectSettings.set_setting("display/screenshots/folder", FOLDER.path_join("auto"))
	ProjectSettings.set_setting("display/screenshots/file_name", "{project} check")
	ProjectSettings.set_setting("display/screenshots/format", "jpg")
	var next := ScreenshotServer.make_screenshot_path()
	expect(next.begins_with(FOLDER.path_join("auto")) and next.ends_with(".jpg"), "make_screenshot_path() uses folder and format")
	var first := ScreenshotServer.save_screenshot(image)
	var second := ScreenshotServer.save_screenshot(image)
	expect(first != "" and second != "" and first != second, "two screenshots with the same name don't overwrite each other")
	expect(second.ends_with(" (2).jpg"), "the second one gets (2)")
	expect(first.get_file().begins_with(ProjectSettings.get_setting("application/config/name")), "{project} is the project name")
	expect_equal(_saved.size(), 5, "screenshot_saved was emitted for every saved file")

	ProjectSettings.set_setting("display/screenshots/folder", "user://screenshots")
	ProjectSettings.set_setting("display/screenshots/file_name", "Screenshot {year}-{month}-{day} {hour}-{minute}-{second}")
	ProjectSettings.set_setting("display/screenshots/format", "png")
	ScreenshotServer.screenshot_saved.disconnect(_on_saved)
	for path in _saved:
		DirAccess.remove_absolute(path)


func _on_saved(path: String, image: Image) -> void:
	if image != null:
		_saved.append(path)
