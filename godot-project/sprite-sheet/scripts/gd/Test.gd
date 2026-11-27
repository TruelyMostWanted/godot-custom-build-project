extends FeatureTest
## Full API test of Sprite Sheet (SpriteSheet): every method and property. Same tests as scripts/cs/Test.cs.

const DIR := "res://sprite-sheet/resources/"
const SHEET_PATH := DIR + "sheet.png"
const USER_DIR := "user://sprite_sheet_gd/"
const PREFIX := "spritesheetgd://"
const FILE_ORDER := ["coin", "sword", "walk_0", "walk_1", "walk_2", "walk_10"]
const SWORD := Rect2i(16, 0, 24, 48)
const TRIMMED_SWORD_MARGIN := Rect2i(4, 8, 8, 16)

var _sheet: Texture2D


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(USER_DIR)
	_sheet = load(SHEET_PATH)
	_test_regions()
	_test_region_names()
	_test_region_texture()
	await _test_nodes()
	await _test_sprite_frames()
	_test_text_formats()
	_test_files()
	_test_texture_packer()
	_test_auto_slice()
	_test_grid()


func _test_regions() -> void:
	var atlas := SpriteSheet.new()
	atlas.texture = _sheet
	expect_equal(atlas.set_region("sword", SWORD), OK, "set_region adds a region")
	expect_equal(atlas.get_region("sword"), SWORD, "get_region returns the rectangle")
	expect(atlas.has_region("sword"), "has_region finds the region")
	expect_equal(atlas.set_region("outside", Rect2i(120, 60, 16, 16)), ERR_INVALID_PARAMETER, "a region outside the texture is rejected")
	expect_equal(atlas.set_region("empty", Rect2i(0, 0, 0, 4)), ERR_INVALID_PARAMETER, "a region without width is rejected")
	expect(not atlas.has_region("outside"), "a rejected region is not added")
	expect_equal(atlas.set_region_margin("sword", Rect2i(2, 2, 4, 4)), OK, "set_region_margin sets a margin")
	expect_equal(atlas.get_region_margin("sword"), Rect2i(2, 2, 4, 4), "get_region_margin returns the margin")
	expect_equal(atlas.regions, {&"sword": SWORD}, "regions holds every rectangle")
	expect_equal(atlas.margins, {&"sword": Rect2i(2, 2, 4, 4)}, "margins holds every margin")
	expect_equal(atlas.set_region("coin", Rect2i(0, 0, 16, 16)), OK, "a second region is added")
	expect_equal(atlas.get_region_count(), 2, "get_region_count counts the regions")
	atlas.remove_region("coin")
	expect(not atlas.has_region("coin"), "remove_region removes a region")
	atlas.clear_regions()
	expect_equal(atlas.get_region_count(), 0, "clear_regions removes every region")


func _test_region_names() -> void:
	var atlas := SpriteSheet.new()
	atlas.texture = _sheet
	atlas.set_region("b", Rect2i(0, 0, 1, 1))
	atlas.set_region("a", Rect2i(0, 0, 1, 1))
	expect_equal(atlas.get_region_names(), PackedStringArray(["b", "a"]), "regions keep the insertion order")
	expect_equal(atlas.region_order, PackedStringArray(["b", "a"]), "region_order is the insertion order")
	expect_equal(atlas.rename_region("b", "c"), OK, "rename_region renames")
	expect_equal(atlas.get_region_names(), PackedStringArray(["c", "a"]), "rename_region keeps the position")
	expect_equal(atlas.rename_region("c", "a"), ERR_ALREADY_EXISTS, "rename_region to a taken name fails")
	expect_equal(atlas.rename_region("missing", "d"), ERR_DOES_NOT_EXIST, "rename_region of a missing region fails")
	expect(SpriteSheet.is_valid_region_name("hero_walk_01"), "a plain name is valid")
	expect(not SpriteSheet.is_valid_region_name(""), "an empty name is invalid")
	expect(not SpriteSheet.is_valid_region_name("a/b"), "a name with / is invalid")
	expect(not SpriteSheet.is_valid_region_name(" a"), "a name with surrounding spaces is invalid")
	expect_equal(atlas.set_region("a:b", Rect2i(0, 0, 1, 1)), ERR_INVALID_PARAMETER, "set_region rejects an invalid name")


