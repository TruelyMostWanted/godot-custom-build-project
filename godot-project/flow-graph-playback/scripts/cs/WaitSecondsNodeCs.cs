using Godot;

namespace TruelyInteractive.FlowGraphPlayback;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>Node of the guide section "Custom node types" that waits for 3 seconds; the elapsed time lives in the node memory.</summary>
[Tool]
[GlobalClass]
public partial class WaitSecondsNodeCs : FlowNode
{
    public override Godot.Collections.Array<FlowPort> _GetInputPorts() => [FlowPort.CreateFlow("in")];
    public override Godot.Collections.Array<FlowPort> _GetOutputPorts() => [FlowPort.CreateFlow("out")];

    public override FlowGraphPlayback.EnterResult _Enter(FlowGraphPlayback playback)
    {
        playback.SetNodeMemory(0.0);
        return FlowGraphPlayback.EnterResult.Wait;
    }

    public override bool _ProcessWaiting(FlowGraphPlayback playback, double delta)
    {
        double elapsed = (double)playback.GetNodeMemory() + delta;
        playback.SetNodeMemory(elapsed);
        return elapsed >= 3.0;
    }
}
