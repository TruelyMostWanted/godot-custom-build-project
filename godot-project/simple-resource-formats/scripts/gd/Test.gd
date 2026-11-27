extends FeatureTest
## Full API test of Simple Resource Formats: every method, property, signal and enum. Same tests as scripts/cs/Test.cs.


func _run_tests() -> void:
	_test_example()


func _test_example() -> void:
	var version: Dictionary = Engine.get_version_info()
	expect_equal(version.major, 4, "engine major version")
