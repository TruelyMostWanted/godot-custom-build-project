extends FeatureCheck
## GDScript checks of 228-feature-tile-coordinate-axes. Check the same things as scripts/cs/Issue228FeatureTileCoordinateAxesCheck.cs.

const DIR := "res://228-feature-tile-coordinate-axes/"


func _get_check_name() -> String:
	return "228-feature-tile-coordinate-axes"


func _run_checks() -> void:
	_check_api()
	_check_isometric_diagonal()
	_check_square_y_up()
	_check_round_trip()
	_check_convert_cells()
	_check_example_scene()


func _tile_set(shape: TileSet.TileShape, layout: TileSet.TileLayout, axes: TileSet.CoordinateAxes) -> TileSet:
	var tile_set := TileSet.new()
	tile_set.tile_shape = shape
	tile_set.tile_layout = layout
	tile_set.coordinate_axes = axes
	tile_set.tile_size = Vector2i(32, 16)
	return tile_set


func _check_api() -> void:
	expect_equal(TileSet.new().coordinate_axes, TileSet.COORDINATE_AXES_DEFAULT, "coordinate_axes defaults to DEFAULT")
	expect_equal(TileSet.COORDINATE_AXES_MAX, 8, "eight coordinate axes")
	var tile_set := _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, TileSet.COORDINATE_AXES_SWAP_FLIP_X)
	expect_equal(tile_set.layout_to_map_coords(Vector2i(1, 2)), Vector2i(-2, 1), "swap, then flip X")
	expect_equal(tile_set.map_to_layout_coords(Vector2i(-2, 1)), Vector2i(1, 2), "map_to_layout_coords() reverses it")


func _check_isometric_diagonal() -> void:
	var layer := TileMapLayer.new()
	layer.tile_set = _tile_set(TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_DIAMOND_RIGHT, TileSet.COORDINATE_AXES_SWAP)
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
	layer.tile_set = _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, TileSet.COORDINATE_AXES_FLIP_Y)
	expect(layer.map_to_local(Vector2i(0, 1)).y < layer.map_to_local(Vector2i(0, 0)).y, "square: +Y goes up")
	expect_equal(layer.get_neighbor_cell(Vector2i(0, 0), TileSet.CELL_NEIGHBOR_TOP_SIDE), Vector2i(0, 1), "the top neighbor is +Y")
	layer.free()


func _check_round_trip() -> void:
	var ok := true
	var layer := TileMapLayer.new()
	for axes in TileSet.COORDINATE_AXES_MAX:
		layer.tile_set = _tile_set(TileSet.TILE_SHAPE_HEXAGON, TileSet.TILE_LAYOUT_STACKED, axes)
		for x in range(-3, 4):
			for y in range(-3, 4):
				ok = ok and layer.local_to_map(layer.map_to_local(Vector2i(x, y))) == Vector2i(x, y)
	expect(ok, "hexagon: local_to_map(map_to_local(c)) == c for every axes")
	layer.free()


func _check_convert_cells() -> void:
	var tile_set := _tile_set(TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_STACKED, TileSet.COORDINATE_AXES_DEFAULT)
	var layer := TileMapLayer.new()
	layer.tile_set = tile_set
	var cells: Array[Vector2i] = [Vector2i(0, 0), Vector2i(1, 0), Vector2i(1, 1), Vector2i(2, 3)]
	var positions := {}
	for cell in cells:
		layer.set_cell(cell, 0, Vector2i(cell.x, 0))
		positions[cell] = layer.map_to_local(cell)
	tile_set.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_RIGHT
	tile_set.coordinate_axes = TileSet.COORDINATE_AXES_SWAP
	layer.convert_cell_coordinates(TileSet.TILE_LAYOUT_STACKED, TileSet.COORDINATE_AXES_DEFAULT)
	var ok := layer.get_used_cells().size() == cells.size()
	for cell in cells:
		var new_cell := layer.local_to_map(positions[cell])
		ok = ok and layer.get_cell_atlas_coords(new_cell) == Vector2i(cell.x, 0)
	expect(ok, "convert_cell_coordinates() keeps every cell at its place")
	layer.free()


func _check_example_scene() -> void:
	var scene := load(DIR + "packed-scenes/isometric_axes_demo.tscn") as PackedScene
	expect(scene != null, "the example scene loads")
	if scene == null:
		return
	var layer := scene.instantiate() as TileMapLayer
	expect_equal(layer.tile_set.coordinate_axes, TileSet.COORDINATE_AXES_SWAP, "the example TileSet uses SWAP")
	expect_equal(layer.get_used_cells().size(), 9, "the example map has 9 cells")
	layer.free()
