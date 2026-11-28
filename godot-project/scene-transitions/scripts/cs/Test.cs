using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.SceneTransitions;

/// <summary>
/// Full API test of Scene Transitions (TransitionScreen, LoadingScreen, SplashScreen, SceneTree.ChangeSceneWithTransition). Same tests as scripts/gd/Test.gd.
/// The test node is the current scene. Before a real scene change it is detached from SceneTree.CurrentScene
/// (it stays in the tree), so the change does not free the runner; it is restored afterwards.
/// State values are compared as numbers (STATE_* order), independent of the generated enum member names.
/// </summary>
public partial class Test : FeatureTest
{
    private const string NextScene = "res://scene-transitions/resources/next_scene.tscn";
    private const string LoadingScene = "res://scene-transitions/resources/loading_screen.tscn";

    private const int Idle = 0;
    private const int FadingIn = 1;
    private const int Loading = 2;
    private const int Loaded = 3;
    private const int FadingOut = 4;
    private const int Completed = 5;

    private static int StateOf(TransitionScreen screen) => (int)screen.GetState();

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task<bool> WaitUntil(Func<bool> condition, int timeoutMsec = 8000)
    {
        ulong start = Time.GetTicksMsec();
        while (!condition())
        {
            if (Time.GetTicksMsec() - start > (ulong)timeoutMsec)
            {
                return false;
            }
            await NextFrame();
        }
        return true;
    }

    private TransitionScreen FindScreen()
    {
        foreach (Node child in GetTree().Root.GetChildren())
        {
            if (child is TransitionScreen screen)
            {
                return screen;
            }
        }
        return null;
    }

    private async Task<TransitionScreen> WaitForScreen()
    {
        await WaitUntil(() => FindScreen() != null);
        return FindScreen();
    }

    /// <summary>Frees the scene the transition changed to and makes the test node the current scene again.</summary>
    private async Task RestoreCurrentScene()
    {
        Node changed = GetTree().CurrentScene;
        GetTree().CurrentScene = this;
        if (changed != null && changed != this)
        {
            changed.QueueFree();
        }
        await NextFrame();
    }

    protected override async Task RunTests()
    {
        await RunTemplates();
        await RunAnimationProgress();
        TestDefaults();
        await TestOverlayFlow();
        await TestAutoFinish();
        await TestCustomFade();
        await TestSceneChange();
        await TestLoadingScreenScene();
        await TestSplashSkip();
        await TestErrors();
    }

