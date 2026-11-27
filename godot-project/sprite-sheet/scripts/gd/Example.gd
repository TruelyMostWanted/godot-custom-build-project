extends Node
## Minimal example of Sprite Sheet: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Sprite-Sheet
## "Edit regions in the editor" needs the editor and is skipped.

const DIR := "res://sprite-sheet/resources/"


func _ready() -> void:
	# Cut a texture into named regions
	var atlas := SpriteSheet.new()
	atlas.texture = load(DIR + "sheet.png")
	atlas.set_region("coin", Rect2i(0, 0, 16, 16))
	atlas.set_region("sword", Rect2i(16, 0, 24, 48))
	atlas.rename_region("coin", "gold")
	ResourceSaver.save(atlas, "user://icons.atlas.tres")
	var icon := Sprite2D.new()
	icon.texture = atlas.get_region_texture("gold")
	add_child(icon)
	var sword := TextureRect.new()
	sword.texture = atlas.get_region_texture("sword")
	add_child(sword)
	atlas.set_region("gold", Rect2i(0, 16, 16, 16))
	var gold: AtlasTexture = icon.texture
	print("Regions: ", atlas.get_region_names(), ", gold now at ", gold.region)

	# Build animations from regions
	atlas.set_region("walk_0", Rect2i(48, 0, 16, 32))
	atlas.set_region("walk_1", Rect2i(64, 0, 16, 32))
	var hero := AnimatedSprite2D.new()
	add_child(hero)
	hero.sprite_frames = atlas.create_sprite_frames("walk_", 12.0)
	hero.play("walk")
	print("Walk frames: ", hero.sprite_frames.get_frame_count("walk"))

	# Load regions from JSON, CSV or INI
	var loaded := SpriteSheet.new()
	if loaded.load_from_file(DIR + "icons.atlas.json") != OK:
		return
	loaded.save_to_file("user://hero.atlas.csv")
	var ini: String = loaded.to_text(ResourceTextSerializer.FORMAT_INI)
	print("INI export:\n", ini)

	# Import a TexturePacker sheet
	var packed := SpriteSheet.new()
	var err: Error = packed.load_from_file(DIR + "texturepacker_hash.json")
	var coin := Sprite2D.new()
	coin.texture = packed.get_region_texture("items_coin")
	add_child(coin)
	print("TexturePacker import: ", error_string(err), ", regions ", packed.get_region_names())

	# Slice a sheet automatically
	var sliced := SpriteSheet.new()
	sliced.texture = load(DIR + "sheet.png")
	var names: PackedStringArray = sliced.auto_slice("enemy_")
	print(names.size(), " sprites: ", names)
	var image: Image = sliced.texture.get_image()
	var rects: Array[Rect2i] = SpriteSheet.detect_sprite_rects(image, 0.1)
	print("Detected rectangles: ", rects.size())
	var sheet_size := Vector2i(sliced.texture.get_size())
	var cells: Array[Rect2i] = SpriteSheet.make_grid_rects(sheet_size, Vector2i(32, 32), Vector2i(2, 2), Vector2i(1, 1))
	sliced.add_regions(cells, "tile_")
	print("Next free name: ", sliced.make_unique_region_name("tile_"))

	get_tree().quit()
