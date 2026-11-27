extends FeatureTest
## Full API test of Seeds (SeedServer): every method, property and signal. Same tests as scripts/cs/Test.cs.

# Golden values, computed outside the engine (FNV-1a 64, SplitMix64, PCG32). The same on every platform.
const DAILY_TEXT := "Daily-2026-11-23"
const DAILY_SEED := 1486713988171126959
const LOOT_SEED := -5897437094724455890
const LOOT_FIRST_RANDI := 1463604985
const TERRAIN_NOISE_SEED := 1699205639

var _changed_seeds: Array[int] = []


func _run_tests() -> void:
	SeedServer.clear()
	_test_seed_from_text()
	_test_seed_changed_signal()
	_test_streams()
	_test_stream_names()
	_test_state()
	_test_noise()
	_test_seed_global_functions()
	_test_randomize_seed()
	_test_clear()


func _test_seed_from_text() -> void:
	var seed_only: int = SeedServer.get_seed_from_text(DAILY_TEXT)
	expect_equal(seed_only, DAILY_SEED, "get_seed_from_text returns the golden value")
	var seed_set: int = SeedServer.set_seed_from_text(DAILY_TEXT)
	expect_equal(seed_set, DAILY_SEED, "set_seed_from_text returns the seed")
	expect_equal(SeedServer.global_seed, DAILY_SEED, "set_seed_from_text sets global_seed")


func _test_seed_changed_signal() -> void:
	SeedServer.seed_changed.connect(_on_seed_changed)
	SeedServer.global_seed = 12345
	SeedServer.seed_changed.disconnect(_on_seed_changed)
	expect_equal(_changed_seeds, [12345], "seed_changed is emitted with the new seed")


func _test_streams() -> void:
	SeedServer.global_seed = 12345
	expect_equal(SeedServer.get_derived_seed("loot"), LOOT_SEED, "get_derived_seed returns the golden value")
	var loot: RandomNumberGenerator = SeedServer.get_rng("loot")
	expect_equal(loot.seed, LOOT_SEED, "get_rng: the stream seed is the derived seed")
	expect_equal(loot.randi(), LOOT_FIRST_RANDI, "get_rng: first randi() is the golden value")
	expect(SeedServer.get_rng("loot") == loot, "get_rng returns the same stream again")
	SeedServer.global_seed = 12345
	expect_equal(loot.randi(), LOOT_FIRST_RANDI, "setting global_seed restarts the stream")
	var weather: RandomNumberGenerator = SeedServer.get_rng("weather")
	expect(weather.seed != loot.seed, "two names give two different seeds")
	SeedServer.global_seed = 12346
	expect(loot.seed != LOOT_SEED, "another global seed gives another derived seed")


func _test_stream_names() -> void:
	expect(SeedServer.has_rng("loot"), "has_rng finds the loot stream")
	expect(SeedServer.has_rng("weather"), "has_rng finds the weather stream")
	expect_equal(Array(SeedServer.get_rng_names()), ["loot", "weather"], "get_rng_names is sorted")
	SeedServer.remove_rng("weather")
	expect(not SeedServer.has_rng("weather"), "remove_rng removes a stream")


func _test_state() -> void:
	SeedServer.global_seed = 777
	var loot: RandomNumberGenerator = SeedServer.get_rng("loot")
	loot.randi()
	var state: Dictionary = SeedServer.get_state()
	var expected: int = loot.randi()
	SeedServer.global_seed = 1
	SeedServer.set_state(state)
	expect_equal(SeedServer.global_seed, 777, "set_state restores global_seed")
	expect_equal(loot.randi(), expected, "set_state restores the stream position")
	SeedServer.remove_rng("loot")
	SeedServer.set_state(state)
	var recreated: RandomNumberGenerator = SeedServer.get_rng("loot")
	expect_equal(recreated.randi(), expected, "set_state recreates a removed stream")


func _test_noise() -> void:
	SeedServer.global_seed = 12345
	var terrain: FastNoiseLite = SeedServer.get_noise("terrain")
	expect(terrain != null, "get_noise creates a FastNoiseLite")
	expect_equal(terrain.seed, TERRAIN_NOISE_SEED, "get_noise: the noise seed is the golden value")
	expect(SeedServer.get_noise("terrain") == terrain, "get_noise returns the same noise again")
	var caves := FastNoiseLite.new()
	SeedServer.register_noise("caves", caves)
	var caves_seed: int = SeedServer.get_derived_seed("caves") >> 32
	expect_equal(caves.seed, caves_seed, "register_noise sets the derived seed")
	expect(SeedServer.has_noise("caves"), "has_noise finds a registered noise")
	expect_equal(Array(SeedServer.get_noise_names()), ["caves", "terrain"], "get_noise_names is sorted")
	SeedServer.global_seed = 99
	expect(caves.seed != caves_seed, "a new global seed reseeds registered noise")
	SeedServer.unregister_noise("caves")
	expect(not SeedServer.has_noise("caves"), "unregister_noise removes a noise")


func _test_seed_global_functions() -> void:
	SeedServer.seed_global_functions = true
	SeedServer.global_seed = 4242
	var first: int = randi()
	SeedServer.global_seed = 4242
	expect_equal(randi(), first, "seed_global_functions seeds randi()")
	SeedServer.seed_global_functions = false


func _test_randomize_seed() -> void:
	var new_seed: int = SeedServer.randomize_seed()
	expect_equal(SeedServer.global_seed, new_seed, "randomize_seed sets and returns the global seed")


func _test_clear() -> void:
	SeedServer.clear()
	expect(SeedServer.get_rng_names().is_empty(), "clear removes all streams")
	expect(SeedServer.get_noise_names().is_empty(), "clear removes all noise")


func _on_seed_changed(global_seed: int) -> void:
	_changed_seeds.append(global_seed)
