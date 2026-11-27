extends ResourceFormatSaver
## Saves the name of a Resource as a .note file.
## last_flags records the saver flags that ResourceSaver passed to _save().

var last_flags: int = -1


func _recognize(_resource: Resource) -> bool:
	return true


func _get_recognized_extensions(_resource: Resource) -> PackedStringArray:
	return PackedStringArray(["note"])


func _save(resource: Resource, path: String, flags: int) -> Error:
	last_flags = flags
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string(resource.resource_name)
	return OK
