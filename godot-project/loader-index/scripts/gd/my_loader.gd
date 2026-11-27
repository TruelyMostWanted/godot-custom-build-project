extends ResourceFormatLoader
## Loader by extension: overrides _get_recognized_extensions() only, so the engine indexes it.
## It loads an empty Resource named after its tag. type_name is what get_resource_type() reports.
## extensions may change at runtime, then ResourceLoader.rebuild_extension_index() is needed.

var tag: String = ""
var extensions: PackedStringArray = PackedStringArray(["mydata"])
var type_name: String = "Resource"


func _get_recognized_extensions() -> PackedStringArray:
	return extensions


func _handles_type(type: StringName) -> bool:
	return type == &"Resource"


func _get_resource_type(path: String) -> String:
	var lower_path: String = path.to_lower()
	for extension: String in extensions:
		if lower_path.ends_with("." + extension.to_lower()):
			return type_name
	return ""


func _load(_path: String, _original_path: String, _use_sub_threads: bool, _cache_mode: int) -> Variant:
	var resource := Resource.new()
	resource.resource_name = tag
	return resource
