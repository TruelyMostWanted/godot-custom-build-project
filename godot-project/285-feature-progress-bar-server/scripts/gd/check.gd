extends FeatureCheck
## GDScript checks of 285-feature-progress-bar-server. Check the same things as scripts/cs/Issue285FeatureProgressBarServerCheck.cs.

const BAR_COUNT := 1000
const BENCH_FRAMES := 60


func _get_check_name() -> String:
	return "285-feature-progress-bar-server"


func _make_style() -> ProgressBarStyle:
	var style := ProgressBarStyle.new()
	var background := StyleBoxFlat.new()
	background.bg_color = Color(0, 0, 0, 0.6)
	var fill := StyleBoxFlat.new()
	fill.bg_color = Color.GREEN
	style.background = background
	style.fill = fill
	return style


func _run_checks() -> void:
	expect(Engine.has_singleton("ProgressBarServer"), "ProgressBarServer is a registered singleton")
	expect(ClassDB.class_exists("ProgressBarStyle"), "ProgressBarStyle is registered")

	# Style defaults.
	var defaults := ProgressBarStyle.new()
	expect_equal(defaults.size, Vector2(32, 4), "default size")
	expect_equal(defaults.offset, Vector2(0, -20), "default offset")
	expect_equal(defaults.fill_mode, ProgressBar.FILL_BEGIN_TO_END, "default fill_mode")
	expect_equal(defaults.follow_mode, ProgressBarStyle.FOLLOW_MODE_INHERIT, "default follow_mode")

	var base_count := ProgressBarServer.get_bar_count()
	var style := _make_style()

	# Single bar: ratio, clamping, setters.
	var unit := Node2D.new()
	add_child(unit)
	var bar := ProgressBarServer.bar_create(unit, style)
	expect(bar.is_valid(), "bar_create returns a valid RID")
	expect_equal(ProgressBarServer.get_bar_count(), base_count + 1, "bar count grows")
	expect(is_equal_approx(ProgressBarServer.bar_get_ratio(bar), 1.0), "new bars are full")
	ProgressBarServer.bar_set_ratio(bar, 0.25)
	expect(is_equal_approx(ProgressBarServer.bar_get_ratio(bar), 0.25), "bar_set_ratio")
	ProgressBarServer.bar_set_ratio(bar, 5.0)
	expect(is_equal_approx(ProgressBarServer.bar_get_ratio(bar), 1.0), "ratio is clamped to 1")
	ProgressBarServer.bar_set_offset(bar, Vector2(0, -8))
	ProgressBarServer.bar_set_modulate(bar, Color.RED)
	ProgressBarServer.bar_set_visible(bar, false)
	ProgressBarServer.bar_set_style(bar, ProgressBarStyle.new()) # Falls back to the ProgressBar theme.
	style.fill_mode = ProgressBar.FILL_BOTTOM_TO_TOP # Redraws all bars of the style.

	# Position-only bars on a rotated unit: updated once per frame (only when drawing), no errors.
	var upright := _make_style()
	upright.follow_mode = ProgressBarStyle.FOLLOW_MODE_POSITION_ONLY
	ProgressBarServer.bar_set_style(bar, upright)
	unit.rotation = PI / 2
	await get_tree().process_frame
	await get_tree().process_frame

	ProgressBarServer.bar_free(bar)
	expect_equal(ProgressBarServer.get_bar_count(), base_count, "bar_free frees the bar")
	unit.queue_free()

	# Freeing a unit frees its bars (no leaked RIDs).
	var doomed := Node2D.new()
	add_child(doomed)
	ProgressBarServer.bar_create(doomed, style)
	ProgressBarServer.bar_create(doomed, style)
	expect_equal(ProgressBarServer.get_bar_count(), base_count + 2, "two bars on one unit")
	doomed.queue_free()
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(ProgressBarServer.get_bar_count(), base_count, "deleting the unit frees its bars")

	# A unit removed from the tree but kept alive keeps its bars until it is freed.
	var pooled := Node2D.new()
	add_child(pooled)
	ProgressBarServer.bar_create(pooled, style)
	remove_child(pooled)
	await get_tree().process_frame
	expect_equal(ProgressBarServer.get_bar_count(), base_count + 1, "a unit outside the tree keeps its bars")
	pooled.free()
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(ProgressBarServer.get_bar_count(), base_count, "a unit freed outside the tree frees its bars")

	await _benchmark(style, base_count)


## 1000 bars with values changing every frame: CPU time of the bulk update per frame.
func _benchmark(style: ProgressBarStyle, base_count: int) -> void:
	var units: Array[Node2D] = []
	var bars: Array[RID] = []
	for i in BAR_COUNT:
		var u := Node2D.new()
		u.position = Vector2(i % 40, floori(i / 40.0)) * 24.0
		add_child(u)
		units.append(u)
		bars.append(ProgressBarServer.bar_create(u, style))
	expect_equal(ProgressBarServer.get_bar_count(), base_count + BAR_COUNT, "%d bars created" % BAR_COUNT)

	var ratios := PackedFloat32Array()
	ratios.resize(BAR_COUNT)
	var total_usec := 0
	for frame in BENCH_FRAMES:
		for i in BAR_COUNT:
			ratios[i] = fmod(i * 0.01 + frame * 0.05, 1.0)
		var start := Time.get_ticks_usec()
		ProgressBarServer.bars_set_ratios(bars, ratios)
		total_usec += Time.get_ticks_usec() - start
		await get_tree().process_frame
	expect(is_equal_approx(ProgressBarServer.bar_get_ratio(bars[10]), ratios[10]), "bars_set_ratios sets every bar")
	print("BENCH gd: %d bars, bars_set_ratios %.3f ms per frame (average of %d frames, %s)" % [BAR_COUNT, total_usec / 1000.0 / BENCH_FRAMES, BENCH_FRAMES, OS.get_name()])

	for u in units:
		u.queue_free()
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(ProgressBarServer.get_bar_count(), base_count, "freeing 1000 units frees all their bars")
