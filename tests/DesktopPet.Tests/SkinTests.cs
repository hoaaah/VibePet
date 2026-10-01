using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Models;
using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

[Collection(LocalizationCollection.Name)]
public class SkinTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DesktopPetSkinTests", Guid.NewGuid().ToString("N"));

    public SkinTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private static void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null) throw ex;
    }

    /// <summary>
    /// Writes an 8×11 atlas. Every cell's top-left pixel encodes (row, col) in its R/G channels;
    /// everything else is transparent.
    /// </summary>
    private static void WriteAtlas(string path, int cellWidth, int cellHeight, bool withAlpha = true)
    {
        int width = cellWidth * AnimationCatalog.SheetColumns;
        int height = cellHeight * AnimationCatalog.SheetRows;
        int stride = width * 4;
        var pixels = new byte[stride * height];

        for (int row = 0; row < AnimationCatalog.SheetRows; row++)
        {
            for (int col = 0; col < AnimationCatalog.SheetColumns; col++)
            {
                int offset = (row * cellHeight) * stride + (col * cellWidth) * 4;
                pixels[offset + 0] = 7;              // B
                pixels[offset + 1] = (byte)col;      // G
                pixels[offset + 2] = (byte)row;      // R
                pixels[offset + 3] = 255;            // A
            }
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        if (!withAlpha)
        {
            bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static (byte R, byte G, byte A) TopLeftPixel(BitmapSource frame)
    {
        var pixel = new byte[4];
        frame.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), pixel, 4, 0);
        return (pixel[2], pixel[1], pixel[3]);
    }

    // ---- skin.json & animation definitions ---------------------------------------------

    [Theory]
    [InlineData("idle", PetAnimationState.Idle)]
    [InlineData("running-right", PetAnimationState.RunningRight)]
    [InlineData("Running_Left", PetAnimationState.RunningLeft)]
    [InlineData("REVIEW", PetAnimationState.Review)]
    [InlineData("running", PetAnimationState.Running)]
    public void TryParseStateKey_AcceptsAgentsMdNames(string key, PetAnimationState expected)
    {
        Assert.True(AnimationCatalog.TryParseStateKey(key, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData("gaze")]
    [InlineData("dancing")]
    [InlineData("3")]
    [InlineData("")]
    public void TryParseStateKey_RejectsUnknownKeys(string key)
    {
        Assert.False(AnimationCatalog.TryParseStateKey(key, out _));
    }

    [Fact]
    public void BuildDefinitions_WithoutManifest_ReturnsDefaults()
    {
        var defs = AnimationCatalog.BuildDefinitions(null);
        foreach (var (state, def) in AnimationCatalog.Animations)
        {
            Assert.Equal(def, defs[state]);
        }
    }

    [Fact]
    public void BuildDefinitions_AppliesOverridesAndKeepsRows()
    {
        var manifest = SkinManifest.Parse("""
            {
              "animations": {
                "idle": { "frames": 3, "durations": [100, 200, 300] },
                "running": { "durations": [90, 90, 90, 90, 90, 180] }
              }
            }
            """);
        var warnings = new List<string>();

        var defs = AnimationCatalog.BuildDefinitions(manifest, warnings);

        Assert.Empty(warnings);
        Assert.Equal(new[] { 0, 1, 2 }, defs[PetAnimationState.Idle].FrameIndices);
        Assert.Equal(new[] { 100, 200, 300 }, defs[PetAnimationState.Idle].FrameDurationsMs);
        Assert.Equal(0, defs[PetAnimationState.Idle].RowIndex);
        Assert.Equal(new[] { 90, 90, 90, 90, 90, 180 }, defs[PetAnimationState.Running].FrameDurationsMs);
        Assert.Equal(7, defs[PetAnimationState.Running].RowIndex);
        Assert.Equal(AnimationCatalog.Animations[PetAnimationState.Jumping], defs[PetAnimationState.Jumping]);
    }

    [Fact]
    public void BuildDefinitions_RepairsInvalidValuesWithWarnings()
    {
        var manifest = SkinManifest.Parse("""
            {
              "animations": {
                "idle": { "frames": 12 },
                "review": { "frames": 4, "durations": [5, 99999] },
                "waving": { "loop": true },
                "dancing": { "frames": 2 }
              }
            }
            """);
        var warnings = new List<string>();

        var defs = AnimationCatalog.BuildDefinitions(manifest, warnings);

        Assert.Equal(8, defs[PetAnimationState.Idle].FrameCount);
        Assert.Equal(4, defs[PetAnimationState.Review].FrameCount);
        Assert.Equal(new[] { 40, 2000, 2000, 2000 }, defs[PetAnimationState.Review].FrameDurationsMs);
        Assert.False(defs[PetAnimationState.Waving].IsLooping);
        Assert.Equal(4, warnings.Count);
    }

    [Fact]
    public void SkinManifest_Parse_IsLenient()
    {
        var manifest = SkinManifest.Parse("""
            {
              // comment
              "Name": "Summer",
              "AUTHOR": "Arief",
              "cellwidth": 96,
              "cellHeight": 104,
            }
            """);

        Assert.Equal("Summer", manifest.Name);
        Assert.Equal("Arief", manifest.Author);
        Assert.Equal(96, manifest.CellWidth);
        Assert.Equal(104, manifest.CellHeight);
    }

    // ---- sheet validation ---------------------------------------------------------------

    [Fact]
    public void ValidateSheet_AcceptsDefaultAtlasAndLargerImages()
    {
        Assert.Empty(SpriteSet.ValidateSheet(1536, 2288, true, 192, 208));
        Assert.Empty(SpriteSet.ValidateSheet(2000, 3000, true, 192, 208));
    }

    [Fact]
    public void ValidateSheet_ReportsSizeAndAlphaProblems()
    {
        Assert.Single(SpriteSet.ValidateSheet(1535, 2288, true, 192, 208));
        Assert.Single(SpriteSet.ValidateSheet(1536, 2288, false, 192, 208));
        Assert.Equal(2, SpriteSet.ValidateSheet(100, 100, false, 192, 208).Count);
        Assert.Single(SpriteSet.ValidateSheet(1536, 2288, true, 0, 208));
        Assert.Single(SpriteSet.ValidateSheet(1536, 2288, true, 5000, 208));
    }

    // ---- scanning -----------------------------------------------------------------------

    [Fact]
    public void Scan_MissingFolder_ReturnsOnlyBuiltIn()
    {
        var manager = new SkinManagerService(Path.Combine(_root, "does-not-exist"));
        var skins = manager.Scan();
        Assert.Single(skins);
        Assert.True(skins[0].IsBuiltIn);
        Assert.Equal(SkinManagerService.BuiltInSkinId, skins[0].Id);
    }

    [Fact]
    public void Scan_ReportsValidAndBrokenSkins()
    {
        RunInSta(() =>
        {
            WriteAtlas(Path.Combine(_root, "plain", "spritesheet.png"), 24, 26);

            WriteAtlas(Path.Combine(_root, "named", "spritesheet.png"), 24, 26);
            File.WriteAllText(Path.Combine(_root, "named", "skin.json"), """{ "name": "Summer", "author": "Arief" }""");

            Directory.CreateDirectory(Path.Combine(_root, "broken-json"));
            File.WriteAllText(Path.Combine(_root, "broken-json", "skin.json"), "{ not json");

            Directory.CreateDirectory(Path.Combine(_root, "empty"));

            Directory.CreateDirectory(Path.Combine(_root, "webp"));
            File.WriteAllText(Path.Combine(_root, "webp", "skin.json"), """{ "spritesheet": "spritesheet.webp" }""");
            File.WriteAllBytes(Path.Combine(_root, "webp", "spritesheet.webp"), [0]);

            Directory.CreateDirectory(Path.Combine(_root, "escape"));
            File.WriteAllText(Path.Combine(_root, "escape", "skin.json"), """{ "spritesheet": "../plain/spritesheet.png" }""");
        });

        var skins = new SkinManagerService(_root).Scan().ToDictionary(s => s.Id);

        Assert.True(skins[SkinManagerService.BuiltInSkinId].IsBuiltIn);

        var plain = skins["skins/plain"];
        Assert.True(plain.IsValid);
        Assert.Equal("plain", plain.DisplayName);

        var named = skins["skins/named"];
        Assert.True(named.IsValid);
        Assert.Equal("Summer — Arief", named.ListLabel);

        Assert.Contains("skin.json", skins["skins/broken-json"].Error);
        Assert.Contains("tidak ditemukan", skins["skins/empty"].Error);
        Assert.Contains("PNG", skins["skins/webp"].Error);
        Assert.Contains("di dalam folder", skins["skins/escape"].Error);
    }

    // ---- loading & slicing --------------------------------------------------------------

    [Fact]
    public void Load_CustomCellSize_SlicesEveryCellFromTheRightPosition()
    {
        RunInSta(() =>
        {
            string folder = Path.Combine(_root, "small");
            WriteAtlas(Path.Combine(folder, "spritesheet.png"), 24, 26);
            File.WriteAllText(Path.Combine(folder, "skin.json"), """
                { "cellWidth": 24, "cellHeight": 26, "animations": { "waving": { "frames": 2 } } }
                """);

            var manager = new SkinManagerService(_root);
            var result = manager.Load(manager.Find("skins/small")!);

            Assert.True(result.Success, result.Error);
            var set = result.SpriteSet!;
            Assert.Equal(24, set.CellWidth);
            Assert.Equal(26, set.CellHeight);

            foreach (var (state, def) in set.Definitions)
            {
                var frames = set.GetAnimationFrames(state);
                Assert.Equal(def.FrameCount, frames.Length);
                for (int i = 0; i < frames.Length; i++)
                {
                    Assert.Equal(24, frames[i].PixelWidth);
                    Assert.Equal(26, frames[i].PixelHeight);
                    Assert.Equal(((byte)def.RowIndex, (byte)def.FrameIndices[i], (byte)255), TopLeftPixel(frames[i]));
                }
            }
            Assert.Equal(2, set.GetAnimationFrames(PetAnimationState.Waving).Length);

            foreach (GazeDirection gaze in Enum.GetValues<GazeDirection>())
            {
                var (row, col) = AnimationCatalog.GetGazeCell(gaze);
                Assert.Equal(((byte)row, (byte)col, (byte)255), TopLeftPixel(set.GetGazeFrame(gaze)));
            }
        });
    }

    [Fact]
    public void Load_ImageWithoutAlpha_FailsWithClearMessage()
    {
        RunInSta(() =>
        {
            WriteAtlas(Path.Combine(_root, "opaque", "spritesheet.png"), 24, 26, withAlpha: false);
            File.WriteAllText(Path.Combine(_root, "opaque", "skin.json"), """{ "cellWidth": 24, "cellHeight": 26 }""");

            var manager = new SkinManagerService(_root);
            var result = manager.Load(manager.Find("skins/opaque")!);

            Assert.False(result.Success);
            Assert.Contains("alpha", result.Error);
        });
    }

    [Fact]
    public void Load_WrongCellSize_FailsInsteadOfCroppingOutside()
    {
        RunInSta(() =>
        {
            // Atlas built with 24×26 cells but skin.json claims the default 192×208
            WriteAtlas(Path.Combine(_root, "mismatch", "spritesheet.png"), 24, 26);

            var manager = new SkinManagerService(_root);
            var result = manager.Load(manager.Find("skins/mismatch")!);

            Assert.False(result.Success);
            Assert.Contains("terlalu kecil", result.Error);
        });
    }

    [Fact]
    public void BuiltInSheet_HasAlphaAndMatchesContract()
    {
        RunInSta(() =>
        {
            var sheet = SpriteSheetManager.LoadBuiltInSheet();
            Assert.True(SpriteSet.HasAlpha(sheet));
            Assert.Empty(SpriteSet.ValidateSheet(sheet.PixelWidth, sheet.PixelHeight, true, AnimationCatalog.CellWidth, AnimationCatalog.CellHeight));
        });
    }

    // ---- hot-swap -----------------------------------------------------------------------

    [Fact]
    public void Apply_HotSwapsPlayerFramesAndKeepsState()
    {
        RunInSta(() =>
        {
            string folder = Path.Combine(_root, "swap");
            WriteAtlas(Path.Combine(folder, "spritesheet.png"), 24, 26);
            File.WriteAllText(Path.Combine(folder, "skin.json"), """
                { "cellWidth": 24, "cellHeight": 26, "animations": { "running": { "frames": 3 } } }
                """);
            var skins = new SkinManagerService(_root);
            var custom = skins.Load(skins.Find("skins/swap")!).SpriteSet!;

            var sheets = new SpriteSheetManager();
            sheets.Load();
            var player = new SpritePlayer(sheets);
            BitmapSource? lastFrame = null;
            player.FrameUpdated += f => lastFrame = f;

            player.PlayAnimation(PetAnimationState.Running);
            Assert.Equal(192, lastFrame!.PixelWidth);
            Assert.Equal(6, player.TotalFrames);

            sheets.Apply(custom);

            Assert.Equal(PetAnimationState.Running, player.CurrentState);
            Assert.Equal(3, player.TotalFrames);
            Assert.Equal(24, lastFrame!.PixelWidth);
            Assert.Equal(((byte)7, (byte)0, (byte)255), TopLeftPixel(lastFrame));

            // Gaze stays gaze after another swap
            player.ShowGaze(GazeDirection.Deg90);
            sheets.Apply(sheets.BuiltIn!);
            Assert.True(player.IsStaticGaze);
            Assert.Equal(PetAnimationState.Gaze, player.CurrentState);
            Assert.Equal(192, lastFrame!.PixelWidth);
            Assert.Equal(192, sheets.CellWidth);
        });
    }
}
