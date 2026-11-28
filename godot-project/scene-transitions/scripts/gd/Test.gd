extends FeatureTest
## Full API test of Scene Transitions (TransitionScreen, LoadingScreen, SplashScreen, SceneTree.change_scene_with_transition). Same tests as scripts/cs/Test.cs.
## The test node is the current scene. Before a real scene change it is detached from SceneTree.current_scene
## (it stays in the tree), so the change does not free the runner; it is restored afterwards.

const NEXT_SCENE := "res://scene-transitions/resources/next_scene.tscn"
const LOADING_SCENE := "res://scene-transitions/resources/loading_screen.tscn"
const CustomTransitionScript := preload("res://scene-transitions/scripts/gd/custom_transition.gd")


func _wait_until(condition: Callable, timeout_msec: int = 8000) -> bool:
	var start := Time.get_ticks_msec()
	while not condition.call():
		if Time.get_ticks_msec() - start > timeout_msec:
			return false
		await get_tree().process_frame
	return true


func _find_screen() -> TransitionScreen:
	for child in get_tree().root.get_children():
		if child is TransitionScreen:
			return child
	return null


func _wait_for_screen() -> TransitionScreen:
	await _wait_until(func(): return _find_screen() != null)
	return _find_screen()


## Frees the scene the transition changed to and makes the test node the current scene again.
func _restore_current_scene() -> void:
	var changed := get_tree().current_scene
	get_tree().current_scene = self
	if changed != null and changed != self:
		changed.queue_free()
	await get_tree().process_frame


func _run_tests() -> void:
	_test_defaults()
	await _test_overlay_flow()
	await _test_auto_finish()
	await _test_custom_fade()
	await _test_scene_change()
	await _test_loading_screen_scene()
	await _test_splash_skip()
	await _test_errors()


func _test_defaults() -> void:
	expect(ClassDB.class_exists("TransitionScreen"), "TransitionScreen is registered")
	expect(ClassDB.is_parent_class("TransitionScreen", "CanvasLayer"), "TransitionScreen is a CanvasLayer")
	expect(ClassDB.is_parent_class("LoadingScreen", "TransitionScreen"), "LoadingScreen is a TransitionScreen")
	expect(ClassDB.is_parent_class("SplashScreen", "TransitionScreen"), "SplashScreen is a TransitionScreen")

	var screen := TransitionScreen.new()
	expect_equal(screen.next_scene, "", "default next_scene")
	expect_equal(screen.min_duration, 0.0, "default min_duration")
	expect(is_equal_approx(screen.fade_in_duration, 0.3), "default fade_in_duration")
	expect(is_equal_approx(screen.fade_out_duration, 0.3), "default fade_out_duration")
	expect_equal(screen.skippable, false, "default skippable")
	expect_equal(screen.auto_finish, true, "default auto_finish")
	expect_equal(screen.free_current_scene_before_loading, false, "default free_current_scene_before_loading")
	expect_equal(screen.preload_assets, null, "default preload_assets")
	expect_equal(screen.layer, 100, "layer is 100")
	expect_equal(screen.process_mode, Node.PROCESS_MODE_ALWAYS, "process_mode is ALWAYS")
	expect_equal(screen.get_state(), TransitionScreen.STATE_IDLE, "state is idle")
	expect_equal(screen.get_progress(), 0.0, "progress is 0")
	expect_equal(screen.get_fade(), 0.0, "fade is 0")

	screen.next_scene = NEXT_SCENE
	screen.min_duration = 1.5
	screen.fade_in_duration = 0.5
	screen.fade_out_duration = 0.25
	screen.skippable = true
	screen.auto_finish = false
	screen.free_current_scene_before_loading = true
	var preloads := Preloads.new()
	screen.preload_assets = preloads
	expect_equal(screen.next_scene, NEXT_SCENE, "next_scene override")
	expect_equal(screen.min_duration, 1.5, "min_duration override")
	expect_equal(screen.fade_in_duration, 0.5, "fade_in_duration override")
	expect_equal(screen.fade_out_duration, 0.25, "fade_out_duration override")
	expect_equal(screen.skippable, true, "skippable override")
	expect_equal(screen.auto_finish, false, "auto_finish override")
	expect_equal(screen.free_current_scene_before_loading, true, "free_current_scene_before_loading override")
	expect(screen.preload_assets == preloads, "preload_assets override")
	screen.preload_assets = null

	screen.min_duration = -1.0
	screen.fade_in_duration = -2.0
	screen.fade_out_duration = -3.0
	expect_equal(screen.min_duration, 0.0, "negative min_duration is clamped to 0")
	expect_equal(screen.fade_in_duration, 0.0, "negative fade_in_duration is clamped to 0")
	expect_equal(screen.fade_out_duration, 0.0, "negative fade_out_duration is clamped to 0")
	screen.free()

	var loading := LoadingScreen.new()
	expect_equal(loading.progress_bar, NodePath(), "default progress_bar")
	expect_equal(loading.tip_label, NodePath(), "default tip_label")
	expect_equal(loading.tips.size(), 0, "default tips are empty")
	expect(is_equal_approx(loading.tip_interval, 5.0), "default tip_interval")
	expect_equal(loading.get_current_tip(), "", "no current tip")
	loading.progress_bar = NodePath("Bar")
	loading.tip_label = NodePath("Tip")
	loading.tips = PackedStringArray(["a", "b"])
	loading.tip_interval = 2.0
	expect_equal(loading.progress_bar, NodePath("Bar"), "progress_bar override")
	expect_equal(loading.tip_label, NodePath("Tip"), "tip_label override")
	expect_equal(loading.tips, PackedStringArray(["a", "b"]), "tips override")
	expect_equal(loading.tip_interval, 2.0, "tip_interval override")
	expect_equal(loading.get_current_tip(), "", "no current tip before the screen is ready")
	loading.tip_interval = -1.0
	expect_equal(loading.tip_interval, 0.0, "negative tip_interval is clamped to 0")
	loading.free()

	var splash := SplashScreen.new()
	expect_equal(splash.min_duration, 2.0, "splash min_duration")
	expect_equal(splash.fade_in_duration, 0.0, "splash fade_in_duration")
	expect(is_equal_approx(splash.fade_out_duration, 0.3), "splash fade_out_duration")
	expect_equal(splash.skippable, true, "splash is skippable")
	expect_equal(splash.auto_finish, true, "splash auto_finish")
	expect_equal(splash.next_scene, "", "splash next_scene is empty (project main scene)")
	splash.free()


