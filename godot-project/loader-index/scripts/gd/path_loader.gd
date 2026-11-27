extends ResourceFormatLoader
## Path-based loader: overrides _recognize_path(), so the engine asks it for every path.
## It recognizes every path that ends with path_ending.

var tag: String = ""
var path_ending: String = ""
var type_name: String = "Resource"


func _get_recognized_extensions() -> PackedStringArray:
	return PackedStringArray()


func _recognize_path(path: String, _type: StringName) -> bool:
	return path.ends_with(path_ending)


func _handles_type(type: StringName) -> bool:
	return type == &"Resource"


func _get_resource_type(path: String) -> String:
	if path.ends_with(path_ending):
		return type_name
	return ""


func _load(_path: String, _original_path: String, _use_sub_threads: bool, _cache_mode: int) -> Variant:
	var resource := Resource.new()
	resource.resource_name = tag
	return resource
