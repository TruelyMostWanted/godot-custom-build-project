extends FeatureTest
## Full API test of Load Results (ResourceLoadResult, ResourceSaveResult, load_result, save_result, load_threaded_get_result).
## Same tests as scripts/cs/Test.cs. Some steps print an engine error on purpose: the result collects it.

const QuizLoader := preload("res://load-results/scripts/gd/quiz_loader.gd")

const ROOT := "user://load-results/gd/"
const QUIZ_TEXT := "What is 6 * 7?"
const EMPTY_MESSAGE := "is empty."

var _loader: QuizLoader = QuizLoader.new()


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	ResourceLoader.add_resource_format_loader(_loader)
	_test_missing_file()
	_test_empty_file()
	_test_loaded_file()
	_test_type_mismatch()
	_test_save_result()
	await _test_threaded_result()
	_cleanup()


func _write(path: String, text: String) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text)
	file.close()


func _test_missing_file() -> void:
	var path: String = ROOT + "missing.lrquiz"
	var result: ResourceLoadResult = ResourceLoader.load_result(path)
	expect(not result.ok, "load_result of a missing file is not ok")
	expect_equal(result.error, ERR_FILE_NOT_FOUND, "a missing file gives ERR_FILE_NOT_FOUND")
	expect(result.error_message.contains("missing.lrquiz"), "the message names the path: " + result.error_message)
	expect(result.resource == null, "a failed load has no resource")
	expect_equal(result.path, path, "the result reports the path")


func _test_empty_file() -> void:
	var path: String = ROOT + "empty.lrquiz"
	_write(path, "")
	var result: ResourceLoadResult = ResourceLoader.load_result(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(not result.ok, "load_result of an empty file is not ok")
	expect_equal(result.error, ERR_FILE_CORRUPT, "an empty file gives the loader's code")
	var lines: PackedStringArray = result.error_message.split("\n")
	expect(lines[0].contains(EMPTY_MESSAGE), "the oldest line is the loader's message: " + lines[0])
	expect(lines.size() >= 2, "the engine adds its own lines after it")


func _test_loaded_file() -> void:
	var path: String = ROOT + "quiz.lrquiz"
	_write(path, QUIZ_TEXT)
	var result: ResourceLoadResult = ResourceLoader.load_result(path)
	expect(result.ok, "load_result of a valid file is ok")
	expect_equal(result.error, OK, "a valid file gives OK")
	expect(result.error_message.is_empty(), "a valid file has no message")
	var questions: PackedStringArray = result.resource.get_meta("questions")
	expect_equal(questions, PackedStringArray([QUIZ_TEXT]), "the result holds the loaded resource")
	var cached: Resource = load(path)
	expect(result.resource == cached, "the resource is the cached instance that load returns")
	expect_equal(result.path, path, "the result reports the path")


func _test_type_mismatch() -> void:
	var path: String = ROOT + "image.lrquiz"
	_write(path, QUIZ_TEXT)
	var result: ResourceLoadResult = ResourceLoader.load_result(path, "Image", ResourceLoader.CACHE_MODE_IGNORE)
	expect(not result.ok, "a resource of another type than the type hint is not ok")
	expect_equal(result.error, ERR_INVALID_DATA, "a type mismatch gives ERR_INVALID_DATA")
	expect(result.error_message.contains("expected Image, got Resource"), "the message names both types: " + result.error_message)


func _test_save_result() -> void:
	var resource := Resource.new()
	var path: String = ROOT + "saved.tres"
	var saved: ResourceSaveResult = ResourceSaver.save_result(resource, path)
	expect(saved.ok, "save_result to a writable path is ok")
	expect_equal(saved.error, OK, "a saved file gives OK")
	expect(saved.error_message.is_empty(), "a saved file has no message")
	expect_equal(saved.path, path, "save_result reports the path")

	var failed: ResourceSaveResult = ResourceSaver.save_result(resource, ROOT + "no_such_folder/saved.tres")
	expect(not failed.ok, "save_result to a path that cannot be written is not ok")
	expect_equal(failed.error, ERR_CANT_OPEN, "the error is the saver's code")
	expect(failed.error_message.contains("Cannot save"), "the message is the saver's message: " + failed.error_message)

	var null_result: ResourceSaveResult = ResourceSaver.save_result(null, ROOT + "null.tres")
	expect_equal(null_result.error, ERR_INVALID_PARAMETER, "saving null gives ERR_INVALID_PARAMETER")


func _test_threaded_result() -> void:
	var path: String = ROOT + "threaded.lrquiz"
	_write(path, "")
	var request_error: Error = ResourceLoader.load_threaded_request(path, "", false, ResourceLoader.CACHE_MODE_IGNORE)
	expect_equal(request_error, OK, "load_threaded_request accepts the empty file")
	var status: ResourceLoader.ThreadLoadStatus = ResourceLoader.load_threaded_get_status(path)
	var frames: int = 0
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and frames < 600:
		await get_tree().process_frame
		frames += 1
		status = ResourceLoader.load_threaded_get_status(path)
	expect_equal(status, ResourceLoader.THREAD_LOAD_FAILED, "the threaded load fails")
	var result: ResourceLoadResult = ResourceLoader.load_threaded_get_result(path)
	expect(not result.ok, "load_threaded_get_result of an empty file is not ok")
	expect_equal(result.error, ERR_FILE_CORRUPT, "the threaded load gives the loader's code")
	expect(result.error_message.contains(EMPTY_MESSAGE), "the message printed on the worker thread is captured: " + result.error_message)
	var again: ResourceLoadResult = ResourceLoader.load_threaded_get_result(path)
	expect_equal(again.error, ERR_INVALID_PARAMETER, "a result that was already collected gives ERR_INVALID_PARAMETER")


func _cleanup() -> void:
	ResourceLoader.remove_resource_format_loader(_loader)
	for file: String in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT + file)
