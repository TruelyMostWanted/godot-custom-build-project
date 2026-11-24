extends FeatureCheck
## GDScript checks of 35-feature-simple-resource-format. Check the same things as scripts/cs/Issue35FeatureSimpleResourceFormatCheck.cs.

const ROOT := "user://35-feature-simple-resource-format/gd/"
const EXT := "gdquiz"
const QUESTIONS := ["Which engine is this?", "What is 6 * 7?", "Is the Web single-threaded?"]


func _get_check_name() -> String:
	return "35-feature-simple-resource-format"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_check_api()
	_check_save_and_load()
	await _check_threaded_load()
	_check_type_rules()
	_check_errors()
	_check_unregister()
	_cleanup()


static func _load_quiz(path: String) -> Variant:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return FileAccess.get_open_error()
	var quiz := Issue35GdQuizSet.new()
	quiz.questions = file.get_as_text().split("\n", false)
	return quiz


static func _save_quiz(quiz: Issue35GdQuizSet, path: String) -> Error:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string("\n".join(quiz.questions))
	return OK


static func _load_corrupt(_path: String) -> Variant:
	return ERR_FILE_CORRUPT


func _make_quiz() -> Issue35GdQuizSet:
	var quiz := Issue35GdQuizSet.new()
	quiz.questions = PackedStringArray(QUESTIONS)
	return quiz


func _check_api() -> void:
	for method in ["register_extension_loader", "unregister_extension_loader", "has_extension_loader"]:
		expect(ClassDB.class_has_method("ResourceLoader", method), "ResourceLoader.%s() is bound" % method)
	for method in ["register_extension_saver", "unregister_extension_saver", "has_extension_saver"]:
		expect(ClassDB.class_has_method("ResourceSaver", method), "ResourceSaver.%s() is bound" % method)


func _check_save_and_load() -> void:
	expect_equal(ResourceLoader.register_extension_loader(EXT, _load_quiz, &"Issue35GdQuizSet"), OK, "register a load function with one call")
	expect_equal(ResourceSaver.register_extension_saver(EXT, _save_quiz, &"Issue35GdQuizSet"), OK, "register a save function with one call")
	expect(ResourceLoader.has_extension_loader(EXT) and ResourceSaver.has_extension_saver("." + EXT.to_upper()), "has_extension_loader()/has_extension_saver() ignore the dot and the case")

	var path := ROOT + "questions." + EXT
	expect_equal(ResourceSaver.save(_make_quiz(), path), OK, "ResourceSaver.save() writes the file through the save function")
	expect_equal(FileAccess.get_file_as_string(path), "\n".join(QUESTIONS), "the file holds one question per line")
	expect(ResourceLoader.exists(path), "ResourceLoader.exists() finds the file")
	expect_equal(ResourceLoader.get_resource_type(path), "Resource", "get_resource_type() reports the native base of the script class")

	var loaded = load(path)
	expect(loaded is Issue35GdQuizSet, "load() returns an Issue35GdQuizSet")
	if loaded is Issue35GdQuizSet:
		expect_equal(loaded.questions, PackedStringArray(QUESTIONS), "the loaded questions match")
	expect(ResourceLoader.has_cached(path) and load(path) == loaded, "the loaded resource is cached like any other resource")
	var hinted := ResourceLoader.load(path, "Issue35GdQuizSet", ResourceLoader.CACHE_MODE_IGNORE)
	expect(hinted is Issue35GdQuizSet and hinted != loaded, "load with the type hint and CACHE_MODE_IGNORE returns a new resource")


func _check_threaded_load() -> void:
	var path := ROOT + "threaded." + EXT
	ResourceSaver.save(_make_quiz(), path)
	expect_equal(ResourceLoader.load_threaded_request(path), OK, "load_threaded_request() accepts the file")
	var status := ResourceLoader.load_threaded_get_status(path)
	var frames := 0
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and frames < 600:
		await get_tree().process_frame
		frames += 1
		status = ResourceLoader.load_threaded_get_status(path)
	expect_equal(status, ResourceLoader.THREAD_LOAD_LOADED, "the threaded load finishes")
	var loaded = ResourceLoader.load_threaded_get(path)
	expect(loaded is Issue35GdQuizSet and loaded.questions.size() == QUESTIONS.size(), "the threaded load calls the load function")


func _check_type_rules() -> void:
	var extensions := ResourceLoader.get_recognized_extensions_for_type("Issue35GdQuizSet")
	expect(extensions.has(EXT), "get_recognized_extensions_for_type() lists the extension for the script class")
	expect(not ResourceLoader.get_recognized_extensions_for_type("Texture2D").has(EXT), "the extension is not offered for unrelated types")
	expect(ResourceSaver.get_recognized_extensions(_make_quiz()).has(EXT), "ResourceSaver offers the extension for the script class")

	var plain := Resource.new()
	expect_equal(ResourceSaver.save(plain, ROOT + "plain." + EXT), ERR_FILE_UNRECOGNIZED, "resources of another type are not saved with the format")

	expect_equal(ResourceLoader.register_extension_loader("gdquizbad", _load_quiz, &"Node"), ERR_INVALID_PARAMETER, "a type that is not a Resource is rejected")
	expect_equal(ResourceLoader.register_extension_loader("", _load_quiz), ERR_INVALID_PARAMETER, "an empty extension is rejected")
	expect_equal(ResourceSaver.register_extension_saver("gdquizbad", Callable()), ERR_INVALID_PARAMETER, "an invalid callable is rejected")
	expect(not ResourceLoader.has_extension_loader("gdquizbad") and not ResourceSaver.has_extension_saver("gdquizbad"), "rejected registrations leave nothing behind")


func _check_errors() -> void:
	var path := ROOT + "broken.gdquizerr"
	FileAccess.open(path, FileAccess.WRITE).store_string("x")
	expect_equal(ResourceLoader.register_extension_loader("gdquizerr", _load_corrupt), OK, "register a load function that fails")
	expect(ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) == null, "an Error returned by the load function makes load() fail (one error message expected)")

	# Registering again replaces the function.
	expect_equal(ResourceLoader.register_extension_loader("gdquizerr", func(p: String) -> Variant: return _load_quiz(p)), OK, "register the extension again with a lambda")
	var loaded = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(loaded is Issue35GdQuizSet and loaded.questions == PackedStringArray(["x"]), "the second registration replaces the first")
	expect_equal(ResourceLoader.unregister_extension_loader("gdquizerr"), OK, "unregister the replaced extension")


func _check_unregister() -> void:
	var path := ROOT + "questions." + EXT
	expect_equal(ResourceLoader.unregister_extension_loader(EXT), OK, "ResourceLoader.unregister_extension_loader() removes the format")
	expect_equal(ResourceSaver.unregister_extension_saver(EXT), OK, "ResourceSaver.unregister_extension_saver() removes the format")
	expect(not ResourceLoader.has_extension_loader(EXT) and not ResourceSaver.has_extension_saver(EXT), "has_extension_loader()/has_extension_saver() are false afterwards")
	expect_equal(ResourceLoader.unregister_extension_loader(EXT), ERR_DOES_NOT_EXIST, "unregistering twice returns ERR_DOES_NOT_EXIST")
	expect(ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) == null, "loading fails after unregistering (one 'No loader found' error expected)")
	expect_equal(ResourceSaver.save(_make_quiz(), ROOT + "after." + EXT), ERR_FILE_UNRECOGNIZED, "saving fails after unregistering")


func _cleanup() -> void:
	for file in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT + file)
