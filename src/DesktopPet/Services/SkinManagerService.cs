using System.Diagnostics;
using System.IO;
using System.Text.Json;
using DesktopPet.Localization;
using DesktopPet.Models;

namespace DesktopPet.Services;

public sealed record SkinLoadResult(SpriteSet? SpriteSet, string? Error)
{
    public bool Success => SpriteSet != null;
}

/// <summary>
/// Mencari dan memuat skin dari <c>%AppData%\DesktopPet\Skins\&lt;id&gt;\</c>.
/// Skin hanya PNG RGBA: decoder WebP bawaan Windows membuang alpha (lihat scripts/import-codex-pet.ps1).
/// </summary>
public class SkinManagerService
{
    public const string BuiltInSkinId = "kawahime";
    public const string CustomSkinPrefix = "skins/";
    public const string DefaultSheetFileName = "spritesheet.png";
    public const string ManifestFileName = "skin.json";
    public const long MaxSheetBytes = 64L * 1024 * 1024;

    // A property so the description follows the current language
    public static SkinInfo BuiltInSkin => new(
        BuiltInSkinId, "Kawahime", null, Loc.T("Skin_BuiltInDescription"), null, null, IsBuiltIn: true, Manifest: null);

    public string SkinsDirectory { get; }

    public SkinManagerService(string? skinsDirectory = null)
    {
        SkinsDirectory = skinsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopPet", "Skins");
    }

    /// <summary>
    /// Skin bawaan + setiap subfolder skin. Folder yang tidak bisa dipakai tetap dikembalikan
    /// dengan <see cref="SkinInfo.Error"/> agar user tahu alasannya.
    /// </summary>
    public IReadOnlyList<SkinInfo> Scan()
    {
        var skins = new List<SkinInfo> { BuiltInSkin };
        if (!Directory.Exists(SkinsDirectory)) return skins;

        IEnumerable<string> folders;
        try
        {
            folders = Directory.GetDirectories(SkinsDirectory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return skins;
        }

        foreach (var folder in folders)
        {
            skins.Add(InspectFolder(folder));
        }
        return skins;
    }

    public SkinInfo? Find(string skinId) =>
        Scan().FirstOrDefault(s => s.Id.Equals(skinId, StringComparison.OrdinalIgnoreCase));

    public static SkinInfo InspectFolder(string folder)
    {
        string folderName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string id = CustomSkinPrefix + folderName;

        SkinManifest? manifest = null;
        string manifestPath = Path.Combine(folder, ManifestFileName);
        if (File.Exists(manifestPath))
        {
            try
            {
                manifest = SkinManifest.Parse(File.ReadAllText(manifestPath));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return new SkinInfo(id, folderName, null, null, folder, null, false, null,
                    Loc.F("Skin_ManifestInvalid", ManifestFileName, ex.Message));
            }
        }

        string displayName = string.IsNullOrWhiteSpace(manifest?.Name) ? folderName : manifest!.Name!.Trim();
        string? author = string.IsNullOrWhiteSpace(manifest?.Author) ? null : manifest!.Author!.Trim();
        string sheetName = string.IsNullOrWhiteSpace(manifest?.Spritesheet) ? DefaultSheetFileName : manifest!.Spritesheet!.Trim();

        SkinInfo Fail(string error) => new(id, displayName, author, manifest?.Description, folder, null, false, manifest, error);

        // The sheet must live inside the skin folder
        string folderFull = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string sheetPath = Path.GetFullPath(Path.Combine(folder, sheetName));
        if (!sheetPath.StartsWith(folderFull, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(Loc.F("Skin_SheetOutsideFolder", sheetName));
        }

        if (!sheetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(Loc.F("Skin_NotPng", sheetName));
        }

        if (!File.Exists(sheetPath))
        {
            return Fail(Loc.F("Skin_SheetMissing", sheetName));
        }

        return new SkinInfo(id, displayName, author, manifest?.Description, folder, sheetPath, false, manifest);
    }

    public Task<SkinLoadResult> LoadAsync(SkinInfo skin) => Task.Run(() => Load(skin));

    /// <summary>
    /// Decodes and slices the skin. Never throws; failures are returned as <see cref="SkinLoadResult.Error"/>.
    /// Safe to call from a background thread (all bitmaps are frozen).
    /// </summary>
    public SkinLoadResult Load(SkinInfo skin)
    {
        if (skin.Error != null) return new SkinLoadResult(null, skin.Error);

        try
        {
            if (skin.IsBuiltIn)
            {
                return new SkinLoadResult(SpriteSet.Create(SpriteSheetManager.LoadBuiltInSheet(), skin, null), null);
            }

            if (skin.SheetPath == null || !File.Exists(skin.SheetPath))
            {
                return new SkinLoadResult(null, Loc.T("Skin_SheetFileMissing"));
            }

            long size = new FileInfo(skin.SheetPath).Length;
            if (size > MaxSheetBytes)
            {
                return new SkinLoadResult(null, Loc.F("Skin_SheetTooLarge", size / (1024 * 1024), MaxSheetBytes / (1024 * 1024)));
            }

            var sheet = SpriteSheetManager.LoadFromFile(skin.SheetPath);
            return new SkinLoadResult(SpriteSet.Create(sheet, skin, skin.Manifest), null);
        }
        catch (InvalidDataException ex)
        {
            return new SkinLoadResult(null, ex.Message);
        }
        catch (Exception ex)
        {
            return new SkinLoadResult(null, Loc.F("Skin_ReadFailed", ex.Message));
        }
    }

    /// <summary>
    /// Creates the skins folder with a README and an example skin.json (only once), then opens it in Explorer.
    /// </summary>
    public void OpenSkinsFolder()
    {
        EnsureSkinsFolder();
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{SkinsDirectory}\"") { UseShellExecute = true });
    }

    public void EnsureSkinsFolder()
    {
        Directory.CreateDirectory(SkinsDirectory);

        // Written once, in the language active at that moment
        string readme = Path.Combine(SkinsDirectory, "README.txt");
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme, WithWindowsNewlines(Loc.T("Skin_Readme")));
        }

        string example = Path.Combine(SkinsDirectory, "skin.example.json");
        if (!File.Exists(example))
        {
            File.WriteAllText(example, WithWindowsNewlines(Loc.T("Skin_ExampleManifest")));
        }
    }

    private static string WithWindowsNewlines(string text) =>
        text.Replace("\r\n", "\n").Replace("\n", "\r\n");
}
