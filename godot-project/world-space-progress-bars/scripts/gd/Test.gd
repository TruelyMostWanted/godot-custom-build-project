extends FeatureTest
## Full API test of World Space Progress Bars (ProgressBarServer, ProgressBarStyle). Same tests as scripts/cs/Test.cs.

const BENCH_BARS := 1000
const BENCH_FRAMES := 60

var _style := ProgressBarStyle.new()
var _base_count: int = 0


func _run_tests() -> void:
	_base_count = ProgressBarServer.get_bar_count()
	_test_style_defaults()
	_test_style_properties()
	_test_ratio()
	_test_bar_setters()
	await _test_position_only()
	await _test_unit_freed()
	await _test_unit_outside_tree()
	await _test_bars_set_ratios()


func _test_style_defaults() -> void:
	var defaults := ProgressBarStyle.new()
	expect_equal(defaults.size, Vector2(32, 4), "default size is 32x4")
	expect_equal(defaults.offset, Vector2(0, -20), "default offset is (0, -20)")
	expect_equal(defaults.background, null, "default background is empty")
	expect_equal(defaults.fill, null, "default fill is empty")
	expect_equal(defaults.fill_mode, ProgressBar.FILL_BEGIN_TO_END, "default fill_mode is begin to end")
	expect_equal(defaults.follow_mode, ProgressBarStyle.FOLLOW_MODE_INHERIT, "default follow_mode is inherit")
	expect_equal(defaults.hide_when_empty, false, "default hide_when_empty is false")
	expect_equal(defaults.hide_when_full, false, "default hide_when_full is false")
	expect_equal(ProgressBarStyle.FOLLOW_MODE_POSITION_ONLY, 1, "FOLLOW_MODE_POSITION_ONLY is 1")
	expect_equal(ProgressBarStyle.FOLLOW_MODE_MAX, 2, "FOLLOW_MODE_MAX is 2")


func _test_style_properties() -> void:
	var background := StyleBoxFlat.new()
	var fill := StyleBoxFlat.new()
	_style.size = Vector2(40, 5)
	_style.offset = Vector2(0, -32)
	_style.background = background
	_style.fill = fill
	_style.fill_mode = ProgressBar.FILL_BOTTOM_TO_TOP
	_style.hide_when_empty = true
	_style.hide_when_full = true
	expect_equal(_style.size, Vector2(40, 5), "size is stored")
	expect_equal(_style.offset, Vector2(0, -32), "offset is stored")
	expect_equal(_style.background, background, "background is stored")
	expect_equal(_style.fill, fill, "fill is stored")
	expect_equal(_style.fill_mode, ProgressBar.FILL_BOTTOM_TO_TOP, "fill_mode is stored")
	expect_equal(_style.hide_when_empty, true, "hide_when_empty is stored")
	expect_equal(_style.hide_when_full, true, "hide_when_full is stored")


func _test_ratio() -> void:
	var unit := Node2D.new()
	add_child(unit)
	var bar: RID = ProgressBarServer.bar_create(unit, _style)
	expect(bar.is_valid(), "bar_create returns a valid RID")
	expect_equal(ProgressBarServer.get_bar_count(), _base_count + 1, "get_bar_count counts the new bar")
	expect_equal(ProgressBarServer.bar_get_ratio(bar), 1.0, "a new bar is full")
	ProgressBarServer.bar_set_ratio(bar, 0.25)
	expect_equal(ProgressBarServer.bar_get_ratio(bar), 0.25, "bar_set_ratio sets the ratio")
	ProgressBarServer.bar_set_ratio(bar, 5.0)
	expect_equal(ProgressBarServer.bar_get_ratio(bar), 1.0, "a ratio above 1 is clamped to 1")
	ProgressBarServer.bar_set_ratio(bar, -1.0)
	expect_equal(ProgressBarServer.bar_get_ratio(bar), 0.0, "a ratio below 0 is clamped to 0")
	ProgressBarServer.bar_free(bar)
	expect_equal(ProgressBarServer.get_bar_count(), _base_count, "bar_free frees the bar")
	unit.free()


