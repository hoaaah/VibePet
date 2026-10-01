using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

[Collection(LocalizationCollection.Name)]
public class ProcessNameFormatterTests
{
    [Theory]
    [InlineData("dotnet", ".NET CLI")]
    [InlineData("node.exe", "Node.js")]
    [InlineData("Code", "Visual Studio Code")]
    [InlineData("WINWORD", "Microsoft Word")]
    [InlineData("pwsh", "PowerShell")]
    [InlineData("agy", "Antigravity")]
    [InlineData("my_build-tool.exe", "My Build Tool")]
    [InlineData("myBuildTool", "My Build Tool")]
    [InlineData("MSBuildHelper", "MSBuild Helper")]
    public void GetFriendlyAppName_MapsKnownAndFormatsUnknown(string processName, string expected)
    {
        Assert.Equal(expected, ProcessNameFormatter.GetFriendlyAppName(processName));
    }

    [Theory]
    [InlineData("code", "MainWindow.xaml.cs - pet-ag - Visual Studio Code", "pet-ag", "MainWindow.xaml.cs")]
    [InlineData("code", "● AGENTS.md - pet-ag - Visual Studio Code", "pet-ag", "AGENTS.md")]
    [InlineData("code", "pet-ag - Visual Studio Code", "pet-ag", null)]
    [InlineData("Code - Insiders", "app.ts - web - Visual Studio Code - Insiders", "web", "app.ts")]
    [InlineData("winword", "Laporan Akhir.docx - Word", "Laporan Akhir.docx", null)]
    [InlineData("WINWORD", "Proposal - Compatibility Mode - Word", "Proposal", null)]
    [InlineData("excel", "Anggaran 2026.xlsx - Excel", "Anggaran 2026.xlsx", null)]
    [InlineData("notepad", "*catatan.txt - Notepad", "catatan.txt", null)]
    [InlineData("devenv", "DesktopPet - Microsoft Visual Studio", "DesktopPet", null)]
    [InlineData("msedge", "Docs - Microsoft\u200B Edge", "Docs", null)]
    [InlineData("explorer", "pet-ag - File Explorer", "pet-ag", null)]
    [InlineData("msedge", "Live Streaming dan 24 halaman lainnya - Pribadi - Microsoft​ Edge", "Live Streaming - Pribadi", null)]
    [InlineData("gitkraken", "GitKraken Desktop", null, null)]
    public void ParseWindowTitle_ExtractsProjectOrDocument(string processName, string title, string? context, string? detail)
    {
        var result = ProcessNameFormatter.ParseWindowTitle(processName, title);
        Assert.Equal(context, result.Context);
        Assert.Equal(detail, result.Detail);
    }

    [Theory]
    [InlineData("winword", "Word")]
    [InlineData("code", "Visual Studio Code")]
    [InlineData("code", "")]
    [InlineData("code", null)]
    public void ParseWindowTitle_TitleWithoutDocument_ReturnsNull(string processName, string? title)
    {
        var result = ProcessNameFormatter.ParseWindowTitle(processName, title);
        Assert.Null(result.Context);
        Assert.Null(result.Detail);
    }

    [Fact]
    public void SplitCommandLine_HandlesQuotedPaths()
    {
        var args = ProcessNameFormatter.SplitCommandLine("\"C:\\Program Files\\Microsoft Office\\WINWORD.EXE\" /n \"D:\\Docs\\Laporan Akhir.docx\"");
        Assert.Equal(new[] { @"C:\Program Files\Microsoft Office\WINWORD.EXE", "/n", @"D:\Docs\Laporan Akhir.docx" }, args);
    }

    [Theory]
    [InlineData("dotnet build", "build")]
    [InlineData("dotnet run --project src\\DesktopPet\\DesktopPet.csproj", "DesktopPet.csproj")]
    [InlineData("\"C:\\Program Files\\nodejs\\node.exe\" --inspect server.js", "server.js")]
    [InlineData("node \"C:\\Program Files\\nodejs\\node_modules\\npm\\bin\\npm-cli.js\" run dev", "npm run dev")]
    [InlineData("\"C:\\Office\\WINWORD.EXE\" /n \"D:\\Docs\\Laporan Akhir.docx\"", "Laporan Akhir.docx")]
    [InlineData("pwsh -NoProfile -File .\\scripts\\send-event.ps1", "send-event.ps1")]
    [InlineData("cargo test --release", "test")]
    [InlineData("ffmpeg -i input.mp4 -c:v libx264 output.mkv", "input.mp4")]
    public void ExtractCommandLineContext_FindsFileOrSubcommand(string commandLine, string expected)
    {
        var args = ProcessNameFormatter.SplitCommandLine(commandLine);
        Assert.Equal(expected, ProcessNameFormatter.ExtractCommandLineContext(args));
    }

    [Theory]
    [InlineData("\"C:\\Code\\Code.exe\" --type=renderer --user-data-dir=\"C:\\Users\\me\\AppData\\Roaming\\Code\"")]
    [InlineData("pwsh -NoProfile -Command \"Get-Process\"")]
    [InlineData("node")]
    [InlineData("")]
    public void ExtractCommandLineContext_NoMeaningfulContext_ReturnsNull(string commandLine)
    {
        var args = ProcessNameFormatter.SplitCommandLine(commandLine);
        Assert.Null(ProcessNameFormatter.ExtractCommandLineContext(args));
    }

