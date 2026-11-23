extends FeatureCheck
## GDScript checks of 50-feature-texture-atlas. Check the same things as scripts/cs/Issue50FeatureTextureAtlasCheck.cs.

const DIR := "res://50-feature-texture-atlas/resources/"
const SHEET_PATH := DIR + "sheet.png"
const USER_DIR := "user://atlas50_gd/"
const PREFIX := "atlas50gd://"


func _get_check_name() -> String:
	return "50-feature-texture-atlas"


func _same_regions(a: TextureAtlas, b: TextureAtlas) -> bool:
	if a.get_region_names() != b.get_region_names():
		return false
	for region_name in a.get_region_names():
		if a.get_region(region_name) != b.get_region(region_name) or a.get_region_margin(region_name) != b.get_region_margin(region_name):
			return false
	return true


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(USER_DIR)
	expect(ClassDB.class_exists("TextureAtlas"), "TextureAtlas is a registered class")

	# Imported PNG (CompressedTexture2D) and the own JSON layout.
	var sheet: Texture2D = load(SHEET_PATH)
	expect(sheet is CompressedTexture2D, "the imported PNG is a CompressedTexture2D")
	var atlas := TextureAtlas.new()
	expect(atlas.load_from_file(DIR + "icons.atlas.json") == OK, "load_from_file reads the JSON layout")
	expect(atlas.texture == sheet, "TexturePath loads the imported PNG")
	expect(atlas.get_region_names() == PackedStringArray(["coin", "sword", "walk_0", "walk_1", "walk_2", "walk_10"]), "regions keep the file order")
	expect(atlas.get_region("sword") == Rect2i(16, 0, 24, 48), "region rectangle from JSON")

	# AtlasTextures reference the atlas texture and stay in sync.
	var coin := atlas.get_texture("coin")
	expect(coin is AtlasTexture and coin.atlas == sheet, "get_texture returns an AtlasTexture of the atlas texture")
	expect(atlas.get_texture("coin") == coin, "get_texture returns the same texture again")
	atlas.set_region("coin", Rect2i(0, 0, 8, 8))
	expect(coin.region == Rect2(0, 0, 8, 8), "changing a region updates the handed-out texture")
	atlas.set_region("coin", Rect2i(0, 0, 16, 16))
	expect(atlas.set_region("outside", Rect2i(120, 60, 16, 16)) == ERR_INVALID_PARAMETER, "a region outside the texture is rejected")
	expect(not atlas.has_region("outside"), "the rejected region is not added")

	# Drawing in Sprite2D and TextureRect: regions of different sizes.
	var sprite := Sprite2D.new()
	sprite.texture = atlas.get_texture("sword")
	add_child(sprite)
	var rect := TextureRect.new()
	rect.texture = atlas.get_texture("coin")
	add_child(rect)
	await get_tree().process_frame
	expect(sprite.get_rect().size == Vector2(24, 48), "Sprite2D draws the 24x48 region")
	expect(rect.get_combined_minimum_size() == Vector2(16, 16), "TextureRect uses the 16x16 region")
	var image := atlas.get_texture("coin").get_image()
	if image != null:
		expect(image.get_size() == Vector2i(16, 16) and image.get_pixel(0, 0).is_equal_approx(Color(1, 1, 0)), "the coin region shows the yellow pixels")
	else:
		print("  INFO  get_image() of the imported texture is null with this renderer; pixel check skipped")

	# Changing the texture updates every handed-out texture.
	var image_texture := ImageTexture.create_from_image(sheet.get_image() if sheet.get_image() != null else Image.create_empty(128, 64, false, Image.FORMAT_RGBA8))
	atlas.texture = image_texture
	expect(coin.atlas == image_texture and sprite.texture.atlas == image_texture, "changing texture updates the handed-out textures")
	expect(atlas.texture_path == "", "setting texture from code clears texture_path")

	# GradientTexture2D as texture.
	var gradient := GradientTexture2D.new()
	gradient.width = 64
	gradient.height = 64
	var gradient_atlas := TextureAtlas.new()
	gradient_atlas.texture = gradient
	expect(gradient_atlas.set_region("half", Rect2i(0, 0, 32, 64)) == OK, "GradientTexture2D works as texture")
	expect(gradient_atlas.get_texture("half").atlas == gradient, "the region references the GradientTexture2D")

	# Without texture_path, the uid:// path of the texture is written and read back.
	var uid_atlas := TextureAtlas.new()
	uid_atlas.texture = sheet
	uid_atlas.set_region("a", Rect2i(0, 0, 1, 1))
	var uid_json := uid_atlas.save_as_json()
	expect(uid_json.contains("\"TexturePath\": \"uid://"), "the uid:// path of the texture is written")
	var uid_copy := TextureAtlas.new()
	expect(uid_copy.load_from_json(uid_json) == OK and uid_copy.texture == sheet, "a uid:// TexturePath is read")

	# create_sprite_frames and AnimatedSprite2D.
	atlas.texture = sheet
	atlas.texture_path = SHEET_PATH # Written instead of the uid:// path of the imported PNG.
	var frames := atlas.create_sprite_frames("walk_", 30.0)
	expect(frames.get_animation_names() == PackedStringArray(["walk"]), "one animation per prefix")
	expect(frames.get_frame_count("walk") == 4, "four walk frames")
	expect(frames.get_frame_texture("walk", 2) == atlas.get_texture("walk_2") and frames.get_frame_texture("walk", 3) == atlas.get_texture("walk_10"), "frames in numeric order")
	var animated := AnimatedSprite2D.new()
	animated.sprite_frames = frames
	add_child(animated)
	animated.play("walk")
	await get_tree().create_timer(0.2).timeout
	expect(animated.is_playing() and animated.animation == &"walk", "AnimatedSprite2D plays the animation")
	expect(animated.frame > 0, "the animation advances (frame %d)" % animated.frame)

	# JSON round-trip, order kept, with a texture path behind a path prefix (#3).
	ResourceSaver.save(ImageTexture.create_from_image(Image.create_empty(128, 64, false, Image.FORMAT_RGBA8)), USER_DIR + "sheet.res")
	expect(PathPrefixServer.register_prefix_folder(PREFIX, USER_DIR) == OK, "register a path prefix for the texture")
	var json := atlas.save_as_json().replace(SHEET_PATH, PREFIX + "sheet.res")
	var prefixed := TextureAtlas.new()
	expect(prefixed.load_from_json(json) == OK, "load_from_json with a prefixed TexturePath")
	expect(prefixed.texture != null and prefixed.texture.get_size() == Vector2(128, 64), "the texture is loaded through the prefix")
	expect(prefixed.texture_path == PREFIX + "sheet.res", "texture_path keeps the prefixed path")
	expect(_same_regions(atlas, prefixed), "JSON keeps every region and the order")
	expect(prefixed.save_as_json() == json, "JSON round-trips without loss")
	PathPrefixServer.unregister_prefix(PREFIX)

	# CSV and INI round-trip through files, .tres through ResourceSaver.
	atlas.set_region_margin("sword", Rect2i(2, 2, 4, 4))
	for extension in ["json", "csv", "ini"]:
		var path := USER_DIR + "icons." + extension
		expect(atlas.save_to_file(path) == OK, "save_to_file(.%s)" % extension)
		var copy := TextureAtlas.new()
		expect(copy.load_from_file(path) == OK and _same_regions(atlas, copy) and copy.texture == sheet, "%s round-trips without loss" % extension.to_upper())
	expect(ResourceSaver.save(atlas, USER_DIR + "icons.atlas.tres") == OK, "ResourceSaver saves a .tres")
	var tres: TextureAtlas = ResourceLoader.load(USER_DIR + "icons.atlas.tres", "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(tres != null and _same_regions(atlas, tres) and tres.texture == sheet, ".tres round-trips without loss")

	# TexturePacker JSON: Hash with a trimmed frame, Array with a rotated frame.
	var packed := TextureAtlas.new()
	expect(packed.load_from_file(DIR + "texturepacker_hash.json") == OK, "TexturePacker Hash imports")
	expect(packed.texture == sheet, "meta.image is relative to the JSON file")
	expect(packed.get_region_names() == PackedStringArray(["items_coin", "items_sword"]), "file names become region names")
	expect(packed.get_texture("items_sword").get_size() == Vector2(32, 64), "a trimmed frame keeps its original size")
	expect(packed.get_region_margin("items_sword") == Rect2i(4, 8, 8, 16), "the trim offset becomes the margin")
	var rotated := TextureAtlas.new()
	expect(rotated.load_from_file(DIR + "texturepacker_array.json") == ERR_INVALID_DATA, "a rotated frame makes the Array import fail")
	expect(rotated.get_region_count() == 0, "a failed import changes nothing")
	var array_text := FileAccess.get_file_as_string(DIR + "texturepacker_array.json").replace("\"rotated\": true", "\"rotated\": false")
	expect(rotated.import_texture_packer_json(array_text, DIR) == OK and rotated.get_region_count() == 3, "TexturePacker Array imports")

	sprite.queue_free()
	rect.queue_free()
	animated.queue_free()
