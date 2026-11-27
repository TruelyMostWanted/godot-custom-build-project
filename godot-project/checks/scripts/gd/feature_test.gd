class_name FeatureTest
extends Node
## Base of every GDScript test (scripts/gd/Test.gd). Override [method _run_tests], call [method expect] for each condition.
## The scene quits with exit code 0 when every expectation passed, else 1.

var _passed: int = 0
var _failed: int = 0


func _ready() -> void:
	var feature: String = get_script().resource_path.get_slice("/", 2)
	print("== %s (GDScript)" % feature)
	await _run_tests()
	print("== %s: %d passed, %d failed" % [feature, _passed, _failed])
	get_tree().quit(0 if _failed == 0 else 1)


## Override: the tests of the feature. May use [code]await[/code].
func _run_tests() -> void:
	pass


func expect(condition: bool, message: String) -> bool:
	if condition:
		_passed += 1
		print("  PASS  %s" % message)
	else:
		_failed += 1
		printerr("  FAIL  %s" % message)
	return condition


func expect_equal(actual: Variant, expected: Variant, message: String) -> bool:
	return expect(actual == expected, "%s (expected %s, got %s)" % [message, str(expected), str(actual)])
