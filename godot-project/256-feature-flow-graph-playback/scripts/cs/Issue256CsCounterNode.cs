using Godot;

namespace TruelyInteractive.Issue256FeatureFlowGraphPlayback;

/// <summary>C# node type of the 256 checks: counts its visits in the variable "count" and outputs count * 10 on "tens".</summary>
[Tool]
[GlobalClass]
public partial class Issue256CsCounterNode : FlowNode
{
    public override Godot.Collections.Array<FlowPort> _GetInputPorts()
    {
        return new Godot.Collections.Array<FlowPort> { FlowPort.CreateFlow("in") };
    }

    public override Godot.Collections.Array<FlowPort> _GetOutputPorts()
    {
        return new Godot.Collections.Array<FlowPort> { FlowPort.CreateFlow("out"), FlowPort.CreateData("tens", Variant.Type.Int) };
    }

    public override FlowGraphPlayback.EnterResult _Enter(FlowGraphPlayback playback)
    {
        playback.SetVariable("count", (int)playback.GetVariable("count") + 1);
        return FlowGraphPlayback.EnterResult.Continue;
    }

    public override Variant _EvaluateOutput(FlowGraphPlayback playback, StringName port)
    {
        return (int)playback.GetVariable("count") * 10;
    }
}
