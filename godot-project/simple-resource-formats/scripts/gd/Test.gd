extends FeatureTest
## Full API test of Simple Resource Formats (register_extension_loader and register_extension_saver). Same tests as scripts/cs/Test.cs.
## Some steps print an engine error on purpose (a failing load function, no loader found).

const ROOT := "user://simple-resource-formats/gd/"
const EXTENSION := "gdquiz"
const QUIZ_TYPE := &"QuizSet"
const QUESTIONS: Array[String] = ["Which engine is this?", "What is 6 * 7?", "Is the Web single-threaded?"]


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_save_and_load()
	await _test_threaded_load()
	_test_type_rules()
	_test_at_front()
	_test_errors()
	_test_unregister()
	_cleanup()


static func _load_quiz(path: String) -> Variant:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return FileAccess.get_open_error()
	var quiz := QuizSet.new()
	quiz.questions = file.get_as_text().split("\n", false)
	return quiz


static func _save_quiz(quiz: QuizSet, path: String) -> Error:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string("\n".join(quiz.questions))
	return OK


static func _load_corrupt(_path: String) -> Variant:
	return ERR_FILE_CORRUPT


func _make_quiz() -> QuizSet:
	var quiz := QuizSet.new()
	quiz.questions = PackedStringArray(QUESTIONS)
	return quiz


func _test_save_and_load() -> void:
	var load_error: Error = ResourceLoader.register_extension_loader(EXTENSION, _load_quiz, QUIZ_TYPE)
	expect_equal(load_error, OK, "register_extension_loader registers a load function")
	var save_error: Error = ResourceSaver.register_extension_saver(EXTENSION, _save_quiz, QUIZ_TYPE)
	expect_equal(save_error, OK, "register_extension_saver registers a save function")
	expect(ResourceLoader.has_extension_loader(EXTENSION), "has_extension_loader finds the extension")
	expect(ResourceSaver.has_extension_saver("." + EXTENSION.to_upper()), "has_extension_saver ignores the dot and the case")

	var path: String = ROOT + "questions." + EXTENSION
	expect_equal(ResourceSaver.save(_make_quiz(), path), OK, "save writes the file through the save function")
	expect_equal(FileAccess.get_file_as_string(path), "\n".join(QUESTIONS), "the file holds one question per line")
	expect(ResourceLoader.exists(path), "exists finds the file")
	expect_equal(ResourceLoader.get_resource_type(path), "Resource", "get_resource_type reports the native base of the script class")

	var loaded: Resource = load(path)
	expect(loaded is QuizSet, "load returns a QuizSet")
	var quiz: QuizSet = loaded
	expect_equal(quiz.questions, PackedStringArray(QUESTIONS), "the loaded questions match")
	expect(ResourceLoader.has_cached(path), "the loaded resource is cached like any other resource")
	expect(load(path) == loaded, "a second load returns the cached resource")
	var hinted: Resource = ResourceLoader.load(path, "QuizSet", ResourceLoader.CACHE_MODE_IGNORE)
	expect(hinted is QuizSet, "load with the type hint returns a QuizSet")
	expect(hinted != loaded, "load with CACHE_MODE_IGNORE returns a new resource")


func _test_threaded_load() -> void:
	var path: String = ROOT + "threaded." + EXTENSION
	ResourceSaver.save(_make_quiz(), path)
	expect_equal(ResourceLoader.load_threaded_request(path), OK, "load_threaded_request accepts the file")
	var status: ResourceLoader.ThreadLoadStatus = ResourceLoader.load_threaded_get_status(path)
	var frames: int = 0
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and frames < 600:
		await get_tree().process_frame
		frames += 1
		status = ResourceLoader.load_threaded_get_status(path)
	expect_equal(status, ResourceLoader.THREAD_LOAD_LOADED, "the threaded load finishes")
	var loaded: Resource = ResourceLoader.load_threaded_get(path)
	expect(loaded is QuizSet, "the threaded load calls the load function")


