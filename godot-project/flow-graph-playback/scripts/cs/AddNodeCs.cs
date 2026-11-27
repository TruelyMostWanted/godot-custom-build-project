using Godot;

namespace TruelyInteractive.FlowGraphPlayback;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>Data node of the guide section "Custom node types" (AddNode in the guide): outputs a + b.</summary>
[Tool]
[GlobalClass]
public partial class AddNodeCs : FlowNode
{
    public override Godot.Collections.Array<FlowPort> _GetInputPorts() => [
        FlowPort.CreateData("a", Variant.Type.Int),
        FlowPort.CreateData("b", Variant.Type.Int),
    ];

    public override Godot.Collections.Array<FlowPort> _GetOutputPorts() => [FlowPort.CreateData("sum", Variant.Type.Int)];

    public override Variant _EvaluateOutput(FlowGraphPlayback playback, StringName port)
    {
        return (long)playback.GetInputValue("a") + (long)playback.GetInputValue("b");
    }
}
