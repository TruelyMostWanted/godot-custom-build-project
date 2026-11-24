extends ResourceFormatLoader
## Loader by extension for 34-feature-loader-extension-index. It loads an empty Resource named after its tag.
## type_name is what get_resource_type() reports, so the test can see which loader answers first.

var tag := ""
var extension := ""
var type_name := "Resource"


func _get_recognized_extensions() -> PackedStringArray:
	return PackedStringArray([extension])


func _handles_type(type: StringName) -> bool:
	return type == &"Resource"


func _get_resource_type(path: String) -> String:
	return type_name if path.get_extension().to_lower() == extension else ""


func _load(path: String, original_path: String, use_sub_threads: bool, cache_mode: int) -> Variant:
	var res := Resource.new()
	res.resource_name = tag
	return res
