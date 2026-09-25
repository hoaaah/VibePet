using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using DesktopPet.Views;
using Application = System.Windows.Application;

namespace DesktopPet.Services;

public class TrayIconManager : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly ControlWindow _controlWindow;
    private NotifyIcon? _notifyIcon;

    public TrayIconManager(MainWindow mainWindow, ControlWindow controlWindow)
    {
        _mainWindow = mainWindow;
        _controlWindow = controlWindow;
        InitializeTray();
    }

    private void InitializeTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Desktop Pet (Kawahime)",
            Visible = true
        };

        // Try load icon
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
        {
            _notifyIcon.Icon = new Icon(iconPath);
        }
        else
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }

        var contextMenu = new ContextMenuStrip();

        var togglePetItem = new ToolStripMenuItem("Tampilkan/Sembunyikan Pet", null, (s, e) =>
        {
            if (_mainWindow.IsVisible)
            {
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
            }
        });

        var openControlItem = new ToolStripMenuItem("Buka Panel Kontrol...", null, (s, e) =>
        {
            _controlWindow.Show();
            _controlWindow.Activate();
        });

        var resetPosItem = new ToolStripMenuItem("Reset Posisi Pet", null, (s, e) =>
        {
            _mainWindow.ResetPosition();
        });

        var exitItem = new ToolStripMenuItem("Keluar", null, (s, e) =>
        {
            Application.Current.Shutdown();
        });

        contextMenu.Items.Add(togglePetItem);
        contextMenu.Items.Add(openControlItem);
        contextMenu.Items.Add(resetPosItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon.DoubleClick += (s, e) =>
        {
            if (_mainWindow.IsVisible)
            {
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            }
        };
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
