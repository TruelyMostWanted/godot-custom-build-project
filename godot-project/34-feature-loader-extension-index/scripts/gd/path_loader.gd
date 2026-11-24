extends ResourceFormatLoader
## Path-based loader for 34-feature-loader-extension-index: overrides _recognize_path() and declares no extension.
## It recognizes every path that ends with path_ending.

var tag := ""
var path_ending := ""
var type_name := "Resource"


func _get_recognized_extensions() -> PackedStringArray:
	return PackedStringArray()


func _recognize_path(path: String, type: StringName) -> bool:
	return path.ends_with(path_ending)


func _handles_type(type: StringName) -> bool:
	return type == &"Resource"


func _get_resource_type(path: String) -> String:
	return type_name if path.ends_with(path_ending) else ""


func _load(path: String, original_path: String, use_sub_threads: bool, cache_mode: int) -> Variant:
	var res := Resource.new()
	res.resource_name = tag
	return res
