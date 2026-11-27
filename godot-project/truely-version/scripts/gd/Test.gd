extends FeatureTest
## Full API test of the Truely version: the extra keys of Engine.get_version_info(). Same tests as scripts/cs/Test.cs.
## The version comes from truely/version.txt of the engine (SemVer). The tests do not pin one number, so they stay
## green when the version is raised.

const TRUELY_KEYS := ["truely_major", "truely_minor", "truely_patch", "truely_hex"]
const GODOT_KEYS := ["major", "minor", "patch", "hex", "status", "build", "hash", "timestamp", "string"]
const FIRST_VERSION_HEX := 0x000100

var _info: Dictionary
var _major: int
var _minor: int
var _patch: int
var _hex: int
var _text: String


func _run_tests() -> void:
	_info = Engine.get_version_info()
	_major = _info["truely_major"]
	_minor = _info["truely_minor"]
	_patch = _info["truely_patch"]
	_hex = _info["truely_hex"]
	_text = _info["truely_version"]
	_test_keys_and_types()
	_test_version_string()
	_test_version_hex()
	_test_first_version()
	_test_godot_keys()


func _test_keys_and_types() -> void:
	for key in TRUELY_KEYS:
		expect(_info.has(key), "get_version_info has %s" % key)
		expect_equal(typeof(_info[key]), TYPE_INT, "%s is an int" % key)
	expect(_info.has("truely_version"), "get_version_info has truely_version")
	expect_equal(typeof(_info["truely_version"]), TYPE_STRING, "truely_version is a String")


func _test_version_string() -> void:
	expect_equal(_text, "%d.%d.%d" % [_major, _minor, _patch], "truely_version is MAJOR.MINOR.PATCH of the ints")
	expect_equal(_text.split(".").size(), 3, "truely_version has three numbers")


func _test_version_hex() -> void:
	var encoded: int = (_major << 16) | (_minor << 8) | _patch
	expect_equal(_hex, encoded, "truely_hex has one byte per number")


func _test_first_version() -> void:
	expect(_major >= 0, "the major number is not negative")
	expect(_minor >= 0, "the minor number is not negative")
	expect(_patch >= 0, "the patch number is not negative")
	expect(_hex >= FIRST_VERSION_HEX, "the Truely version is 0.1.0 or newer")


func _test_godot_keys() -> void:
	expect_equal(_info["major"], 4, "the Godot major version is still 4")
	for key in GODOT_KEYS:
		expect(_info.has(key), "the Godot key %s is still there" % key)