func _test_region_texture() -> void:
	var atlas := SpriteSheet.new()
	atlas.texture = _sheet
	atlas.texture_path = SHEET_PATH
	atlas.set_region("coin", Rect2i(0, 0, 16, 16))
	var coin: AtlasTexture = atlas.get_region_texture("coin")
	expect_equal(coin.atlas, _sheet, "get_region_texture returns an AtlasTexture of the sheet")
	expect_equal(atlas.get_region_texture("coin"), coin, "get_region_texture returns the same texture again")
	atlas.set_region("coin", Rect2i(0, 0, 8, 8))
	expect_equal(coin.region, Rect2(0, 0, 8, 8), "a moved region updates the handed-out texture")
	atlas.set_region_margin("coin", Rect2i(1, 2, 3, 4))
	expect_equal(coin.margin, Rect2(1, 2, 3, 4), "the region margin is the AtlasTexture margin")
	atlas.filter_clip = true
	expect(coin.filter_clip, "filter_clip reaches every handed-out texture")
	var gradient := GradientTexture2D.new()
	atlas.texture = gradient
	expect_equal(coin.atlas, gradient, "a new texture reaches every handed-out texture")
	expect_equal(atlas.texture_path, "", "setting texture from code clears texture_path")


func _test_nodes() -> void:
	var atlas := SpriteSheet.new()
	atlas.texture = _sheet
	atlas.set_region("sword", SWORD)
	atlas.set_region("coin", Rect2i(0, 0, 16, 16))
	var sprite := Sprite2D.new()
	sprite.texture = atlas.get_region_texture("sword")
	add_child(sprite)
	var rect := TextureRect.new()
	rect.texture = atlas.get_region_texture("coin")
	add_child(rect)
	await get_tree().process_frame
	expect_equal(sprite.get_rect().size, Vector2(24, 48), "Sprite2D draws the 24x48 region")
	expect_equal(rect.get_combined_minimum_size(), Vector2(16, 16), "TextureRect uses the 16x16 region")
	sprite.queue_free()
	rect.queue_free()


func _test_sprite_frames() -> void:
	var atlas := SpriteSheet.new()
	atlas.load_from_file(DIR + "icons.atlas.json")
	var frames: SpriteFrames = atlas.create_sprite_frames("walk_", 30.0)
	expect_equal(frames.get_animation_names(), PackedStringArray(["walk"]), "one animation per prefix")
	expect_equal(frames.get_frame_count("walk"), 4, "four walk frames")
	expect_equal(frames.get_animation_speed("walk"), 30.0, "the animation runs at the given fps")
	expect_equal(frames.get_frame_texture("walk", 3), atlas.get_region_texture("walk_10"), "frames are sorted by number")
	var all_frames: SpriteFrames = atlas.create_sprite_frames()
	expect_equal(all_frames.get_animation_names(), PackedStringArray(["coin", "sword", "walk"]), "an empty prefix makes one animation per name group")
	var animated := AnimatedSprite2D.new()
	animated.sprite_frames = frames
	add_child(animated)
	animated.play("walk")
	await get_tree().create_timer(0.2).timeout
	expect(animated.frame > 0, "AnimatedSprite2D plays the frames (frame %d)" % animated.frame)
	animated.queue_free()


