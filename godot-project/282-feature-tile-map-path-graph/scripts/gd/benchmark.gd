extends Node
## Benchmark of 282-feature-tile-map-path-graph: build time, memory and path query time of a 400x400 map
## for both backends (grid: square, graph: hexagon). Prints a Markdown table and quits.
## Run on desktop: godot --path godot-project res://282-feature-tile-map-path-graph/scenes/benchmark.tscn
## Run on the Web: export with this scene as main scene and read the browser console.
## Memory is OS.get_static_memory_usage(), which counts engine allocations only in builds with memory tracking
## (editor and debug templates); a release template shows 0.

const SIZE := 400
const QUERIES := 20
const FLOOR := Vector2i(0, 0)
const WALL := Vector2i(1, 0)


func _ready() -> void:
	print("| Backend | Map | Cells | Build | Memory | Path query (corner to corner) | update_cells (100 cells) |")
	print("|---|---|---|---|---|---|---|")
	_measure("grid", "square", TileSet.TILE_SHAPE_SQUARE)
	_measure("graph", "hexagon", TileSet.TILE_SHAPE_HEXAGON)
	get_tree().quit()


func _measure(backend: String, map: String, shape: TileSet.TileShape) -> void:
	var tile_set := TileSet.new()
	tile_set.tile_shape = shape
	tile_set.tile_size = Vector2i(16, 16)
	tile_set.add_custom_data_layer()
	tile_set.set_custom_data_layer_name(0, "blocked")
	tile_set.set_custom_data_layer_type(0, TYPE_BOOL)
	var atlas := TileSetAtlasSource.new()
	atlas.texture = ImageTexture.create_from_image(Image.create_empty(32, 16, false, Image.FORMAT_RGBA8))
	tile_set.add_source(atlas, 0)
	atlas.create_tile(FLOOR)
	atlas.create_tile(WALL)
	atlas.get_tile_data(WALL, 0).set_custom_data("blocked", true)

	# Walls every 20 columns with a gap that alternates between top and bottom, so paths wind through the map.
	var layer := TileMapLayer.new()
	layer.tile_set = tile_set
	for y in SIZE:
		for x in SIZE:
			var wall: bool = (x % 40 == 10 and y < SIZE - 5) or (x % 40 == 30 and y > 4)
			layer.set_cell(Vector2i(x, y), 0, WALL if wall else FLOOR)

	var options := TileMapPathGraphOptions.new()
	options.blocked_data_layer = "blocked"
	var memory_before := OS.get_static_memory_usage()
	var start := Time.get_ticks_usec()
	var graph := TileMapPathGraph.create(layer, options)
	var build_ms := (Time.get_ticks_usec() - start) / 1000.0
	var memory_mb := (OS.get_static_memory_usage() - memory_before) / 1048576.0

	start = Time.get_ticks_usec()
	var length := 0
	for i in QUERIES:
		length = graph.get_cell_path(Vector2i(0, 0), Vector2i(SIZE - 1, SIZE - 1)).size()
	var query_ms := (Time.get_ticks_usec() - start) / 1000.0 / QUERIES

	var cells: Array[Vector2i] = []
	for i in 100:
		cells.append(Vector2i(i * 3 % SIZE, i * 7 % SIZE))
	start = Time.get_ticks_usec()
	graph.update_cells(cells)
	var update_ms := (Time.get_ticks_usec() - start) / 1000.0

	print("| %s | %s | %d | %.1f ms | %.1f MB | %.2f ms (%d cells) | %.2f ms |" % [backend, map, SIZE * SIZE, build_ms, memory_mb, query_ms, length, update_ms])
	layer.free()
