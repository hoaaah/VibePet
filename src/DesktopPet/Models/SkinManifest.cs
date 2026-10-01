using System.Text.Json;

namespace DesktopPet.Models;

/// <summary>
/// Isi opsional <c>skin.json</c> di folder skin. Semua field opsional; yang tidak diisi memakai nilai bawaan Kawahime.
/// Layout baris atlas tetap kontrak Codex v2 (baris 0–8 animasi, 9–10 arah pandang, 8 kolom).
/// </summary>
public sealed class SkinManifest
{
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Spritesheet { get; set; }
    public int? CellWidth { get; set; }
    public int? CellHeight { get; set; }

    /// <summary>Kunci: idle, running-right, running-left, waving, jumping, failed, waiting, running, review.</summary>
    public Dictionary<string, AnimationOverride>? Animations { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <exception cref="JsonException">JSON tidak valid.</exception>
    public static SkinManifest Parse(string json) =>
        JsonSerializer.Deserialize<SkinManifest>(json, Options) ?? new SkinManifest();
}

public sealed class AnimationOverride
{
    public int? Frames { get; set; }
    public int[]? Durations { get; set; }
    public bool? Loop { get; set; }
}

/// <summary>
/// Skin yang ditemukan saat scan. <see cref="Error"/> terisi bila folder skin tidak bisa dipakai.
/// </summary>
public sealed record SkinInfo(
    string Id,
    string DisplayName,
    string? Author,
    string? Description,
    string? Directory,
    string? SheetPath,
    bool IsBuiltIn,
    SkinManifest? Manifest,
    string? Error = null)
{
    public bool IsValid => Error == null;

    public string ListLabel
    {
        get
        {
            string label = Author == null ? DisplayName : $"{DisplayName} — {Author}";
            if (IsBuiltIn) label += " (bawaan)";
            if (Error != null) label += " ⚠";
            return label;
        }
    }
}
