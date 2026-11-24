extends FeatureCheck
## GDScript checks of 70-feature-load-result. Check the same things as scripts/cs/Issue70FeatureLoadResultCheck.cs.

const ROOT := "user://70-feature-load-result/gd/"
const EXT := "gdlr"
const EXT_BROKEN := "gdlrbroken"
const EXT_IMAGE := "gdlrimage"
const BROKEN_MESSAGE := "Broken gdlr file"


func _get_check_name() -> String:
	return "70-feature-load-result"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_check_api()
	_register_formats()
	_check_missing_file()
	_check_broken_file()
	_check_success()
	_check_type_mismatch()
	_check_save()
	await _check_threaded_load()
	_cleanup()


# A file of the test format holds one line of text, stored as the "text" meta of a plain Resource.
static func _load_text(path: String) -> Variant:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return FileAccess.get_open_error()
	var resource := Resource.new()
	resource.set_meta("text", file.get_as_text())
	return resource


static func _save_text(resource: Resource, path: String) -> Error:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return ERR_CANT_CREATE
	file.store_string(resource.get_meta("text", ""))
	return OK


# Like a loader that finds a broken file: prints the reason, then returns the code.
static func _load_broken(path: String) -> Variant:
	push_error("%s: '%s'." % [BROKEN_MESSAGE, path])
	return ERR_FILE_CORRUPT


func _make_resource(text: String) -> Resource:
	var resource := Resource.new()
	resource.set_meta("text", text)
	return resource


func _write(path: String, text: String) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file:
		file.store_string(text)


func _check_api() -> void:
	expect(ClassDB.class_exists("ResourceLoadResult"), "ResourceLoadResult exists")
	expect(ClassDB.class_exists("ResourceSaveResult"), "ResourceSaveResult exists")
	expect(ClassDB.class_has_method("ResourceLoader", "load_result"), "ResourceLoader.load_result() is bound")
	expect(ClassDB.class_has_method("ResourceLoader", "load_threaded_get_result"), "ResourceLoader.load_threaded_get_result() is bound")
	expect(ClassDB.class_has_method("ResourceSaver", "save_result"), "ResourceSaver.save_result() is bound")


func _register_formats() -> void:
	expect_equal(ResourceLoader.register_extension_loader(EXT, _load_text), OK, "register the text format loader")
	expect_equal(ResourceSaver.register_extension_saver(EXT, _save_text), OK, "register the text format saver")
	expect_equal(ResourceLoader.register_extension_loader(EXT_BROKEN, _load_broken), OK, "register the broken format loader")
	expect_equal(ResourceLoader.register_extension_loader(EXT_IMAGE, _load_text, &"Image"), OK, "register a format that claims to load Images")


func _check_missing_file() -> void:
	var path := ROOT + "missing." + EXT
	DirAccess.remove_absolute(path)
	var result := ResourceLoader.load_result(path)
	expect(result != null and not result.ok, "load_result() of a missing file is not ok")
	expect_equal(result.error, ERR_FILE_NOT_FOUND, "a missing file gives ERR_FILE_NOT_FOUND")
	expect(result.error_message.contains("missing." + EXT), "the message names the path: %s" % result.error_message)
	expect(result.resource == null, "a failed load has no resource")


func _check_broken_file() -> void:
	var path := ROOT + "broken." + EXT_BROKEN
	_write(path, "broken")
	var result := ResourceLoader.load_result(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(not result.ok, "load_result() of a broken file is not ok")
	expect_equal(result.error, ERR_FILE_CORRUPT, "a broken file gives the loader's code")
	expect(result.error_message.contains(BROKEN_MESSAGE), "the message is the loader's message: %s" % result.error_message)


func _check_success() -> void:
	var path := ROOT + "ok." + EXT
	var saved := ResourceSaver.save_result(_make_resource("Hello"), path)
	expect(saved.ok and saved.error == OK and saved.error_message.is_empty(), "save_result() of a writable path is ok")
	expect_equal(saved.path, path, "save_result() reports the path")
	var result := ResourceLoader.load_result(path)
	expect(result.ok and result.error == OK and result.error_message.is_empty(), "load_result() of a valid file is ok")
	expect(result.resource != null and result.resource.get_meta("text", "") == "Hello", "the result holds the loaded resource")
	expect(result.resource == load(path), "the resource is the cached instance that load() returns")
	expect_equal(result.path, path, "load_result() reports the path")


func _check_type_mismatch() -> void:
	var path := ROOT + "plain." + EXT_IMAGE
	_write(path, "not an image")
	var result := ResourceLoader.load_result(path, "Image", ResourceLoader.CACHE_MODE_IGNORE)
	expect(not result.ok, "a resource of another type than the type hint is not ok")
	expect_equal(result.error, ERR_INVALID_DATA, "a type mismatch gives ERR_INVALID_DATA")
	expect(result.error_message.contains("expected Image, got Resource"), "the message names both types: %s" % result.error_message)


func _check_save() -> void:
	var path := ROOT + "no_such_folder/settings." + EXT
	var result := ResourceSaver.save_result(_make_resource("Hello"), path)
	expect(not result.ok, "save_result() to a path that cannot be written is not ok")
	expect_equal(result.error, ERR_CANT_CREATE, "the error is the saver's code")
	expect(result.error_message.contains("Can't save"), "the message is the saver's message: %s" % result.error_message)
	var null_result := ResourceSaver.save_result(null, ROOT + "null." + EXT)
	expect_equal(null_result.error, ERR_INVALID_PARAMETER, "saving null gives ERR_INVALID_PARAMETER")


func _check_threaded_load() -> void:
	var path := ROOT + "threaded." + EXT_BROKEN
	_write(path, "broken")
	expect_equal(ResourceLoader.load_threaded_request(path, "", false, ResourceLoader.CACHE_MODE_IGNORE), OK, "load_threaded_request() accepts the broken file")
	var status := ResourceLoader.load_threaded_get_status(path)
	var frames := 0
	while status == ResourceLoader.THREAD_LOAD_IN_PROGRESS and frames < 600:
		await get_tree().process_frame
		frames += 1
		status = ResourceLoader.load_threaded_get_status(path)
	expect_equal(status, ResourceLoader.THREAD_LOAD_FAILED, "the threaded load fails")
	var result := ResourceLoader.load_threaded_get_result(path)
	expect(not result.ok, "load_threaded_get_result() of a broken file is not ok")
	expect_equal(result.error, ERR_FILE_CORRUPT, "the threaded load gives the loader's code")
	expect(result.error_message.contains(BROKEN_MESSAGE), "the message printed on the worker thread is captured: %s" % result.error_message)
	var again := ResourceLoader.load_threaded_get_result(path)
	expect_equal(again.error, ERR_INVALID_PARAMETER, "a result that was already collected gives ERR_INVALID_PARAMETER")


func _cleanup() -> void:
	ResourceLoader.unregister_extension_loader(EXT)
	ResourceSaver.unregister_extension_saver(EXT)
	ResourceLoader.unregister_extension_loader(EXT_BROKEN)
	ResourceLoader.unregister_extension_loader(EXT_IMAGE)
	for file in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT + file)
