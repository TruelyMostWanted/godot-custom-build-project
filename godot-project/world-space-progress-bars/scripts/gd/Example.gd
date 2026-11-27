extends Node
## Minimal example of World Space Progress Bars: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/World-Space-Progress-Bars


func _ready() -> void:
	# Fill directions
	var bar := ProgressBar.new()
	bar.fill_mode = ProgressBar.FILL_BOTTOM_TO_TOP
	bar.custom_minimum_size = Vector2(12, 80)
	bar.show_percentage = false
	bar.value = 40
	print("Vertical bar value: ", bar.value)

	# A separate look for vertical bars
	var theme := Theme.new()
	theme.set_type_variation("VProgressBar", "ProgressBar")
	var fill := StyleBoxFlat.new()
	fill.bg_color = Color.ORANGE
	theme.set_stylebox("fill", "VProgressBar", fill)
	bar.theme = theme
	bar.theme_type_variation = &"VProgressBar"
	print("Type variation: ", bar.theme_type_variation)
	bar.free()

	# A health bar over a unit
	var unit := Node2D.new()
	add_child(unit)
	var unit_bar := ProgressBar.new()
	unit_bar.show_percentage = false
	unit_bar.size = Vector2(32, 4)
	unit_bar.position = Vector2(-16, -24)
	unit_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	unit.add_child(unit_bar)
	unit_bar.max_value = 100.0
	unit_bar.value = 75.0
	print("Control bar over the unit: ", unit_bar.value, " / ", unit_bar.max_value)

	# Give a unit a health bar
	var bar_style: ProgressBarStyle = load("res://world-space-progress-bars/resources/health_bar.tres")
	var max_health := 100.0
	var health := 100.0
	var health_bar: RID = ProgressBarServer.bar_create(unit, bar_style)
	health = maxf(health - 30.0, 0.0)
	ProgressBarServer.bar_set_ratio(health_bar, health / max_health)
	print("Server bar ratio after 30 damage: ", ProgressBarServer.bar_get_ratio(health_bar))

	# Make a style
	var style := ProgressBarStyle.new()
	style.size = Vector2(40, 5)
	style.offset = Vector2(0, -32)
	var green := StyleBoxFlat.new()
	green.bg_color = Color.GREEN
	style.fill = green
	style.hide_when_full = true
	ProgressBarServer.bar_set_style(health_bar, style)
	print("Style size: ", style.size)

	# Keep bars upright on rotating units
	style.follow_mode = ProgressBarStyle.FOLLOW_MODE_POSITION_ONLY
	print("Follow mode: ", style.follow_mode)

	# Update thousands of bars at once
	var mana_bar: RID = ProgressBarServer.bar_create(unit, style)
	ProgressBarServer.bar_set_offset(mana_bar, Vector2(0, 6))
	var bars: Array[RID] = [health_bar, mana_bar]
	var ratios := PackedFloat32Array([0.5, 0.25])
	ProgressBarServer.bars_set_ratios(bars, ratios)
	print("Bars: ", ProgressBarServer.get_bar_count(), ", mana ratio: ", ProgressBarServer.bar_get_ratio(mana_bar))

	ProgressBarServer.bar_free(health_bar)
	ProgressBarServer.bar_free(mana_bar)
	get_tree().quit()
