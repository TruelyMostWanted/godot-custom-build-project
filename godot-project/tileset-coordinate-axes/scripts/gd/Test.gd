extends FeatureTest
## Full API test of TileSet Coordinate Axes (TileSet: swap_x_and_y_axis, flip_x, flip_y, layout_to_map_coords,
## map_to_layout_coords, convert_map_coords; TileMapLayer.convert_cell_coordinates). Same tests as scripts/cs/Test.cs.

const DIR := "res://tileset-coordinate-axes/"
## The eight combinations, by index: bit 0 is flip_x, bit 1 flip_y, bit 2 swap_x_and_y_axis
## (the values of the former coordinate_axes property). Expected layout_to_map_coords(Vector2i(1, 2)) for each.
const EXPECTED_1_2: Array[Vector2i] = [
	Vector2i(1, 2), Vector2i(-1, 2), Vector2i(1, -2), Vector2i(-1, -2),
	Vector2i(2, 1), Vector2i(-2, 1), Vector2i(2, -1), Vector2i(-2, -1),
]
const FLIP_Y := 2
const SWAP := 4
const SWAP_FLIP_X := 5


func _run_tests() -> void:
	_test_defaults()
	_test_combinations()
	_test_legacy_value()
	_test_directions()
	_test_round_trip()
	_test_convert_map_coords()
	_test_convert_cell_coordinates()
	_test_example_scene()


func _tile_set(shape: TileSet.TileShape, layout: TileSet.TileLayout, index: int) -> TileSet:
	var tile_set := TileSet.new()
	tile_set.tile_shape = shape
	tile_set.tile_layout = layout
	tile_set.swap_x_and_y_axis = (index & 4) != 0
	tile_set.flip_x = (index & 1) != 0
	tile_set.flip_y = (index & 2) != 0
	tile_set.tile_size = Vector2i(32, 16)
	return tile_set


func _test_defaults() -> void:
	var plain := TileSet.new()
	var axes: Array = [plain.swap_x_and_y_axis, plain.flip_x, plain.flip_y]
	expect_equal(axes, [false, false, false], "the coordinate system defaults to swap, flip X, flip Y off")
	expect(not ("coordinate_axes" in plain), "the old coordinate_axes property is not listed")
	var group: String = ""
	var group_of_swap: String = ""
	for property: Dictionary in plain.get_property_list():
		if property.usage & PROPERTY_USAGE_GROUP:
			group = property.name
		elif property.name == "swap_x_and_y_axis":
			group_of_swap = group
	expect_equal(group_of_swap, "Coordinate System", "swap_x_and_y_axis is in the inspector group Coordinate System")


func _test_combinations() -> void:
	for index in 8:
		var tile_set: TileSet = _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, index)
		var map_coords: Vector2i = tile_set.layout_to_map_coords(Vector2i(1, 2))
		expect_equal(map_coords, EXPECTED_1_2[index], "combination %d: layout_to_map_coords((1, 2))" % index)
		var layout_coords: Vector2i = tile_set.map_to_layout_coords(EXPECTED_1_2[index])
		expect_equal(layout_coords, Vector2i(1, 2), "combination %d: map_to_layout_coords() reverses it" % index)


func _test_legacy_value() -> void:
	var tile_set: TileSet = load(DIR + "resources/legacy_coordinate_axes.tres")
	var axes: Array = [tile_set.swap_x_and_y_axis, tile_set.flip_x, tile_set.flip_y]
	expect_equal(axes, [true, false, true], "a .tres with coordinate_axes = 6 loads as swap and flip Y")
	for index in 8:
		var fresh := TileSet.new()
		fresh.set("coordinate_axes", index)
		var actual: Array = [fresh.swap_x_and_y_axis, fresh.flip_x, fresh.flip_y]
		var expected: Array = [(index & 4) != 0, (index & 1) != 0, (index & 2) != 0]
		expect_equal(actual, expected, "coordinate_axes = %d sets the three bools" % index)


