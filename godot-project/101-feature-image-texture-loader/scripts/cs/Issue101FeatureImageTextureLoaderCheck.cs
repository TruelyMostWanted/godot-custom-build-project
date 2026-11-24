using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue101FeatureImageTextureLoader;

/// <summary>C# checks of 101-feature-image-texture-loader. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>All test images are generated at runtime: encoded with ResourceSaver (png, jpg, webp, exr, dds) or written
/// byte by byte (bmp, tga, hdr, svg, ktx).</remarks>
public partial class Issue101FeatureImageTextureLoaderCheck : FeatureCheckCs
{
    private const string Dir = "user://101-feature-image-texture-loader-cs";
    private const string ImportedSvg = "res://07-feature-files-manifest/resources/data/a/icon.svg";

    protected override string CheckName => "101-feature-image-texture-loader";

    private static void PushU16(List<byte> data, uint value)
    {
        data.Add((byte)(value & 0xff));
        data.Add((byte)((value >> 8) & 0xff));
    }

    private static void PushU32(List<byte> data, uint value)
    {
        PushU16(data, value & 0xffff);
        PushU16(data, value >> 16);
    }

    // 4x4 RGBA8: top half red, bottom half blue.
    private static Image MakeImage()
    {
        Image image = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                image.SetPixel(x, y, y < 2 ? new Color(1, 0, 0) : new Color(0, 0, 1));
            }
        }
        return image;
    }

    // 64x64 pattern, so that lossy encoders give different sizes at different qualities.
    private static Image MakePatternImage()
    {
        Image image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                image.SetPixel(x, y, new Color((x * 37 + y * 11) % 64 / 63.0f, x * y % 64 / 63.0f, (x + y * 53) % 64 / 63.0f));
            }
        }
        return image;
    }

    // Uncompressed 2x2 BMP, 24 bits, bottom-up: bottom row green, top row red.
    private static byte[] MakeBmp()
    {
        var data = new List<byte> { 0x42, 0x4D };
        foreach (uint value in new uint[] { 54 + 16, 0, 54, 40, 2, 2 })
        {
            PushU32(data, value);
        }
        PushU16(data, 1);
        PushU16(data, 24);
        foreach (uint value in new uint[] { 0, 16, 2835, 2835, 0, 0 })
        {
            PushU32(data, value);
        }
        for (int row = 0; row < 2; row++)
        {
            for (int x = 0; x < 2; x++)
            {
                data.AddRange(row == 0 ? new byte[] { 0, 255, 0 } : new byte[] { 0, 0, 255 });
            }
            data.AddRange([0, 0]);
        }
        return data.ToArray();
    }

    // Uncompressed true-color 2x2 TGA, 32 bits, top-left origin, all red.
    private static byte[] MakeTga()
    {
        var data = new List<byte> { 0, 0, 2, 0, 0, 0, 0, 0 };
        PushU16(data, 0);
        PushU16(data, 0);
        PushU16(data, 2);
        PushU16(data, 2);
        data.AddRange([32, 0x28]);
        for (int i = 0; i < 4; i++)
        {
            data.AddRange([0, 0, 255, 255]);
        }
        return data.ToArray();
    }

    // Flat 2x2 Radiance HDR, all (1, 0, 0).
    private static byte[] MakeHdr()
    {
        var data = new List<byte>(Encoding.ASCII.GetBytes("#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 2 +X 2\n"));
        for (int i = 0; i < 4; i++)
        {
            data.AddRange([128, 0, 0, 129]);
        }
        return data.ToArray();
    }

    // KTX 1.1, 2x2 GL_RGBA8, one level, all red.
    private static byte[] MakeKtx()
    {
        var data = new List<byte> { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A };
        foreach (uint value in new uint[] { 0x04030201, 0x1401, 1, 0x1908, 0x8058, 0x1908, 2, 2, 0, 0, 1, 1, 0, 16 })
        {
            PushU32(data, value);
        }
        for (int i = 0; i < 4; i++)
        {
            data.AddRange([255, 0, 0, 255]);
        }
        return data.ToArray();
    }

    private static void Write(string path, byte[] data)
    {
        FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreBuffer(data);
        file.Close();
    }

    private ImageTexture ExpectTexture(string path, int width, string label)
    {
        var texture = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as ImageTexture;
        Expect(texture != null, $"{label} loads as ImageTexture");
        if (texture != null)
        {
            Expect(texture.GetWidth() == width, $"{label} has width {width}");
        }
        return texture;
    }

    private Image ExpectImage(string path, int width, string label)
    {
        var image = ResourceLoader.Load(path, "Image", ResourceLoader.CacheMode.Ignore) as Image;
        Expect(image != null, $"{label} loads as Image with the hint \"Image\"");
        if (image != null)
        {
            Expect(image.GetWidth() == width, $"{label} Image has width {width}");
        }
        return image;
    }

    protected override async Task RunChecks()
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        Expect(ClassDB.ClassExists("ImageSaveOptions"), "ImageSaveOptions is registered");

        // ImageSaveOptions defaults.
        var defaults = new ImageSaveOptions();
        Expect(Mathf.IsEqualApprox(defaults.JpgQuality, 0.75f), "jpg_quality defaults to 0.75");
        Expect(!defaults.WebpLossy, "webp_lossy defaults to false");
        Expect(Mathf.IsEqualApprox(defaults.WebpQuality, 0.75f), "webp_quality defaults to 0.75");
        Expect(!defaults.ExrGrayscale, "exr_grayscale defaults to false");

        // PNG: save an ImageTexture, load it back as ImageTexture and as Image.
        string png = Dir + "/round_trip.png";
        ImageTexture texture = ImageTexture.CreateFromImage(MakeImage());
        Expect(ResourceSaver.Save(texture, png) == Error.Ok, "ResourceSaver.save writes .png");
        Expect(ResourceLoader.Exists(png), "ResourceLoader.exists finds the .png");
        Expect(ResourceLoader.Exists(png, "Image"), "ResourceLoader.exists with hint Image");
        ImageTexture pngTexture = ExpectTexture(png, 4, "png");
        if (pngTexture != null)
        {
            Image pixels = pngTexture.GetImage();
            Expect(pixels.GetPixel(0, 0).IsEqualApprox(new Color(1, 0, 0)), "png pixel (0, 0) is red");
            Expect(pixels.GetPixel(3, 3).IsEqualApprox(new Color(0, 0, 1)), "png pixel (3, 3) is blue");
        }
        ExpectImage(png, 4, "png");
        Resource cachedA = ResourceLoader.Load(png);
        Resource cachedB = ResourceLoader.Load(png);
        Expect(cachedA != null && cachedA == cachedB, "png loads through the resource cache");

        // An Image saves directly.
        string imagePng = Dir + "/from_image.png";
        Expect(ResourceSaver.Save(MakeImage(), imagePng) == Error.Ok, "ResourceSaver.save writes an Image to .png");
        ExpectImage(imagePng, 4, "Image png");

        // JPEG with quality options.
        ImageTexture pattern = ImageTexture.CreateFromImage(MakePatternImage());
        var options = new ImageSaveOptions { JpgQuality = 0.1f };
        options.AttachTo(pattern);
        Expect(ImageSaveOptions.GetAttached(pattern) == options, "get_attached returns the attached options");
        Expect(ResourceSaver.Save(pattern, Dir + "/low.jpg") == Error.Ok, "ResourceSaver.save writes .jpg");
        options.JpgQuality = 1.0f;
        Expect(ResourceSaver.Save(pattern, Dir + "/high.jpeg") == Error.Ok, "ResourceSaver.save writes .jpeg");
        Expect(FileAccess.GetSize(Dir + "/low.jpg") < FileAccess.GetSize(Dir + "/high.jpeg"), "jpg_quality changes the file size");
        ExpectTexture(Dir + "/low.jpg", 64, "jpg");
        ExpectTexture(Dir + "/high.jpeg", 64, "jpeg");

        // WebP lossless and lossy.
        ImageSaveOptions.DetachFrom(pattern);
        Expect(ImageSaveOptions.GetAttached(pattern) == null, "detach_from removes the options");
        Expect(ResourceSaver.Save(pattern, Dir + "/lossless.webp") == Error.Ok, "ResourceSaver.save writes lossless .webp");
        options.WebpLossy = true;
        options.WebpQuality = 0.1f;
        options.AttachTo(pattern);
        Expect(ResourceSaver.Save(pattern, Dir + "/lossy.webp") == Error.Ok, "ResourceSaver.save writes lossy .webp");
        Expect(FileAccess.GetSize(Dir + "/lossy.webp") < FileAccess.GetSize(Dir + "/lossless.webp"), "webp_lossy makes a smaller file");
        ExpectTexture(Dir + "/lossless.webp", 64, "webp");

        // OpenEXR and DDS.
        Expect(ResourceSaver.Save(texture, Dir + "/float.exr") == Error.Ok, "ResourceSaver.save writes .exr");
        ExpectTexture(Dir + "/float.exr", 4, "exr");
        Expect(ResourceSaver.Save(MakeImage(), Dir + "/texture.dds") == Error.Ok, "ResourceSaver.save writes .dds");
        ExpectTexture(Dir + "/texture.dds", 4, "dds");
        ExpectImage(Dir + "/texture.dds", 4, "dds");

        // Formats without an encoder: written by hand, loaded through ResourceLoader.
        Write(Dir + "/image.bmp", MakeBmp());
        ImageTexture bmp = ExpectTexture(Dir + "/image.bmp", 2, "bmp");
        if (bmp != null)
        {
            Expect(bmp.GetImage().GetPixel(0, 1).IsEqualApprox(new Color(0, 1, 0)), "bmp bottom row is green");
        }
        Write(Dir + "/image.tga", MakeTga());
        ExpectTexture(Dir + "/image.tga", 2, "tga");
        Write(Dir + "/image.hdr", MakeHdr());
        ExpectTexture(Dir + "/image.hdr", 2, "hdr");
        Write(Dir + "/image.svg", Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"6\"><rect width=\"8\" height=\"6\" fill=\"#ff0000\"/></svg>"));
        ExpectTexture(Dir + "/image.svg", 8, "svg");
        Write(Dir + "/image.ktx", MakeKtx());
        ExpectTexture(Dir + "/image.ktx", 2, "ktx");
        ExpectImage(Dir + "/image.ktx", 2, "ktx");

        // No encoder for bmp.
        Expect(ResourceSaver.Save(texture, Dir + "/nope.bmp") != Error.Ok, "ResourceSaver.save refuses .bmp");

        // Imported files keep their importer.
        Resource imported = GD.Load(ImportedSvg);
        Expect(imported != null && imported is not ImageTexture, "imported res:// svg still loads through the importer");

        // Background loading.
        string threaded = Dir + "/lossless.webp";
        Expect(ResourceLoader.LoadThreadedRequest(threaded) == Error.Ok, "load_threaded_request accepts an image file");
        while (ResourceLoader.LoadThreadedGetStatus(threaded) == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Expect(ResourceLoader.LoadThreadedGetStatus(threaded) == ResourceLoader.ThreadLoadStatus.Loaded, "threaded load finishes");
        var threadedTexture = ResourceLoader.LoadThreadedGet(threaded) as ImageTexture;
        Expect(threadedTexture != null && threadedTexture.GetWidth() == 64, "load_threaded_get returns the ImageTexture");
    }
}
