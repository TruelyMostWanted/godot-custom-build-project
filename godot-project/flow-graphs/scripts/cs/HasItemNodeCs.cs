using Godot;

namespace TruelyInteractive.FlowGraphs;

/// <summary>
/// Node type of the guide section "Write your own node type" (HasItemNode in the guide; Cs suffix because the GDScript
/// class has that name). _GetInlineProperties() and _GetTranslatableStrings() are not in the guide snippet; Test.cs uses them.
/// </summary>
[Tool]
[GlobalClass]
public partial class HasItemNodeCs : FlowNode
{
    private StringName _item = "";

    [Export]
    public StringName Item
    {
        get => _item;
        set
        {
            _item = value;
            NotifyPortsChanged();
        }
    }

    public override Godot.Collections.Array<FlowPort> _GetInputPorts()
    {
        return [FlowPort.CreateFlow("in")];
    }

    public override Godot.Collections.Array<FlowPort> _GetOutputPorts()
    {
        return [FlowPort.CreateFlow("yes"), FlowPort.CreateFlow("no")];
    }

    public override string _GetCategory() => "Inventory";

    public override string[] _GetConfigurationWarnings()
    {
        return Item == null || Item.IsEmpty ? ["No item is set."] : [];
    }

    public override string[] _GetInlineProperties() => ["Item"];

    public override Godot.Collections.Array<string[]> _GetTranslatableStrings()
    {
        return [[$"Do you have the {Item}?"]];
    }
}
