extends FeatureCheck
## GDScript checks of 228-feature-tile-coordinate-axes (coordinate system as three bools since engine #261).
## Check the same things as scripts/cs/Issue228FeatureTileCoordinateAxesCheck.cs.

const DIR := "res://228-feature-tile-coordinate-axes/"

## The eight combinations, by index: bit 0 is flip_x, bit 1 flip_y, bit 2 swap_x_and_y_axis
## (the values of the former coordinate_axes property). Expected layout_to_map_coords(Vector2i(1, 2)) for each.
const EXPECTED_1_2: Array[Vector2i] = [
	Vector2i(1, 2), Vector2i(-1, 2), Vector2i(1, -2), Vector2i(-1, -2),
	Vector2i(2, 1), Vector2i(-2, 1), Vector2i(2, -1), Vector2i(-2, -1),
]


func _get_check_name() -> String:
	return "228-feature-tile-coordinate-axes"


func _run_checks() -> void:
	_check_api()
	_check_all_combinations()
	_check_legacy_value()
	_check_isometric_diagonal()
	_check_square_y_up()
	_check_round_trip()
	_check_convert_cells()
	_check_example_scene()


func _tile_set(shape: TileSet.TileShape, layout: TileSet.TileLayout, swap: bool, flip_x: bool, flip_y: bool) -> TileSet:
	var tile_set := TileSet.new()
	tile_set.tile_shape = shape
	tile_set.tile_layout = layout
	tile_set.swap_x_and_y_axis = swap
	tile_set.flip_x = flip_x
	tile_set.flip_y = flip_y
	tile_set.tile_size = Vector2i(32, 16)
	return tile_set


func _tile_set_index(shape: TileSet.TileShape, layout: TileSet.TileLayout, index: int) -> TileSet:
	return _tile_set(shape, layout, (index & 4) != 0, (index & 1) != 0, (index & 2) != 0)


func _check_api() -> void:
	var plain := TileSet.new()
	expect(not plain.swap_x_and_y_axis and not plain.flip_x and not plain.flip_y, "the coordinate system defaults to false, false, false")
	expect(not ("coordinate_axes" in plain), "the old coordinate_axes property is gone")
	var grouped := false
	var in_group := false
	for property in plain.get_property_list():
		if property.usage & PROPERTY_USAGE_GROUP:
			in_group = property.name == "Coordinate System"
		elif property.name == "swap_x_and_y_axis":
			grouped = in_group
	expect(grouped, "swap_x_and_y_axis is in the inspector group Coordinate System")
	var tile_set := _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, true, true, false)
	expect_equal(tile_set.layout_to_map_coords(Vector2i(1, 2)), Vector2i(-2, 1), "swap, then flip X")
	expect_equal(tile_set.map_to_layout_coords(Vector2i(-2, 1)), Vector2i(1, 2), "map_to_layout_coords() reverses it")


func _check_all_combinations() -> void:
	for index in 8:
		var tile_set := _tile_set_index(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, index)
		expect_equal(tile_set.layout_to_map_coords(Vector2i(1, 2)), EXPECTED_1_2[index], "combination %d: layout (1, 2)" % index)
		expect_equal(tile_set.map_to_layout_coords(EXPECTED_1_2[index]), Vector2i(1, 2), "combination %d: reverse" % index)


func _check_legacy_value() -> void:
	var tile_set := load(DIR + "resources/legacy_coordinate_axes.tres") as TileSet
	expect(tile_set != null, "a TileSet saved with coordinate_axes loads")
	if tile_set == null:
		return
	expect(tile_set.swap_x_and_y_axis and not tile_set.flip_x and tile_set.flip_y, "coordinate_axes = 6 loads as swap and flip Y")
	for index in 8:
		var fresh := TileSet.new()
		fresh.set("coordinate_axes", index)
		var actual := [fresh.swap_x_and_y_axis, fresh.flip_x, fresh.flip_y]
		var expected := [(index & 4) != 0, (index & 1) != 0, (index & 2) != 0]
		expect_equal(actual, expected, "coordinate_axes = %d maps to the three bools" % index)


