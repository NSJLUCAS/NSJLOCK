using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using NSJLock.Config;
using NSJLock.App.ViewModels;

namespace NSJLock.App;

public partial class MainWindow : Window
{
    private static readonly ThemeColors DarkTheme = new(
        "#0F1217",
        "#171C24",
        "#10151D",
        "#2D3542",
        "#F4F7FA",
        "#B3BBC8",
        "#838C9A",
        "#61D8A2",
        "#244A3C",
        "#4FC58F",
        "#FF5D86",
        "#38BDF8",
        "#159064",
        "#117A55",
        "#29313F",
        "#141A23",
        "#2E3847");

    private static readonly ThemeColors LightTheme = new(
        "#F5F7FA",
        "#FFFFFF",
        "#F1F5F9",
        "#DDE6F0",
        "#17202A",
        "#4D5A68",
        "#7C8896",
        "#1FA474",
        "#E2F6EC",
        "#168B63",
        "#D84E6D",
        "#0EA5E9",
        "#1FA474",
        "#168B63",
        "#E0E7F0",
        "#FAFCFE",
        "#DCE5EF");

    private bool allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        SystemEvents.UserPreferenceChanged += HandleUserPreferenceChanged;
        ApplyTheme(viewModel.ThemeMode);
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    public void AllowClose()
    {
        allowClose = true;
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!allowClose)
        {
            e.Cancel = true;
            allowClose = true;
            IsEnabled = false;

            try
            {
                var saved = await ViewModel.SaveSettingsAsync();
                if (!saved)
                {
                    System.Windows.MessageBox.Show(
                        ViewModel.StatusText,
                        "NSJ Lock",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save settings before exit: {exception.Message}",
                    "NSJ Lock",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            System.Windows.Application.Current.Shutdown();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        SystemEvents.UserPreferenceChanged -= HandleUserPreferenceChanged;
        base.OnClosed(e);
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ThemeMode))
        {
            ApplyTheme(ViewModel.ThemeMode);
        }
    }

    private void HandleUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (ViewModel.ThemeMode == AppThemeMode.System)
        {
            Dispatcher.BeginInvoke(() => ApplyTheme(ViewModel.ThemeMode));
        }
    }

    private static void ApplyTheme(AppThemeMode themeMode)
    {
        var effectiveTheme = themeMode == AppThemeMode.System && IsWindowsLightTheme()
            ? LightTheme
            : themeMode == AppThemeMode.Light
                ? LightTheme
                : DarkTheme;

        SetBrush("WindowBackgroundBrush", effectiveTheme.WindowBackground);
        SetBrush("PanelBackgroundBrush", effectiveTheme.PanelBackground);
        SetBrush("SurfaceBrush", effectiveTheme.Surface);
        SetBrush("HairlineBrush", effectiveTheme.Hairline);
        SetBrush("PrimaryTextBrush", effectiveTheme.PrimaryText);
        SetBrush("SecondaryTextBrush", effectiveTheme.SecondaryText);
        SetBrush("MutedTextBrush", effectiveTheme.MutedText);
        SetBrush("AccentBrush", effectiveTheme.Accent);
        SetBrush("AccentSoftBrush", effectiveTheme.AccentSoft);
        SetBrush("AccentPressedBrush", effectiveTheme.AccentPressed);
        SetBrush("WarningAccentBrush", effectiveTheme.WarningAccent);
        SetBrush("CurrentLevelBrush", effectiveTheme.CurrentLevel);
        SetBrush("PrimaryActionBrush", effectiveTheme.PrimaryAction);
        SetBrush("PrimaryActionHoverBrush", effectiveTheme.PrimaryActionHover);
        SetBrush("ProgressTrackBrush", effectiveTheme.ProgressTrack);
        SetBrush("MeterInnerBrush", effectiveTheme.MeterInner);
        SetBrush("MeterBorderBrush", effectiveTheme.MeterBorder);
    }

    private static bool IsWindowsLightTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                0);
            return value is int intValue && intValue > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void SetBrush(string resourceKey, string color)
    {
        var parsedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
        System.Windows.Application.Current.Resources[resourceKey] = new SolidColorBrush(parsedColor);
    }

    private sealed record ThemeColors(
        string WindowBackground,
        string PanelBackground,
        string Surface,
        string Hairline,
        string PrimaryText,
        string SecondaryText,
        string MutedText,
        string Accent,
        string AccentSoft,
        string AccentPressed,
        string WarningAccent,
        string CurrentLevel,
        string PrimaryAction,
        string PrimaryActionHover,
        string ProgressTrack,
        string MeterInner,
        string MeterBorder);
}
