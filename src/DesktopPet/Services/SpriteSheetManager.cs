using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using DesktopPet.Models;

namespace DesktopPet.Services;

public class SpriteSheetManager
{
    private static readonly Lazy<SpriteSheetManager> _instance = new(() => new SpriteSheetManager());
    public static SpriteSheetManager Instance => _instance.Value;

    private readonly Dictionary<PetAnimationState, BitmapSource[]> _animationFrames = new();
    private readonly Dictionary<GazeDirection, BitmapSource> _gazeFrames = new();

    private readonly object _lock = new();
    public bool IsLoaded { get; private set; }

    public void Load()
    {
        if (IsLoaded) return;
        lock (_lock)
        {
            if (IsLoaded) return;

            BitmapSource fullSheet = LoadSheetSource();

            // Slice animation frames (rows 0-8)
            foreach (var (state, def) in AnimationCatalog.Animations)
            {
                var frames = new BitmapSource[def.FrameCount];
                for (int i = 0; i < def.FrameCount; i++)
                {
                    int col = def.FrameIndices[i];
                    int row = def.RowIndex;
                    frames[i] = ExtractCell(fullSheet, row, col);
                }
                _animationFrames[state] = frames;
            }

            // Slice gaze poses (rows 9-10)
            foreach (GazeDirection gaze in Enum.GetValues<GazeDirection>())
            {
                var (row, col) = AnimationCatalog.GetGazeCell(gaze);
                _gazeFrames[gaze] = ExtractCell(fullSheet, row, col);
            }

            IsLoaded = true;
        }
    }

    private static BitmapSource LoadSheetSource()
    {
        // 1. Try Component Pack URI
        string[] packUris =
        [
            "pack://application:,,,/DesktopPet;component/Assets/spritesheet.png",
            "pack://application:,,,/Assets/spritesheet.png"
        ];

        foreach (var uriStr in packUris)
        {
            try
            {
                var uri = new Uri(uriStr, UriKind.Absolute);
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = uri;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                // Continue to next option
            }
        }

        // 2. Search local and parent directories
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            string p1 = Path.Combine(current, "Assets", "spritesheet.png");
            if (File.Exists(p1)) return LoadFromFile(p1);

            string p2 = Path.Combine(current, "src", "DesktopPet", "Assets", "spritesheet.png");
            if (File.Exists(p2)) return LoadFromFile(p2);

            string p3 = Path.Combine(current, "spritesheet.png");
            if (File.Exists(p3)) return LoadFromFile(p3);

            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        throw new FileNotFoundException("Cannot find spritesheet.png as WPF resource or local file.");
    }

    private static BitmapSource LoadFromFile(string path)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    private static BitmapSource ExtractCell(BitmapSource sheet, int row, int col)
    {
        var rect = new Int32Rect(
            col * AnimationCatalog.CellWidth,
            row * AnimationCatalog.CellHeight,
            AnimationCatalog.CellWidth,
            AnimationCatalog.CellHeight
        );

        var cropped = new CroppedBitmap(sheet, rect);
        // Convert to WriteableBitmap or freeze to ensure high performance
        var frozen = new FormatConvertedBitmap(cropped, cropped.Format, null, 0);
        frozen.Freeze();
        return frozen;
    }

    public BitmapSource[] GetAnimationFrames(PetAnimationState state)
    {
        if (!_animationFrames.TryGetValue(state, out var frames))
        {
            throw new ArgumentOutOfRangeException(nameof(state), $"Animation state {state} is not cached.");
        }
        return frames;
    }

    public BitmapSource GetGazeFrame(GazeDirection direction)
    {
        if (!_gazeFrames.TryGetValue(direction, out var frame))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), $"Gaze direction {direction} is not cached.");
        }
        return frame;
    }
}
