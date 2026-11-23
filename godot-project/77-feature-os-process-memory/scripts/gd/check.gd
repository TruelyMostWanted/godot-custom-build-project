extends FeatureCheck
## GDScript checks of 77-feature-os-process-memory. Check the same things as scripts/cs/Issue77FeatureOsProcessMemoryCheck.cs.

const KEYS: Array[String] = ["resident", "private", "peak", "heap"]
const MIB := 1024 * 1024


func _get_check_name() -> String:
	return "77-feature-os-process-memory"


func _run_checks() -> void:
	_check_bindings()
	_check_usage()
	_check_info()
	_check_growth()
	_check_cost()


func _check_bindings() -> void:
	expect(ClassDB.class_has_method("OS", "get_process_memory_usage"), "OS.get_process_memory_usage() is bound")
	expect(ClassDB.class_has_method("OS", "get_process_memory_info"), "OS.get_process_memory_info() is bound")


# Known on Windows, Linux, macOS, Android, iOS and Web (BSD only with /proc).
func _check_usage() -> void:
	var usage := OS.get_process_memory_usage()
	print("  process memory usage: %s (%d bytes)" % [String.humanize_size(usage), usage])
	expect(usage > 0, "process memory usage is known (%d bytes)" % usage)
	# A running Godot project with a window needs more than 10 MiB.
	expect(usage > 10 * MIB, "process memory usage is plausible (> 10 MiB)")


func _check_info() -> void:
	var info := OS.get_process_memory_info()
	print("  process memory info: %s" % info)
	expect_equal(info.size(), KEYS.size(), "info has exactly %d entries" % KEYS.size())
	for key in KEYS:
		if not expect(info.has(key), "info has the key '%s'" % key):
			continue
		expect(info[key] is int, "'%s' is an int" % key)
		expect(info[key] >= -1, "'%s' is -1 or a byte count (%d)" % [key, info[key]])
	var resident: int = info.get("resident", -1)
	var peak: int = info.get("peak", -1)
	expect(resident > 0, "resident memory is known (%d bytes)" % resident)
	if peak != -1:
		expect(peak >= resident, "peak (%d) is not lower than resident (%d)" % [peak, resident])
	if OS.get_name() == "Windows":
		expect(info["private"] > 0, "private memory is known on Windows")
		expect_equal(info["heap"], -1, "heap is -1 on Windows")
	if OS.get_name() in ["Linux", "Android", "macOS", "iOS", "Web"]:
		expect(info["heap"] > 0, "heap is known on %s" % OS.get_name())


# Touching 64 MiB must show up in the usage (the Web may reuse free wasm memory, so only "not lower" there).
func _check_growth() -> void:
	var before := OS.get_process_memory_usage()
	var block := PackedByteArray()
	block.resize(64 * MIB)
	block.fill(1)
	var after := OS.get_process_memory_usage()
	print("  usage before: %s, with 64 MiB block: %s" % [String.humanize_size(before), String.humanize_size(after)])
	if OS.get_name() == "Web":
		expect(after >= before, "usage does not drop while a 64 MiB block is alive")
	else:
		expect(after >= before + 32 * MIB, "usage grows by at least 32 MiB with a 64 MiB block (%d -> %d)" % [before, after])
	var peak: int = OS.get_process_memory_info()["peak"]
	if peak != -1:
		expect(peak >= after - 4 * MIB, "peak covers the 64 MiB block")
	block.clear()


# Cheap enough for every few frames: target < 10 µs per call; checked with a generous margin.
func _check_cost() -> void:
	const CALLS := 1000
	var start := Time.get_ticks_usec()
	var sum := 0
	for i in CALLS:
		sum += OS.get_process_memory_usage()
	var usage_usec := float(Time.get_ticks_usec() - start) / CALLS
	start = Time.get_ticks_usec()
	for i in 100:
		OS.get_process_memory_info()
	var info_usec := float(Time.get_ticks_usec() - start) / 100
	print("  cost: get_process_memory_usage() %.2f µs, get_process_memory_info() %.2f µs per call" % [usage_usec, info_usec])
	expect(sum > 0, "repeated calls return values")
	expect(usage_usec < 50.0, "get_process_memory_usage() takes < 50 µs per call (%.2f µs)" % usage_usec)
