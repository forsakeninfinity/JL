using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using JL.Core.Statistics;
using JL.Core.Utilities;
using JL.Windows.Utilities;

namespace JL.Windows.GUI;

/// <summary>
/// Interaction logic for StatsWindow.xaml
/// </summary>
internal sealed partial class StatsWindow : Window
{
    private static StatsWindow? s_instance;
    private IntPtr _windowHandle;

    public static StatsWindow Instance => s_instance ??= new StatsWindow();

    public StatsWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowHandle = new WindowInteropHelper(this).Handle;
        WinApi.BringToFront(_windowHandle);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (ConfigManager.Focusable)
        {
            WinApi.AllowActivation(_windowHandle);
        }
        else
        {
            WinApi.PreventActivation(_windowHandle);
        }
    }

    public static bool IsItVisible()
    {
        return s_instance?.IsVisible ?? false;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await UpdateStatsDisplay(StatsMode.Session).ConfigureAwait(false);
    }

    private async ValueTask UpdateStatsDisplay(StatsMode mode)
    {
        Stats stats = mode switch
        {
            StatsMode.Session => Stats.SessionStats,
            StatsMode.Lifetime => await Stats.GetLifetimeStats().ConfigureAwait(true),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        TextBlockCharacters.Text = stats.Characters.ToString(CultureInfo.InvariantCulture);
        TextBlockLines.Text = stats.Lines.ToString(CultureInfo.InvariantCulture);
        TextBlockTime.Text = stats.Time.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture);
        TextBlockCharactersPerMinute.Text = Math.Round(stats.Characters / stats.Time.TotalMinutes).ToString(CultureInfo.InvariantCulture);
        TextBlockCardsMined.Text = stats.CardsMined.ToString(CultureInfo.InvariantCulture);
        TextBlockTimesPlayedAudio.Text = stats.TimesPlayedAudio.ToString(CultureInfo.InvariantCulture);
        TextBlockImoutos.Text = stats.Imoutos.ToString(CultureInfo.InvariantCulture);
    }

    private async void ButtonSwapStats_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        if (Enum.TryParse(button.Content.ToString(), out StatsMode mode))
        {
            switch (mode)
            {
                case StatsMode.Session:
                    await Stats.UpdateLifetimeStats().ConfigureAwait(true);
                    await UpdateStatsDisplay(StatsMode.Lifetime).ConfigureAwait(true);
                    button.Content = StatsMode.Lifetime.ToString();
                    break;
                case StatsMode.Lifetime:
                    await UpdateStatsDisplay(StatsMode.Session).ConfigureAwait(true);
                    button.Content = StatsMode.Session.ToString();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(null, "StatsMode out of range");
            }
        }

        else
        {
            Utils.Logger.Error("Cannot parse {SwapButtonText} into a StatsMode enum", ButtonSwapStats.Content.ToString());
        }
    }

    private async void ButtonResetStats_OnClick(object sender, RoutedEventArgs e)
    {
        if (Utils.Frontend.ShowYesNoDialog(
                string.Create(CultureInfo.InvariantCulture, $"Are you really sure that you want to reset the {ButtonSwapStats.Content.ToString()!.ToLowerInvariant()} stats?"),
                string.Create(CultureInfo.InvariantCulture, $"Reset {ButtonSwapStats.Content} Stats?")))
        {
            if (Enum.TryParse(ButtonSwapStats.Content.ToString(), out StatsMode statsMode))
            {
                await Stats.ResetStats(statsMode).ConfigureAwait(true);

                if (statsMode is StatsMode.Lifetime)
                {
                    await Stats.UpdateLifetimeStats().ConfigureAwait(true);
                }

                await UpdateStatsDisplay(statsMode).ConfigureAwait(true);
            }

            else
            {
                Utils.Logger.Error("Cannot parse {SwapButtonText} into a StatsMode enum", ButtonSwapStats.Content.ToString());
            }
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        WindowsUtils.UpdateMainWindowVisibility();
        _ = MainWindow.Instance.Focus();
        s_instance = null;
    }
}