## No next_scene: the screen is an overlay (fade in, wait min_duration, fade out, free itself).
func _test_overlay_flow() -> void:
	var screen := TransitionScreen.new()
	screen.fade_in_duration = 0.2
	screen.fade_out_duration = 0.2
	screen.min_duration = 0.2
	var rec := {"started": 0, "loaded": 0, "completed": 0, "progress": [], "started_state": -1, "started_fade": -1.0, "loaded_state": -1, "loaded_fade": -1.0, "completed_state": -1, "completed_fade": -1.0}
	screen.started.connect(func():
		rec.started += 1
		rec.started_state = screen.get_state()
		rec.started_fade = screen.get_fade())
	screen.progress_changed.connect(func(progress: float): rec.progress.append(progress))
	screen.loaded.connect(func():
		rec.loaded += 1
		rec.loaded_state = screen.get_state()
		rec.loaded_fade = screen.get_fade())
	screen.finished.connect(func():
		rec.completed += 1
		rec.completed_state = screen.get_state()
		rec.completed_fade = screen.get_fade())
	add_child(screen)
	expect_equal(screen.get_state(), TransitionScreen.STATE_IDLE, "overlay is idle until its first process frame")

	var seen := {}
	var start := Time.get_ticks_msec()
	while is_instance_valid(screen) and Time.get_ticks_msec() - start < 8000:
		seen[screen.get_state()] = true
		await get_tree().process_frame
	expect_equal(rec.started, 1, "started is emitted once")
	expect_equal(rec.started_state, TransitionScreen.STATE_FADING_IN, "state is fading in at started")
	expect_equal(rec.started_fade, 0.0, "fade is 0 at started")
	expect_equal(rec.loaded, 1, "loaded is emitted once")
	expect_equal(rec.loaded_state, TransitionScreen.STATE_LOADED, "state is loaded at loaded")
	expect_equal(rec.loaded_fade, 1.0, "fully visible at loaded")
	expect(rec.progress.size() > 0 and is_equal_approx(rec.progress[-1], 1.0), "progress_changed ends at 1.0")
	expect_equal(rec.completed, 1, "finished is emitted once")
	expect_equal(rec.completed_state, TransitionScreen.STATE_COMPLETED, "state is completed at finished")
	expect_equal(rec.completed_fade, 0.0, "fade is 0 at finished")
	expect(seen.has(TransitionScreen.STATE_FADING_IN), "state fading in was observed")
	expect(seen.has(TransitionScreen.STATE_LOADED), "state loaded was observed (min_duration wait)")
	expect(seen.has(TransitionScreen.STATE_FADING_OUT), "state fading out was observed")
	expect(not is_instance_valid(screen), "the screen frees itself")


