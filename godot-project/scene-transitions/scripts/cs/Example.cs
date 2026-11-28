using Godot;

namespace TruelyInteractive.SceneTransitions;

/// <summary>Minimal example of Scene Transitions: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    private const string NextScene = "res://scene-transitions/resources/next_scene.tscn";
    private const string LoadingScreenPath = "res://scene-transitions/resources/loading_screen.tscn";

    public override async void _Ready()
    {
        // Only for this example: a scene change frees the current scene, which would end the example.
        GetTree().CurrentScene = null;

        // Quick start: a black fade.
        GetTree().ChangeSceneWithTransition(NextScene);
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);

        // Quick start: your own screen scene.
        GetTree().ChangeSceneWithTransition(NextScene, GD.Load<PackedScene>(LoadingScreenPath));
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);

        // Designing a loading screen: the same in code.
        var loading = new LoadingScreen();
        loading.ProgressBar = "Bar";
        loading.TipLabel = "Tip";
        loading.Tips = ["Save often.", "Keys open doors."];
        loading.TipInterval = 4.0;
        GD.Print(loading.GetCurrentTip());
        loading.Free();

        // Start a screen yourself and react to the progress.
        var screen = GD.Load<PackedScene>(LoadingScreenPath).Instantiate<TransitionScreen>();
        screen.NextScene = NextScene;
        screen.ProgressChanged += p => GD.Print("loaded ", p * 100.0, " %");
        GetTree().Root.AddChild(screen);
        await ToSignal(screen, TransitionScreen.SignalName.Finished);

        // Memory option: free the current scene while loading.
        screen = GD.Load<PackedScene>(LoadingScreenPath).Instantiate<TransitionScreen>();
        screen.FreeCurrentSceneBeforeLoading = true;
        screen.NextScene = NextScene;
        GetTree().Root.AddChild(screen);
        await ToSignal(screen, TransitionScreen.SignalName.Finished);

        // Preloading assets.
        var preloads = new Preloads();
        preloads.Paths = ["res://resource-database/resources/items/sword.tres", "res://resource-database/resources/items/shield.tres"];
        screen = GD.Load<PackedScene>(LoadingScreenPath).Instantiate<TransitionScreen>();
        screen.PreloadAssets = preloads;
        screen.NextScene = NextScene;
        GetTree().Root.AddChild(screen);
        await ToSignal(screen, TransitionScreen.SignalName.Finished);

        // Press any key, the boot splash and custom fades are scripts of their own (see CustomTransition in scripts/cs).
        GetTree().Quit();
    }
}
