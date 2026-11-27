using Godot;

namespace TruelyInteractive.FlowGraphPlayback;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>Node of the guide section "Waiting" (WaitForSignalNode in the guide): waits until Main/ContinueButton is pressed.</summary>
[Tool]
[GlobalClass]
public partial class WaitForSignalNodeCs : FlowNode
{
    public override Godot.Collections.Array<FlowPort> _GetInputPorts() => [FlowPort.CreateFlow("in")];
    public override Godot.Collections.Array<FlowPort> _GetOutputPorts() => [FlowPort.CreateFlow("out")];

    public override FlowGraphPlayback.EnterResult _Enter(FlowGraphPlayback playback)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var button = tree.Root.GetNode<Button>("Main/ContinueButton");
        button.Connect(BaseButton.SignalName.Pressed,
            new Callable(playback, FlowGraphPlayback.MethodName.Advance),
            (uint)GodotObject.ConnectFlags.OneShot);
        return FlowGraphPlayback.EnterResult.Wait;
    }
}
