using System.Collections.Generic;
using Godot;

namespace TruelyInteractive.SceneTransitions;

/// <summary>Overrides _UpdateFade() and records the values.</summary>
public partial class CustomTransition : TransitionScreen
{
    public List<float> Fades { get; } = new();

    public override void _UpdateFade(float fade)
    {
        Fades.Add(fade);
    }
}