    private void TestDefaults()
    {
        Expect(ClassDB.ClassExists("TransitionScreen"), "TransitionScreen is registered");
        Expect(ClassDB.IsParentClass("TransitionScreen", "CanvasLayer"), "TransitionScreen is a CanvasLayer");
        Expect(ClassDB.IsParentClass("LoadingScreen", "TransitionScreen"), "LoadingScreen is a TransitionScreen");
        Expect(ClassDB.IsParentClass("SplashScreen", "TransitionScreen"), "SplashScreen is a TransitionScreen");

        var screen = new TransitionScreen();
        ExpectEqual(screen.NextScene, "", "default next_scene");
        ExpectEqual(screen.MinDuration, 0.0, "default min_duration");
        Expect(Mathf.IsEqualApprox((float)screen.FadeInDuration, 0.3f), "default fade_in_duration");
        Expect(Mathf.IsEqualApprox((float)screen.FadeOutDuration, 0.3f), "default fade_out_duration");
        ExpectEqual(screen.Skippable, false, "default skippable");
        ExpectEqual(screen.AutoFinish, true, "default auto_finish");
        ExpectEqual(screen.FreeCurrentSceneBeforeLoading, false, "default free_current_scene_before_loading");
        Expect(screen.PreloadAssets == null, "default preload_assets");
        ExpectEqual(screen.Layer, 100, "layer is 100");
        ExpectEqual(screen.ProcessMode, Node.ProcessModeEnum.Always, "process_mode is ALWAYS");
        ExpectEqual(StateOf(screen), Idle, "state is idle");
        ExpectEqual(screen.GetProgress(), 0.0f, "progress is 0");
        ExpectEqual(screen.GetFade(), 0.0f, "fade is 0");

        screen.NextScene = NextScene;
        screen.MinDuration = 1.5;
        screen.FadeInDuration = 0.5;
        screen.FadeOutDuration = 0.25;
        screen.Skippable = true;
        screen.AutoFinish = false;
        screen.FreeCurrentSceneBeforeLoading = true;
        var preloads = new Preloads();
        screen.PreloadAssets = preloads;
        ExpectEqual(screen.NextScene, NextScene, "next_scene override");
        ExpectEqual(screen.MinDuration, 1.5, "min_duration override");
        ExpectEqual(screen.FadeInDuration, 0.5, "fade_in_duration override");
        ExpectEqual(screen.FadeOutDuration, 0.25, "fade_out_duration override");
        ExpectEqual(screen.Skippable, true, "skippable override");
        ExpectEqual(screen.AutoFinish, false, "auto_finish override");
        ExpectEqual(screen.FreeCurrentSceneBeforeLoading, true, "free_current_scene_before_loading override");
        Expect(screen.PreloadAssets == preloads, "preload_assets override");
        screen.PreloadAssets = null;

        screen.MinDuration = -1.0;
        screen.FadeInDuration = -2.0;
        screen.FadeOutDuration = -3.0;
        ExpectEqual(screen.MinDuration, 0.0, "negative min_duration is clamped to 0");
        ExpectEqual(screen.FadeInDuration, 0.0, "negative fade_in_duration is clamped to 0");
        ExpectEqual(screen.FadeOutDuration, 0.0, "negative fade_out_duration is clamped to 0");
        screen.Free();

        var loading = new LoadingScreen();
        ExpectEqual(loading.ProgressBar, new NodePath(), "default progress_bar");
        ExpectEqual(loading.TipLabel, new NodePath(), "default tip_label");
        ExpectEqual(loading.Tips.Length, 0, "default tips are empty");
        Expect(Mathf.IsEqualApprox((float)loading.TipInterval, 5.0f), "default tip_interval");
        ExpectEqual(loading.GetCurrentTip(), "", "no current tip");
        loading.ProgressBar = new NodePath("Bar");
        loading.TipLabel = new NodePath("Tip");
        loading.Tips = new[] { "a", "b" };
        loading.TipInterval = 2.0;
        ExpectEqual(loading.ProgressBar, new NodePath("Bar"), "progress_bar override");
        ExpectEqual(loading.TipLabel, new NodePath("Tip"), "tip_label override");
        Expect(loading.Tips.Length == 2 && loading.Tips[0] == "a" && loading.Tips[1] == "b", "tips override");
        ExpectEqual(loading.TipInterval, 2.0, "tip_interval override");
        ExpectEqual(loading.GetCurrentTip(), "", "no current tip before the screen is ready");
        loading.TipInterval = -1.0;
        ExpectEqual(loading.TipInterval, 0.0, "negative tip_interval is clamped to 0");
        loading.Free();

        var splash = new SplashScreen();
        ExpectEqual(splash.MinDuration, 2.0, "splash min_duration");
        ExpectEqual(splash.FadeInDuration, 0.0, "splash fade_in_duration");
        Expect(Mathf.IsEqualApprox((float)splash.FadeOutDuration, 0.3f), "splash fade_out_duration");
        ExpectEqual(splash.Skippable, true, "splash is skippable");
        ExpectEqual(splash.AutoFinish, true, "splash auto_finish");
        ExpectEqual(splash.NextScene, "", "splash next_scene is empty (project main scene)");
        splash.Free();
    }

