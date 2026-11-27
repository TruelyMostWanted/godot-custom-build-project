extends FeatureTest
## Full API test of Process Memory (OS.get_process_memory_usage and OS.get_process_memory_info). Same tests as scripts/cs/Test.cs.

const KEYS: Array[String] = ["resident", "private", "peak", "heap"]
const MIB := 1024 * 1024
const BLOCK_MIB := 64


func _run_tests() -> void:
	_test_usage()
	_test_info()
	_test_platforms()
	_test_growth()


func _test_usage() -> void:
	var usage: int = OS.get_process_memory_usage()
	expect(usage > 10 * MIB, "get_process_memory_usage is a plausible byte count (> 10 MiB): %d" % usage)


func _test_info() -> void:
	var info: Dictionary = OS.get_process_memory_info()
	expect_equal(info.size(), KEYS.size(), "get_process_memory_info has exactly %d entries" % KEYS.size())
	for key: String in KEYS:
		expect(info.has(key), "the info has the key '%s'" % key)
		var value: int = info[key]
		expect(value >= -1, "'%s' is -1 or a byte count: %d" % [key, value])
	var resident: int = info["resident"]
	var peak: int = info["peak"]
	expect(resident > 0, "resident memory is known: %d" % resident)
	expect(peak >= resident, "peak is not lower than resident")


# Windows: private is known, heap is not. Linux, Android, macOS, iOS and Web know the heap.
func _test_platforms() -> void:
	var info: Dictionary = OS.get_process_memory_info()
	var platform: String = OS.get_name()
	if platform == "Windows":
		expect(info["private"] > 0, "private memory is known on Windows")
		expect_equal(info["heap"], -1, "heap is -1 on Windows")
	else:
		expect(info["heap"] > 0, "heap is known on %s" % platform)


# Touching 64 MiB must show up in the usage; the Web may reuse free memory, so only "not lower" there.
func _test_growth() -> void:
	var before: int = OS.get_process_memory_usage()
	var block := PackedByteArray()
	block.resize(BLOCK_MIB * MIB)
	block.fill(1)
	var after: int = OS.get_process_memory_usage()
	var minimum_growth: int = BLOCK_MIB * MIB / 2
	if OS.get_name() == "Web":
		minimum_growth = 0
	expect(after >= before + minimum_growth, "usage grows with a 64 MiB block: %d -> %d" % [before, after])
	var info: Dictionary = OS.get_process_memory_info()
	var peak: int = info["peak"]
	expect(peak >= after - 4 * MIB, "peak covers the 64 MiB block")
