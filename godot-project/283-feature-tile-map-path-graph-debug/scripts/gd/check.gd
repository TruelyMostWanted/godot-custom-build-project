extends FeatureCheck
## GDScript checks of 283-feature-tile-map-path-graph-debug (TileMapPathGraph.draw_debug()).
## Check the same things as scripts/cs/Issue283FeatureTileMapPathGraphDebugCheck.cs.

const FLOOR := Vector2i(0, 0)
const WALL := Vector2i(1, 0) # Custom data "blocked" = true.
const MUD := Vector2i(2, 0) # Custom data "cost" = 20.
## Every side step changes x by at most 1, so the wall in column 3 splits the map except at the gap.
const MAP := Rect2i(0, 0, 7, 5)
const FROM := Vector2i(0, 2)
const TO := Vector2i(6, 2)
const GAP := Vector2i(3, 4)
const MUD_CELL := Vector2i(1, 2)


func _get_check_name() -> String:
	return "283-feature-tile-map-path-graph-debug"


func _run_checks() -> void:
	expect(ClassDB.class_has_method("TileMapPathGraph", "draw_debug"), "TileMapPathGraph has draw_debug()")
	await _check_shape("square", TileSet.TILE_SHAPE_SQUARE)
	await _check_shape("stacked isometric", TileSet.TILE_SHAPE_ISOMETRIC)
	await _check_shape("hexagon", TileSet.TILE_SHAPE_HEXAGON)


func _tile_set(shape: TileSet.TileShape) -> TileSet:
	var tile_set := TileSet.new()
	tile_set.tile_shape = shape
	tile_set.tile_size = Vector2i(16, 16)
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


func _check_shape(label: String, shape: TileSet.TileShape) -> void:
	var layer := TileMapLayer.new()
	layer.tile_set = _tile_set(shape)
	for y in range(MAP.position.y, MAP.end.y):
		for x in range(MAP.position.x, MAP.end.x):
			var cell := Vector2i(x, y)
			layer.set_cell(cell, 0, WALL if x == 3 and cell != GAP else FLOOR)
	layer.set_cell(MUD_CELL, 0, MUD)
	add_child(layer)

	var options := TileMapPathGraphOptions.new()
	options.blocked_data_layer = "blocked"
	options.cost_data_layer = "cost"
	var graph := TileMapPathGraph.create(layer, options)

	# The overlay draws what the pathfinder uses: no path cell is drawn as blocked,
	# the wall is, and the mud cell is the most expensive (fully shaded) cell.
	var path := graph.get_cell_path(FROM, TO)
	expect(not path.is_empty() and path.has(GAP), label + ": the path goes through the gap")
	expect(path.filter(func(cell: Vector2i) -> bool: return graph.is_cell_blocked(cell)).is_empty(), label + ": no path cell is drawn as blocked")
	expect(graph.is_cell_blocked(Vector2i(3, 0)) and not graph.is_cell_blocked(GAP), label + ": wall cells are drawn as blocked")
	expect(is_equal_approx(graph.get_cell_cost(MUD_CELL), 20.0) and is_equal_approx(graph.get_cell_cost(Vector2i(0, 0)), 1.0), label + ": mud is shaded, floor is not")
	expect(not path.has(MUD_CELL), label + ": the path avoids the shaded cell")

	# The documented recipe: a Node2D child of the layer draws the graph in _draw() and redraws on updated.
	var overlay := Node2D.new()
	var draws := [0]
	var on_draw := func() -> void:
		graph.draw_debug(overlay.get_canvas_item())
		graph.draw_debug(overlay.get_canvas_item(), Rect2i(2, 1, 3, 3), Color.RED, Color.BLUE)
		draws[0] += 1
	overlay.draw.connect(on_draw)
	graph.updated.connect(overlay.queue_redraw)
	layer.add_child(overlay)
	await get_tree().process_frame
	expect(draws[0] >= 1, label + ": draw_debug() runs in _draw()")
	var before: int = draws[0]
	layer.set_cell(GAP, 0, WALL)
	graph.update_cells([GAP])
	await get_tree().process_frame
	expect(draws[0] > before, label + ": updated redraws the overlay")
	expect(graph.get_cell_path(FROM, TO).is_empty(), label + ": closed wall, no path")

	# A canvas item of its own, without a node.
	var canvas_item := RenderingServer.canvas_item_create()
	RenderingServer.canvas_item_set_parent(canvas_item, layer.get_canvas_item())
	graph.draw_debug(canvas_item, Rect2i(100, 100, 4, 4)) # Outside the region: nothing drawn, no error.
	graph.draw_debug(canvas_item)
	RenderingServer.free_rid(canvas_item)

	graph.updated.disconnect(overlay.queue_redraw)
	layer.queue_free()
	await get_tree().process_frame
