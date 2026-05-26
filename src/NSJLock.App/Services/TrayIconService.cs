using System.ComponentModel;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using NSJLock.App.ViewModels;

namespace NSJLock.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow mainWindow;
    private readonly MainViewModel viewModel;
    private readonly Forms.NotifyIcon notifyIcon;
    private readonly Forms.ToolStripMenuItem openMainWindowMenuItem;
    private readonly Forms.ToolStripMenuItem toggleProtectionMenuItem;
    private readonly Forms.ToolStripMenuItem exitMenuItem;
    private readonly Icon trayIcon;
    private bool isDisposed;
    private bool isExiting;

    public TrayIconService(MainWindow mainWindow, MainViewModel viewModel)
    {
        this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        openMainWindowMenuItem = new Forms.ToolStripMenuItem("打开主界面");
        openMainWindowMenuItem.Click += HandleOpenMainWindowClick;

        toggleProtectionMenuItem = new Forms.ToolStripMenuItem();
        toggleProtectionMenuItem.Click += HandleToggleProtectionClick;

        exitMenuItem = new Forms.ToolStripMenuItem("退出");
        exitMenuItem.Click += HandleExitClick;

        var contextMenu = new Forms.ContextMenuStrip();
        contextMenu.Items.Add(openMainWindowMenuItem);
        contextMenu.Items.Add(toggleProtectionMenuItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);

        trayIcon = LoadTrayIcon();
        notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = trayIcon,
            Text = "NSJ Lock",
            Visible = true
        };
        notifyIcon.MouseClick += HandleNotifyIconMouseClick;
        notifyIcon.DoubleClick += HandleNotifyIconDoubleClick;

        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        RefreshToggleMenuText();
    }

    public void ShowMainWindow()
    {
        if (isDisposed || isExiting)
        {
            return;
        }

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Show();
        mainWindow.Activate();
    }

    public async void ExitApplication()
    {
        if (isDisposed || isExiting)
        {
            return;
        }

        isExiting = true;
        SetMenuEnabled(false);

        try
        {
            var saved = await viewModel.SaveSettingsAsync();
            if (!saved)
            {
                System.Windows.MessageBox.Show(
                    viewModel.StatusText,
                    "NSJ Lock",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"退出前保存设置失败：{exception.Message}",
                "NSJ Lock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            mainWindow.AllowClose();
            System.Windows.Application.Current.Shutdown();
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        notifyIcon.MouseClick -= HandleNotifyIconMouseClick;
        notifyIcon.DoubleClick -= HandleNotifyIconDoubleClick;
        openMainWindowMenuItem.Click -= HandleOpenMainWindowClick;
        toggleProtectionMenuItem.Click -= HandleToggleProtectionClick;
        exitMenuItem.Click -= HandleExitClick;

        notifyIcon.Visible = false;
        notifyIcon.ContextMenuStrip?.Dispose();
        notifyIcon.Dispose();
        trayIcon.Dispose();
    }

    private void HandleNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void HandleNotifyIconMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            ShowMainWindow();
        }
    }

    private void HandleOpenMainWindowClick(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void HandleToggleProtectionClick(object? sender, EventArgs e)
    {
        if (isExiting)
        {
            return;
        }

        viewModel.ToggleProtection();
    }

    private void HandleExitClick(object? sender, EventArgs e)
    {
        ExitApplication();
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsProtectionEnabled) or null or "")
        {
            RefreshToggleMenuText();
        }
    }

    private void RefreshToggleMenuText()
    {
        toggleProtectionMenuItem.Text = viewModel.IsProtectionEnabled ? "关闭保护" : "开启保护";
    }

    private void SetMenuEnabled(bool isEnabled)
    {
        openMainWindowMenuItem.Enabled = isEnabled;
        toggleProtectionMenuItem.Enabled = isEnabled;
        exitMenuItem.Enabled = isEnabled;
    }

    private static Icon LoadTrayIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var associatedIcon = Icon.ExtractAssociatedIcon(processPath);
            if (associatedIcon is not null)
            {
                return associatedIcon;
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