func _test_bar_setters() -> void:
	var unit := Node2D.new()
	add_child(unit)
	var bar: RID = ProgressBarServer.bar_create(unit, _style)
	ProgressBarServer.bar_set_ratio(bar, 0.5)
	ProgressBarServer.bar_set_offset(bar, Vector2(0, 6))
	ProgressBarServer.bar_set_modulate(bar, Color.RED)
	ProgressBarServer.bar_set_visible(bar, false)
	var theme_style := ProgressBarStyle.new()
	ProgressBarServer.bar_set_style(bar, theme_style)
	_style.fill_mode = ProgressBar.FILL_TOP_TO_BOTTOM
	expect_equal(ProgressBarServer.bar_get_ratio(bar), 0.5, "offset, modulate, visible and style keep the ratio")
	ProgressBarServer.bar_free(bar)
	unit.free()


func _test_position_only() -> void:
	var upright := ProgressBarStyle.new()
	upright.follow_mode = ProgressBarStyle.FOLLOW_MODE_POSITION_ONLY
	expect_equal(upright.follow_mode, ProgressBarStyle.FOLLOW_MODE_POSITION_ONLY, "follow_mode is stored")
	var unit := Node2D.new()
	add_child(unit)
	var bar: RID = ProgressBarServer.bar_create(unit, upright)
	unit.rotation = PI / 2
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(ProgressBarServer.get_bar_count(), _base_count + 1, "a position-only bar on a rotated unit lives on")
	ProgressBarServer.bar_free(bar)
	unit.free()


func _test_unit_freed() -> void:
	var unit := Node2D.new()
	add_child(unit)
	ProgressBarServer.bar_create(unit, _style)
	ProgressBarServer.bar_create(unit, _style)
	expect_equal(ProgressBarServer.get_bar_count(), _base_count + 2, "two bars on one unit")
	unit.queue_free()
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(ProgressBarServer.get_bar_count(), _base_count, "deleting the unit frees its bars")


func _test_unit_outside_tree() -> void:
	var pooled := Node2D.new()
	add_child(pooled)
	ProgressBarServer.bar_create(pooled, _style)
	remove_child(pooled)
	await get_tree().process_frame
	expect_equal(ProgressBarServer.get_bar_count(), _base_count + 1, "a unit outside the tree keeps its bars")
	pooled.free()
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(ProgressBarServer.get_bar_count(), _base_count, "a unit freed outside the tree frees its bars")


## Also the benchmark of the wiki: 1000 bars, every value changes every frame.
func _test_bars_set_ratios() -> void:
	var units: Array[Node2D] = []
	var bars: Array[RID] = []
	for i in BENCH_BARS:
		var unit := Node2D.new()
		add_child(unit)
		units.append(unit)
		var bar: RID = ProgressBarServer.bar_create(unit, _style)
		bars.append(bar)
	var ratios := PackedFloat32Array()
	ratios.resize(BENCH_BARS)
	var total_usec: int = 0
	for frame in BENCH_FRAMES:
		for i in BENCH_BARS:
			ratios[i] = fmod(i * 0.01 + frame * 0.05, 1.0)
		var start: int = Time.get_ticks_usec()
		ProgressBarServer.bars_set_ratios(bars, ratios)
		total_usec += Time.get_ticks_usec() - start
		await get_tree().process_frame
	expect_equal(ProgressBarServer.bar_get_ratio(bars[10]), ratios[10], "bars_set_ratios sets every bar")
	print("BENCH gd: %d bars, bars_set_ratios %.3f ms per frame" % [BENCH_BARS, total_usec / 1000.0 / BENCH_FRAMES])
	for unit in units:
		unit.queue_free()
	await get_tree().process_frame
	await get_tree().process_frame
	expect_equal(ProgressBarServer.get_bar_count(), _base_count, "freeing 1000 units frees all their bars")
