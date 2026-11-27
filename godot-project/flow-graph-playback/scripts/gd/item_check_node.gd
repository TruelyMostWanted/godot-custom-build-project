@tool
class_name ItemCheckNode
extends FlowNode
## Branch node of the guide section "Custom node types" (HasItemNode in the guide; renamed because Flow Graphs has a
## HasItemNode already): leaves through "yes" when the variable "items" has the item.

@export var item: StringName


func _get_input_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("in")]


func _get_output_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("yes"), FlowPort.create_flow("no")]


func _choose_exit(playback: FlowGraphPlayback) -> StringName:
	return &"yes" if playback.get_variable("items").has(item) else &"no"
