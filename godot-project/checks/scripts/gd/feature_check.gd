class_name FeatureCheck
extends Node
## Base of every GDScript feature check. Override [method _run_checks], call [method expect] for each condition.
## The scene quits with exit code 0 when every expectation passed, else 1.

var _passed: int = 0
var _failed: int = 0


func _ready() -> void:
	print("== %s (GDScript)" % _get_check_name())
	await _run_checks()
	print("== %s: %d passed, %d failed" % [_get_check_name(), _passed, _failed])
	get_tree().quit(0 if _failed == 0 else 1)


## Override: the checks of the feature. May use [code]await[/code].
func _run_checks() -> void:
	pass


## Override: the name printed in the log. Default: the name of the root node.
func _get_check_name() -> String:
	return name


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