func _test_text_formats() -> void:
	var atlas := SpriteSheet.new()
	expect_equal(atlas.load_from_file(DIR + "icons.atlas.json"), OK, "load_from_file reads the JSON layout")
	expect_equal(atlas.texture, _sheet, "TexturePath loads the imported PNG")
	expect_equal(atlas.texture_path, SHEET_PATH, "texture_path keeps the path of the file")
	expect_equal(atlas.get_region_names(), PackedStringArray(FILE_ORDER), "regions keep the file order")
	var uid_atlas := SpriteSheet.new()
	uid_atlas.texture = _sheet
	uid_atlas.set_region("a", Rect2i(0, 0, 1, 1))
	var uid_json: String = uid_atlas.to_text(ResourceTextSerializer.FORMAT_JSON)
	expect(uid_json.contains("\"TexturePath\": \"uid://"), "without texture_path the uid:// path of the texture is written")
	var uid_copy := SpriteSheet.new()
	expect_equal(uid_copy.load_from_text(uid_json, ResourceTextSerializer.FORMAT_JSON), OK, "load_from_text reads a uid:// TexturePath")
	expect_equal(uid_copy.texture, _sheet, "the uid:// TexturePath loads the sheet")
	var one_line := SerializerOptions.new()
	one_line.indent = ""
	expect(not atlas.to_text(ResourceTextSerializer.FORMAT_JSON, one_line).contains("\n"), "to_text uses SerializerOptions.indent")
	ResourceSaver.save(ImageTexture.create_from_image(Image.create_empty(128, 64, false, Image.FORMAT_RGBA8)), USER_DIR + "sheet.res")
	PathPrefixServer.register_prefix_folder(PREFIX, USER_DIR)
	var json: String = atlas.to_text(ResourceTextSerializer.FORMAT_JSON).replace(SHEET_PATH, PREFIX + "sheet.res")
	var prefixed := SpriteSheet.new()
	expect_equal(prefixed.load_from_text(json, ResourceTextSerializer.FORMAT_JSON), OK, "load_from_text reads a prefixed TexturePath")
	expect_equal(prefixed.texture.get_size(), Vector2(128, 64), "the texture is loaded through the path prefix")
	expect_equal(prefixed.to_text(ResourceTextSerializer.FORMAT_JSON), json, "the prefixed path round-trips unchanged")
	PathPrefixServer.unregister_prefix(PREFIX)
	var broken: String = json.replace("\"Width\": 24", "\"Width\": 2400")
	expect_equal(prefixed.load_from_text(broken, ResourceTextSerializer.FORMAT_JSON), ERR_INVALID_DATA, "a region outside the texture fails the load")
	expect(_same_regions(atlas, prefixed), "a failed load leaves the regions unchanged")