func _test_auto_finish() -> void:
	# auto_finish = false waits for finish(), however short min_duration is.
	var screen := TransitionScreen.new()
	screen.fade_in_duration = 0.05
	screen.fade_out_duration = 0.05
	screen.auto_finish = false
	add_child(screen)
	expect(await _wait_until(func(): return screen.get_state() == TransitionScreen.STATE_LOADED), "auto_finish=false reaches loaded")
	var hold := Time.get_ticks_msec()
	while Time.get_ticks_msec() - hold < 300:
		await get_tree().process_frame
	expect_equal(screen.get_state(), TransitionScreen.STATE_LOADED, "auto_finish=false stays loaded")
	screen.finish()
	expect(await _wait_until(func(): return not is_instance_valid(screen)), "finish() ends the transition")

	# finish() skips a long min_duration.
	var long_screen := TransitionScreen.new()
	long_screen.fade_in_duration = 0.05
	long_screen.fade_out_duration = 0.05
	long_screen.min_duration = 30.0
	add_child(long_screen)
	expect(await _wait_until(func(): return long_screen.get_state() == TransitionScreen.STATE_LOADED), "long min_duration reaches loaded")
	expect_equal(long_screen.get_state(), TransitionScreen.STATE_LOADED, "min_duration holds the screen")
	long_screen.finish()
	expect(await _wait_until(func(): return not is_instance_valid(long_screen), 3000), "finish() skips min_duration")


func _test_custom_fade() -> void:
	var screen: CustomTransition = CustomTransitionScript.new()
	screen.fade_in_duration = 0.1
	screen.fade_out_duration = 0.1
	add_child(screen)
	var fades := screen.fades
	expect(await _wait_until(func(): return not is_instance_valid(screen)), "a screen with _update_fade() completes")
	expect(fades.size() > 2, "_update_fade() is called every frame of a fade")
	expect_equal(fades.max(), 1.0, "_update_fade() receives the fully visible fade")
	expect_equal(fades[-1], 0.0, "_update_fade() ends at 0")


## change_scene_with_transition() with the built-in black fade.
func _test_scene_change() -> void:
	var tree := get_tree()
	tree.current_scene = null
	expect_equal(tree.change_scene_with_transition(NEXT_SCENE), OK, "change_scene_with_transition(path) returns OK")
	var screen := await _wait_for_screen()
	expect(screen != null, "the screen is added to the root")
	if screen == null:
		await _restore_current_scene()
		return
	expect_equal(screen.get_class(), "TransitionScreen", "the built-in screen is a TransitionScreen")
	expect_equal(screen.next_scene, NEXT_SCENE, "next_scene is set")
	expect_equal(screen.get_child_count(), 1, "the built-in screen has a black ColorRect")
	var info := {"loaded_fade": -1.0, "progress": 0.0}
	screen.loaded.connect(func():
		info.loaded_fade = screen.get_fade()
		info.progress = screen.get_progress())
	expect(await _wait_until(func(): return not is_instance_valid(screen)), "the transition completes")
	expect_equal(info.loaded_fade, 1.0, "the scene changes while fully visible")
	expect_equal(info.progress, 1.0, "progress is 1.0 when loaded")
	expect(tree.current_scene != null and tree.current_scene.name == "NextScene", "current_scene is the next scene")
	await _restore_current_scene()
	expect(tree.current_scene == self, "the test node is the current scene again")