    /// <summary>No next_scene: the screen is an overlay (fade in, wait min_duration, fade out, free itself).</summary>
    private async Task TestOverlayFlow()
    {
        var screen = new TransitionScreen
        {
            FadeInDuration = 0.2,
            FadeOutDuration = 0.2,
            MinDuration = 0.2,
        };
        int started = 0;
        int loaded = 0;
        int completed = 0;
        int startedState = -1;
        int loadedState = -1;
        int completedState = -1;
        float startedFade = -1.0f;
        float loadedFade = -1.0f;
        float completedFade = -1.0f;
        float lastProgress = -1.0f;
        screen.Started += () =>
        {
            started++;
            startedState = StateOf(screen);
            startedFade = screen.GetFade();
        };
        screen.ProgressChanged += progress => lastProgress = (float)progress;
        screen.Loaded += () =>
        {
            loaded++;
            loadedState = StateOf(screen);
            loadedFade = screen.GetFade();
        };
        screen.Finished += () =>
        {
            completed++;
            completedState = StateOf(screen);
            completedFade = screen.GetFade();
        };
        AddChild(screen);
        ExpectEqual(StateOf(screen), Idle, "overlay is idle until its first process frame");

        var seen = new HashSet<int>();
        ulong start = Time.GetTicksMsec();
        while (GodotObject.IsInstanceValid(screen) && Time.GetTicksMsec() - start < 8000)
        {
            seen.Add(StateOf(screen));
            await NextFrame();
        }
        ExpectEqual(started, 1, "started is emitted once");
        ExpectEqual(startedState, FadingIn, "state is fading in at started");
        ExpectEqual(startedFade, 0.0f, "fade is 0 at started");
        ExpectEqual(loaded, 1, "loaded is emitted once");
        ExpectEqual(loadedState, Loaded, "state is loaded at loaded");
        ExpectEqual(loadedFade, 1.0f, "fully visible at loaded");
        Expect(Mathf.IsEqualApprox(lastProgress, 1.0f), "progress_changed ends at 1.0");
        ExpectEqual(completed, 1, "finished is emitted once");
        ExpectEqual(completedState, Completed, "state is completed at finished");
        ExpectEqual(completedFade, 0.0f, "fade is 0 at finished");
        Expect(seen.Contains(FadingIn), "state fading in was observed");
        Expect(seen.Contains(Loaded), "state loaded was observed (min_duration wait)");
        Expect(seen.Contains(FadingOut), "state fading out was observed");
        Expect(!GodotObject.IsInstanceValid(screen), "the screen frees itself");
    }

    private async Task TestAutoFinish()
    {
        // auto_finish = false waits for Finish(), however short min_duration is.
        var screen = new TransitionScreen
        {
            FadeInDuration = 0.05,
            FadeOutDuration = 0.05,
            AutoFinish = false,
        };
        AddChild(screen);
        Expect(await WaitUntil(() => StateOf(screen) == Loaded), "auto_finish=false reaches loaded");
        ulong hold = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - hold < 300)
        {
            await NextFrame();
        }
        ExpectEqual(StateOf(screen), Loaded, "auto_finish=false stays loaded");
        screen.Finish();
        Expect(await WaitUntil(() => !GodotObject.IsInstanceValid(screen)), "Finish() ends the transition");

