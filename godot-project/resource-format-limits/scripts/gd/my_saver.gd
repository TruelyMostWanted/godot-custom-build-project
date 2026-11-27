extends ResourceFormatSaver
## Test saver: recognizes one extension and counts its calls instead of writing a file.

var extension: String
var saved: int = 0


func _init(p_extension: String = "") -> void:
	extension = p_extension


func _recognize(_resource: Resource) -> bool:
	return true


func _get_recognized_extensions(_resource: Resource) -> PackedStringArray:
	return PackedStringArray([extension])


func _recognize_path(_resource: Resource, path: String) -> bool:
	return path.get_extension() == extension


func _save(_resource: Resource, _path: String, _flags: int) -> Error:
	saved += 1
	return OK