    [Fact]
    public void Compose_PrefersOwnTitle_ThenCommandLine_ThenSiblingTitle()
    {
        var own = ProcessNameFormatter.Compose("code", "a.cs - pet-ag - Visual Studio Code", "ignored.js", "b.cs - other - Visual Studio Code");
        Assert.Equal(new ProcessIdentity("Visual Studio Code", "pet-ag", "a.cs"), own);

        var cmd = ProcessNameFormatter.Compose("node", null, "server.js", "x - Node.js");
        Assert.Equal(new ProcessIdentity("Node.js", "server.js"), cmd);

        var sibling = ProcessNameFormatter.Compose("code", null, null, "b.cs - pet-ag - Visual Studio Code");
        Assert.Equal(new ProcessIdentity("Visual Studio Code", "pet-ag", "b.cs"), sibling);

        var bare = ProcessNameFormatter.Compose("dotnet", null, null, null);
        Assert.Equal(new ProcessIdentity(".NET CLI"), bare);
    }

    [Fact]
    public void DisplayName_FormatsAppContextAndDetail()
    {
        var identity = new ProcessIdentity("Visual Studio Code", "pet-ag", "AGENTS.md");
        Assert.Equal("Visual Studio Code — pet-ag (AGENTS.md)", identity.DisplayName);
        Assert.Equal("Visual Studio Code: pet-ag", identity.ShortLabel);
        Assert.Equal(".NET CLI", new ProcessIdentity(".NET CLI").DisplayName);
    }

    [Fact]
    public void FormatTrayText_GroupsDuplicates()
    {
        const string baseText = "Desktop Pet";

        Assert.Equal(baseText, ProcessNameFormatter.FormatTrayText(baseText, []));

        var vscode = new ProcessIdentity("Visual Studio Code", "pet-ag", "a.cs");
        var otherFile = new ProcessIdentity("Visual Studio Code", "pet-ag", "b.cs");
        Assert.Equal($"{baseText}\nVisual Studio Code: pet-ag", ProcessNameFormatter.FormatTrayText(baseText, [vscode, otherFile]));
    }

    [Fact]
    public void FormatTrayText_OneAppPerLineThenSummarizesRemainder()
    {
        var text = ProcessNameFormatter.FormatTrayText("Desktop Pet",
        [
            new ProcessIdentity("Node.js", "server.js"),
            new ProcessIdentity(".NET CLI", "build"),
            new ProcessIdentity("Visual Studio Code", "pet-ag"),
            new ProcessIdentity("Microsoft Word", "Laporan.docx"),
        ]);

        Assert.True(text.Length <= ProcessNameFormatter.TrayTextLimit);
        var lines = text.Split('\n');
        Assert.Equal("Desktop Pet", lines[0]);
        Assert.Equal("Node.js: server.js", lines[1]);
        Assert.Equal(".NET CLI: build", lines[2]);
        Assert.Equal("+2 lainnya", lines[^1]);
    }

    [Fact]
    public void FormatTrayText_TruncatesTooLongFirstLabel()
    {
        var text = ProcessNameFormatter.FormatTrayText("Desktop Pet",
        [
            new ProcessIdentity("Microsoft Word", "Laporan Akhir Proyek Desktop Pet Versi Final Revisi.docx"),
            new ProcessIdentity("Node.js", "server.js"),
        ]);

        Assert.True(text.Length <= ProcessNameFormatter.TrayTextLimit);
        var lines = text.Split('\n');
        Assert.StartsWith("Microsoft Word: Laporan", lines[1]);
        Assert.EndsWith("…", lines[1]);
        Assert.Equal("+1 lainnya", lines[2]);
    }

    [Theory]
    [InlineData("\"C:\\Code\\Code.exe\" --type=renderer --enable-features=x", true)]
    [InlineData("\"C:\\Edge\\msedge.exe\" --type=gpu-process", true)]
    [InlineData("\"C:\\Code\\Code.exe\"", false)]
    [InlineData("dotnet build", false)]
    [InlineData("node --input-type=module app.js", false)]
    public void IsHelperCommandLine_DetectsChromiumHelpers(string commandLine, bool expected)
    {
        Assert.Equal(expected, ProcessNameFormatter.IsHelperCommandLine(ProcessNameFormatter.SplitCommandLine(commandLine)));
    }

    [Fact]
    public void FormatStartedSummary_SingleAndMultipleApps()
    {
        Assert.Equal(
            "Visual Studio Code — pet-ag (AGENTS.md) terdeteksi sedang berjalan.",
            ProcessNameFormatter.FormatStartedSummary([new ProcessIdentity("Visual Studio Code", "pet-ag", "AGENTS.md")]));

        Assert.Equal(
            "Terdeteksi: Visual Studio Code: pet-ag, Node.js: index.js, .NET CLI: build.",
            ProcessNameFormatter.FormatStartedSummary(
            [
                new ProcessIdentity("Visual Studio Code", "pet-ag", "AGENTS.md"),
                new ProcessIdentity("Node.js", "index.js"),
                new ProcessIdentity(".NET CLI", "build"),
            ]));

        var many = ProcessNameFormatter.FormatStartedSummary(
            Enumerable.Range(1, 6).Select(i => new ProcessIdentity($"App {i}")).ToList());
        Assert.Equal("Terdeteksi: App 1, App 2, App 3, App 4, +2 lainnya.", many);
    }
}
