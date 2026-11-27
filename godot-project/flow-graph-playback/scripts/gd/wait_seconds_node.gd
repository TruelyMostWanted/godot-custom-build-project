@tool
class_name WaitSecondsNode
extends FlowNode
## Node of the guide section "Custom node types" that waits for 3 seconds; the elapsed time lives in the node memory.


func _get_input_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("in")]


func _get_output_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("out")]


func _enter(playback: FlowGraphPlayback) -> FlowGraphPlayback.EnterResult:
	playback.set_node_memory(0.0)
	return FlowGraphPlayback.ENTER_RESULT_WAIT


func _process_waiting(playback: FlowGraphPlayback, delta: float) -> bool:
	var elapsed: float = playback.get_node_memory() + delta
	playback.set_node_memory(elapsed)
	return elapsed >= 3.0
