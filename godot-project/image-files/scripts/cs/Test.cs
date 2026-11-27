using System;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.ImageFiles;

/// <summary>Full API test of Image Files (runtime image loader and saver, ImageSaveOptions). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string Dir = "user://image-files-test";
    private const string ImportedSvg = "res://icon.svg";
    private const int Size = 4;
    private const int PatternSize = 64;
    private const float DefaultQuality = 0.75f;
    private const string SampleOptions = "res://image-files/resources/high_quality_save_options.tres";
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"6\"><rect width=\"8\" height=\"6\" fill=\"#ff0000\"/></svg>";

    // Hand-written 2x2 files of formats without an encoder: extension, hex bytes. All are 2 pixels wide.
    private static readonly (string Extension, string Hex)[] RawFiles =
    [
        ("bmp", "424d460000000000000036000000280000000200000002000000010018000000000010000000130b0000130b0000000000000000000000ff0000ff0000000000ff0000ff0000"),
        ("tga", "0000020000000000000000000200020020280000ffff0000ffff0000ffff0000ffff"),
        ("hdr", "233f52414449414e43450a464f524d41543d33322d6269745f726c655f726762650a0a2d592032202b5820320a80000081800000818000008180000081"),
        ("ktx", "ab4b5458203131bb0d0a1a0a0102030401140000010000000819000058800000081900000200000002000000000000000000000001000000010000000000000010000000ff0000ffff0000ffff0000ffff0000ff"),
    ];

    protected override async Task RunTests()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        TestOptions();
        TestPng();
        TestJpeg();
        TestWebp();
        TestExrDds();
        TestRawFormats();
        TestUnsupportedSave();
        TestImported();
        await TestThreaded();
    }

    private static Image MakeImage()
    {
        Image image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        image.FillRect(new Rect2I(0, 0, Size, 2), Colors.Red);
        image.FillRect(new Rect2I(0, 2, Size, 2), Colors.Blue);
        return image;
    }

    // A pattern, so that lossy encoders give different sizes at different qualities.
    private static ImageTexture MakePattern()
    {
        Image image = Image.CreateEmpty(PatternSize, PatternSize, false, Image.Format.Rgba8);
        for (int y = 0; y < PatternSize; y++)
        {
            for (int x = 0; x < PatternSize; x++)
            {
                float red = (x * 37 + y * 11) % 64 / 63.0f;
                float green = x * y % 64 / 63.0f;
                float blue = (x + y * 53) % 64 / 63.0f;
                image.SetPixel(x, y, new Color(red, green, blue));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    private ImageTexture LoadTexture(string path, int width)
    {
        var texture = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as ImageTexture;
        Expect(texture != null, $"{path.GetFile()} loads as ImageTexture");
        ExpectEqual(texture.GetWidth(), width, $"{path.GetFile()} has the width of the file");
        return texture;
    }

    private Image LoadImage(string path, int width)
    {
        var image = ResourceLoader.Load(path, "Image", ResourceLoader.CacheMode.Ignore) as Image;
        Expect(image != null, $"{path.GetFile()} loads as Image with the type hint Image");
        ExpectEqual(image.GetWidth(), width, $"{path.GetFile()} Image has the width of the file");
        return image;
    }

    private void TestOptions()
    {
        var options = new ImageSaveOptions();
        ExpectEqual(options.JpgQuality, DefaultQuality, "JpgQuality defaults to 0.75");
        Expect(!options.WebpLossy, "WebpLossy defaults to false");
        ExpectEqual(options.WebpQuality, DefaultQuality, "WebpQuality defaults to 0.75");
        Expect(!options.ExrGrayscale, "ExrGrayscale defaults to false");
        var sample = GD.Load<ImageSaveOptions>(SampleOptions);
        Expect(sample.WebpLossy, "the sample .tres loads with its values");
        Image image = MakeImage();
        options.AttachTo(image);
        Expect(ImageSaveOptions.GetAttached(image) == options, "GetAttached returns the attached options");
        ImageSaveOptions.DetachFrom(image);
        Expect(ImageSaveOptions.GetAttached(image) == null, "DetachFrom removes the options");
    }

    private void TestPng()
    {
        string path = Dir + "/round_trip.png";
        ImageTexture texture = ImageTexture.CreateFromImage(MakeImage());
        ExpectEqual(ResourceSaver.Save(texture, path), Error.Ok, "ResourceSaver.Save writes a texture to .png");
        Expect(ResourceLoader.Exists(path), "ResourceLoader.Exists finds the .png");
        Expect(ResourceLoader.Exists(path, "Image"), "ResourceLoader.Exists finds the .png with the hint Image");
        ImageTexture loaded = LoadTexture(path, Size);
        Image pixels = loaded.GetImage();
        Expect(pixels.GetPixel(0, 0).IsEqualApprox(Colors.Red), "top-left pixel is red");
        Expect(pixels.GetPixel(Size - 1, Size - 1).IsEqualApprox(Colors.Blue), "bottom-right pixel is blue");
        LoadImage(path, Size);
        Resource first = ResourceLoader.Load(path);
        Resource second = ResourceLoader.Load(path);
        Expect(first == second, "the png loads through the resource cache");
        string imagePath = Dir + "/from_image.png";
        ExpectEqual(ResourceSaver.Save(MakeImage(), imagePath), Error.Ok, "ResourceSaver.Save writes an Image to .png");
        LoadImage(imagePath, Size);
    }

    private void TestJpeg()
    {
        ImageTexture pattern = MakePattern();
        var options = new ImageSaveOptions { JpgQuality = 0.1f };
        options.AttachTo(pattern);
        ExpectEqual(ResourceSaver.Save(pattern, Dir + "/low.jpg"), Error.Ok, "ResourceSaver.Save writes .jpg");
        options.JpgQuality = 1.0f;
        ExpectEqual(ResourceSaver.Save(pattern, Dir + "/high.jpeg"), Error.Ok, "ResourceSaver.Save writes .jpeg");
        long lowSize = FileAccess.GetSize(Dir + "/low.jpg");
        long highSize = FileAccess.GetSize(Dir + "/high.jpeg");
        Expect(lowSize < highSize, "JpgQuality changes the file size");
        LoadTexture(Dir + "/low.jpg", PatternSize);
        LoadTexture(Dir + "/high.jpeg", PatternSize);
    }

    private void TestWebp()
    {
        ImageTexture pattern = MakePattern();
        ExpectEqual(ResourceSaver.Save(pattern, Dir + "/lossless.webp"), Error.Ok, "ResourceSaver.Save writes lossless .webp");
        var options = new ImageSaveOptions { WebpLossy = true, WebpQuality = 0.1f };
        options.AttachTo(pattern);
        ExpectEqual(ResourceSaver.Save(pattern, Dir + "/lossy.webp"), Error.Ok, "ResourceSaver.Save writes lossy .webp");
        long losslessSize = FileAccess.GetSize(Dir + "/lossless.webp");
        long lossySize = FileAccess.GetSize(Dir + "/lossy.webp");
        Expect(lossySize < losslessSize, "WebpLossy makes a smaller file");
        LoadTexture(Dir + "/lossless.webp", PatternSize);
    }

    private void TestExrDds()
    {
        ImageTexture texture = ImageTexture.CreateFromImage(MakeImage());
        var options = new ImageSaveOptions { ExrGrayscale = true };
        options.AttachTo(texture);
        ExpectEqual(ResourceSaver.Save(texture, Dir + "/float.exr"), Error.Ok, "ResourceSaver.Save writes .exr");
        LoadTexture(Dir + "/float.exr", Size);
        ExpectEqual(ResourceSaver.Save(texture, Dir + "/texture.dds"), Error.Ok, "ResourceSaver.Save writes .dds");
        LoadTexture(Dir + "/texture.dds", Size);
        LoadImage(Dir + "/texture.dds", Size);
    }

    private void TestRawFormats()
    {
        foreach ((string extension, string hex) in RawFiles)
        {
            string path = Dir + "/image." + extension;
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreBuffer(Convert.FromHexString(hex));
            file.Close();
            LoadTexture(path, 2);
        }
        Image bmp = LoadTexture(Dir + "/image.bmp", 2).GetImage();
        Expect(bmp.GetPixel(0, 1).IsEqualApprox(Colors.Green), "bmp bottom row is green");
        LoadImage(Dir + "/image.ktx", 2);
        string svgPath = Dir + "/image.svg";
        using FileAccess svgFile = FileAccess.Open(svgPath, FileAccess.ModeFlags.Write);
        svgFile.StoreString(Svg);
        svgFile.Close();
        LoadTexture(svgPath, 8);
    }

    private void TestUnsupportedSave()
    {
        ImageTexture texture = ImageTexture.CreateFromImage(MakeImage());
        Expect(ResourceSaver.Save(texture, Dir + "/nope.bmp") != Error.Ok, "ResourceSaver.Save refuses .bmp (no encoder)");
    }

    private void TestImported()
    {
        Resource imported = GD.Load(ImportedSvg);
        Expect(imported != null && imported is not ImageTexture, "an imported res:// file still loads through the importer");
    }

    private async Task TestThreaded()
    {
        string path = Dir + "/lossless.webp";
        ExpectEqual(ResourceLoader.LoadThreadedRequest(path), Error.Ok, "LoadThreadedRequest accepts an image file");
        while (ResourceLoader.LoadThreadedGetStatus(path) == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        ExpectEqual(ResourceLoader.LoadThreadedGetStatus(path), ResourceLoader.ThreadLoadStatus.Loaded, "the threaded load finishes");
        var texture = (ImageTexture)ResourceLoader.LoadThreadedGet(path);
        ExpectEqual(texture.GetWidth(), PatternSize, "LoadThreadedGet returns the ImageTexture");
    }
}
