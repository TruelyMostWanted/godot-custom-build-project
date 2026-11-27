extends Node
## Minimal example of Tile Map Pathfinding: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Tile-Map-Pathfinding
## Steps 1 to 4 (custom data layers, painting) are editor work: resources/ground.tres and the painted Ground layer.

@onready var ground: TileMapLayer = $Ground
var graph: TileMapPathGraph


func _ready() -> void:
	# Grid pathfinding in 6 steps
	var options := TileMapPathGraphOptions.new()
	options.blocked_data_layer = "blocked"
	options.cost_data_layer = "cost"
	graph = TileMapPathGraph.create(ground, options)
	print("Path around the wall: ", walk(Vector2i(0, 2), Vector2i(7, 2)))

	# Update the graph when cells change
	open_door(Vector2i(4, 2))
	print("Path through the door: ", walk(Vector2i(0, 2), Vector2i(7, 2)))

	# Diagonal steps and corner cutting
	options.neighbor_mode = TileMapPathGraphOptions.NEIGHBOR_MODE_SIDES_AND_CORNERS
	options.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_AT_LEAST_ONE_WALKABLE
	graph.update_all()
	print("Path with corner steps: ", walk(Vector2i(0, 0), Vector2i(7, 4)))

	# Costs and walls from code (RiverGraph.gd)
	var river_graph := RiverGraph.new()
	river_graph.set_layer(ground)
	river_graph.update_all()
	print("Cost of shallow water: ", river_graph.get_cell_cost(Vector2i(2, 2)))

	# Large maps
	graph.get_astar_grid().jumping_enabled = true
	var partial: Array[Vector2i] = graph.get_cell_path(Vector2i(0, 0), Vector2i(4, 0), true)
	print("Partial path to deep water: ", partial)
	get_tree().quit()


func walk(from: Vector2i, to: Vector2i) -> PackedVector2Array:
	return graph.get_point_path(from, to) # Local positions of the layer.


func open_door(cell: Vector2i) -> void:
	ground.set_cell(cell, 0, Vector2i(0, 0)) # A floor tile.
	graph.update_cells([cell])
