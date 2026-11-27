extends FeatureTest
## Full API test of Tile Map Pathfinding (TileMapPathGraph, TileMapPathGraphOptions). Same tests as scripts/cs/Test.cs.
## draw_debug() is tested in tile-map-path-graph-debug.

const GROUND: TileSet = preload("res://tile-map-pathfinding/resources/ground.tres")
const OPTIONS: TileMapPathGraphOptions = preload("res://tile-map-pathfinding/resources/path_graph_options.tres")
const FLOOR := Vector2i(0, 0)
const WALL := Vector2i(1, 0) # Deep water: custom data "blocked" = true, terrain 2.
const MUD := Vector2i(2, 0) # Shallow water: custom data "cost" = 20, terrain 1.
## Negative and positive cells. Every side step changes x by at most 1, so the column x = 0 splits the map.
const MAP := Rect2i(-4, -3, 9, 7)
const FROM := Vector2i(-4, 0)
const TO := Vector2i(4, 0)
## Two gap cells in the wall column, one in an even and one in an odd row: odd rows of stacked isometric maps are
## shifted half a cell, so a single gap cell cannot be passed with side neighbors only.
const GAP := Vector2i(0, 3)
const GAP2 := Vector2i(0, 2)
const LOWEST_ID := 0
const HIGHEST_ID := 4294967295

var _updates: int = 0


func _run_tests() -> void:
	_test_options_defaults()
	_test_new_graph()
	_test_point_ids()
	_test_map("square", TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, false, TileMapPathGraph.BACKEND_GRID)
	_test_map("diamond isometric", TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_DIAMOND_RIGHT, false, TileMapPathGraph.BACKEND_GRID)
	_test_map("stacked isometric", TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_STACKED, false, TileMapPathGraph.BACKEND_GRAPH)
	_test_map("half-offset square", TileSet.TILE_SHAPE_HALF_OFFSET_SQUARE, TileSet.TILE_LAYOUT_STACKED, false, TileMapPathGraph.BACKEND_GRAPH)
	_test_map("hexagon", TileSet.TILE_SHAPE_HEXAGON, TileSet.TILE_LAYOUT_STACKED, false, TileMapPathGraph.BACKEND_GRAPH)
	_test_map("square, swapped and flipped", TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, true, TileMapPathGraph.BACKEND_GRID)
	_test_map("hexagon, swapped and flipped", TileSet.TILE_SHAPE_HEXAGON, TileSet.TILE_LAYOUT_STACKED, true, TileMapPathGraph.BACKEND_GRAPH)
	_test_corners()
	_test_empty_cells()
	_test_virtuals()


## A copy of resources/ground.tres with another shape. swapped = swap_x_and_y_axis and flip_y.
func _tile_set(shape: TileSet.TileShape, layout: TileSet.TileLayout, swapped: bool) -> TileSet:
	var tile_set: TileSet = GROUND.duplicate(true)
	tile_set.tile_shape = shape
	tile_set.tile_layout = layout
	tile_set.swap_x_and_y_axis = swapped
	tile_set.flip_y = swapped
	return tile_set


func _layer(tile_set: TileSet, rect: Rect2i) -> TileMapLayer:
	var layer := TileMapLayer.new()
	layer.tile_set = tile_set
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			layer.set_cell(Vector2i(x, y), 0, FLOOR)
	return layer


## From FROM to TO, every step to a side neighbor, no blocked cell.
func _is_valid_path(graph: TileMapPathGraph, layer: TileMapLayer, path: Array[Vector2i]) -> bool:
	if path.is_empty() or path[0] != FROM or path[-1] != TO:
		return false
	for i in path.size():
		if graph.is_cell_blocked(path[i]):
			return false
		if i > 0 and not layer.get_surrounding_cells(path[i - 1]).has(path[i]):
			return false
	return true


func _passes_gap(path: Array[Vector2i]) -> bool:
	return path.has(GAP) or path.has(GAP2)


func _test_options_defaults() -> void:
	var options := TileMapPathGraphOptions.new()
	expect_equal(options.neighbor_mode, TileMapPathGraphOptions.NEIGHBOR_MODE_SIDES, "neighbor_mode defaults to NEIGHBOR_MODE_SIDES")
	expect_equal(options.diagonal_mode, AStarGrid2D.DIAGONAL_MODE_ONLY_IF_NO_OBSTACLES, "diagonal_mode defaults to no corner cutting")
	expect_equal(options.default_cost, 1.0, "default_cost defaults to 1")
	expect(options.empty_cells_blocked, "empty_cells_blocked defaults to true")
	expect_equal(options.cost_data_layer, "", "cost_data_layer defaults to empty")
	expect_equal(options.blocked_data_layer, "", "blocked_data_layer defaults to empty")
	expect_equal(TileMapPathGraphOptions.NEIGHBOR_MODE_SIDES_AND_CORNERS, 1, "NEIGHBOR_MODE_SIDES_AND_CORNERS is 1")
	expect_equal(TileMapPathGraphOptions.NEIGHBOR_MODE_MAX, 2, "NEIGHBOR_MODE_MAX is 2")
	expect_equal(OPTIONS.blocked_data_layer, "blocked", "the sample options .tres loads")


