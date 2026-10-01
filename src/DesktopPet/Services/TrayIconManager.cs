using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using DesktopPet.Localization;
using DesktopPet.Views;
using Application = System.Windows.Application;

namespace DesktopPet.Services;

public class TrayIconManager : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly ControlWindow _controlWindow;
    private NotifyIcon? _notifyIcon;

    private const string BaseTrayText = "Desktop Pet (Kawahime)";
    private readonly List<(ToolStripMenuItem Item, string Key)> _localizedItems = new();
    private IReadOnlyList<ProcessIdentity> _lastActiveProcesses = [];

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
            Text = BaseTrayText,
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

        var togglePetItem = new ToolStripMenuItem(Loc.T("Tray_TogglePet"), null, (s, e) =>
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

        var openControlItem = new ToolStripMenuItem(Loc.T("Tray_OpenControlPanel"), null, (s, e) =>
        {
            _controlWindow.Show();
            _controlWindow.Activate();
        });

        var resetPosItem = new ToolStripMenuItem(Loc.T("Tray_ResetPosition"), null, (s, e) =>
        {
            _mainWindow.ResetPosition();
        });

        var exitItem = new ToolStripMenuItem(Loc.T("Menu_Exit"), null, (s, e) =>
        {
            Application.Current.Shutdown();
        });

        // Item -> resource key, so a language switch only rewrites the texts
        _localizedItems.Clear();
        _localizedItems.Add((togglePetItem, "Tray_TogglePet"));
        _localizedItems.Add((openControlItem, "Tray_OpenControlPanel"));
        _localizedItems.Add((resetPosItem, "Tray_ResetPosition"));
        _localizedItems.Add((exitItem, "Menu_Exit"));
        Loc.Instance.LanguageChanged += OnLanguageChanged;

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

    private void OnLanguageChanged()
    {
        foreach (var (item, key) in _localizedItems)
        {
            item.Text = Loc.T(key);
        }
        UpdateActiveProcesses(_lastActiveProcesses);
    }

    public void UpdateActiveProcesses(IReadOnlyList<ProcessIdentity> activeProcesses)
    {
        _lastActiveProcesses = activeProcesses;
        if (_notifyIcon == null) return;
        // Header lebih pendek saat ada proses aktif supaya lebih banyak aplikasi muat dalam 63 karakter.
        _notifyIcon.Text = activeProcesses.Count == 0
            ? BaseTrayText
            : ProcessNameFormatter.FormatTrayText("Desktop Pet", activeProcesses);
    }

    public void Dispose()
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