func _test_files() -> void:
	var atlas := SpriteSheet.new()
	atlas.load_from_file(DIR + "icons.atlas.json")
	atlas.set_region_margin("sword", Rect2i(2, 2, 4, 4))
	for extension: String in ["json", "csv", "ini"]:
		var path: String = USER_DIR + "icons." + extension
		expect_equal(atlas.save_to_file(path), OK, "save_to_file writes .%s" % extension)
		var copy := SpriteSheet.new()
		copy.load_from_file(path)
		expect(_same_regions(atlas, copy), "%s keeps regions, order and margins" % extension.to_upper())
	ResourceSaver.save(atlas, USER_DIR + "icons.atlas.tres")
	var tres: SpriteSheet = ResourceLoader.load(USER_DIR + "icons.atlas.tres", "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(_same_regions(atlas, tres), ".tres keeps regions, order and margins")
	expect_equal(tres.texture, _sheet, ".tres keeps the texture")


func _test_texture_packer() -> void:
	expect_equal(SpriteSheet.texture_packer_name_to_region_name("hero/walk_01.png"), "hero_walk_01", "texture_packer_name_to_region_name drops the extension and replaces /")
	var packed := SpriteSheet.new()
	expect_equal(packed.load_from_file(DIR + "texturepacker_hash.json"), OK, "load_from_file imports the TexturePacker Hash layout")
	expect_equal(packed.texture, _sheet, "meta.image is relative to the JSON file")
	expect_equal(packed.get_region_names(), PackedStringArray(["items_coin", "items_sword"]), "file names become region names")
	expect_equal(packed.get_region_texture("items_sword").get_size(), Vector2(32, 64), "a trimmed frame keeps its original size")
	expect_equal(packed.get_region_margin("items_sword"), TRIMMED_SWORD_MARGIN, "the trim offset becomes the margin")
	var rotated := SpriteSheet.new()
	expect_equal(rotated.load_from_file(DIR + "texturepacker_array.json"), ERR_INVALID_DATA, "a rotated frame fails the Array import")
	expect_equal(rotated.get_region_count(), 0, "a failed import changes nothing")
	var array_text: String = FileAccess.get_file_as_string(DIR + "texturepacker_array.json")
	var unrotated: String = array_text.replace("\"rotated\": true", "\"rotated\": false")
	expect_equal(rotated.import_texture_packer_json(unrotated, DIR), OK, "import_texture_packer_json imports the Array layout")
	expect_equal(rotated.get_region_count(), 3, "every Array frame becomes a region")


func _test_auto_slice() -> void:
	var expected: Array[Rect2i] = []
	var image := Image.create_empty(240, 120, false, Image.FORMAT_RGBA8)
	for i in 50:
		var rect := Rect2i((i % 10) * 24 + 1, floori(i / 10.0) * 24 + 1, 4 + (i % 5) * 4, 6 + (i % 3) * 7)
		image.fill_rect(rect, Color(0.2 + 0.015 * i, 0.5, 1.0))
		expected.append(rect)
	var found: Array[Rect2i] = SpriteSheet.detect_sprite_rects(image)
	expect_equal(found, expected, "detect_sprite_rects finds 50 sprites of mixed sizes, in row order")
	var opaque: Array[Rect2i] = SpriteSheet.detect_sprite_rects(image, 0.99)
	expect_equal(opaque.size(), 50, "a high alpha threshold finds opaque sprites too")
	var atlas := SpriteSheet.new()
	atlas.texture = ImageTexture.create_from_image(image)
	var names: PackedStringArray = atlas.auto_slice("enemy_")
	expect_equal(names.size(), 50, "auto_slice adds one region per sprite")
	expect_equal(names[49], "enemy_49", "auto_slice numbers the names")
	expect_equal(atlas.get_region("enemy_17"), expected[17], "auto_slice regions have the sprite rectangles")
	expect(atlas.auto_slice("enemy_").is_empty(), "auto_slice again adds nothing")


func _test_grid() -> void:
	var cells: Array[Rect2i] = SpriteSheet.make_grid_rects(Vector2i(128, 64), Vector2i(32, 32), Vector2i(2, 2), Vector2i(1, 1))
	expect_equal(cells, [Rect2i(1, 1, 32, 32), Rect2i(35, 1, 32, 32), Rect2i(69, 1, 32, 32)], "make_grid_rects keeps separation and offset inside the area")
	var atlas := SpriteSheet.new()
	atlas.texture = _sheet
	var names: PackedStringArray = atlas.add_regions(cells, "tile_")
	expect_equal(names, PackedStringArray(["tile_0", "tile_1", "tile_2"]), "add_regions adds the cells with generated names")
	expect_equal(atlas.make_unique_region_name("tile_"), "tile_3", "make_unique_region_name returns the next free name")
	var too_big: Array[Rect2i] = [Rect2i(0, 0, 8, 8), Rect2i(120, 0, 32, 32)]
	expect(atlas.add_regions(too_big, "tile_").is_empty(), "add_regions with one invalid rectangle adds nothing")
	expect_equal(atlas.get_region_count(), 3, "a failed add_regions changes nothing")


func _same_regions(a: SpriteSheet, b: SpriteSheet) -> bool:
	if a.get_region_names() != b.get_region_names():
		return false
	for region_name in a.get_region_names():
		if a.get_region(region_name) != b.get_region(region_name):
			return false
		if a.get_region_margin(region_name) != b.get_region_margin(region_name):
			return false
	return true
