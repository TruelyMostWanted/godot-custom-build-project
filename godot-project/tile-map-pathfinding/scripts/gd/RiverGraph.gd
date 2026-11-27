class_name RiverGraph extends TileMapPathGraph
## The "Costs and walls from code" snippet of the guide: terrains of resources/ground.tres decide walls and costs.


func _is_cell_blocked(_cell: Vector2i, tile_data: TileData) -> bool:
	return tile_data == null or tile_data.terrain == 2 # Deep water.


func _get_cell_cost(_cell: Vector2i, tile_data: TileData) -> float:
	if tile_data and tile_data.terrain == 1:
		return 2.0 # Shallow water is slow.
	return 1.0
