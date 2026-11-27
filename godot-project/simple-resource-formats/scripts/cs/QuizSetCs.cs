using Godot;

namespace TruelyInteractive.SimpleResourceFormats;

/// <summary>Resource class from C#: one question per line in a .quiz file. Named QuizSetCs because QuizSet is the GDScript class.</summary>
[GlobalClass]
public partial class QuizSetCs : Resource
{
    [Export] public string[] Questions { get; set; } = [];
}
