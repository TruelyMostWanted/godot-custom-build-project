extends FeatureCheck
## GDScript checks of 11-feature-seed-server. Check the same things as scripts/cs/Issue11FeatureSeedServerCheck.cs.

# Golden values, computed outside the engine (FNV-1a 64 + SplitMix64 + PCG32).
# They are the same on every platform and in every language.
const DAILY_TEXT := "Daily-2026-11-23"
const DAILY_SEED := 1486713988171126959
const LOOT_SEED := -5897437094724455890
const LOOT_FIRST := [1463604985, 632838076, 1232541005, 3482282562, 3706291731]
const TERRAIN_NOISE_SEED := 1699205639

var _signal_seeds: Array[int] = []


func _get_check_name() -> String:
	return "11-feature-seed-server"


func _draw(rng: RandomNumberGenerator, count: int) -> Array[int]:
	var values: Array[int] = []
	for i in count:
		values.append(rng.randi())
	return values


func _on_seed_changed(global_seed: int) -> void:
	_signal_seeds.append(global_seed)


func _run_checks() -> void:
	expect(Engine.has_singleton("SeedServer"), "SeedServer is a registered singleton")
	SeedServer.clear()

	# Seed from text: stable value.
	expect(SeedServer.text_to_seed(DAILY_TEXT) == DAILY_SEED, "text_to_seed gives the golden value")
	expect(SeedServer.set_seed_from_text(DAILY_TEXT) == DAILY_SEED, "set_seed_from_text returns the seed")
	expect(SeedServer.global_seed == DAILY_SEED, "set_seed_from_text sets global_seed")

	# Signal.
	SeedServer.seed_changed.connect(_on_seed_changed)
	SeedServer.global_seed = 12345
	expect(_signal_seeds == [12345], "seed_changed is emitted with the new seed")
	SeedServer.seed_changed.disconnect(_on_seed_changed)

	# Derived stream: golden values.
	expect(SeedServer.get_derived_seed("loot") == LOOT_SEED, "derived seed of 'loot' is the golden value")
	var loot := SeedServer.get_rng("loot")
	expect(loot != null, "get_rng creates a stream")
	expect(loot.seed == LOOT_SEED, "stream seed is the derived seed")
	expect(_draw(loot, 5) == LOOT_FIRST, "first five randi() of 'loot' are the golden values")
	expect(SeedServer.get_rng("loot") == loot, "get_rng returns the same stream again")

	# Same seed, same numbers.
	SeedServer.global_seed = 12345
	expect(_draw(loot, 5) == LOOT_FIRST, "setting the seed again restarts the stream")

	# Two names give different numbers.
	var other := SeedServer.get_rng("weather")
	SeedServer.global_seed = 12345
	expect(_draw(SeedServer.get_rng("loot"), 10) != _draw(other, 10), "two names give different numbers")

	# Different global seeds give different numbers.
	SeedServer.global_seed = 12345
	var a := _draw(loot, 10)
	SeedServer.global_seed = 12346
	expect(_draw(loot, 10) != a, "another global seed gives other numbers")

	# Bookkeeping.
	expect(SeedServer.has_rng("loot") and SeedServer.has_rng("weather"), "has_rng knows both streams")
	expect(Array(SeedServer.get_rng_names()) == ["loot", "weather"], "get_rng_names is sorted")
	SeedServer.remove_rng("weather")
	expect(not SeedServer.has_rng("weather"), "remove_rng removes a stream")

	# Save and restore.
	SeedServer.global_seed = 777
	_draw(loot, 13)
	var state := SeedServer.get_state()
	var first := _draw(loot, 100)
	SeedServer.global_seed = 1
	_draw(loot, 7)
	SeedServer.set_state(state)
	expect(SeedServer.global_seed == 777, "set_state restores global_seed")
	expect(_draw(loot, 100) == first, "get_state, draw 100, set_state: the same 100 numbers")
	# A stream that does not exist yet is created by set_state.
	SeedServer.remove_rng("loot")
	SeedServer.set_state(state)
	expect(_draw(SeedServer.get_rng("loot"), 100) == first, "set_state recreates a removed stream")

	# Noise.
	SeedServer.global_seed = 12345
	var noise = SeedServer.get_noise("terrain")
	expect(noise is FastNoiseLite, "get_noise creates a FastNoiseLite")
	expect(noise.seed == TERRAIN_NOISE_SEED, "noise seed is the golden derived value")
	expect(SeedServer.get_noise("terrain") == noise, "get_noise returns the same noise again")
	var custom := FastNoiseLite.new()
	SeedServer.register_noise("caves", custom)
	expect(custom.seed == (SeedServer.get_derived_seed("caves") >> 32), "register_noise sets the derived seed")
	var before: int = custom.seed
	SeedServer.global_seed = 99
	expect(custom.seed != before, "changing the global seed reseeds registered noise")
	expect(Array(SeedServer.get_noise_names()) == ["caves", "terrain"], "get_noise_names is sorted")
	SeedServer.remove_noise("caves")
	expect(not SeedServer.has_noise("caves"), "remove_noise removes a noise")

	# Global functions.
	SeedServer.seed_global_functions = true
	SeedServer.global_seed = 4242
	var g1 := randi()
	SeedServer.global_seed = 4242
	expect(randi() == g1, "seed_global_functions seeds randi()")
	SeedServer.seed_global_functions = false

	# randomize_seed.
	var r := SeedServer.randomize_seed()
	expect(SeedServer.global_seed == r, "randomize_seed sets and returns the seed")

	SeedServer.clear()
	expect(SeedServer.get_rng_names().is_empty() and SeedServer.get_noise_names().is_empty(), "clear removes streams and noise")
