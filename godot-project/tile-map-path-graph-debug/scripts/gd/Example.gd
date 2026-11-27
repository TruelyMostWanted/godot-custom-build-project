extends Node2D
## Minimal example of Tile Map Path Graph Debug: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Tile-Map-Path-Graph-Debug
## A child of the painted layer of the scene (map and options from tile-map-pathfinding/resources).
## "In the TileMap editor" is editor-only and not part of this example.

const OPTIONS_PATH := "res://tile-map-pathfinding/resources/path_graph_options.tres"

var graph: TileMapPathGraph
var _shown := Rect2i()


# Runtime overlay in 4 steps
func _ready() -> void:
	if not OS.is_debug_build():
		return
	graph = TileMapPathGraph.create(get_parent(), load(OPTIONS_PATH))
	graph.updated.connect(queue_redraw)
	queue_redraw()
	await get_tree().process_frame
	await get_tree().process_frame
	print("Drawn cells: ", _shown)

	# Colors and shading
	graph.draw_debug(get_canvas_item(), Rect2i(), Color(0.8, 0, 0.8, 0.6), Color(0, 0.6, 1, 0.6))
	print("Wall blocked: ", graph.is_cell_blocked(Vector2i(4, 0)))
	print("Cost of the shaded cell: ", graph.get_cell_cost(Vector2i(2, 2)))
	get_tree().quit()


# Large maps: draw only the visible cells
func _process(_delta: float) -> void:
	var cells: Rect2i = _visible_cells()
	if cells != _shown:
		_shown = cells
		queue_redraw()


func _draw() -> void:
	if graph:
		graph.draw_debug(get_canvas_item(), _shown)


func _visible_cells() -> Rect2i:
	var layer := get_parent() as TileMapLayer
	var to_local: Transform2D = get_global_transform_with_canvas().affine_inverse()
	var screen: Rect2 = get_viewport_rect()
	var rect := Rect2i(layer.local_to_map(to_local * screen.position), Vector2i())
	rect = rect.expand(layer.local_to_map(to_local * screen.end))
	rect = rect.expand(layer.local_to_map(to_local * Vector2(screen.end.x, screen.position.y)))
	rect = rect.expand(layer.local_to_map(to_local * Vector2(screen.position.x, screen.end.y)))
	return rect.grow(1)
