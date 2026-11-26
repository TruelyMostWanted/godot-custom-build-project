extends FeatureCheck
## GDScript checks of 265-task-truely-version. Check the same things as scripts/cs/Issue265TaskTruelyVersionCheck.cs.
##
## The Truely version comes from truely/version.txt of the engine (SemVer, first version 0.1.0).
## The checks do not pin one number, so they stay green when the version is raised.


func _get_check_name() -> String:
	return "265-task-truely-version"


func _run_checks() -> void:
	var info := Engine.get_version_info()

	# The new keys exist and have the right types.
	for key in ["truely_major", "truely_minor", "truely_patch", "truely_hex"]:
		expect(info.has(key), "get_version_info has %s" % key)
		expect(typeof(info.get(key)) == TYPE_INT, "%s is an int" % key)
	expect(info.has("truely_version"), "get_version_info has truely_version")
	expect(typeof(info.get("truely_version")) == TYPE_STRING, "truely_version is a String")

	var major: int = info.get("truely_major", -1)
	var minor: int = info.get("truely_minor", -1)
	var patch: int = info.get("truely_patch", -1)
	var hex: int = info.get("truely_hex", -1)
	var text: String = str(info.get("truely_version", ""))

	# The string always has three numbers and matches the ints.
	expect(text == "%d.%d.%d" % [major, minor, patch], "truely_version is MAJOR.MINOR.PATCH of the ints")
	expect(text.split(".").size() == 3, "truely_version has three numbers")
	expect(hex == ((major << 16) | (minor << 8) | patch), "truely_hex encodes the version like hex")

	# The first release is 0.1.0; the version never goes below it.
	expect(major >= 0 and minor >= 0 and patch >= 0, "version numbers are not negative")
	expect(hex >= 0x000100, "Truely version is 0.1.0 or newer")

	# The Godot base version keys are unchanged.
	expect(info.get("major") == 4, "Godot major version is still 4")
	for key in ["minor", "patch", "hex", "status", "build", "hash", "timestamp", "string"]:
		expect(info.has(key), "Godot key %s is still there" % key)
