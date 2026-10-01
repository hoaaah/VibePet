using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DesktopPet.Localization;

namespace DesktopPet.Services;

/// <summary>
/// Identitas proses yang ramah pengguna, menggantikan PID.
/// AppName = nama aplikasi (misal "Visual Studio Code"),
/// Context = project/dokumen/skrip yang sedang dibuka (misal "pet-ag", "Laporan.docx"),
/// Detail = info tambahan opsional (misal file aktif di VS Code).
/// </summary>
public sealed record ProcessIdentity(string AppName, string? Context = null, string? Detail = null)
{
    public string DisplayName
    {
        get
        {
            var sb = new StringBuilder(AppName);
            if (Context != null) sb.Append(" — ").Append(Context);
            if (Detail != null) sb.Append(" (").Append(Detail).Append(')');
            return sb.ToString();
        }
    }

    public string ShortLabel => Context == null ? AppName : $"{AppName}: {Context}";
}

public static class ProcessNameFormatter
{
    private sealed record AppProfile(string FriendlyName, string[] TitleAliases, bool WorkspaceLastInTitle = false);

    private static readonly Dictionary<string, AppProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dotnet"] = new(".NET CLI", []),
        ["msbuild"] = new("MSBuild", []),
        ["node"] = new("Node.js", []),
        ["pwsh"] = new("PowerShell", ["PowerShell"]),
        ["powershell"] = new("Windows PowerShell", ["Windows PowerShell"]),
        ["cmd"] = new("Command Prompt", ["Command Prompt"]),
        ["code"] = new("Visual Studio Code", ["Visual Studio Code"], WorkspaceLastInTitle: true),
        ["code - insiders"] = new("VS Code Insiders", ["Visual Studio Code - Insiders"], WorkspaceLastInTitle: true),
        ["cursor"] = new("Cursor", ["Cursor"], WorkspaceLastInTitle: true),
        ["devenv"] = new("Visual Studio", ["Microsoft Visual Studio"]),
        ["cargo"] = new("Rust Cargo", []),
        ["rustc"] = new("Rust Compiler", []),
        ["ffmpeg"] = new("FFmpeg", []),
        ["python"] = new("Python", []),
        ["py"] = new("Python", []),
        ["java"] = new("Java", []),
        ["git"] = new("Git", []),
        ["docker"] = new("Docker", []),
        ["winword"] = new("Microsoft Word", ["Word", "Microsoft Word"]),
        ["excel"] = new("Microsoft Excel", ["Excel", "Microsoft Excel"]),
        ["powerpnt"] = new("Microsoft PowerPoint", ["PowerPoint", "Microsoft PowerPoint"]),
        ["notepad"] = new("Notepad", ["Notepad"]),
        ["notepad++"] = new("Notepad++", ["Notepad++"]),
        ["acrobat"] = new("Adobe Acrobat", ["Adobe Acrobat", "Adobe Acrobat Reader", "Adobe Acrobat Pro"]),
        ["explorer"] = new("File Explorer", ["File Explorer"]),
        ["chrome"] = new("Google Chrome", ["Google Chrome"]),
        ["msedge"] = new("Microsoft Edge", ["Microsoft Edge"]),
        ["firefox"] = new("Mozilla Firefox", ["Mozilla Firefox"]),
        ["gitkraken"] = new("GitKraken", ["GitKraken Desktop", "GitKraken"]),
        ["claude"] = new("Claude Code", []),
        ["codex"] = new("Codex CLI", []),
        ["agy"] = new("Antigravity", ["Antigravity"]),
        ["antigravity"] = new("Antigravity", ["Antigravity"], WorkspaceLastInTitle: true),
    };

    // Aplikasi GUI interaktif: penggunaan CPU-nya (render, tab browser) bukan "pekerjaan komputer",
    // jadi hanya memunculkan balon, tidak memicu state ComputerWork.
    private static readonly HashSet<string> InteractiveApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "code - insiders", "cursor", "antigravity", "devenv",
        "winword", "excel", "powerpnt", "notepad", "notepad++", "acrobat",
        "explorer", "chrome", "msedge", "firefox", "gitkraken",
    };

    // Segmen judul yang hanya status, bukan nama dokumen.
    private static readonly HashSet<string> TitleNoise = new(StringComparer.OrdinalIgnoreCase)
    {
        "Compatibility Mode", "Mode Kompatibilitas", "Read-Only", "Hanya-Baca", "Hanya Baca",
        "Protected View", "Tampilan Terproteksi", "Saved", "Tersimpan",
    };

    // Launcher Node.js yang lebih dikenal dengan nama perintahnya.
    private static readonly Dictionary<string, string> NodeLaunchers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["npm-cli.js"] = "npm",
        ["npx-cli.js"] = "npx",
        ["yarn.js"] = "yarn",
        ["yarn.cjs"] = "yarn",
        ["pnpm.cjs"] = "pnpm",
    };

    private const int MaxContextLength = 60;
    public const int TrayTextLimit = 63;

    private static readonly Regex TitleSeparator = new(@"\s+[-–—]\s+", RegexOptions.Compiled);
    // Penanda jumlah tab browser: "and 24 more pages" / "dan 24 halaman lainnya"
    private static readonly Regex BrowserTabCount = new(@"\s+(and \d+ more pages?|dan \d+ halaman lainnya)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WindowsSwitch = new(@"^/[A-Za-z?][\w:-]*$", RegexOptions.Compiled);
    private static readonly Regex SimpleWord = new(@"^[A-Za-z][\w:.-]*$", RegexOptions.Compiled);
    private static readonly Regex CamelBoundary = new(@"(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

    public static string NormalizeProcessName(string processName)
    {
        string clean = processName.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^4];
        }
        return clean;
    }

    /// <summary>
    /// True untuk tool CLI/build/agent (termasuk proses yang tidak dikenal): CPU-nya yang sibuk berarti komputer sedang bekerja.
    /// </summary>
    public static bool CountsAsWork(string processName) =>
        !InteractiveApps.Contains(NormalizeProcessName(processName));

    public static string GetFriendlyAppName(string processName)
    {
        string clean = NormalizeProcessName(processName);
        if (Profiles.TryGetValue(clean, out var profile)) return profile.FriendlyName;
        if (clean.Length == 0) return Loc.T("Process_Unknown");

        // "my_build-tool" -> "My Build Tool", "myBuildTool" -> "My Build Tool"
        var words = CamelBoundary.Replace(clean, " ")
            .Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        string result = string.Join(' ', words);
        return result.Length == 0 ? clean : result;
    }

    /// <summary>
    /// Mengambil nama project/dokumen dari judul jendela, misal
    /// "● MainWindow.xaml.cs - pet-ag - Visual Studio Code" -> ("pet-ag", "MainWindow.xaml.cs"),
    /// "Laporan.docx - Word" -> ("Laporan.docx", null).
    /// </summary>
    public static (string? Context, string? Detail) ParseWindowTitle(string processName, string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return (null, null);

        string clean = NormalizeProcessName(processName);
        Profiles.TryGetValue(clean, out var profile);

        // Edge menyisipkan zero-width space pada "Microsoft​ Edge"; penanda file belum disimpan (●, *) dibuang.
        string text = BrowserTabCount.Replace(title.Replace("​", ""), "").Trim().TrimStart('●', '•', '*').Trim();

        var aliases = new List<string> { clean, GetFriendlyAppName(clean) };
        if (profile != null) aliases.AddRange(profile.TitleAliases);
        aliases.Sort((a, b) => b.Length.CompareTo(a.Length));

        if (aliases.Any(a => text.Equals(a, StringComparison.OrdinalIgnoreCase))) return (null, null);

        // Buang akhiran nama aplikasi sebelum memecah, karena alias bisa mengandung " - ".
        bool stripped;
        do
        {
            stripped = false;
            foreach (var alias in aliases)
            {
                var m = Regex.Match(text, @"\s+[-–—]\s+" + Regex.Escape(alias) + "$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    text = text[..m.Index].TrimEnd();
                    stripped = true;
                    break;
                }
            }
        } while (stripped);

        var segments = TitleSeparator.Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && !TitleNoise.Contains(s))
            .ToList();

        if (segments.Count == 0) return (null, null);

        if (profile?.WorkspaceLastInTitle == true && segments.Count >= 2)
        {
            // Pola VS Code: "<file aktif> - <folder/workspace>"
            string workspace = segments[^1];
            string activeFile = string.Join(" - ", segments.Take(segments.Count - 1));
            return (Truncate(workspace, MaxContextLength), Truncate(activeFile, MaxContextLength));
        }

        return (Truncate(string.Join(" - ", segments), MaxContextLength), null);
    }

    /// <summary>
    /// Memecah command line Windows menjadi argumen (aturan tanda kutip sederhana).
    /// </summary>
    public static IReadOnlyList<string> SplitCommandLine(string? commandLine)
    {
        var args = new List<string>();
        if (string.IsNullOrWhiteSpace(commandLine)) return args;

        var current = new StringBuilder();
        bool inQuotes = false;
        bool hasToken = false;
        foreach (char c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasToken) args.Add(current.ToString());
                current.Clear();
                hasToken = false;
            }
            else
            {
                current.Append(c);
                hasToken = true;
            }
        }
        if (hasToken) args.Add(current.ToString());
        return args;
    }

    /// <summary>
    /// Mengambil konteks singkat dari argumen proses: nama file/project/skrip yang dibuka
    /// (misal "Foo.csproj", "server.js", "Laporan.docx") atau subcommand CLI ("build").
    /// Hanya potongan ini yang dipakai; command line utuh tidak disimpan karena bisa berisi rahasia.
    /// </summary>
    public static string? ExtractCommandLineContext(IReadOnlyList<string> args)
    {
        for (int i = 1; i < args.Count; i++)
        {
            string arg = args[i];
            if (IsOption(arg) || arg.Contains("://")) continue;

            string? fileName = TryGetFileName(arg);
            if (fileName == null) continue;

            if (NodeLaunchers.TryGetValue(fileName, out var tool))
            {
                // "npm run dev" -> ambil sampai dua kata perintah setelah launcher
                var words = args.Skip(i + 1).TakeWhile(a => !IsOption(a) && SimpleWord.IsMatch(a)).Take(2);
                return Truncate(string.Join(' ', words.Prepend(tool)), MaxContextLength);
            }
            return Truncate(fileName, MaxContextLength);
        }

        // Subcommand langsung setelah executable: "dotnet build", "cargo test"
        if (args.Count > 1 && !IsOption(args[1]) && SimpleWord.IsMatch(args[1]) && TryGetFileName(args[1]) == null)
        {
            return Truncate(args[1], MaxContextLength);
        }

        return null;
    }

    /// <summary>
    /// Menggabungkan sumber identitas dengan urutan prioritas:
    /// judul jendela sendiri → argumen command line → judul jendela proses saudara bernama sama
    /// (helper Electron seperti VS Code tidak punya jendela sendiri).
    /// </summary>
    public static ProcessIdentity Compose(string processName, string? ownWindowTitle, string? commandLineContext, string? siblingWindowTitle)
    {
        string appName = GetFriendlyAppName(processName);

        var (context, detail) = ParseWindowTitle(processName, ownWindowTitle);
        if (context != null) return new ProcessIdentity(appName, context, detail);

        if (commandLineContext != null) return new ProcessIdentity(appName, commandLineContext);

        (context, detail) = ParseWindowTitle(processName, siblingWindowTitle);
        return new ProcessIdentity(appName, context, detail);
    }

    /// <summary>
    /// Proses pembantu Chromium/Electron (VS Code, Edge, Chrome, GitKraken) diluncurkan dengan "--type=renderer/gpu-process/utility/...".
    /// </summary>
    public static bool IsHelperCommandLine(IReadOnlyList<string> args) =>
        args.Skip(1).Any(a => a.StartsWith("--type=", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Pesan balon untuk satu atau beberapa aplikasi yang terdeteksi dalam satu scan.
    /// </summary>
    public static string FormatStartedSummary(IReadOnlyList<ProcessIdentity> identities, int maxItems = 4)
    {
        if (identities.Count == 1) return Loc.F("Process_RunningSingle", identities[0].DisplayName);

        var labels = identities.Select(p => p.ShortLabel).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        string shown = string.Join(", ", labels.Take(maxItems));
        return labels.Count > maxItems
            ? Loc.F("Process_DetectedListMore", shown, labels.Count - maxItems)
            : Loc.F("Process_DetectedList", shown);
    }

    /// <summary>
    /// Teks tooltip tray: satu aplikasi per baris selama muat dalam batas NotifyIcon.Text,
    /// sisanya diringkas menjadi "+N lainnya".
    /// </summary>
    public static string FormatTrayText(string baseText, IEnumerable<ProcessIdentity> activeProcesses)
    {
        var labels = activeProcesses.Select(p => p.ShortLabel).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (labels.Count == 0) return Truncate(baseText, TrayTextLimit);

        static string MoreLine(int remaining) => remaining > 0 ? "\n" + Loc.F("Tray_More", remaining) : "";

        var sb = new StringBuilder(baseText);
        int shown = 0;
        foreach (var label in labels)
        {
            int remainingAfter = labels.Count - shown - 1;
            if (sb.Length + 1 + label.Length + MoreLine(remainingAfter).Length > TrayTextLimit) break;
            sb.Append('\n').Append(label);
            shown++;
        }

        if (shown == 0)
        {
            // Label pertama terlalu panjang: potong supaya minimal satu aplikasi tetap terlihat.
            int available = TrayTextLimit - sb.Length - 1 - MoreLine(labels.Count - 1).Length;
            if (available >= 8)
            {
                sb.Append('\n').Append(Truncate(labels[0], available));
                shown = 1;
            }
        }

        sb.Append(MoreLine(labels.Count - shown));
        return Truncate(sb.ToString(), TrayTextLimit);
    }

    private static bool IsOption(string arg) =>
        arg.StartsWith('-') || WindowsSwitch.IsMatch(arg);

    private static string? TryGetFileName(string arg)
    {
        if (arg.Contains('=') || arg.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return null;

        string name = Path.GetFileName(arg.TrimEnd('\\', '/'));
        string ext = Path.GetExtension(name);
        if (ext.Length < 2 || ext.Length > 9 || !ext.Skip(1).All(char.IsLetterOrDigit) || !ext.Any(char.IsLetter))
        {
            return null;
        }
        if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)) return null;
        return name;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
