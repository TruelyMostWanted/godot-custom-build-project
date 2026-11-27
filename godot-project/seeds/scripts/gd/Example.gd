extends Node
## Minimal example of Seeds: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Seeds


func _ready() -> void:
	# Give each system its own random stream.
	SeedServer.global_seed = 12345
	var loot: RandomNumberGenerator = SeedServer.get_rng("loot")
	var weather: RandomNumberGenerator = SeedServer.get_rng("weather")
	var item: int = loot.randi_range(0, 99)
	var rain: bool = weather.randf() < 0.3
	print("Loot item: ", item)
	print("Rain: ", rain)

	# Offer a daily challenge.
	var date: String = Time.get_date_string_from_system(true)
	var daily_seed: int = SeedServer.set_seed_from_text("Daily-" + date)
	print("Daily seed: ", daily_seed)

	# Seed a noise generator.
	var terrain: FastNoiseLite = SeedServer.get_noise("terrain")
	terrain.frequency = 0.02
	var caves := FastNoiseLite.new()
	caves.noise_type = FastNoiseLite.TYPE_CELLULAR
	SeedServer.register_noise("caves", caves)
	print("Terrain noise seed: ", terrain.seed)

	# Store the random state in a save game.
	var save_file := FileAccess.open("user://save.dat", FileAccess.WRITE)
	save_file.store_var(SeedServer.get_state())
	save_file.close()
	var load_file := FileAccess.open("user://save.dat", FileAccess.READ)
	var state: Dictionary = load_file.get_var()
	SeedServer.set_state(state)
	print("Restored global seed: ", SeedServer.global_seed)

	# Seed randi() and randf().
	SeedServer.seed_global_functions = true
	SeedServer.global_seed = 4242
	print("randi() after seeding: ", randi())

	SeedServer.seed_global_functions = false
	SeedServer.clear()
	get_tree().quit()
