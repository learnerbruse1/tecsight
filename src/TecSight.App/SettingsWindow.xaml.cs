using System.Windows;
using System.Windows.Controls;
using TecSight.App.Localization;

namespace TecSight.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = LocalizationManager.Instance;

        foreach (var item in RefreshIntervalBox.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is string s && double.TryParse(s, out var v) && Math.Abs(v - AppSettings.RefreshIntervalSeconds) < 0.01)
            {
                RefreshIntervalBox.SelectedItem = item;
                break;
            }
        }

        foreach (var item in PeripheralIntervalBox.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is string s && double.TryParse(s, out var v) && Math.Abs(v - AppSettings.PeripheralScanSeconds) < 0.01)
            {
                PeripheralIntervalBox.SelectedItem = item;
                break;
            }
        }

        foreach (var item in InventoryIntervalBox.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is string s && double.TryParse(s, out var v) && Math.Abs(v - AppSettings.InventoryRefreshSeconds) < 0.01)
            {
                InventoryIntervalBox.SelectedItem = item;
                break;
            }
        }

        foreach (var item in LanguageBox.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is string lang && string.Equals(lang, AppSettings.Language, StringComparison.OrdinalIgnoreCase))
            {
                LanguageBox.SelectedItem = item;
                break;
            }
        }
        if (LanguageBox.SelectedItem is null)
        {
            foreach (var item in LanguageBox.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is string lang && string.Equals(lang, LocalizationManager.Instance.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageBox.SelectedItem = item;
                    break;
                }
            }
        }

        DarkThemeBox.IsChecked = AppSettings.DarkTheme;
        NoiseFilterBox.IsChecked = AppSettings.HideNetworkNoise;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshIntervalBox.SelectedItem is ComboBoxItem r && r.Tag is string rs && double.TryParse(rs, out var rv))
        {
            AppSettings.SetRefreshIntervalSeconds(rv);
        }

        if (PeripheralIntervalBox.SelectedItem is ComboBoxItem p && p.Tag is string ps && double.TryParse(ps, out var pv))
        {
            AppSettings.SetPeripheralScanSeconds(pv);
        }

        if (InventoryIntervalBox.SelectedItem is ComboBoxItem inv && inv.Tag is string ivs && double.TryParse(ivs, out var ivv))
        {
            AppSettings.SetInventoryRefreshSeconds(ivv);
        }

        if (LanguageBox.SelectedItem is ComboBoxItem lang && lang.Tag is string ls)
        {
            AppSettings.SetLanguage(ls);
        }

        AppSettings.SetDarkTheme(DarkThemeBox.IsChecked == true);
        AppSettings.SetHideNetworkNoise(NoiseFilterBox.IsChecked == true);
        AppSettings.Save();
        DialogResult = true;
    }
}
