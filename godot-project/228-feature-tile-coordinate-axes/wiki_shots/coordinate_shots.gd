extends Node
## Wiki images of the TileSet coordinate system (engine #261): one picture per combination of
## swap_x_and_y_axis, flip_x and flip_y, named tileset-coordinates-<prefix><swap><flip_x><flip_y>.png (e.g. 101).
##
## --shape picks the tile shape (default square); the prefix keeps the square names unchanged:
##   square       Square                                               prefix ""
##   isometric    Isometric, Diamond Right, horizontal offset axis     prefix "iso-"
##   half-offset  Half-Offset Square, Stacked, horizontal offset axis  prefix "half-"
##   hexagon      Hexagon, Stacked, horizontal offset axis             prefix "hex-"
##
## Each picture is the Inspector group "Coordinate System" on the left and the grid on the right:
## cell coordinates as labels, origin (0, 0) yellow, +X arrow red, +Y arrow green (the colors of the
## TileMap editor's axis arrows).
##
## 1. Inspector crops (optional): take the 8 shots of editor_shots_job.json with the editor-shots tool
##    (inspector of a TileSet with the three bools set; one max_height for all, tuned once so the crop
##    ends below the "Coordinate System" group). They are named inspector-<prefix><code>.png.
## 2. Run this scene with a window (not --headless, it renders):
##      godot --path godot-project res://228-feature-tile-coordinate-axes/wiki_shots/coordinate_shots.tscn \
##            -- --out=<folder> [--shape=square|isometric|half-offset|hexagon] [--inspector-dir=<folder>]
##    It writes grid-<prefix><code>.png and, when the inspector crop exists, the combined
##    tileset-coordinates-<prefix><code>.png (inspector left, grid right), then quits.
## 3. Copy tileset-coordinates-*.png to the wiki: images/TileSet-Coordinate-Axes/.

const CELL := 64
const RANGE := 2 # Cells from -RANGE to RANGE on both axes.
const MARGIN := 24
const GAP := 16
const BACKGROUND := Color(0.129, 0.149, 0.192) # Close to the editor's dark theme.
const GRID := Color(0.55, 0.58, 0.65)
const LABEL := Color(0.86, 0.88, 0.92)
const ORIGIN := Color(0.95, 0.80, 0.25, 0.35)
const X_COLOR := Color(0.96, 0.32, 0.37)
const Y_COLOR := Color(0.53, 0.84, 0.01)

## Shape name: [tile_shape, tile_layout, tile_offset_axis, tile_size, file prefix].
const SHAPES := {
	"square": [TileSet.TILE_SHAPE_SQUARE, TileSet.TILE_LAYOUT_STACKED, TileSet.TILE_OFFSET_AXIS_HORIZONTAL, Vector2i(64, 64), ""],
	"isometric": [TileSet.TILE_SHAPE_ISOMETRIC, TileSet.TILE_LAYOUT_DIAMOND_RIGHT, TileSet.TILE_OFFSET_AXIS_HORIZONTAL, Vector2i(96, 48), "iso-"],
	"half-offset": [TileSet.TILE_SHAPE_HALF_OFFSET_SQUARE, TileSet.TILE_LAYOUT_STACKED, TileSet.TILE_OFFSET_AXIS_HORIZONTAL, Vector2i(64, 64), "half-"],
	"hexagon": [TileSet.TILE_SHAPE_HEXAGON, TileSet.TILE_LAYOUT_STACKED, TileSet.TILE_OFFSET_AXIS_HORIZONTAL, Vector2i(64, 72), "hex-"],
}

var _out_dir := "user://wiki_shots"
var _inspector_dir := ""
var _shape := "square"


class Grid:
	extends Node2D

	var tile_set: TileSet
	var _layer := TileMapLayer.new() # TileSet has no bound map_to_local(); the layer has.

	func _map(cell: Vector2i) -> Vector2:
		_layer.tile_set = tile_set
		return _layer.map_to_local(cell)

	func _draw() -> void:
		if tile_set == null:
			return
		var font := ThemeDB.fallback_font
		for x in range(-RANGE, RANGE + 1):
			for y in range(-RANGE, RANGE + 1):
				var center := _map(Vector2i(x, y))
				var outline := _cell_outline(center)
				if x == 0 and y == 0:
					draw_colored_polygon(outline, ORIGIN)
				var closed := outline.duplicate()
				closed.append(outline[0])
				draw_polyline(closed, GRID, 1.0)
				draw_string(font, center + Vector2(-CELL / 2.0, 5), "%d, %d" % [x, y], HORIZONTAL_ALIGNMENT_CENTER, CELL, 13, LABEL)
		_draw_axis(Vector2i(RANGE, 0), X_COLOR, "+X")
		_draw_axis(Vector2i(0, RANGE), Y_COLOR, "+Y")

	## Outline of one cell (horizontal offset axis: diamonds, pointy-top hexagons).
	func _cell_outline(center: Vector2) -> PackedVector2Array:
		var half := Vector2(tile_set.tile_size) / 2.0
		var points: PackedVector2Array
		match tile_set.tile_shape:
			TileSet.TILE_SHAPE_ISOMETRIC:
				points = [Vector2(0, -half.y), Vector2(half.x, 0), Vector2(0, half.y), Vector2(-half.x, 0)]
			TileSet.TILE_SHAPE_HEXAGON:
				points = [Vector2(0, -half.y), Vector2(half.x, -half.y / 2.0), Vector2(half.x, half.y / 2.0),
						Vector2(0, half.y), Vector2(-half.x, half.y / 2.0), Vector2(-half.x, -half.y / 2.0)]
			_:
				points = [-half, Vector2(half.x, -half.y), half, Vector2(-half.x, half.y)]
		for i in points.size():
			points[i] += center
		return points

	func _draw_axis(target_cell: Vector2i, color: Color, text: String) -> void:
		var origin := _map(Vector2i.ZERO)
		var target := _map(target_cell)
		var direction := (target - origin).normalized()
		var normal := direction.orthogonal()
		var start := origin + direction * CELL * 0.3
		var tip := target - direction * CELL * 0.3
		var head := 12.0
		draw_line(start, tip - direction * head, color, 4.0)
		draw_colored_polygon(PackedVector2Array([tip, tip - direction * head + normal * head * 0.6, tip - direction * head - normal * head * 0.6]), color)
		var font := ThemeDB.fallback_font
		draw_string_outline(font, tip + direction * 14 - Vector2(12, -6), text, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, 4, BACKGROUND)
		draw_string(font, tip + direction * 14 - Vector2(12, -6), text, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, color)


