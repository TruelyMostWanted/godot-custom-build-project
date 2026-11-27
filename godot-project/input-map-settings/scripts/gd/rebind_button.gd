class_name RebindButton
extends Button

## The action this button rebinds and the column it shows.
@export var action: StringName = &"jump"
@export var device_filter := InputActionRebinder.DEVICE_KEYBOARD_MOUSE
@export var event_index := 0
## Seconds without input until the button gives up.
@export var timeout := 5.0

var rebinder := InputActionRebinder.new()
var _timer: SceneTreeTimer


func _ready() -> void:
	rebinder.device_filter = device_filter
	rebinder.conflict_mode = InputActionRebinder.CONFLICT_SWAP
	rebinder.started.connect(_on_started)
	rebinder.canceled.connect(_on_finished.unbind(2))
	rebinder.completed.connect(_on_finished.unbind(3))
	pressed.connect(func() -> void: rebinder.begin(action, event_index))
	# Also fires for the other action of a swap, a reset and a loaded file.
	InputMapSettingsServer.action_events_changed.connect(_on_action_events_changed)
	_update_text()


func _input(event: InputEvent) -> void:
	# While the button waits, the input belongs to it: no other control or action sees it.
	if rebinder.update(event):
		get_viewport().set_input_as_handled()


func _on_started(_action: StringName, _index: int) -> void:
	text = "Press a key..."
	_timer = get_tree().create_timer(timeout)
	_timer.timeout.connect(_on_timeout.bind(_timer))


func _on_timeout(timer: SceneTreeTimer) -> void:
	if timer == _timer:
		rebinder.cancel()


func _on_finished() -> void:
	_timer = null
	_update_text()
	InputMapSettingsServer.save()


func _on_action_events_changed(changed: StringName) -> void:
	if changed == action and not rebinder.is_active():
		_update_text()


func _update_text() -> void:
	var event: InputEvent = _event_in_column()
	if event:
		text = InputMapSettingsServer.get_event_label(event)
	else:
		text = "-"


func _event_in_column() -> InputEvent:
	# The event_index-th event of the action in this column, counted like the rebinder counts.
	var index: int = 0
	for event: InputEvent in InputMapSettingsServer.get_events(action):
		var is_pad: bool = InputAction.get_event_device(event) == InputAction.INPUT_DEVICE_GAMEPAD
		if device_filter == InputActionRebinder.DEVICE_KEYBOARD_MOUSE and is_pad:
			continue
		if device_filter == InputActionRebinder.DEVICE_GAMEPAD and not is_pad:
			continue
		if index == event_index:
			return event
		index += 1
	return null
