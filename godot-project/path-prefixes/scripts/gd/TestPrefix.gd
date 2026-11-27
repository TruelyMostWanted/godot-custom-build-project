extends PathPrefix
## A prefix for tests: test://a is user://pp_test/a. Overrides all four virtual methods.

var available: bool = true
var read_only: bool = false


func _globalize(path: String) -> String:
	return ProjectSettings.globalize_path("user://pp_test/" + path.trim_prefix("test://"))


func _localize(global_path: String) -> String:
	var root: String = ProjectSettings.globalize_path("user://pp_test/")
	if global_path.begins_with(root):
		return "test://" + global_path.trim_prefix(root)
	return ""


func _is_available() -> bool:
	return available


func _is_read_only() -> bool:
	return read_only
