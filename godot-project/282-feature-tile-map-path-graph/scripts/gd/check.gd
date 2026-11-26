extends FeatureCheck
## GDScript checks of 282-feature-tile-map-path-graph (TileMapPathGraph, TileMapPathGraphOptions).
## Check the same things as scripts/cs/Issue282FeatureTileMapPathGraphCheck.cs.

const FLOOR := Vector2i(0, 0)
const WALL := Vector2i(1, 0) # Custom data "blocked" = true.
const MUD := Vector2i(2, 0) # Custom data "cost" = 20.
## Negative and positive cells. Every side step changes x by at most 1, so the column x = 0 splits the map.
const MAP := Rect2i(-4, -3, 9, 7)
const FROM := Vector2i(-4, 0)
const TO := Vector2i(4, 0)
const GAP := Vector2i(0, 3)


## Blocks the column x = 0 except the gap and makes row y = 0 more expensive, without custom data.
class ColumnGraph extends TileMapPathGraph:
	func _is_cell_blocked(cell: Vector2i, _tile_data: TileData) -> bool:
		return cell.x == 0 and cell != GAP

	func _get_cell_cost(cell: Vector2i, _tile_data: TileData) -> float:
		return 3.0 if cell.y == 0 else 1.0


func _get_check_name() -> String:
	return "282-feature-tile-map-path-graph"


func _run_checks() -> void:
	_check_api()
	_check_point_ids()
	_check_map("square", TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, false, TileMapPathGraph.BACKEND_GRID)
	_check_map("diamond isometric", TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_DIAMOND_RIGHT, false, TileMapPathGraph.BACKEND_GRID)
	_check_map("stacked isometric", TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_STACKED, false, TileMapPathGraph.BACKEND_GRAPH)
	_check_map("half-offset square", TileSet.TILE_SHAPE_HALF_OFFSET_SQUARE, TileSet.TILE_LAYOUT_STACKED, false, TileMapPathGraph.BACKEND_GRAPH)
	_check_map("hexagon", TileSet.TILE_SHAPE_HEXAGON, TileSet.TILE_LAYOUT_STACKED, false, TileMapPathGraph.BACKEND_GRAPH)
	_check_map("square, swapped and flipped", TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, true, TileMapPathGraph.BACKEND_GRID)
	_check_map("hexagon, swapped and flipped", TileSet.TILE_SHAPE_HEXAGON, TileSet.TILE_LAYOUT_STACKED, true, TileMapPathGraph.BACKEND_GRAPH)
	_check_corners()
	_check_empty_cells()
	_check_virtuals()


func _tile_set(shape: TileSet.TileShape, layout: TileSet.TileLayout, swapped: bool) -> TileSet:
	var tile_set := TileSet.new()
	tile_set.tile_shape = shape
	tile_set.tile_layout = layout
	tile_set.tile_size = Vector2i(16, 16)
	tile_set.swap_x_and_y_axis = swapped
	tile_set.flip_y = swapped
	tile_set.add_custom_data_layer()
	tile_set.set_custom_data_layer_name(0, "blocked")
	tile_set.set_custom_data_layer_type(0, TYPE_BOOL)
	tile_set.add_custom_data_layer()
	tile_set.set_custom_data_layer_name(1, "cost")
	tile_set.set_custom_data_layer_type(1, TYPE_FLOAT)
	var atlas := TileSetAtlasSource.new()
	atlas.texture = ImageTexture.create_from_image(Image.create_empty(48, 16, false, Image.FORMAT_RGBA8))
	atlas.texture_region_size = Vector2i(16, 16)
	tile_set.add_source(atlas, 0)
	for tile: Vector2i in [FLOOR, WALL, MUD]:
		atlas.create_tile(tile)
	atlas.get_tile_data(WALL, 0).set_custom_data("blocked", true)
	atlas.get_tile_data(MUD, 0).set_custom_data("cost", 20.0)
	return tile_set


func _layer(tile_set: TileSet, rect: Rect2i) -> TileMapLayer:
	var layer := TileMapLayer.new()
	layer.tile_set = tile_set
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			layer.set_cell(Vector2i(x, y), 0, FLOOR)
	return layer


func _options() -> TileMapPathGraphOptions:
	var options := TileMapPathGraphOptions.new()
	options.blocked_data_layer = "blocked"
	options.cost_data_layer = "cost"
	return options