        // Finish() skips a long min_duration.
        var longScreen = new TransitionScreen
        {
            FadeInDuration = 0.05,
            FadeOutDuration = 0.05,
            MinDuration = 30.0,
        };
        AddChild(longScreen);
        Expect(await WaitUntil(() => StateOf(longScreen) == Loaded), "long min_duration reaches loaded");
        ExpectEqual(StateOf(longScreen), Loaded, "min_duration holds the screen");
        longScreen.Finish();
        Expect(await WaitUntil(() => !GodotObject.IsInstanceValid(longScreen), 3000), "Finish() skips min_duration");
    }

    private async Task TestCustomFade()
    {
        var screen = new CustomTransition
        {
            FadeInDuration = 0.1,
            FadeOutDuration = 0.1,
        };
        AddChild(screen);
        List<float> fades = screen.Fades;
        Expect(await WaitUntil(() => !GodotObject.IsInstanceValid(screen)), "a screen with _UpdateFade() completes");
        Expect(fades.Count > 2, "_UpdateFade() is called every frame of a fade");
        float max = 0.0f;
        foreach (float fade in fades)
        {
            max = Mathf.Max(max, fade);
        }
        ExpectEqual(max, 1.0f, "_UpdateFade() receives the fully visible fade");
        ExpectEqual(fades[^1], 0.0f, "_UpdateFade() ends at 0");
    }

    /// <summary>ChangeSceneWithTransition() with the built-in black fade.</summary>
    private async Task TestSceneChange()
    {
        SceneTree tree = GetTree();
        tree.CurrentScene = null;
        ExpectEqual(tree.ChangeSceneWithTransition(NextScene), Error.Ok, "ChangeSceneWithTransition(path) returns OK");
        TransitionScreen screen = await WaitForScreen();
        Expect(screen != null, "the screen is added to the root");
        if (screen == null)
        {
            await RestoreCurrentScene();
            return;
        }
        ExpectEqual(screen.GetClass(), "TransitionScreen", "the built-in screen is a TransitionScreen");
        ExpectEqual(screen.NextScene, NextScene, "next_scene is set");
        ExpectEqual(screen.GetChildCount(), 1, "the built-in screen has a black ColorRect");
        float loadedFade = -1.0f;
        float progress = 0.0f;
        screen.Loaded += () =>
        {
            loadedFade = screen.GetFade();
            progress = screen.GetProgress();
        };
        Expect(await WaitUntil(() => !GodotObject.IsInstanceValid(screen)), "the transition completes");
        ExpectEqual(loadedFade, 1.0f, "the scene changes while fully visible");
        ExpectEqual(progress, 1.0f, "progress is 1.0 when loaded");
        Expect(tree.CurrentScene != null && tree.CurrentScene.Name == "NextScene", "current_scene is the next scene");
        await RestoreCurrentScene();
        Expect(tree.CurrentScene == this, "the test node is the current scene again");
    }

    /// <summary>ChangeSceneWithTransition() with a LoadingScreen scene (ProgressBar + Label + tips).</summary>
    private async Task TestLoadingScreenScene()
    {
        SceneTree tree = GetTree();
        var packed = GD.Load<PackedScene>(LoadingScene);
        tree.CurrentScene = null;
        ExpectEqual(tree.ChangeSceneWithTransition(NextScene, packed), Error.Ok, "ChangeSceneWithTransition(path, loading screen) returns OK");
        var screen = await WaitForScreen() as LoadingScreen;
        Expect(screen != null, "the loading screen is added to the root");
        if (screen == null)
        {
            await RestoreCurrentScene();
            return;
        }
        var bar = screen.GetNode<Godot.ProgressBar>("ProgressBar");
        var label = screen.GetNode<Label>("Tip");
        ExpectEqual(screen.NextScene, NextScene, "next_scene is set on the loading screen");
        ExpectEqual(screen.GetCurrentTip().Length == 0, false, "a tip is shown");
        var seen = new HashSet<string>();
        bool reached = false;
        double barValue = 0.0;
        double barMax = 1.0;
        float progress = 0.0f;
        float fade = 0.0f;
        string tip = "";
        string text = "";
        ulong start = Time.GetTicksMsec();
        while (GodotObject.IsInstanceValid(screen) && Time.GetTicksMsec() - start < 8000)
        {
            seen.Add(label.Text);
            if (!reached && StateOf(screen) == Loaded)
            {
                reached = true;
                barValue = bar.Value;
                barMax = bar.MaxValue;
                progress = screen.GetProgress();
                fade = screen.GetFade();
                tip = screen.GetCurrentTip();
                text = label.Text;
            }
            await NextFrame();
        }
        Expect(reached, "the loading screen reached loaded");
        if (reached)
        {
            ExpectEqual(barValue, barMax, "the progress bar is at max when loaded");
            ExpectEqual(progress, 1.0f, "GetProgress() is 1.0 when loaded");
            ExpectEqual(fade, 1.0f, "the loading screen is fully visible when loaded");
            ExpectEqual(text, tip, "the tip label shows GetCurrentTip()");
        }
        Expect(seen.Contains("Tip one") && seen.Contains("Tip two"), "the tips rotate every tip_interval");
        Expect(tree.CurrentScene != null && tree.CurrentScene.Name == "NextScene", "current_scene is the next scene after the loading screen");
        await RestoreCurrentScene();
    }

    /// <summary>A skippable screen ends early on a key press and emits Skipped.</summary>
    private async Task TestSplashSkip()
    {
        SceneTree tree = GetTree();
        var splash = new SplashScreen
        {
            NextScene = NextScene,
            FadeOutDuration = 0.05,
        };
        int skipped = 0;
        splash.Skipped += () => skipped++;
        tree.CurrentScene = null;
        AddChild(splash);
        Expect(await WaitUntil(() => StateOf(splash) == Loaded), "the splash reached loaded");
        var key = new InputEventKey { Keycode = Key.Space, Pressed = true };
        tree.Root.PushInput(key);
        ExpectEqual(skipped, 1, "a key press emits skipped");
        Expect(await WaitUntil(() => !GodotObject.IsInstanceValid(splash), 1500), "skipping ends the 2 second minimum");
        Expect(tree.CurrentScene != null && tree.CurrentScene.Name == "NextScene", "the splash changed to its next scene");
        await RestoreCurrentScene();
    }

    /// <summary>Expected engine errors are printed (ERR_PRINT); no script errors.</summary>
    private async Task TestErrors()
    {
        SceneTree tree = GetTree();
        ExpectEqual(tree.ChangeSceneWithTransition(""), Error.InvalidParameter, "an empty path is rejected");
        var notAScreen = GD.Load<PackedScene>(NextScene);
        ExpectEqual(tree.ChangeSceneWithTransition(NextScene, notAScreen), Error.InvalidParameter, "a transition scene without a TransitionScreen root is rejected");
        await NextFrame();
        Expect(FindScreen() == null, "rejected calls add no screen");

        // A missing scene: rejected like ChangeSceneToFile().
        const string missing = "res://scene-transitions/resources/missing.tscn";
        ExpectEqual(tree.ChangeSceneWithTransition(missing), Error.CantOpen, "a missing path returns ERR_CANT_OPEN");

        // A screen whose next scene fails to load: LoadFailed instead of Loaded, the current scene stays.
        var failing = new TransitionScreen();
        failing.NextScene = missing;
        failing.FadeInDuration = 0.0;
        failing.FadeOutDuration = 0.0;
        int loaded = 0;
        int failed = 0;
        long error = 0;
        int finished = 0;
        failing.Loaded += () => loaded++;
        failing.LoadFailed += e =>
        {
            failed++;
            error = (long)e;
        };
        failing.Finished += () => finished++;
        tree.Root.AddChild(failing);
        Expect(await WaitUntil(() => !GodotObject.IsInstanceValid(failing)), "the screen for a missing scene finishes");
        ExpectEqual(loaded, 0, "loaded is not emitted when loading fails");
        ExpectEqual(failed, 1, "load_failed is emitted once");
        Expect(error != 0, "load_failed carries the error");
        ExpectEqual(finished, 1, "finished follows load_failed");
        Expect(tree.CurrentScene == this, "the current scene stays when loading fails");

        // One transition at a time.
        var overlay = new TransitionScreen();
        overlay.FadeInDuration = 0.05;
        overlay.FadeOutDuration = 0.05;
        tree.Root.AddChild(overlay);
        await NextFrame();
        await NextFrame();
        ExpectEqual(tree.ChangeSceneWithTransition(NextScene), Error.Busy, "a second transition returns ERR_BUSY");
        Expect(await WaitUntil(() => !GodotObject.IsInstanceValid(overlay)), "the running overlay finishes");
    }


    private Preloads _watched;
    private readonly List<double> _progressValues = new();
    private readonly List<string> _order = new();

    private void OnPreloadProgress(Preloads source, double progress)
    {
        if (source == _watched)
        {
            _progressValues.Add(progress);
            _order.Add("progress");
        }
    }

    private void OnPreloadFinished(Preloads source, Error error)
    {
        if (source == _watched)
        {
            _order.Add("finished");
        }
    }

    private async Task<bool> WaitUntilAnimationProgress(Func<bool> condition, int timeoutMsec = 8000)
    {
        ulong start = Time.GetTicksMsec();
        while (!condition())
        {
            if (Time.GetTicksMsec() - start > (ulong)timeoutMsec)
            {
                return false;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        return true;
    }

    /// <summary>A screen with an AnimationPlayer child that has "intro" and "outro" animations of the given lengths.</summary>
    private static TransitionScreen MakeScreen(double introLength, double outroLength)
    {
        var screen = new TransitionScreen();
        var player = new AnimationPlayer { Name = "Player" };
        var library = new AnimationLibrary();
        var intro = new Animation { Length = (float)introLength };
        library.AddAnimation("intro", intro);
        var outro = new Animation { Length = (float)outroLength };
        library.AddAnimation("outro", outro);
        player.AddAnimationLibrary("", library);
        screen.AddChild(player);
        screen.AnimationPlayer = "Player";
        return screen;
    }

    private async Task RunAnimationProgress()
    {
        TestDefaultsAnimationProgress();
        await TestAnimationFlow();
        await TestFallbacks();
        await TestPreloadProgress();
        await TestManifestProgress();
        await TestScreenProgress();
    }

    private void TestDefaultsAnimationProgress()
    {
        var screen = new TransitionScreen();
        Expect(screen.AnimationPlayer.IsEmpty, "default animation_player is empty");
        ExpectEqual(screen.IntroAnimation.ToString(), "", "default intro_animation");
        ExpectEqual(screen.OutroAnimation.ToString(), "", "default outro_animation");
        screen.AnimationPlayer = "Player";
        screen.IntroAnimation = "intro";
        screen.OutroAnimation = "outro";
        ExpectEqual(screen.AnimationPlayer.ToString(), "Player", "animation_player is stored");
        ExpectEqual(screen.IntroAnimation.ToString(), "intro", "intro_animation is stored");
        ExpectEqual(screen.OutroAnimation.ToString(), "outro", "outro_animation is stored");
        screen.Free();
        Expect(ClassDB.ClassHasSignal("ResourceDatabase", "preload_progress"), "ResourceDatabase has preload_progress");
        Expect(ClassDB.ClassHasMethod("ResourceDatabase", "get_preload_progress"), "ResourceDatabase has get_preload_progress");
    }

    /// <summary>The intro and outro animations replace the fade: the player plays them, their length is the duration.</summary>
    private async Task TestAnimationFlow()
    {
        TransitionScreen screen = MakeScreen(0.2, 0.2);
        screen.IntroAnimation = "intro";
        screen.OutroAnimation = "outro";
        screen.FadeInDuration = 30.0;
        screen.FadeOutDuration = 30.0;
        var player = screen.GetNode<AnimationPlayer>("Player");
        var played = new List<string>();
        player.AnimationStarted += animationName => played.Add(animationName.ToString());
        GetTree().Root.AddChild(screen);
        ulong start = Time.GetTicksMsec();
        Expect(await WaitUntilAnimationProgress(() => !IsInstanceValid(screen)), "an animated screen completes");
        Expect(Time.GetTicksMsec() - start < 5000, "the animation length replaces the 30 s fade durations");
        ExpectEqual(string.Join(",", played), "intro,outro", "the player plays intro, then outro");
    }

    /// <summary>A missing animation or player falls back to the built-in fade; intro and outro can be mixed.</summary>
    private async Task TestFallbacks()
    {
        TransitionScreen screen = MakeScreen(0.2, 0.2);
        screen.IntroAnimation = "missing";
        screen.OutroAnimation = "outro";
        screen.FadeInDuration = 0.1;
        GetTree().Root.AddChild(screen);
        Expect(await WaitUntilAnimationProgress(() => !IsInstanceValid(screen)), "a missing intro animation falls back to the fade");

        var noPlayer = new TransitionScreen { IntroAnimation = "intro", OutroAnimation = "outro", FadeInDuration = 0.1, FadeOutDuration = 0.1 };
        GetTree().Root.AddChild(noPlayer);
        Expect(await WaitUntilAnimationProgress(() => !IsInstanceValid(noPlayer)), "animation names without a player fall back to the fade");
    }

    /// <summary>ResourceDatabase.PreloadProgress: one emission per file, ending at 1.0 before PreloadFinished.</summary>
    private async Task TestPreloadProgress()
    {
        ExpectEqual((float)ResourceDatabase.GetPreloadProgress(), 1.0f, "no preload: progress is 1.0");
        _watched = new Preloads { Paths = [NextScene, "res://icon.svg"] };
        _progressValues.Clear();
        _order.Clear();
        ResourceDatabase.Singleton.PreloadProgress += OnPreloadProgress;
        ResourceDatabase.Singleton.PreloadFinished += OnPreloadFinished;
        ExpectEqual(ResourceDatabase.PreloadFrom(_watched, false), Error.Ok, "PreloadFrom returns Ok");
        Expect(await WaitUntilAnimationProgress(() => _order.Contains("finished")), "PreloadFinished follows");
        ResourceDatabase.Singleton.PreloadProgress -= OnPreloadProgress;
        ResourceDatabase.Singleton.PreloadFinished -= OnPreloadFinished;
        ExpectEqual(_progressValues.Count, 2, "one PreloadProgress per file");
        Expect(_progressValues.Count == 2 && Mathf.IsEqualApprox(_progressValues[0], 0.5) && Mathf.IsEqualApprox(_progressValues[1], 1.0), "progress is 0.5, then 1.0");
        ExpectEqual(_order[_order.Count - 1], "finished", "PreloadFinished comes after the last progress");
        ExpectEqual((float)ResourceDatabase.GetPreloadProgress(), 1.0f, "finished: progress is 1.0");
        ResourceDatabase.Clear();
    }

    /// <summary>ResourceLoader.LoadThreadedGetManifestStatus(): n / X loaded for a FilesManifest, advancing every call.</summary>
    private async Task TestManifestProgress()
    {
        var manifest = new FilesManifest { FilePaths = [NextScene, "res://icon.svg"] };
        LoadProgress progress = ResourceLoader.LoadThreadedGetManifestStatus(manifest);
        ExpectEqual((int)progress.GetLoadedCount(), 0, "the status is a query: nothing is loaded before the request");
        ResourceLoader.LoadThreadedRequestManifest(manifest);
        progress = ResourceLoader.LoadThreadedStepManifest(manifest);
        ExpectEqual(progress.GetTotalCount(), 2, "the manifest has 2 files");
        float lastRatio = progress.GetRatio();
        bool ascending = true;
        ulong start = Time.GetTicksMsec();
        while (!progress.IsFinished() && Time.GetTicksMsec() - start < 8000)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            progress = ResourceLoader.LoadThreadedStepManifest(manifest);
            ascending = ascending && progress.GetRatio() >= lastRatio;
            lastRatio = progress.GetRatio();
        }
        Expect(progress.IsFinished(), "the manifest load finishes");
        Expect(ascending, "the manifest progress never goes back");
        ExpectEqual(progress.GetLoadedCount(), 2, "2 / 2 loaded");
        Expect(Mathf.IsEqualApprox(progress.GetRatio(), 1.0f), "the ratio is 1.0");
        Godot.Collections.Dictionary resources = ResourceLoader.LoadThreadedGetManifest(manifest);
        ExpectEqual(resources.Count, 2, "LoadThreadedGetManifest returns both resources");
        Expect(resources.ContainsKey("res://icon.svg"), "the result is keyed by path");
    }

    /// <summary>A screen with PreloadAssets reports one progress for the scene and the assets.</summary>
    private async Task TestScreenProgress()
    {
        var screen = new LoadingScreen { FadeInDuration = 0.0, FadeOutDuration = 0.0 };
        var counter = new Label { Name = "Count" };
        screen.AddChild(counter);
        screen.CountLabel = "Count";
        string lastCount = "";
        screen.PreloadAssets = new Preloads { Paths = ["res://icon.svg"] };
        var values = new List<double>();
        screen.ProgressChanged += progress =>
        {
            values.Add(progress);
            lastCount = counter.Text;
        };
        GetTree().Root.AddChild(screen);
        Expect(await WaitUntilAnimationProgress(() => !IsInstanceValid(screen)), "a screen with PreloadAssets completes");
        bool ascending = true;
        for (int idx = 1; idx < values.Count; idx++)
        {
            ascending = ascending && values[idx] >= values[idx - 1];
        }
        Expect(ascending, "the progress never goes back");
        Expect(values.Count > 0 && Mathf.IsEqualApprox(values[values.Count - 1], 1.0), "the progress ends at 1.0");
        ExpectEqual(lastCount, "1 / 1", "the count label shows 1 / 1 at the end");
        Expect(ResourceDatabase.HasResource("res://icon.svg"), "the preload assets are loaded");
        ResourceDatabase.Clear();
    }

    private async Task RunTemplates()
    {
        TestSplashTemplate();
        TestLoadingTemplate();
        await TestSaveAndRun();
        TestCountLabelEmptyUntilTotal();
    }

    private void TestSplashTemplate()
    {
        PackedScene scene = TransitionScreen.CreateTemplate(TransitionScreen.Template.Splash);
        Expect(scene != null, "splash template is created");
        Node screen = scene.Instantiate();
        Expect(screen is SplashScreen, "splash template root is a SplashScreen");
        Expect(screen.GetNodeOrNull("Background") is ColorRect, "splash has a Background");
        Expect(screen.GetNodeOrNull("Content/Column") != null, "splash has a Content/Column");
        var splash = (SplashScreen)screen;
        var player = splash.GetNode<AnimationPlayer>(splash.AnimationPlayer);
        Expect(player.HasAnimation(splash.IntroAnimation), "splash has its intro animation");
        Expect(player.HasAnimation(splash.OutroAnimation), "splash has its outro animation");
        Expect(player.GetAnimation(splash.IntroAnimation).Length > 0.0, "the intro animation has a length");
        screen.Free();
    }

    private void TestLoadingTemplate()
    {
        PackedScene scene = TransitionScreen.CreateTemplate(TransitionScreen.Template.Loading);
        var screen = scene.Instantiate() as LoadingScreen;
        Expect(screen != null, "loading template root is a LoadingScreen");
        Expect(screen.GetNode(screen.ProgressBar) is ProgressBar, "progress_bar points to a ProgressBar");
        Expect(screen.GetNode(screen.TipLabel) is Label, "tip_label points to a Label");
        Expect(screen.GetNode(screen.CountLabel) is Label, "count_label points to a Label");
        Expect(screen.Tips.Length > 0, "the loading template has tips");
        screen.Free();
    }

    private async Task TestSaveAndRun()
    {
        string path = "user://template_loading_screen_cs.tscn";
        PackedScene scene = TransitionScreen.CreateTemplate(TransitionScreen.Template.Loading);
        ExpectEqual(ResourceSaver.Save(scene, path).ToString(), Error.Ok.ToString(), "the template is saved as a scene file");
        var loaded = ResourceLoader.Load<PackedScene>(path);
        Expect(loaded != null, "the saved template loads again");
        var screen = loaded.Instantiate<LoadingScreen>();
        screen.MinDuration = 0.0;
        bool done = false;
        screen.Finished += () => done = true;
        GetTree().Root.AddChild(screen);
        ulong start = Time.GetTicksMsec();
        while (!done && Time.GetTicksMsec() - start < 8000)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Expect(done, "the template screen runs and finishes");
        DirAccess.RemoveAbsolute(path);
    }

    private void TestCountLabelEmptyUntilTotal()
    {
        var screen = new LoadingScreen();
        var label = new Label { Name = "Count" };
        screen.AddChild(label);
        screen.CountLabel = "Count";
        ExpectEqual(label.Text, "", "the count label is empty before the total is known");
        screen.Free();
    }
}
