extends ResourceFormatLoader
## Loader of .lrquiz files: one question per line, stored in the meta "questions" of a plain Resource.
## It accepts every type hint, so the engine does the type check. An empty file is reported like in the wiki guide.


func _get_recognized_extensions() -> PackedStringArray:
	return PackedStringArray(["lrquiz"])


func _handles_type(_type: StringName) -> bool:
	return true


func _get_resource_type(path: String) -> String:
	if path.get_extension() == "lrquiz":
		return "Resource"
	return ""


func _load(path: String, _original_path: String, _use_sub_threads: bool, _cache_mode: int) -> Variant:
	var text := FileAccess.get_file_as_string(path)
	if text.is_empty():
		push_error("'%s' is empty." % path)
		return ERR_FILE_CORRUPT
	var quiz := Resource.new()
	quiz.set_meta("questions", text.split("\n", false))
	return quiz
