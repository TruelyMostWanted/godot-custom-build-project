extends ResourceFormatLoader
## Loads a .note file as a Resource whose name is the text of the file.
## last_cache_mode records the cache mode that ResourceLoader passed to _load().

var last_cache_mode: int = -1


func _get_recognized_extensions() -> PackedStringArray:
	return PackedStringArray(["note"])


func _handles_type(type: StringName) -> bool:
	return type == &"Resource"


func _get_resource_type(path: String) -> String:
	if path.get_extension() == "note":
		return "Resource"
	return ""


func _load(path: String, _original_path: String, _use_sub_threads: bool, cache_mode: int) -> Variant:
	last_cache_mode = cache_mode
	var text := FileAccess.get_file_as_string(path)
	if text.is_empty():
		return FileAccess.get_open_error()
	var note := Resource.new()
	note.resource_name = text
	return note
