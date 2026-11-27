using System.Threading.Tasks;
using Godot;

namespace TruelyInteractive.ImageFiles;

/// <summary>Minimal example of Image Files: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override async void _Ready()
    {
        // Setup: sample pictures in user://, a Sprite2D and a TextureRect (the guide assumes them).
        DirAccess.MakeDirRecursiveAbsolute("user://screenshots");
        DirAccess.MakeDirRecursiveAbsolute("user://mods");
        DirAccess.MakeDirRecursiveAbsolute("user://photos");
        Image sample = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        sample.Fill(Colors.Red);
        sample.SavePng("user://screenshots/shot.png");
        sample.SaveWebp("user://mods/skin.webp");
        sample.SaveJpg("user://photos/large.jpg");
        var sprite = new Sprite2D { Name = "Sprite2D" };
        AddChild(sprite);
        var textureRect = new TextureRect { Name = "TextureRect" };
        AddChild(textureRect);

        // Show a picture from the player's folder
        GetNode<Sprite2D>("Sprite2D").Texture = GD.Load<ImageTexture>("user://screenshots/shot.png");
        GD.Print("Texture: ", sprite.Texture.GetSize());

        // Edit the pixels of a loaded image
        var skin = ResourceLoader.Load<Image>("user://mods/skin.webp", "Image", ResourceLoader.CacheMode.Ignore);
        skin.AdjustBcs(1.0f, 1.2f, 1.0f);
        GetNode<Sprite2D>("Sprite2D").Texture = ImageTexture.CreateFromImage(skin);
        GD.Print("Skin pixel: ", skin.GetPixel(0, 0));

        // Save a screenshot as JPEG with a quality (the sample stands in for the viewport image, which headless has none)
        Image image = sample;
        var options = new ImageSaveOptions { JpgQuality = 0.9f };
        options.AttachTo(image);
        Error error = ResourceSaver.Save(image, "user://screenshots/shot.jpg");
        ImageSaveOptions.DetachFrom(image);
        GD.Print("Saved JPEG: ", error == Error.Ok);

        // Load a large image in the background
        ResourceLoader.LoadThreadedRequest("user://photos/large.jpg");
        while (ResourceLoader.LoadThreadedGetStatus("user://photos/large.jpg") == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetNode<TextureRect>("TextureRect").Texture = (Texture2D)ResourceLoader.LoadThreadedGet("user://photos/large.jpg");
        GD.Print("Background load: ", textureRect.Texture.GetSize());
        GetTree().Quit();
    }
}
