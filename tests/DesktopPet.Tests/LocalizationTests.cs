using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DesktopPet.Localization;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// Loc is process-wide, so every test class that asserts on localized text joins this collection:
/// xUnit runs them one after another, with Indonesian as the baseline language.
/// </summary>
[CollectionDefinition(Name)]
public class LocalizationCollection : ICollectionFixture<IndonesianLanguageFixture>
{
    public const string Name = "Localization";
}

public sealed class IndonesianLanguageFixture
{
    public IndonesianLanguageFixture() => Loc.Instance.SetLanguage(Loc.Indonesian);
}

[Collection(LocalizationCollection.Name)]
public class LocalizationTests
{
    private static readonly Regex KeyLiteral = new(
        "\"((?:Common|Lang|Menu|Tray|Bubble|Ipc|Process|Skin|Sheet|Anim|Ctl)_[A-Za-z0-9_]+)\"", RegexOptions.Compiled);
    private static readonly Regex XamlKey = new(@"\{l:Tr ([A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly Regex Placeholder = new(@"\{(\d+)(?::[^}]*)?\}", RegexOptions.Compiled);

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "src", "DesktopPet", "DesktopPet.csproj")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "DesktopPet");
    }

    private static Dictionary<string, string> ReadResx(string fileName) =>
        XDocument.Load(Path.Combine(SourceRoot(), "Localization", fileName))
            .Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    private static HashSet<string> Placeholders(string text) =>
        Placeholder.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(SourceRoot(), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static HashSet<string> KeysUsedInSource()
    {
        var used = new HashSet<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            foreach (Match m in KeyLiteral.Matches(File.ReadAllText(file))) used.Add(m.Groups[1].Value);
        }
        foreach (var file in SourceFiles("*.xaml"))
        {
            foreach (Match m in XamlKey.Matches(File.ReadAllText(file))) used.Add(m.Groups[1].Value);
        }
        return used;
    }

    [Fact]
    public void BothLanguages_HaveTheSameKeysAndPlaceholders()
    {
        var english = ReadResx("Strings.resx");
        var indonesian = ReadResx("Strings.id.resx");

        Assert.Empty(english.Keys.Except(indonesian.Keys));
        Assert.Empty(indonesian.Keys.Except(english.Keys));

        foreach (var (key, text) in english)
        {
            Assert.True(Placeholders(text).SetEquals(Placeholders(indonesian[key])),
                $"Placeholders differ for '{key}': '{text}' vs '{indonesian[key]}'");
            Assert.False(string.IsNullOrWhiteSpace(indonesian[key]), $"'{key}' is empty in Indonesian");
        }
    }

    [Fact]
    public void EveryKeyUsedInCode_Exists()
    {
        var english = ReadResx("Strings.resx");
        var missing = KeysUsedInSource().Where(k => !english.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "Missing resource keys: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryResourceKey_IsUsed()
    {
        var used = KeysUsedInSource();
        var unused = ReadResx("Strings.resx").Keys.Where(k => !used.Contains(k)).ToList();
        Assert.True(unused.Count == 0, "Unused resource keys: " + string.Join(", ", unused));
    }

    [Theory]
    [InlineData("auto", "id-ID", "id")]
    [InlineData("auto", "en-US", "en")]
    [InlineData("auto", "fr-FR", "en")]
    [InlineData(null, "id-ID", "id")]
    [InlineData("en", "id-ID", "en")]
    [InlineData("ID", "en-US", "id")]
    [InlineData("xx", "en-US", "en")]
    public void ResolveCulture_FollowsWindowsUnlessChosen(string? setting, string system, string expected)
    {
        Assert.Equal(expected, Loc.ResolveCulture(setting, CultureInfo.GetCultureInfo(system)).Name);
    }

    [Fact]
    public void SetLanguage_SwitchesTextsAndRaisesEventOnlyOnChange()
    {
        int raised = 0;
        void Handler() => raised++;
        Loc.Instance.LanguageChanged += Handler;
        try
        {
            Loc.Instance.SetLanguage(Loc.Indonesian);
            Assert.Equal("Keluar", Loc.T("Menu_Exit"));
            Assert.Equal("Proses Gagal", Loc.T("Process_FailedTitle"));

            Loc.Instance.SetLanguage(Loc.English);
            Assert.Equal("Exit", Loc.T("Menu_Exit"));
            Assert.Equal("Node.js is running.", Loc.F("Process_RunningSingle", "Node.js"));
            Assert.Equal(1, raised);

            Loc.Instance.SetLanguage(Loc.English);
            Assert.Equal(1, raised);
            Assert.Equal(Loc.English, Loc.Instance.LanguageSetting);

            Assert.Equal("[No_Such_Key]", Loc.T("No_Such_Key"));
        }
        finally
        {
            Loc.Instance.LanguageChanged -= Handler;
            Loc.Instance.SetLanguage(Loc.Indonesian);
        }
    }

    [Fact]
    public void XamlTrBinding_UpdatesWhenLanguageChanges()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                const string xaml = """
                    <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:l="clr-namespace:DesktopPet.Localization;assembly=DesktopPet">
                        <TextBlock Text="{l:Tr Ctl_SkinGroup}"/>
                        <Button Content="{l:Tr Menu_Exit}"/>
                    </StackPanel>
                    """;
                var panel = (System.Windows.Controls.StackPanel)System.Windows.Markup.XamlReader.Parse(xaml);
                var text = (System.Windows.Controls.TextBlock)panel.Children[0];
                var button = (System.Windows.Controls.Button)panel.Children[1];

                Loc.Instance.SetLanguage(Loc.Indonesian);
                Assert.Equal("Skin / Paket Sprite", text.Text);
                Assert.Equal("Keluar", button.Content);

                Loc.Instance.SetLanguage(Loc.English);
                Assert.Equal("Skin / Sprite Pack", text.Text);
                Assert.Equal("Exit", button.Content);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                Loc.Instance.SetLanguage(Loc.Indonesian);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw failure;
    }

    [Fact]
    public void Indexer_IsWhatXamlBindsTo()
    {
        Assert.Equal(Loc.T("Ctl_SkinGroup"), Loc.Instance["Ctl_SkinGroup"]);
        Assert.Equal("Skin / Paket Sprite", Loc.Instance["Ctl_SkinGroup"]);
    }
}
