extends Node
## Minimal example of TileSet Coordinate Axes: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/TileSet-Coordinate-Axes
## Choosing the axes in the Inspector is editor-only and not part of this example.


func _ready() -> void:
	# Setting the axes in code
	var tile_set := TileSet.new()
	tile_set.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	tile_set.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_RIGHT
	tile_set.swap_x_and_y_axis = true
	$TileMapLayer.tile_set = tile_set
	var layer: TileMapLayer = $TileMapLayer
	var cell: Vector2i = layer.local_to_map(layer.get_local_mouse_position())
	print("Clicked cell: ", cell) # +X towards the bottom right, +Y towards the top right.

	# Changing the axes of a painted map
	var old_layout: TileSet.TileLayout = tile_set.tile_layout
	var old_swap: bool = tile_set.swap_x_and_y_axis
	var old_flip_x: bool = tile_set.flip_x
	var old_flip_y: bool = tile_set.flip_y
	tile_set.flip_y = true
	$TileMapLayer.convert_cell_coordinates(old_layout, old_swap, old_flip_x, old_flip_y)
	var stored: Vector2i = tile_set.convert_map_coords(Vector2i(1, 2), old_layout, old_swap, old_flip_x, old_flip_y)
	print("Stored cell (1, 2) after flip_y: ", stored)

	# Layout coordinates (flip_y back off, so the values match the comments)
	tile_set.flip_y = false
	tile_set.swap_x_and_y_axis = true
	tile_set.flip_x = true
	print(tile_set.layout_to_map_coords(Vector2i(1, 2))) # (-2, 1)
	print(tile_set.map_to_layout_coords(Vector2i(-2, 1))) # (1, 2)
	get_tree().quit()