func _ready() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--out="):
			_out_dir = arg.trim_prefix("--out=")
		elif arg.begins_with("--inspector-dir="):
			_inspector_dir = arg.trim_prefix("--inspector-dir=")
		elif arg.begins_with("--shape="):
			_shape = arg.trim_prefix("--shape=")
	if not SHAPES.has(_shape):
		push_error("coordinate_shots: unknown --shape=%s (square, isometric, half-offset, hexagon)" % _shape)
		get_tree().quit(1)
		return
	DirAccess.make_dir_recursive_absolute(_out_dir)
	var shape: Array = SHAPES[_shape]
	var prefix: String = shape[4]

	var tile_sets: Array[TileSet] = []
	for index in 8:
		var tile_set := TileSet.new()
		tile_set.tile_shape = shape[0]
		tile_set.tile_layout = shape[1]
		tile_set.tile_offset_axis = shape[2]
		tile_set.tile_size = shape[3]
		tile_set.swap_x_and_y_axis = (index & 4) != 0
		tile_set.flip_x = (index & 2) != 0
		tile_set.flip_y = (index & 1) != 0
		tile_sets.append(tile_set)

	# One image size for all 8 combinations: cell centers relative to cell (0, 0), grown by half a tile,
	# plus room for the arrow labels.
	var grid := Grid.new()
	var bounds := Rect2()
	for tile_set in tile_sets:
		grid.tile_set = tile_set
		var origin := grid._map(Vector2i.ZERO)
		for x in range(-RANGE, RANGE + 1):
			for y in range(-RANGE, RANGE + 1):
				bounds = bounds.expand(grid._map(Vector2i(x, y)) - origin)
	var half := Vector2(shape[3]) / 2.0
	bounds = bounds.grow_individual(half.x, half.y, half.x, half.y)
	var pad := Vector2(MARGIN + CELL / 2.0, MARGIN + CELL / 2.0)
	var size := Vector2i((bounds.size + 2.0 * pad).ceil())

	var viewport := SubViewport.new()
	viewport.size = size
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	var background := ColorRect.new()
	background.color = BACKGROUND
	background.size = Vector2(size)
	viewport.add_child(background)
	viewport.add_child(grid)

	for tile_set in tile_sets:
		var code := prefix + "%d%d%d" % [int(tile_set.swap_x_and_y_axis), int(tile_set.flip_x), int(tile_set.flip_y)]
		grid.tile_set = tile_set
		grid.position = pad - bounds.position - grid._map(Vector2i.ZERO)
		grid.queue_redraw()
		await RenderingServer.frame_post_draw
		await RenderingServer.frame_post_draw
		var grid_image := viewport.get_texture().get_image()
		grid_image.convert(Image.FORMAT_RGBA8)
		grid_image.save_png(_out_dir.path_join("grid-%s.png" % code))
		_combine(code, grid_image)
	print("coordinate_shots: written to ", ProjectSettings.globalize_path(_out_dir))
	get_tree().quit()


## Inspector crop left, grid right, both centered vertically.
func _combine(code: String, grid_image: Image) -> void:
	if _inspector_dir.is_empty():
		return
	var path := _inspector_dir.path_join("inspector-%s.png" % code)
	if not FileAccess.file_exists(path):
		push_warning("coordinate_shots: no inspector crop " + path)
		return
	var inspector := Image.load_from_file(path)
	if inspector == null:
		return
	inspector.convert(Image.FORMAT_RGBA8)
	var width := inspector.get_width() + GAP + grid_image.get_width()
	var height := maxi(inspector.get_height(), grid_image.get_height())
	var combined := Image.create_empty(width, height, false, Image.FORMAT_RGBA8)
	combined.fill(BACKGROUND)
	combined.blit_rect(inspector, Rect2i(Vector2i.ZERO, inspector.get_size()), Vector2i(0, (height - inspector.get_height()) / 2))
	combined.blit_rect(grid_image, Rect2i(Vector2i.ZERO, grid_image.get_size()), Vector2i(inspector.get_width() + GAP, (height - grid_image.get_height()) / 2))
	combined.save_png(_out_dir.path_join("tileset-coordinates-%s.png" % code))
