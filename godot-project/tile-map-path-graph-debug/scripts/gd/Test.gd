extends FeatureTest
## Full API test of Tile Map Path Graph Debug (TileMapPathGraph.draw_debug(), the updated signal and the values the
## overlay draws). Map and options from tile-map-pathfinding/resources. Same tests as scripts/cs/Test.cs.

const GROUND: TileSet = preload("res://tile-map-pathfinding/resources/ground.tres")
const OPTIONS: TileMapPathGraphOptions = preload("res://tile-map-pathfinding/resources/path_graph_options.tres")
const FLOOR := Vector2i(0, 0)
const WALL := Vector2i(1, 0) # Custom data "blocked" = true.
const MUD := Vector2i(2, 0) # Custom data "cost" = 20.
const MAP := Rect2i(0, 0, 7, 5)
const FLOOR_CELL := Vector2i(0, 0)
const WALL_CELL := Vector2i(3, 0)
const MUD_CELL := Vector2i(1, 2)
const OUTSIDE := Rect2i(100, 100, 4, 4)

var _graph: TileMapPathGraph
var _overlay: Node2D
var _draws: int = 0


func _run_tests() -> void:
	await _test_shape("square", TileSet.TILE_SHAPE_SQUARE)
	await _test_shape("stacked isometric", TileSet.TILE_SHAPE_ISOMETRIC)
	await _test_shape("hexagon", TileSet.TILE_SHAPE_HEXAGON)


func _test_shape(label: String, shape: TileSet.TileShape) -> void:
	var tile_set: TileSet = GROUND.duplicate(true)
	tile_set.tile_shape = shape
	var layer := TileMapLayer.new()
	layer.tile_set = tile_set
	for y in range(MAP.position.y, MAP.end.y):
		for x in range(MAP.position.x, MAP.end.x):
			layer.set_cell(Vector2i(x, y), 0, FLOOR)
	layer.set_cell(WALL_CELL, 0, WALL)
	layer.set_cell(MUD_CELL, 0, MUD)
	add_child(layer)
	_graph = TileMapPathGraph.create(layer, OPTIONS)
	expect(_graph.is_cell_blocked(WALL_CELL), label + ": a wall cell is drawn as blocked")
	expect(not _graph.is_cell_blocked(FLOOR_CELL), label + ": a floor cell is not drawn as blocked")
	expect_equal(_graph.get_cell_cost(MUD_CELL), 20.0, label + ": the mud cell is the most expensive (fully shaded) cell")

	# The recipe of the guide: a Node2D child of the layer draws in _draw() and redraws on updated.
	_overlay = Node2D.new()
	_overlay.draw.connect(_on_overlay_draw)
	_graph.updated.connect(_overlay.queue_redraw)
	_draws = 0
	layer.add_child(_overlay)
	await get_tree().process_frame
	expect(_draws > 0, label + ": draw_debug() runs in _draw() (all cells, a rect, own colors, a rect outside)")
	var before: int = _draws
	layer.set_cell(FLOOR_CELL, 0, WALL)
	_graph.update_cells([FLOOR_CELL])
	await get_tree().process_frame
	expect(_draws > before, label + ": updated redraws the overlay")
	expect(_graph.is_cell_blocked(FLOOR_CELL), label + ": the overlay draws the updated cell as blocked")
	layer.queue_free()
	await get_tree().process_frame


func _on_overlay_draw() -> void:
	var canvas_item: RID = _overlay.get_canvas_item()
	_graph.draw_debug(canvas_item)
	_graph.draw_debug(canvas_item, Rect2i(2, 1, 3, 3), Color.RED, Color.BLUE)
	_graph.draw_debug(canvas_item, OUTSIDE) # Outside the region: draws nothing.
	_draws += 1
