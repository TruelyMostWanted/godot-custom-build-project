extends FeatureCheck
## GDScript checks of 51-feature-texture-atlas-editor. Check the same things as scripts/cs/Issue51FeatureTextureAtlasEditorCheck.cs.
## The editor itself is checked by hand (see the issue). These checks cover the runtime API the editor uses
## (auto slice, grid, generated names) and the files the editor writes (.tres, JSON, CSV, INI, SpriteFrames).

const USER_DIR := "user://atlas51_gd/"


func _get_check_name() -> String:
	return "51-feature-texture-atlas-editor"


## 50 sprites of mixed sizes in 5 rows of 10 cells (24x24), like a hand-made sheet.
func _make_sheet(expected: Array[Rect2i]) -> Image:
	var image := Image.create_empty(240, 120, false, Image.FORMAT_RGBA8)
	for i in 50:
		var rect := Rect2i((i % 10) * 24 + 1, int(i / 10.0) * 24 + 1, 4 + (i % 5) * 4, 6 + (i % 3) * 7)
		image.fill_rect(rect, Color(0.2 + 0.015 * i, 0.5, 1.0, 1.0))
		expected.append(rect)
	return image


func _same_regions(a: TextureAtlas, b: TextureAtlas) -> bool:
	if a.get_region_names() != b.get_region_names():
		return false
	for region_name in a.get_region_names():
		if a.get_region(region_name) != b.get_region(region_name) or a.get_region_margin(region_name) != b.get_region_margin(region_name):
			return false
	return true


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(USER_DIR)
	var expected: Array[Rect2i] = []
	var image := _make_sheet(expected)

	# Auto slice: 50 sprites of mixed sizes become 50 correct regions, in rows from left to right.
	var found := TextureAtlas.detect_sprite_rects(image)
	expect(found.size() == 50, "detect_sprite_rects finds 50 sprites (found %d)" % found.size())
	var all_match := found.size() == 50
	for i in mini(found.size(), 50):
		all_match = all_match and found[i] == expected[i]
	expect(all_match, "every detected rectangle matches its sprite, in row order")
	expect(TextureAtlas.detect_sprite_rects(image, 0.99).size() == 50, "opaque sprites are found with a high alpha threshold too")

	# The same through a PNG file (as an editor user would paint it).
	var png_path := USER_DIR + "sheet50.png"
	expect(image.save_png(png_path) == OK, "the sheet is saved as PNG")
	var loaded := Image.load_from_file(png_path)
	var sheet := ImageTexture.create_from_image(loaded)
	var atlas := TextureAtlas.new()
	atlas.texture = sheet
	var names := atlas.auto_slice("enemy_")
	expect(names.size() == 50 and names[0] == "enemy_0" and names[49] == "enemy_49", "auto_slice adds 50 regions named enemy_0 ... enemy_49")
	expect(atlas.get_region("enemy_17") == expected[17], "auto_slice region enemy_17 has the sprite rectangle")
	expect(atlas.auto_slice("enemy_").is_empty(), "auto_slice again adds nothing")

	# Grid and generated names ("Add from Grid" in the editor).
	var cells := TextureAtlas.make_grid_rects(Vector2i(240, 120), Vector2i(24, 24))
	expect(cells.size() == 50 and cells[11] == Rect2i(24, 24, 24, 24), "make_grid_rects returns 10x5 cells, row by row")
	var grid_atlas := TextureAtlas.new()
	grid_atlas.texture = sheet
	var grid_names := grid_atlas.add_regions(cells, "cell_")
	expect(grid_names.size() == 50 and grid_atlas.get_region("cell_11") == Rect2i(24, 24, 24, 24), "add_regions adds the grid cells with generated names")
	expect(grid_atlas.make_unique_region_name("cell_") == "cell_50", "make_unique_region_name returns the next free name")
	var count := grid_atlas.get_region_count()
	expect(grid_atlas.add_regions([Rect2i(0, 0, 8, 8), Rect2i(230, 0, 32, 32)], "cell_").is_empty() and grid_atlas.get_region_count() == count, "add_regions is all-or-nothing")

	# Files the editor writes: the atlas as .tres (with order), JSON, CSV and INI exports.
	atlas.rename_region("enemy_0", "boss")
	atlas.set_region_margin("boss", Rect2i(1, 2, 3, 4))
	var tres_path := USER_DIR + "enemies.atlas.tres"
	expect(ResourceSaver.save(atlas, tres_path) == OK, "the atlas is saved as .tres")
	var reloaded: TextureAtlas = ResourceLoader.load(tres_path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(reloaded != null and _same_regions(atlas, reloaded), ".tres keeps 50 regions, their order and the margin")
	for extension in ["json", "csv", "ini"]:
		var path: String = USER_DIR + "enemies." + extension
		atlas.texture_path = png_path
		expect(atlas.save_to_file(path) == OK, "export as " + extension)
		# The editor imports into an atlas that has the sheet already; the same path keeps that texture.
		var imported := TextureAtlas.new()
		imported.texture = sheet
		imported.texture_path = png_path
		expect(imported.load_from_file(path) == OK and _same_regions(atlas, imported), "import of the " + extension + " export gives the same regions")

	# "Create SpriteFrames" from selected regions: a copy with only the selected ones.
	var selected := TextureAtlas.new()
	selected.texture = sheet
	for region_name in ["enemy_12", "enemy_10", "enemy_11"]:
		selected.set_region(region_name, atlas.get_region(region_name))
	var frames := selected.create_sprite_frames()
	expect(frames.has_animation("enemy") and frames.get_frame_count("enemy") == 3, "SpriteFrames from 3 selected regions has 3 frames")
	var first: AtlasTexture = frames.get_frame_texture("enemy", 0)
	expect(first.region == Rect2(atlas.get_region("enemy_10")), "frames are sorted by number")
	var frames_path := USER_DIR + "enemy_frames.tres"
	expect(ResourceSaver.save(frames, frames_path) == OK, "the SpriteFrames are saved as .tres")
	var frames_reloaded: SpriteFrames = ResourceLoader.load(frames_path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(frames_reloaded != null and frames_reloaded.get_frame_count("enemy") == 3, "the saved SpriteFrames load with 3 frames")

	# "Assign to Selected Nodes": a Sprite2D shows the region.
	var sprite := Sprite2D.new()
	sprite.texture = atlas.get_texture("enemy_17")
	add_child(sprite)
	await get_tree().process_frame
	expect(sprite.get_rect().size == Vector2(expected[17].size), "a Sprite2D with the region texture has the sprite size")
	sprite.queue_free()
