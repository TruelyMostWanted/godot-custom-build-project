using Godot;

namespace TruelyInteractive.Issue255FeatureFlowGraph;

/// <summary>C# node type of the 255 checks: a flow node with a typed data input "text" and a data output "length".</summary>
[Tool]
[GlobalClass]
public partial class Issue255CsNode : FlowNode
{
    [Export] public string Prefix { get; set; } = ">";

    public override Godot.Collections.Array<FlowPort> _GetInputPorts()
    {
        return new Godot.Collections.Array<FlowPort> { FlowPort.CreateFlow("in"), FlowPort.CreateData("text", Variant.Type.String, "", "Text") };
    }

    public override Godot.Collections.Array<FlowPort> _GetOutputPorts()
    {
        return new Godot.Collections.Array<FlowPort> { FlowPort.CreateFlow("out"), FlowPort.CreateData("length", Variant.Type.Int) };
    }

    public override string _GetTitle()
    {
        return "C# Node";
    }

    public override string _GetCategory()
    {
        return "Checks";
    }
}
