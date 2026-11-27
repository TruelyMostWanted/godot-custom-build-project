using Godot;

namespace TruelyInteractive.FlowGraphPlayback;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>
/// Choice node of the guide section "Choices and exits": waits for a choice between "a" and "b"; "b" is disabled while
/// the variable "locked" is true. Counts its visits in the node memory and sets the variable "left" in _Exit().
/// </summary>
[Tool]
[GlobalClass]
public partial class LockedChoiceNodeCs : FlowNode
{
    public override Godot.Collections.Array<FlowPort> _GetInputPorts() => [FlowPort.CreateFlow("in")];
    public override Godot.Collections.Array<FlowPort> _GetOutputPorts() => [FlowPort.CreateFlow("a", "Take A"), FlowPort.CreateFlow("b", "Take B")];

    public override FlowGraphPlayback.EnterResult _Enter(FlowGraphPlayback playback)
    {
        int visits = playback.GetNodeMemory().AsInt32();
        playback.SetNodeMemory(visits + 1);
        return FlowGraphPlayback.EnterResult.Wait;
    }

    public override void _Exit(FlowGraphPlayback playback)
    {
        playback.SetVariable("left", true);
    }

    public override Godot.Collections.Array<FlowExit> _GetAvailableExits(FlowGraphPlayback playback)
    {
        var a = new FlowExit { Port = "a", Label = "Take A" };
        var b = new FlowExit { Port = "b", Label = "Take B", Enabled = !playback.GetVariable("locked").AsBool() };
        return [a, b];
    }
}