## A path is valid when it goes from `from` to `to`, every step goes to a side neighbor and no cell is blocked.
func _is_valid_path(graph: TileMapPathGraph, layer: TileMapLayer, path: Array[Vector2i], from: Vector2i, to: Vector2i) -> bool:
	if path.is_empty() or path[0] != from or path[-1] != to:
		return false
	for i in path.size():
		if graph.is_cell_blocked(path[i]):
			return false
		if i > 0 and not layer.get_surrounding_cells(path[i - 1]).has(path[i]):
			return false
	return true


func _check_api() -> void:
	for name_of_class: String in ["TileMapPathGraph", "TileMapPathGraphOptions"]:
		expect(ClassDB.class_exists(name_of_class), "%s is registered" % name_of_class)
	var options := TileMapPathGraphOptions.new()
	expect_equal(options.neighbor_mode, TileMapPathGraphOptions.NEIGHBOR_MODE_SIDES, "neighbor_mode defaults to sides")
	expect_equal(options.diagonal_mode, AStarGrid2D.DIAGONAL_MODE_ONLY_IF_NO_OBSTACLES, "diagonal_mode defaults to no corner cutting")
	expect_equal(options.default_cost, 1.0, "default_cost defaults to 1")
	expect(options.empty_cells_blocked, "empty_cells_blocked defaults to true")
	expect(options.cost_data_layer.is_empty() and options.blocked_data_layer.is_empty(), "no data layers by default")
	var graph := TileMapPathGraph.new()
	expect(graph.options != null, "a new graph has default options")
	graph.options = null
	expect(graph.options != null, "setting null options restores the defaults")
	expect(graph.get_layer() == null, "a new graph has no layer")
	expect(graph.get_cell_path(Vector2i(), Vector2i(1, 0)).is_empty(), "an unbuilt graph has no paths")
	expect(graph.is_cell_blocked(Vector2i()), "an unbuilt graph blocks every cell")


func _check_point_ids() -> void:
	expect_equal(TileMapPathGraph.cell_to_point_id(Vector2i(-32768, -32768)), 0, "the lowest cell has id 0")
	expect_equal(TileMapPathGraph.cell_to_point_id(Vector2i(32767, 32767)), 4294967295, "the highest cell has id 2^32 - 1")
	var astar := AStar2D.new()
	for cell: Vector2i in [Vector2i(-1, -1), Vector2i(-32768, 32767), Vector2i(32767, -32768), Vector2i(0, 0), Vector2i(5, -7)]:
		var id := TileMapPathGraph.cell_to_point_id(cell)
		expect(id >= 0, "cell %s has a non-negative id" % cell)
		expect_equal(TileMapPathGraph.point_id_to_cell(id), cell, "id of %s converts back" % cell)
		astar.add_point(id, Vector2(cell))
		expect(astar.has_point(id), "AStar2D accepts the id of %s" % cell)


func _check_map(label: String, shape: TileSet.TileShape, layout: TileSet.TileLayout, swapped: bool, backend: TileMapPathGraph.Backend) -> void:
	var tile_set := _tile_set(shape, layout, swapped)
	var layer := _layer(tile_set, MAP)
	var graph := TileMapPathGraph.create(layer, _options())
	var updates := [0]
	graph.updated.connect(func() -> void: updates[0] += 1)
	expect_equal(graph.get_backend(), backend, label + ": backend")
	expect_equal(graph.get_astar_grid() != null, backend == TileMapPathGraph.BACKEND_GRID, label + ": get_astar_grid() only on the grid backend")
	expect_equal(graph.get_astar() != null, backend == TileMapPathGraph.BACKEND_GRAPH, label + ": get_astar() only on the graph backend")
	expect_equal(graph.get_region(), MAP, label + ": the region is the used rect")
	var path := graph.get_cell_path(FROM, TO)
	expect(_is_valid_path(graph, layer, path, FROM, TO), label + ": path on the open map")
	var points := graph.get_point_path(FROM, TO)
	expect(points.size() == path.size() and points[0].is_equal_approx(layer.map_to_local(FROM)) and points[-1].is_equal_approx(layer.map_to_local(TO)), label + ": point path in local positions")

	# A wall at x = 0 with one gap.
	var wall: Array[Vector2i] = []
	for y in range(MAP.position.y, MAP.end.y):
		if Vector2i(0, y) != GAP:
			layer.set_cell(Vector2i(0, y), 0, WALL)
			wall.append(Vector2i(0, y))
	graph.update_cells(wall)
	expect_equal(updates[0], 1, label + ": update_cells() emits updated")
	expect(graph.is_cell_blocked(Vector2i(0, 0)) and not graph.is_cell_blocked(GAP), label + ": wall cells are blocked")
	path = graph.get_cell_path(FROM, TO)
	expect(_is_valid_path(graph, layer, path, FROM, TO) and path.has(GAP), label + ": the path goes through the gap")

	# Close the gap in the layer but update another cell: only the given cells are recomputed.
	layer.set_cell(GAP, 0, WALL)
	graph.update_cells([FROM])
	expect(graph.get_cell_path(FROM, TO).has(GAP), label + ": update_cells() recomputes only the given cells")
	graph.update_cells([GAP])
	expect(graph.get_cell_path(FROM, TO).is_empty(), label + ": closed wall, no path")
	var partial := graph.get_cell_path(FROM, TO, true)
	expect(not partial.is_empty() and partial[0] == FROM and partial[-1].x < 0, label + ": partial path ends before the wall")

	# Costs: mud everywhere at x = 0 except the gap.
	for cell in wall:
		layer.set_cell(cell, 0, MUD)
	layer.set_cell(GAP, 0, FLOOR)
	wall.append(GAP)
	graph.update_cells(wall)
	expect(is_equal_approx(graph.get_cell_cost(Vector2i(0, 0)), 20.0) and is_equal_approx(graph.get_cell_cost(GAP), 1.0), label + ": costs from the custom data layer")
	path = graph.get_cell_path(FROM, TO)
	expect(_is_valid_path(graph, layer, path, FROM, TO) and path.has(GAP), label + ": the cheap way goes through the gap")
	layer.free()