func _test_directions() -> void:
	var layer := TileMapLayer.new()
	layer.tile_set = _tile_set(TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_DIAMOND_RIGHT, SWAP)
	var origin: Vector2 = layer.map_to_local(Vector2i(0, 0))
	var plus_x: Vector2 = layer.map_to_local(Vector2i(1, 0)) - origin
	var plus_y: Vector2 = layer.map_to_local(Vector2i(0, 1)) - origin
	expect_equal(plus_x.sign(), Vector2(1, 1), "isometric with swap: +X goes down right")
	expect_equal(plus_y.sign(), Vector2(1, -1), "isometric with swap: +Y goes up right")
	var bottom_right: Vector2i = layer.get_neighbor_cell(Vector2i(2, 3), TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_SIDE)
	expect_equal(bottom_right, Vector2i(3, 3), "isometric with swap: the bottom right neighbor is +X")

	layer.tile_set = _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, FLIP_Y)
	var step_y: Vector2 = layer.map_to_local(Vector2i(0, 1)) - layer.map_to_local(Vector2i(0, 0))
	expect(step_y.y < 0, "square with flip Y: +Y goes up")
	var top: Vector2i = layer.get_neighbor_cell(Vector2i(0, 0), TileSet.CELL_NEIGHBOR_TOP_SIDE)
	expect_equal(top, Vector2i(0, 1), "square with flip Y: the top neighbor is +Y")
	layer.free()


func _test_round_trip() -> void:
	var layer := TileMapLayer.new()
	var failures: int = 0
	for index in 8:
		layer.tile_set = _tile_set(TileSet.TILE_SHAPE_HEXAGON, TileSet.TILE_LAYOUT_STACKED, index)
		for x in range(-3, 4):
			for y in range(-3, 4):
				var cell := Vector2i(x, y)
				var back: Vector2i = layer.local_to_map(layer.map_to_local(cell))
				if back != cell:
					failures += 1
	expect_equal(failures, 0, "hexagon: local_to_map(map_to_local(cell)) == cell for every combination")
	layer.free()


func _test_convert_map_coords() -> void:
	var tile_set: TileSet = _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, SWAP_FLIP_X)
	var from_default: Vector2i = tile_set.convert_map_coords(Vector2i(1, 2), TileSet.TILE_LAYOUT_STACKED)
	expect_equal(from_default, Vector2i(-2, 1), "convert_map_coords() from the default axes")
	var from_same: Vector2i = tile_set.convert_map_coords(Vector2i(-2, 1), TileSet.TILE_LAYOUT_STACKED, true, true, false)
	expect_equal(from_same, Vector2i(-2, 1), "convert_map_coords() from the same axes keeps the cell")


func _test_convert_cell_coordinates() -> void:
	var tile_set: TileSet = _tile_set(TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_STACKED, 0)
	var layer := TileMapLayer.new()
	layer.tile_set = tile_set
	var cells: Array[Vector2i] = [Vector2i(0, 0), Vector2i(1, 0), Vector2i(1, 1), Vector2i(2, 3)]
	var positions: Dictionary[Vector2i, Vector2] = {}
	for cell in cells:
		layer.set_cell(cell, 0, Vector2i(cell.x, 0))
		positions[cell] = layer.map_to_local(cell)
	tile_set.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_RIGHT
	tile_set.swap_x_and_y_axis = true
	layer.convert_cell_coordinates(TileSet.TILE_LAYOUT_STACKED)
	var moved: int = 0
	for cell in cells:
		var new_cell: Vector2i = layer.local_to_map(positions[cell])
		if layer.get_cell_atlas_coords(new_cell) != Vector2i(cell.x, 0):
			moved += 1
	expect_equal(moved, 0, "convert_cell_coordinates() keeps every cell at its place")

	tile_set.tile_layout = TileSet.TILE_LAYOUT_STACKED
	tile_set.swap_x_and_y_axis = false
	layer.convert_cell_coordinates(TileSet.TILE_LAYOUT_DIAMOND_RIGHT, true, false, false)
	var lost: int = 0
	for cell in cells:
		if layer.get_cell_atlas_coords(cell) != Vector2i(cell.x, 0):
			lost += 1
	expect_equal(lost, 0, "converting back restores every cell")
	expect_equal(layer.get_used_cells().size(), cells.size(), "converting keeps the number of cells")
	layer.free()


func _test_example_scene() -> void:
	var scene: PackedScene = load(DIR + "scenes/isometric_axes_demo.tscn")
	var layer: TileMapLayer = scene.instantiate()
	expect(layer.tile_set.swap_x_and_y_axis, "the demo TileSet swaps X and Y")
	expect_equal(layer.get_used_cells().size(), 9, "the demo map has 9 cells")
	layer.free()
