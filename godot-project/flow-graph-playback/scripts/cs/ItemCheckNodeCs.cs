using Godot;

namespace TruelyInteractive.FlowGraphPlayback;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>
/// Branch node of the guide section "Custom node types" (HasItemNode in the guide; renamed because Flow Graphs has a
/// HasItemNode already): leaves through "yes" when the variable "items" has the item.
/// </summary>
[Tool]
[GlobalClass]
public partial class ItemCheckNodeCs : FlowNode
{
    [Export] public StringName Item { get; set; }

    public override Godot.Collections.Array<FlowPort> _GetInputPorts() => [FlowPort.CreateFlow("in")];
    public override Godot.Collections.Array<FlowPort> _GetOutputPorts() => [FlowPort.CreateFlow("yes"), FlowPort.CreateFlow("no")];

    public override StringName _ChooseExit(FlowGraphPlayback playback)
    {
        var items = (Godot.Collections.Array)playback.GetVariable("items");
        return items.Contains(Item) ? "yes" : "no";
    }
}
