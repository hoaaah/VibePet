using DesktopPet.Services;
using Microsoft.Win32;
using Xunit;

namespace DesktopPet.Tests;

public class AutoStartServiceTests : IDisposable
{
    private readonly string _testSubKey;
    private readonly string _testAppName;
    private readonly AutoStartService _service;

    public AutoStartServiceTests()
    {
        _testAppName = $"DesktopPetTest_{Guid.NewGuid():N}";
        _testSubKey = $@"Software\DesktopPetUnitTest_{Guid.NewGuid():N}";
        _service = new AutoStartService(_testAppName, _testSubKey);
    }

    public void Dispose()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_testSubKey, false);
        }
        catch { }
    }

    [Fact]
    public void InitiallyDisabled()
    {
        Assert.False(_service.IsAutoStartEnabled());
        Assert.Null(_service.GetRegisteredPath());
    }

    [Fact]
    public void EnableAndDisableAutoStart()
    {
        string fakeExe = @"C:\Program Files\DesktopPet\DesktopPet.exe";
        bool setResult = _service.SetAutoStart(true, fakeExe);
        Assert.True(setResult);

        Assert.True(_service.IsAutoStartEnabled());
        string? registered = _service.GetRegisteredPath();
        Assert.NotNull(registered);
        Assert.Equal($"\"{fakeExe}\"", registered);

        bool unsetResult = _service.SetAutoStart(false);
        Assert.True(unsetResult);
        Assert.False(_service.IsAutoStartEnabled());
        Assert.Null(_service.GetRegisteredPath());
    }

    [Fact]
    public void UnsettingWhenAlreadyDisabledReturnsTrue()
    {
        bool result = _service.SetAutoStart(false);
        Assert.True(result);
        Assert.False(_service.IsAutoStartEnabled());
    }
}