func _check_corners() -> void:
	var tile_set := _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, false)
	var layer := _layer(tile_set, Rect2i(0, 0, 7, 7))
	var options := _options()
	expect_equal(TileMapPathGraph.create(layer, options).get_cell_path(Vector2i(0, 0), Vector2i(6, 6)).size(), 13, "sides only: 13 cells to the opposite corner")
	options.neighbor_mode = TileMapPathGraphOptions.NEIGHBOR_MODE_SIDES_AND_CORNERS
	var graph := TileMapPathGraph.create(layer, options)
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(6, 6)).size(), 7, "sides and corners: 7 cells on the diagonal")
	# No corner cutting by default: a blocked side cell forbids the corner step past it.
	layer.set_cell(Vector2i(1, 0), 0, WALL)
	graph.update_cells([Vector2i(1, 0)])
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(1, 1)).size(), 3, "no corner cutting past a blocked cell")
	layer.free()


func _check_empty_cells() -> void:
	var tile_set := _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, false)
	var layer := _layer(tile_set, Rect2i(0, 0, 5, 1))
	layer.erase_cell(Vector2i(2, 0))
	var graph := TileMapPathGraph.create(layer)
	expect(graph.is_cell_blocked(Vector2i(2, 0)), "empty cells are blocked by default")
	expect(graph.get_cell_path(Vector2i(0, 0), Vector2i(4, 0)).is_empty(), "no path through a hole")
	graph.options.empty_cells_blocked = false
	graph.options.default_cost = 2.0
	expect_equal(graph.update_all(), OK, "update_all() returns OK")
	expect(not graph.is_cell_blocked(Vector2i(2, 0)) and is_equal_approx(graph.get_cell_cost(Vector2i(2, 0)), 2.0), "walkable empty cells cost default_cost")
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(4, 0)).size(), 5, "path through the hole")
	# Painting outside the region grows the graph.
	layer.set_cell(Vector2i(5, 0), 0, FLOOR)
	graph.update_cells([Vector2i(5, 0)])
	expect_equal(graph.get_region(), Rect2i(0, 0, 6, 1), "update_cells() grows the region")
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(5, 0)).size(), 6, "path to the new cell")
	layer.free()
	expect(graph.get_layer() == null, "get_layer() is null after the layer is freed")


func _check_virtuals() -> void:
	var tile_set := _tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, false)
	var layer := _layer(tile_set, MAP)
	var graph := ColumnGraph.new()
	graph.set_layer(layer)
	expect_equal(graph.update_all(), OK, "a script subclass builds")
	expect(graph.is_cell_blocked(Vector2i(0, 0)) and not graph.is_cell_blocked(GAP), "_is_cell_blocked() decides blocked cells")
	expect(is_equal_approx(graph.get_cell_cost(Vector2i(1, 0)), 3.0) and is_equal_approx(graph.get_cell_cost(Vector2i(1, 1)), 1.0), "_get_cell_cost() decides costs")
	expect(graph.get_cell_path(FROM, TO).has(GAP), "the path uses the gap left by _is_cell_blocked()")
	layer.free()