func _test_type_rules() -> void:
	var quiz_extensions: PackedStringArray = ResourceLoader.get_recognized_extensions_for_type("QuizSet")
	expect(quiz_extensions.has(EXTENSION), "get_recognized_extensions_for_type lists the extension for the script class")
	var texture_extensions: PackedStringArray = ResourceLoader.get_recognized_extensions_for_type("Texture2D")
	expect(not texture_extensions.has(EXTENSION), "the extension is not offered for unrelated types")
	var saver_extensions: PackedStringArray = ResourceSaver.get_recognized_extensions(_make_quiz())
	expect(saver_extensions.has(EXTENSION), "ResourceSaver offers the extension for the script class")

	var plain := Resource.new()
	var plain_error: Error = ResourceSaver.save(plain, ROOT + "plain." + EXTENSION)
	expect_equal(plain_error, ERR_FILE_UNRECOGNIZED, "resources of another type are not saved with the format")

	var node_error: Error = ResourceLoader.register_extension_loader("gdquizbad", _load_quiz, &"Node")
	expect_equal(node_error, ERR_INVALID_PARAMETER, "a type that is not a Resource is rejected")
	var empty_error: Error = ResourceLoader.register_extension_loader("", _load_quiz)
	expect_equal(empty_error, ERR_INVALID_PARAMETER, "an empty extension is rejected")
	var callable_error: Error = ResourceSaver.register_extension_saver("gdquizbad", Callable())
	expect_equal(callable_error, ERR_INVALID_PARAMETER, "an invalid callable is rejected")
	expect(not ResourceLoader.has_extension_loader("gdquizbad"), "a rejected loader leaves nothing behind")
	expect(not ResourceSaver.has_extension_saver("gdquizbad"), "a rejected saver leaves nothing behind")


func _test_at_front() -> void:
	var path: String = ROOT + "data.json"
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string("{}")
	file.close()
	ResourceLoader.register_extension_loader("json", _load_quiz, QUIZ_TYPE)
	var behind: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(behind is JSON, "without at_front the built-in JSON loader is asked first")
	ResourceLoader.unregister_extension_loader("json")
	ResourceLoader.register_extension_loader("json", _load_quiz, QUIZ_TYPE, true)
	var front: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(front is QuizSet, "with at_front the new function is asked before the built-in loaders")
	ResourceLoader.unregister_extension_loader("json")


func _test_errors() -> void:
	var path: String = ROOT + "broken.gdquizerr"
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string("x")
	file.close()
	expect_equal(ResourceLoader.register_extension_loader("gdquizerr", _load_corrupt), OK, "register a load function that fails")
	var failed: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(failed == null, "an Error returned by the load function makes load fail")

	var replace_error: Error = ResourceLoader.register_extension_loader("gdquizerr", func(p: String) -> Variant: return _load_quiz(p))
	expect_equal(replace_error, OK, "register the extension again with a lambda")
	var replaced: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(replaced is QuizSet, "the second registration replaces the first")
	var quiz: QuizSet = replaced
	expect_equal(quiz.questions, PackedStringArray(["x"]), "the replaced function loaded the file")
	expect_equal(ResourceLoader.unregister_extension_loader("gdquizerr"), OK, "unregister the replaced extension")


func _test_unregister() -> void:
	var path: String = ROOT + "questions." + EXTENSION
	expect_equal(ResourceLoader.unregister_extension_loader(EXTENSION), OK, "unregister_extension_loader removes the loader")
	expect_equal(ResourceSaver.unregister_extension_saver(EXTENSION), OK, "unregister_extension_saver removes the saver")
	expect(not ResourceLoader.has_extension_loader(EXTENSION), "has_extension_loader is false afterwards")
	expect(not ResourceSaver.has_extension_saver(EXTENSION), "has_extension_saver is false afterwards")
	expect_equal(ResourceLoader.unregister_extension_loader(EXTENSION), ERR_DOES_NOT_EXIST, "unregistering twice returns ERR_DOES_NOT_EXIST")
	var failed: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(failed == null, "loading fails after unregistering")
	var save_error: Error = ResourceSaver.save(_make_quiz(), ROOT + "after." + EXTENSION)
	expect_equal(save_error, ERR_FILE_UNRECOGNIZED, "saving fails after unregistering")


func _cleanup() -> void:
	for file: String in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT + file)
