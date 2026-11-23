extends FeatureCheck
## GDScript checks of __FEATURE_NAME__. Check the same things as scripts/cs/__FEATURE_PASCAL__Check.cs.


func _get_check_name() -> String:
	return "__FEATURE_NAME__"


func _run_checks() -> void:
	# Example: replace with checks of the feature's API.
	expect(Engine.get_version_info().major == 4, "engine major version is 4")
	expect(ClassDB.class_exists("Node"), "ClassDB knows Node")
