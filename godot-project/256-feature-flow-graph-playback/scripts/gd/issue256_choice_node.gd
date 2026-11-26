@tool
class_name Issue256ChoiceNode
extends FlowNode
## Node type of the 256 checks: waits for a choice between the flow outputs "a" and "b"; "b" is disabled while the
## variable "locked" is true.


func _get_input_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow(&"in")]


func _get_output_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow(&"a", "Take A"), FlowPort.create_flow(&"b", "Take B")]


func _enter(playback: FlowGraphPlayback) -> FlowGraphPlayback.EnterResult:
	playback.set_node_memory(playback.get_node_memory() + 1 if playback.get_node_memory() != null else 1)
	return FlowGraphPlayback.ENTER_RESULT_WAIT


func _get_available_exits(playback: FlowGraphPlayback) -> Array[FlowExit]:
	var a := FlowExit.new()
	a.port = &"a"
	a.label = "Take A"
	var b := FlowExit.new()
	b.port = &"b"
	b.label = "Take B"
	b.enabled = not playback.get_variable(&"locked")
	return [a, b]
