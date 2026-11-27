extends ResourceFormatLoader
## Test loader: recognizes one extension and returns a new Resource that records which loader created it.

var extension: String
var id: int


func _init(p_extension: String = "", p_id: int = 0) -> void:
	extension = p_extension
	id = p_id


func _get_recognized_extensions() -> PackedStringArray:
	return PackedStringArray([extension])


func _handles_type(type: StringName) -> bool:
	return type == &"Resource"


func _get_resource_type(path: String) -> String:
	if path.get_extension() == extension:
		return "Resource"
	return ""


func _load(_path: String, _original_path: String, _use_sub_threads: bool, _cache_mode: int) -> Variant:
	var resource := Resource.new()
	resource.set_meta("loader_id", id)
	return resource