## change_scene_with_transition() with a LoadingScreen scene (ProgressBar + Label + tips).
func _test_loading_screen_scene() -> void:
	var tree := get_tree()
	var packed: PackedScene = load(LOADING_SCENE)
	tree.current_scene = null
	expect_equal(tree.change_scene_with_transition(NEXT_SCENE, packed), OK, "change_scene_with_transition(path, loading screen) returns OK")
	var found := await _wait_for_screen()
	var screen := found as LoadingScreen
	expect(screen != null, "the loading screen is added to the root")
	if screen == null:
		await _restore_current_scene()
		return
	var bar := screen.get_node("ProgressBar") as ProgressBar
	var label := screen.get_node("Tip") as Label
	expect_equal(screen.next_scene, NEXT_SCENE, "next_scene is set on the loading screen")
	expect_equal(screen.get_current_tip().is_empty(), false, "a tip is shown")
	var seen := {}
	var at_loaded := {}
	var start := Time.get_ticks_msec()
	while is_instance_valid(screen) and Time.get_ticks_msec() - start < 8000:
		seen[label.text] = true
		if at_loaded.is_empty() and screen.get_state() == TransitionScreen.STATE_LOADED:
			at_loaded = {"value": bar.value, "max": bar.max_value, "progress": screen.get_progress(), "fade": screen.get_fade(), "tip": screen.get_current_tip(), "text": label.text}
		await tree.process_frame
	expect(not at_loaded.is_empty(), "the loading screen reached loaded")
	if not at_loaded.is_empty():
		expect_equal(at_loaded.value, at_loaded.max, "the progress bar is at max when loaded")
		expect_equal(at_loaded.progress, 1.0, "get_progress() is 1.0 when loaded")
		expect_equal(at_loaded.fade, 1.0, "the loading screen is fully visible when loaded")
		expect_equal(at_loaded.text, at_loaded.tip, "the tip label shows get_current_tip()")
	expect(seen.has("Tip one") and seen.has("Tip two"), "the tips rotate every tip_interval")
	expect(tree.current_scene != null and tree.current_scene.name == "NextScene", "current_scene is the next scene after the loading screen")
	await _restore_current_scene()


## A skippable screen ends early on a key press and emits skipped.
func _test_splash_skip() -> void:
	var tree := get_tree()
	var splash := SplashScreen.new()
	splash.next_scene = NEXT_SCENE
	splash.fade_out_duration = 0.05
	var skipped := {"count": 0}
	splash.skipped.connect(func(): skipped.count += 1)
	tree.current_scene = null
	add_child(splash)
	expect(await _wait_until(func(): return splash.get_state() == TransitionScreen.STATE_LOADED), "the splash reached loaded")
	var key := InputEventKey.new()
	key.keycode = KEY_SPACE
	key.pressed = true
	tree.root.push_input(key)
	expect_equal(skipped.count, 1, "a key press emits skipped")
	expect(await _wait_until(func(): return not is_instance_valid(splash), 1500), "skipping ends the 2 second minimum")
	expect(tree.current_scene != null and tree.current_scene.name == "NextScene", "the splash changed to its next scene")
	await _restore_current_scene()


## Expected engine errors are printed (ERR_PRINT); no script errors.
func _test_errors() -> void:
	var tree := get_tree()
	expect_equal(tree.change_scene_with_transition(""), ERR_INVALID_PARAMETER, "an empty path is rejected")
	var not_a_screen: PackedScene = load(NEXT_SCENE)
	expect_equal(tree.change_scene_with_transition(NEXT_SCENE, not_a_screen), ERR_INVALID_PARAMETER, "a transition scene without a TransitionScreen root is rejected")
	await tree.process_frame
	expect(_find_screen() == null, "rejected calls add no screen")

	# A missing scene: rejected like change_scene_to_file().
	var missing := "res://scene-transitions/resources/missing.tscn"
	expect_equal(tree.change_scene_with_transition(missing), ERR_CANT_OPEN, "a missing path returns ERR_CANT_OPEN")

	# A screen whose next scene fails to load: load_failed instead of loaded, the current scene stays.
	var failing := TransitionScreen.new()
	failing.next_scene = missing
	failing.fade_in_duration = 0.0
	failing.fade_out_duration = 0.0
	var rec := {"loaded": 0, "failed": 0, "error": OK, "finished": 0}
	failing.loaded.connect(func(): rec.loaded += 1)
	failing.load_failed.connect(func(error: int):
		rec.failed += 1
		rec.error = error)
	failing.finished.connect(func(): rec.finished += 1)
	tree.root.add_child(failing)
	expect(await _wait_until(func(): return not is_instance_valid(failing)), "the screen for a missing scene finishes")
	expect_equal(rec.loaded, 0, "loaded is not emitted when loading fails")
	expect_equal(rec.failed, 1, "load_failed is emitted once")
	expect(rec.error != OK, "load_failed carries the error")
	expect_equal(rec.finished, 1, "finished follows load_failed")
	expect(tree.current_scene == self, "the current scene stays when loading fails")

	# One transition at a time.
	var overlay := TransitionScreen.new()
	overlay.fade_in_duration = 0.05
	overlay.fade_out_duration = 0.05
	tree.root.add_child(overlay)
	await tree.process_frame
	await tree.process_frame
	expect_equal(tree.change_scene_with_transition(NEXT_SCENE), ERR_BUSY, "a second transition returns ERR_BUSY")
	expect(await _wait_until(func(): return not is_instance_valid(overlay)), "the running overlay finishes")