func _test_new_graph() -> void:
	var graph := TileMapPathGraph.new()
	expect(graph.options != null, "a new graph has default options")
	graph.options = null
	expect(graph.options != null, "setting options to null restores the defaults")
	expect(graph.get_layer() == null, "a new graph has no layer")
	expect(graph.get_cell_path(Vector2i(), Vector2i(1, 0)).is_empty(), "an unbuilt graph has no paths")
	expect(graph.is_cell_blocked(Vector2i()), "an unbuilt graph blocks every cell")
	expect_equal(TileMapPathGraph.BACKEND_MAX, 2, "BACKEND_MAX is 2")


func _test_point_ids() -> void:
	expect_equal(TileMapPathGraph.cell_to_point_id(Vector2i(-32768, -32768)), LOWEST_ID, "the lowest cell has id 0")
	expect_equal(TileMapPathGraph.cell_to_point_id(Vector2i(32767, 32767)), HIGHEST_ID, "the highest cell has id 2^32 - 1")
	var astar := AStar2D.new()
	for cell: Vector2i in [Vector2i(-1, -1), Vector2i(-32768, 32767), Vector2i(32767, -32768), Vector2i(5, -7)]:
		var id: int = TileMapPathGraph.cell_to_point_id(cell)
		expect_equal(TileMapPathGraph.point_id_to_cell(id), cell, "point_id_to_cell() reverses cell_to_point_id() for %s" % cell)
		astar.add_point(id, Vector2(cell))
		expect(astar.has_point(id), "AStar2D accepts the id of %s" % cell)


func _test_map(label: String, shape: TileSet.TileShape, layout: TileSet.TileLayout, swapped: bool, backend: TileMapPathGraph.Backend) -> void:
	var layer: TileMapLayer = _layer(_tile_set(shape, layout, swapped), MAP)
	var graph: TileMapPathGraph = TileMapPathGraph.create(layer, OPTIONS)
	_updates = 0
	graph.updated.connect(_on_updated)
	expect_equal(graph.get_backend(), backend, label + ": get_backend()")
	expect_equal(graph.get_astar_grid() != null, backend == TileMapPathGraph.BACKEND_GRID, label + ": get_astar_grid() only on the grid backend")
	expect_equal(graph.get_astar() != null, backend == TileMapPathGraph.BACKEND_GRAPH, label + ": get_astar() only on the graph backend")
	expect_equal(graph.get_region(), MAP, label + ": get_region() is the used rect")
	expect(graph.get_layer() == layer, label + ": get_layer() is the layer")
	var path: Array[Vector2i] = graph.get_cell_path(FROM, TO)
	expect(_is_valid_path(graph, layer, path), label + ": path on the open map")
	var points: PackedVector2Array = graph.get_point_path(FROM, TO)
	expect_equal(points.size(), path.size(), label + ": get_point_path() has one point per cell")
	expect(points[-1].is_equal_approx(layer.map_to_local(TO)), label + ": get_point_path() returns local positions")

	var wall: Array[Vector2i] = []
	for y in range(MAP.position.y, MAP.end.y):
		var cell := Vector2i(0, y)
		if cell != GAP and cell != GAP2:
			layer.set_cell(cell, 0, WALL)
			wall.append(cell)
	graph.update_cells(wall)
	expect_equal(_updates, 1, label + ": update_cells() emits updated")
	expect(graph.is_cell_blocked(Vector2i(0, 0)), label + ": wall cells are blocked")
	expect(not graph.is_cell_blocked(GAP), label + ": the gap is walkable")
	path = graph.get_cell_path(FROM, TO)
	expect(_is_valid_path(graph, layer, path), label + ": path with a wall")
	expect(_passes_gap(path), label + ": the path goes through the gap")

	layer.set_cell(GAP, 0, WALL)
	layer.set_cell(GAP2, 0, WALL)
	graph.update_cells([FROM])
	expect(_passes_gap(graph.get_cell_path(FROM, TO)), label + ": update_cells() recomputes only the given cells")
	graph.update_cells([GAP, GAP2])
	expect(graph.get_cell_path(FROM, TO).is_empty(), label + ": closed wall, no path")
	var partial: Array[Vector2i] = graph.get_cell_path(FROM, TO, true)
	expect(partial[-1].x < 0, label + ": allow_partial_path ends before the wall")

	for cell in wall:
		layer.set_cell(cell, 0, MUD)
	layer.set_cell(GAP, 0, FLOOR)
	layer.set_cell(GAP2, 0, FLOOR)
	wall.append(GAP)
	wall.append(GAP2)
	graph.update_cells(wall)
	expect_equal(graph.get_cell_cost(Vector2i(0, 0)), 20.0, label + ": cost from the custom data layer")
	expect_equal(graph.get_cell_cost(GAP), 1.0, label + ": unpainted cost is default_cost")
	expect(_passes_gap(graph.get_cell_path(FROM, TO)), label + ": the cheap way goes through the gap")
	layer.free()


