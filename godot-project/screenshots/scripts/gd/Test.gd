extends FeatureTest
## Full API test of Screenshots (ScreenshotServer): every method, property setting and signal. Same tests as scripts/cs/Test.cs.
## Runs headless: viewports and screens give nothing, so the capture methods are tested for that answer (they print an ERROR).

const FOLDER := "user://screenshots-test"
const SIZE := Vector2i(64, 64)
const DEFAULT_FILE_NAME := "Screenshot {year}-{month}-{day} {hour}-{minute}-{second}"
const DEFAULT_QUALITY := 0.9

var _saved: Array[String] = []
var _image: Image


func _run_tests() -> void:
	var noise := FastNoiseLite.new()
	_image = noise.get_image(SIZE.x, SIZE.y)
	ScreenshotServer.screenshot_saved.connect(_on_screenshot_saved)
	_test_singleton_and_settings()
	_test_save_screenshot_formats()
	_test_save_screenshot_quality()
	_test_save_screenshot_failures()
	_test_make_screenshot_path()
	_test_file_name_placeholders()
	_test_unique_names()
	_test_screenshot_saved_signal()
	_test_capture_in_headless()
	ScreenshotServer.screenshot_saved.disconnect(_on_screenshot_saved)
	_remove_saved_files()


func _test_singleton_and_settings() -> void:
	expect(Engine.has_singleton("ScreenshotServer"), "ScreenshotServer is a singleton")
	expect_equal(ProjectSettings.get_setting("display/screenshots/folder"), "user://screenshots", "default folder")
	expect_equal(ProjectSettings.get_setting("display/screenshots/file_name"), DEFAULT_FILE_NAME, "default file name")
	expect_equal(ProjectSettings.get_setting("display/screenshots/format"), "png", "default format")
	expect_equal(ProjectSettings.get_setting("display/screenshots/quality"), DEFAULT_QUALITY, "default quality")


func _test_save_screenshot_formats() -> void:
	for extension: String in ["png", "jpg", "webp"]:
		var path: String = FOLDER.path_join("explicit." + extension)
		var saved: String = ScreenshotServer.save_screenshot(_image, path)
		expect_equal(saved, path, "save_screenshot writes .%s and returns the path" % extension)
		var loaded: Image = Image.load_from_file(path)
		expect_equal(loaded.get_size(), SIZE, ".%s loads again with the same size" % extension)


func _test_save_screenshot_quality() -> void:
	ProjectSettings.set_setting("display/screenshots/quality", 0.1)
	var low: String = ScreenshotServer.save_screenshot(_image, FOLDER.path_join("low.jpg"))
	ProjectSettings.set_setting("display/screenshots/quality", 1.0)
	var high: String = ScreenshotServer.save_screenshot(_image, FOLDER.path_join("high.jpg"))
	ProjectSettings.set_setting("display/screenshots/quality", DEFAULT_QUALITY)
	var low_size: int = FileAccess.get_file_as_bytes(low).size()
	var high_size: int = FileAccess.get_file_as_bytes(high).size()
	expect(low_size < high_size, "a lower quality makes a smaller .jpg")


func _test_save_screenshot_failures() -> void:
	var no_image: String = ScreenshotServer.save_screenshot(null, FOLDER.path_join("none.png"))
	expect_equal(no_image, "", "save_screenshot without an image returns an empty string")
	var bad_extension: String = ScreenshotServer.save_screenshot(_image, FOLDER.path_join("bad.bmp"))
	expect_equal(bad_extension, "", "save_screenshot with an unsupported extension returns an empty string")


func _test_make_screenshot_path() -> void:
	ProjectSettings.set_setting("display/screenshots/folder", FOLDER.path_join("auto"))
	ProjectSettings.set_setting("display/screenshots/file_name", "{project} check")
	ProjectSettings.set_setting("display/screenshots/format", "jpg")
	var path: String = ScreenshotServer.make_screenshot_path()
	var project_name: String = ProjectSettings.get_setting("application/config/name")
	expect_equal(path, FOLDER.path_join("auto").path_join(project_name + " check.jpg"), "make_screenshot_path uses folder, file name and format")
	expect(not FileAccess.file_exists(path), "make_screenshot_path does not create the file")


func _test_file_name_placeholders() -> void:
	var year: int = Time.get_date_dict_from_system().year
	ProjectSettings.set_setting("display/screenshots/file_name", "{year}_{unix}_{millisecond}")
	var parts: PackedStringArray = ScreenshotServer.make_screenshot_path().get_file().get_basename().split("_")
	expect_equal(parts[0], "%04d" % year, "{year} is the year with four digits")
	expect(parts[1].is_valid_int(), "{unix} is a number")
	expect_equal(parts[2].length(), 3, "{millisecond} has three digits")
	ProjectSettings.set_setting("display/screenshots/file_name", "a:b?c")
	var file_name: String = ScreenshotServer.make_screenshot_path().get_file().get_basename()
	expect_equal(file_name, "a-b-c", "characters not allowed in file names become -")
	ProjectSettings.set_setting("display/screenshots/file_name", "   ")
	file_name = ScreenshotServer.make_screenshot_path().get_file().get_basename()
	expect_equal(file_name, "Screenshot", "an empty file name becomes Screenshot")


func _test_unique_names() -> void:
	ProjectSettings.set_setting("display/screenshots/file_name", "same")
	var first: String = ScreenshotServer.save_screenshot(_image)
	var second: String = ScreenshotServer.save_screenshot(_image)
	var third: String = ScreenshotServer.save_screenshot(_image)
	expect(first.ends_with("same.jpg"), "the first screenshot gets the plain name")
	expect(second.ends_with("same (2).jpg"), "the second one gets (2)")
	expect(third.ends_with("same (3).jpg"), "the third one gets (3)")


func _test_screenshot_saved_signal() -> void:
	var before: int = _saved.size()
	var path: String = ScreenshotServer.save_screenshot(_image, FOLDER.path_join("signal.png"))
	expect_equal(_saved.size(), before + 1, "screenshot_saved is emitted once per saved file")
	expect_equal(_saved[before], path, "screenshot_saved carries the path")


func _test_capture_in_headless() -> void:
	expect_equal(DisplayServer.get_name(), "headless", "the tests run headless")
	expect(ScreenshotServer.capture_viewport(get_viewport()) == null, "capture_viewport gives null: nothing is rendered")
	expect(ScreenshotServer.capture_screen() == null, "capture_screen gives null: no screen")
	expect_equal(ScreenshotServer.take_screenshot(), "", "take_screenshot gives an empty string")
	expect_equal(ScreenshotServer.take_screen_screenshot(0), "", "take_screen_screenshot gives an empty string")


func _remove_saved_files() -> void:
	for path in _saved:
		DirAccess.remove_absolute(path)


func _on_screenshot_saved(path: String, _saved_image: Image) -> void:
	_saved.append(path)
