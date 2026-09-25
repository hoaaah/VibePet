using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace DesktopPet;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pet_error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                File.AppendAllText(LogPath, $"[AppDomain Unhandled] {args.ExceptionObject}\n");
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                File.AppendAllText(LogPath, $"[Dispatcher Unhandled] {args.Exception}\n");
            }
            catch { }
        };

        base.OnStartup(e);
    }
}

