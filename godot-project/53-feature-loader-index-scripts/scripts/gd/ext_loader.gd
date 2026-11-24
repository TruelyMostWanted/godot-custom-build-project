extends ResourceFormatLoader
## Loader by extension for 53-feature-loader-index-scripts: overrides _get_recognized_extensions() only, so the engine indexes it.
## It loads an empty Resource named after its tag; type_name is what get_resource_type() reports.
## extensions may change at runtime; the check then calls ResourceLoader.rebuild_extension_index().

var tag := ""
var extensions := PackedStringArray()
var type_name := "Resource"


func _get_recognized_extensions() -> PackedStringArray:
	return extensions


func _handles_type(type: StringName) -> bool:
	return type == &"Resource"


func _get_resource_type(path: String) -> String:
	for extension in extensions:
		if path.to_lower().ends_with("." + extension.to_lower()):
			return type_name
	return ""


func _load(path: String, original_path: String, use_sub_threads: bool, cache_mode: int) -> Variant:
	var res := Resource.new()
	res.resource_name = tag
	return res
