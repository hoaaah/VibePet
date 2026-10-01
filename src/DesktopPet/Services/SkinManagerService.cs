using System.Diagnostics;
using System.IO;
using System.Text.Json;
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

    public static readonly SkinInfo BuiltInSkin = new(
        BuiltInSkinId, "Kawahime", null, "Skin bawaan Desktop Pet.", null, null, IsBuiltIn: true, Manifest: null);

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
                    $"{ManifestFileName} tidak valid: {ex.Message}");
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
            return Fail($"Spritesheet '{sheetName}' harus berada di dalam folder skin.");
        }

        if (!sheetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return Fail($"'{sheetName}' bukan PNG. Hanya PNG RGBA yang didukung karena decoder WebP Windows membuang transparansi; " +
                        "konversi dengan scripts/import-codex-pet.ps1.");
        }

        if (!File.Exists(sheetPath))
        {
            return Fail($"{sheetName} tidak ditemukan.");
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
                return new SkinLoadResult(null, "File spritesheet tidak ditemukan.");
            }

            long size = new FileInfo(skin.SheetPath).Length;
            if (size > MaxSheetBytes)
            {
                return new SkinLoadResult(null, $"File spritesheet terlalu besar ({size / (1024 * 1024)} MB; maks {MaxSheetBytes / (1024 * 1024)} MB).");
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
            return new SkinLoadResult(null, $"Gagal membaca gambar: {ex.Message}");
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

        string readme = Path.Combine(SkinsDirectory, "README.txt");
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme, ReadmeText);
        }

        string example = Path.Combine(SkinsDirectory, "skin.example.json");
        if (!File.Exists(example))
        {
            File.WriteAllText(example, ExampleManifest);
        }
    }

    private const string ReadmeText =
        """
        Desktop Pet — Folder Skin
        =========================

        Setiap skin adalah satu folder di sini:

          Skins\<NamaSkin>\spritesheet.png   (wajib, PNG RGBA dengan transparansi)
          Skins\<NamaSkin>\skin.json         (opsional, lihat skin.example.json)

        Kontrak atlas (sama dengan pet Codex v2):
          - 8 kolom x 11 baris sel. Bawaan: sel 192 x 208 px, atlas 1536 x 2288 px.
          - Baris 0-8 : idle, running-right, running-left, waving, jumping, failed, waiting, running, review
          - Baris 9-10: 16 arah pandang (0 derajat = atas, searah jarum jam, langkah 22,5 derajat)

        skin.json boleh mengubah ukuran sel, jumlah frame (1-8), durasi tiap frame (40-2000 ms), dan loop.
        Urutan baris tidak bisa diubah. waving dan jumping selalu diputar sekali.

        Pet Codex (spritesheet.webp) harus dikonversi ke PNG dulu, karena decoder WebP
        bawaan Windows membuang transparansi. Gunakan:
          powershell -File scripts\import-codex-pet.ps1 -Name <nama-pet>

        Setelah menambah folder, pilih skin di Panel Kontrol (tombol "Muat Ulang Daftar")
        atau klik kanan pet > Skin. Tidak perlu restart.
        """;

    private const string ExampleManifest =
        """
        {
          // Salin file ini ke Skins\<NamaSkin>\skin.json lalu ubah sesuai kebutuhan.
          "name": "Nama Skin",
          "author": "Nama Kamu",
          "description": "Deskripsi singkat",
          "spritesheet": "spritesheet.png",
          "cellWidth": 192,
          "cellHeight": 208,
          "animations": {
            "idle":    { "frames": 6, "durations": [280, 110, 110, 140, 140, 320] },
            "running": { "durations": [120, 120, 120, 120, 120, 220] }
          }
        }
        """;
}
