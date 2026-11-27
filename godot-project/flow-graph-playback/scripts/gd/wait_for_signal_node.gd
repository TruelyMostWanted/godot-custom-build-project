@tool
class_name WaitForSignalNode
extends FlowNode
## Node of the guide section "Waiting": waits until the button Main/ContinueButton is pressed.


func _get_input_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("in")]


func _get_output_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("out")]


func _enter(playback: FlowGraphPlayback) -> FlowGraphPlayback.EnterResult:
	var button: Button = Engine.get_main_loop().root.get_node("Main/ContinueButton")
	button.pressed.connect(playback.advance, CONNECT_ONE_SHOT)
	return FlowGraphPlayback.ENTER_RESULT_WAIT
