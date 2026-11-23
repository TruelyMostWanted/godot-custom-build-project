extends FeatureCheck
## GDScript checks of 96-feature-font-format-loaders. Check the same things as scripts/cs/Issue96FeatureFontFormatLoadersCheck.cs.


func _get_check_name() -> String:
	return "96-feature-font-format-loaders"


func _run_checks() -> void:
	# Example: replace with checks of the feature's API.
	expect(Engine.get_version_info().major == 4, "engine major version is 4")
	expect(ClassDB.class_exists("Node"), "ClassDB knows Node")