func _test_corners() -> void:
	var layer: TileMapLayer = _layer(_tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, false), Rect2i(0, 0, 7, 7))
	var options: TileMapPathGraphOptions = OPTIONS.duplicate()
	var graph: TileMapPathGraph = TileMapPathGraph.create(layer, options)
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(6, 6)).size(), 13, "sides only: 13 cells to the opposite corner")
	options.neighbor_mode = TileMapPathGraphOptions.NEIGHBOR_MODE_SIDES_AND_CORNERS
	graph.update_all()
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(6, 6)).size(), 7, "sides and corners: 7 cells on the diagonal")
	layer.set_cell(Vector2i(1, 0), 0, WALL)
	graph.update_cells([Vector2i(1, 0)])
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(1, 1)).size(), 3, "DIAGONAL_MODE_ONLY_IF_NO_OBSTACLES: no corner cutting past a wall")
	options.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_AT_LEAST_ONE_WALKABLE
	graph.update_all()
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(1, 1)).size(), 2, "DIAGONAL_MODE_AT_LEAST_ONE_WALKABLE: corner step past a wall")
	layer.free()


func _test_empty_cells() -> void:
	var layer: TileMapLayer = _layer(_tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, false), Rect2i(0, 0, 5, 1))
	layer.erase_cell(Vector2i(2, 0))
	var graph: TileMapPathGraph = TileMapPathGraph.create(layer)
	expect(graph.is_cell_blocked(Vector2i(2, 0)), "empty cells are blocked by default")
	expect(graph.get_cell_path(Vector2i(0, 0), Vector2i(4, 0)).is_empty(), "no path through a hole")
	graph.options.empty_cells_blocked = false
	graph.options.default_cost = 2.0
	expect_equal(graph.update_all(), OK, "update_all() returns OK")
	expect(not graph.is_cell_blocked(Vector2i(2, 0)), "empty_cells_blocked = false: the hole is walkable")
	expect_equal(graph.get_cell_cost(Vector2i(2, 0)), 2.0, "a walkable empty cell costs default_cost")
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(4, 0)).size(), 5, "path through the hole")
	layer.set_cell(Vector2i(5, 0), 0, FLOOR)
	graph.update_cells([Vector2i(5, 0)])
	expect_equal(graph.get_region(), Rect2i(0, 0, 6, 1), "update_cells() outside the region grows it")
	expect_equal(graph.get_cell_path(Vector2i(0, 0), Vector2i(5, 0)).size(), 6, "path to the new cell")
	layer.free()
	expect(graph.get_layer() == null, "get_layer() is null after the layer is freed")


func _test_virtuals() -> void:
	var layer: TileMapLayer = _layer(_tile_set(TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, false), MAP)
	layer.set_cell(Vector2i(0, 0), 0, WALL)
	layer.set_cell(Vector2i(1, 0), 0, MUD)
	layer.erase_cell(Vector2i(2, 0))
	var graph := RiverGraph.new()
	graph.set_layer(layer)
	expect_equal(graph.update_all(), OK, "a script subclass builds")
	expect(graph.is_cell_blocked(Vector2i(0, 0)), "_is_cell_blocked(): deep water is blocked")
	expect(graph.is_cell_blocked(Vector2i(2, 0)), "_is_cell_blocked() gets null tile_data for an empty cell")
	expect(not graph.is_cell_blocked(Vector2i(0, 1)), "_is_cell_blocked(): floor is walkable")
	expect_equal(graph.get_cell_cost(Vector2i(1, 0)), 2.0, "_get_cell_cost(): shallow water costs 2")
	expect_equal(graph.get_cell_cost(Vector2i(0, 1)), 1.0, "_get_cell_cost(): floor costs 1")
	layer.free()


func _on_updated() -> void:
	_updates += 1
