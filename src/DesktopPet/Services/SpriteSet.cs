using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Models;

namespace DesktopPet.Services;

/// <summary>
/// Semua frame satu skin, sudah di-decode dan di-freeze sekali saat dimuat.
/// Immutable dan aman dibuat di thread background lalu dipakai di UI thread.
/// </summary>
public sealed class SpriteSet
{
    public const int MaxCellSize = 2048;

    private readonly Dictionary<PetAnimationState, BitmapSource[]> _animationFrames;
    private readonly Dictionary<GazeDirection, BitmapSource> _gazeFrames;

    public SkinInfo Skin { get; }
    public int CellWidth { get; }
    public int CellHeight { get; }
    public IReadOnlyDictionary<PetAnimationState, AnimationDefinition> Definitions { get; }
    public IReadOnlyList<string> Warnings { get; }

    private SpriteSet(
        SkinInfo skin, int cellWidth, int cellHeight,
        IReadOnlyDictionary<PetAnimationState, AnimationDefinition> definitions,
        Dictionary<PetAnimationState, BitmapSource[]> animationFrames,
        Dictionary<GazeDirection, BitmapSource> gazeFrames,
        IReadOnlyList<string> warnings)
    {
        Skin = skin;
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        Definitions = definitions;
        _animationFrames = animationFrames;
        _gazeFrames = gazeFrames;
        Warnings = warnings;
    }

    /// <exception cref="InvalidDataException">Atlas tidak memenuhi kontrak (ukuran, alpha).</exception>
    public static SpriteSet Create(BitmapSource sheet, SkinInfo skin, SkinManifest? manifest)
    {
        int cellWidth = manifest?.CellWidth ?? AnimationCatalog.CellWidth;
        int cellHeight = manifest?.CellHeight ?? AnimationCatalog.CellHeight;

        var errors = ValidateSheet(sheet.PixelWidth, sheet.PixelHeight, HasAlpha(sheet), cellWidth, cellHeight);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(" ", errors));
        }

        var warnings = new List<string>();
        var definitions = AnimationCatalog.BuildDefinitions(manifest, warnings);

        // Normalize once so every frame shares a render-friendly format with alpha
        BitmapSource source = sheet.Format == PixelFormats.Bgra32 || sheet.Format == PixelFormats.Pbgra32
            ? sheet
            : new FormatConvertedBitmap(sheet, PixelFormats.Bgra32, null, 0);

        var animationFrames = new Dictionary<PetAnimationState, BitmapSource[]>();
        foreach (var (state, def) in definitions)
        {
            animationFrames[state] = def.FrameIndices
                .Select(col => ExtractCell(source, def.RowIndex, col, cellWidth, cellHeight))
                .ToArray();
        }

        var gazeFrames = new Dictionary<GazeDirection, BitmapSource>();
        foreach (GazeDirection gaze in Enum.GetValues<GazeDirection>())
        {
            var (row, col) = AnimationCatalog.GetGazeCell(gaze);
            gazeFrames[gaze] = ExtractCell(source, row, col, cellWidth, cellHeight);
        }

        return new SpriteSet(skin, cellWidth, cellHeight, definitions, animationFrames, gazeFrames, warnings);
    }

    /// <summary>
    /// Kontrak atlas: 8 kolom × 11 baris sel, dan gambar harus punya kanal alpha.
    /// Gambar boleh lebih besar dari atlas; area sisa diabaikan.
    /// </summary>
    public static List<string> ValidateSheet(int width, int height, bool hasAlpha, int cellWidth, int cellHeight)
    {
        var errors = new List<string>();

        if (cellWidth <= 0 || cellHeight <= 0 || cellWidth > MaxCellSize || cellHeight > MaxCellSize)
        {
            errors.Add($"Ukuran sel {cellWidth}×{cellHeight} tidak valid (1–{MaxCellSize} px).");
            return errors;
        }

        long neededWidth = (long)cellWidth * AnimationCatalog.SheetColumns;
        long neededHeight = (long)cellHeight * AnimationCatalog.SheetRows;
        if (width < neededWidth || height < neededHeight)
        {
            errors.Add($"Atlas {width}×{height} px terlalu kecil; butuh minimal {neededWidth}×{neededHeight} px " +
                       $"({AnimationCatalog.SheetColumns} kolom × {AnimationCatalog.SheetRows} baris sel {cellWidth}×{cellHeight}).");
        }

        if (!hasAlpha)
        {
            errors.Add("Gambar tidak punya kanal alpha (transparansi); latar pet akan terlihat kotak. Simpan sebagai PNG RGBA.");
        }

        return errors;
    }

    public static bool HasAlpha(BitmapSource bitmap)
    {
        var format = bitmap.Format;
        if (format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32
            || format == PixelFormats.Rgba64 || format == PixelFormats.Prgba64
            || format == PixelFormats.Rgba128Float || format == PixelFormats.Prgba128Float)
        {
            return true;
        }

        // Paletted PNG with a transparent palette entry (tRNS)
        return bitmap.Palette?.Colors.Any(c => c.A < 255) == true;
    }

    /// <summary>
    /// Copies one cell into its own frozen bitmap, so the full atlas can be released and
    /// rendering never re-crops the sheet.
    /// </summary>
    private static BitmapSource ExtractCell(BitmapSource sheet, int row, int col, int cellWidth, int cellHeight)
    {
        var rect = new Int32Rect(col * cellWidth, row * cellHeight, cellWidth, cellHeight);
        int stride = cellWidth * ((sheet.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[stride * cellHeight];
        sheet.CopyPixels(rect, pixels, stride, 0);

        var frame = BitmapSource.Create(cellWidth, cellHeight, sheet.DpiX, sheet.DpiY, sheet.Format, sheet.Palette, pixels, stride);
        frame.Freeze();
        return frame;
    }

    public BitmapSource[] GetAnimationFrames(PetAnimationState state) =>
        _animationFrames.TryGetValue(state, out var frames)
            ? frames
            : throw new ArgumentOutOfRangeException(nameof(state), $"Animation state {state} is not cached.");

    public BitmapSource GetGazeFrame(GazeDirection direction) =>
        _gazeFrames.TryGetValue(direction, out var frame)
            ? frame
            : throw new ArgumentOutOfRangeException(nameof(direction), $"Gaze direction {direction} is not cached.");

    public AnimationDefinition GetDefinition(PetAnimationState state) =>
        Definitions.TryGetValue(state, out var def)
            ? def
            : throw new ArgumentOutOfRangeException(nameof(state), $"Animation state {state} is not defined.");
}
