class_name CustomTransition
extends TransitionScreen
## Overrides _update_fade() and records the values.

var fades: Array[float] = []


func _update_fade(fade: float) -> void:
	fades.append(fade)
