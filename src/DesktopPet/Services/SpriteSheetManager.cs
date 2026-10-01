using System.IO;
using System.Windows.Media.Imaging;
using DesktopPet.Models;

namespace DesktopPet.Services;

/// <summary>
/// Menyimpan skin aktif. Skin bawaan dimuat sekali lewat <see cref="Load"/>; skin lain diganti
/// secara atomik lewat <see cref="Apply"/> (hot-swap) dan diumumkan melalui <see cref="SkinChanged"/>.
/// </summary>
public class SpriteSheetManager
{
    private static readonly Lazy<SpriteSheetManager> _instance = new(() => new SpriteSheetManager());
    public static SpriteSheetManager Instance => _instance.Value;

    private readonly object _lock = new();
    private SpriteSet? _current;

    public bool IsLoaded => _current != null;
    /// <summary>The built-in skin loaded at startup, kept so switching back needs no reload.</summary>
    public SpriteSet? BuiltIn { get; private set; }
    public SpriteSet Current => _current ?? throw new InvalidOperationException("Sprite sheet has not been loaded.");
    public int CellWidth => Current.CellWidth;
    public int CellHeight => Current.CellHeight;

    public event Action<SpriteSet>? SkinChanged;

    public void Load()
    {
        if (IsLoaded) return;
        lock (_lock)
        {
            if (IsLoaded) return;
            BuiltIn = SpriteSet.Create(LoadBuiltInSheet(), SkinManagerService.BuiltInSkin, null);
            _current = BuiltIn;
        }
    }

    /// <summary>
    /// Replace the active skin. Call on the UI thread; listeners re-render immediately.
    /// </summary>
    public void Apply(SpriteSet spriteSet)
    {
        lock (_lock)
        {
            _current = spriteSet;
        }
        SkinChanged?.Invoke(spriteSet);
    }

    public static BitmapSource LoadBuiltInSheet()
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

    /// <summary>
    /// Decodes fully into memory (no file lock), so users can replace the PNG while the pet runs.
    /// </summary>
    public static BitmapSource LoadFromFile(string path)
    {
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    public BitmapSource[] GetAnimationFrames(PetAnimationState state) => Current.GetAnimationFrames(state);

    public BitmapSource GetGazeFrame(GazeDirection direction) => Current.GetGazeFrame(direction);

    public AnimationDefinition GetDefinition(PetAnimationState state) => Current.GetDefinition(state);
}
