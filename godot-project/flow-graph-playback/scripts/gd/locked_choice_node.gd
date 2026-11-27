@tool
class_name LockedChoiceNode
extends FlowNode
## Choice node of the guide section "Choices and exits": waits for a choice between "a" and "b"; "b" is disabled while
## the variable "locked" is true. Counts its visits in the node memory and sets the variable "left" in _exit().


func _get_input_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("in")]


func _get_output_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("a", "Take A"), FlowPort.create_flow("b", "Take B")]


func _enter(playback: FlowGraphPlayback) -> FlowGraphPlayback.EnterResult:
	var visits: int = playback.get_node_memory() if playback.get_node_memory() != null else 0
	playback.set_node_memory(visits + 1)
	return FlowGraphPlayback.ENTER_RESULT_WAIT


func _exit(playback: FlowGraphPlayback) -> void:
	playback.set_variable(&"left", true)


func _get_available_exits(playback: FlowGraphPlayback) -> Array[FlowExit]:
	var a := FlowExit.new()
	a.port = &"a"
	a.label = "Take A"
	var b := FlowExit.new()
	b.port = &"b"
	b.label = "Take B"
	b.enabled = not playback.get_variable(&"locked")
	return [a, b]