func _check_isometric_diagonal() -> void:
	var layer := TileMapLayer.new()
	layer.tile_set = _tile_set(TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_DIAMOND_RIGHT, true, false, false)
	var origin := layer.map_to_local(Vector2i(0, 0))
	var plus_x := layer.map_to_local(Vector2i(1, 0)) - origin
	var plus_y := layer.map_to_local(Vector2i(0, 1)) - origin
	expect(plus_x.x > 0 and plus_x.y > 0, "isometric diagonal: +X goes down right")
	expect(plus_y.x > 0 and plus_y.y < 0, "isometric diagonal: +Y goes up right")
	var right := layer.get_neighbor_cell(Vector2i(2, 3), TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_SIDE)
	expect_equal(right, Vector2i(3, 3), "the bottom right neighbor is +X")
	layer.free()


func _check_square_y_up() -> void:
	var layer := TileMapLayer.new()
	layer.tile_set = _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, false, false, true)
	expect(layer.map_to_local(Vector2i(0, 1)).y < layer.map_to_local(Vector2i(0, 0)).y, "square: +Y goes up")
	expect_equal(layer.get_neighbor_cell(Vector2i(0, 0), TileSet.CELL_NEIGHBOR_TOP_SIDE), Vector2i(0, 1), "the top neighbor is +Y")
	layer.free()


func _check_round_trip() -> void:
	var ok := true
	var layer := TileMapLayer.new()
	for index in 8:
		layer.tile_set = _tile_set_index(TileSet.TILE_SHAPE_HEXAGON, TileSet.TILE_LAYOUT_STACKED, index)
		for x in range(-3, 4):
			for y in range(-3, 4):
				ok = ok and layer.local_to_map(layer.map_to_local(Vector2i(x, y))) == Vector2i(x, y)
	expect(ok, "hexagon: local_to_map(map_to_local(c)) == c for every combination")
	layer.free()


func _check_convert_cells() -> void:
	var tile_set := _tile_set(TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_STACKED, false, false, false)
	var layer := TileMapLayer.new()
	layer.tile_set = tile_set
	var cells: Array[Vector2i] = [Vector2i(0, 0), Vector2i(1, 0), Vector2i(1, 1), Vector2i(2, 3)]
	var positions := {}
	for cell in cells:
		layer.set_cell(cell, 0, Vector2i(cell.x, 0))
		positions[cell] = layer.map_to_local(cell)
	tile_set.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_RIGHT
	tile_set.swap_x_and_y_axis = true
	layer.convert_cell_coordinates(TileSet.TILE_LAYOUT_STACKED)
	var ok := layer.get_used_cells().size() == cells.size()
	for cell in cells:
		var new_cell := layer.local_to_map(positions[cell])
		ok = ok and layer.get_cell_atlas_coords(new_cell) == Vector2i(cell.x, 0)
	expect(ok, "convert_cell_coordinates() keeps every cell at its place")

	# And back, from the settings the cells were converted to.
	tile_set.tile_layout = TileSet.TILE_LAYOUT_STACKED
	tile_set.swap_x_and_y_axis = false
	layer.convert_cell_coordinates(TileSet.TILE_LAYOUT_DIAMOND_RIGHT, true, false, false)
	ok = layer.get_used_cells().size() == cells.size()
	for cell in cells:
		ok = ok and layer.get_cell_atlas_coords(cell) == Vector2i(cell.x, 0)
	expect(ok, "converting back restores the cells")
	layer.free()


func _check_example_scene() -> void:
	var scene := load(DIR + "packed-scenes/isometric_axes_demo.tscn") as PackedScene
	expect(scene != null, "the example scene loads")
	if scene == null:
		return
	var layer := scene.instantiate() as TileMapLayer
	var tile_set := layer.tile_set
	expect(tile_set.swap_x_and_y_axis and not tile_set.flip_x and not tile_set.flip_y, "the example TileSet swaps X and Y")
	expect_equal(layer.get_used_cells().size(), 9, "the example map has 9 cells")
	layer.free()
