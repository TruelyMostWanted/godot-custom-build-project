extends Node
## Minimal example of Scene Transitions: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Scene-Transitions

const NEXT_SCENE := "res://scene-transitions/resources/next_scene.tscn"
const LOADING_SCREEN := preload("res://scene-transitions/resources/loading_screen.tscn")


func _ready() -> void:
	# Only for this example: a scene change frees the current scene, which would end the example.
	get_tree().current_scene = null

	# Quick start: a black fade.
	get_tree().change_scene_with_transition(NEXT_SCENE)
	await get_tree().create_timer(1.5).timeout

	# Quick start: your own screen scene.
	get_tree().change_scene_with_transition(NEXT_SCENE, LOADING_SCREEN)
	await get_tree().create_timer(1.5).timeout

	# Designing a loading screen: the same in code.
	var loading := LoadingScreen.new()
	loading.progress_bar = ^"Bar"
	loading.tip_label = ^"Tip"
	loading.tips = PackedStringArray(["Save often.", "Keys open doors."])
	loading.tip_interval = 4.0
	print(loading.get_current_tip())
	loading.free()

	# Start a screen yourself and react to the progress.
	var screen: TransitionScreen = LOADING_SCREEN.instantiate()
	screen.next_scene = NEXT_SCENE
	screen.progress_changed.connect(func(p: float) -> void: print("loaded ", p * 100.0, " %"))
	get_tree().root.add_child(screen)
	await screen.finished

	# Memory option: free the current scene while loading.
	screen = LOADING_SCREEN.instantiate()
	screen.free_current_scene_before_loading = true
	screen.next_scene = NEXT_SCENE
	get_tree().root.add_child(screen)
	await screen.finished

	# Preloading assets.
	var preloads := Preloads.new()
	preloads.paths = PackedStringArray(["res://resource-database/resources/items/sword.tres", "res://resource-database/resources/items/shield.tres"])
	screen = LOADING_SCREEN.instantiate()
	screen.preload_assets = preloads
	screen.next_scene = NEXT_SCENE
	get_tree().root.add_child(screen)
	await screen.finished

	# Press any key, the boot splash and custom fades are scripts of their own (see CustomTransition in scripts/gd).
	get_tree().quit()
