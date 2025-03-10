using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JL.Core.Dicts;
using JL.Core.Dicts.CustomWordDict;
using JL.Core.Utilities;
using JL.Windows.Utilities;

namespace JL.Windows.GUI;

/// <summary>
/// Interaction logic for AddWordWindow.xaml
/// </summary>
internal sealed partial class AddWordWindow : Window
{
    private static AddWordWindow? s_instance;

    public static AddWordWindow Instance => s_instance ??= new AddWordWindow();

    public AddWordWindow()
    {
        InitializeComponent();
    }

    public static bool IsItVisible()
    {
        return s_instance?.IsVisible ?? false;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs? e)
    {
        bool isValid = true;

        if (!JapaneseUtils.JapaneseRegex.IsMatch(SpellingsTextBox.Text))
        {
            SpellingsTextBox.BorderBrush = Brushes.Red;
            isValid = false;
        }
        else if (SpellingsTextBox.BorderBrush == Brushes.Red)
        {
            SpellingsTextBox.ClearValue(BorderBrushProperty);
        }

        string rawDefinitions = DefinitionsTextBox.Text.Replace("\t", "  ", StringComparison.Ordinal);
        string[] definitions = rawDefinitions.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (definitions.Length is 0)
        {
            DefinitionsTextBox.BorderBrush = Brushes.Red;
            isValid = false;
        }
        else if (DefinitionsTextBox.BorderBrush == Brushes.Red)
        {
            DefinitionsTextBox.ClearValue(BorderBrushProperty);
        }

        if (isValid)
        {
            string rawSpellings = SpellingsTextBox.Text.Replace("\t", "  ", StringComparison.Ordinal);
            string rawReadings = ReadingsTextBox.Text.Replace("\t", "  ", StringComparison.Ordinal);
            string rawPartOfSpeech = PartOfSpeechStackPanel.Children.OfType<RadioButton>()
                .FirstOrDefault(static r => r.IsChecked.HasValue && r.IsChecked.Value)!.Content.ToString()!;
            string rawWordClasses = WordClassTextBox.Text.Replace("\t", "  ", StringComparison.Ordinal);

            string[] spellings = rawSpellings.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            string[]? readings = rawReadings.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (readings.Length is 0)
            {
                readings = null;
            }

            string[]? wordClasses = rawWordClasses.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (wordClasses.Length is 0)
            {
                wordClasses = null;
            }

            DictType dictType = ComboBoxDictType.SelectedValue.ToString() is "Global"
                ? DictType.CustomWordDictionary
                : DictType.ProfileCustomWordDictionary;

            Dict dict = DictUtils.Dicts.Values.First(dict => dict.Type == dictType);
            if (dict.Active)
            {
                CustomWordLoader.AddToDictionary(spellings, readings, definitions, rawPartOfSpeech, wordClasses, dict.Contents);
                Utils.Frontend.InvalidateDisplayCache();
            }

            PopupWindowUtils.HidePopups(MainWindow.Instance.FirstPopupWindow);
            Close();

            string line = string.IsNullOrWhiteSpace(rawWordClasses)
                ? string.Create(CultureInfo.InvariantCulture, $"{spellings}\t{readings}\t{definitions}\t{rawPartOfSpeech}\n")
                : string.Create(CultureInfo.InvariantCulture, $"{spellings}\t{readings}\t{definitions}\t{rawPartOfSpeech}\t{wordClasses}\n");

            string path = Path.GetFullPath(dict.Path, Utils.ApplicationPath);
            await File.AppendAllTextAsync(path, line).ConfigureAwait(false);
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        WindowsUtils.UpdateMainWindowVisibility();
        _ = MainWindow.Instance.Focus();
        s_instance = null;
    }

    private void VerbRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        WordClassStackPanel.Visibility = Visibility.Visible;
    }

    private void OtherRadioButtons_Checked(object sender, RoutedEventArgs e)
    {
        WordClassTextBox.Text = "";
        WordClassStackPanel.Visibility = Visibility.Collapsed;
    }

    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        const string supportedWordClasses = """
                                            v1: Ichidan verb
                                            v1-s: Ichidan verb - kureru special class
                                            v4r: Yodan verb with `ru' ending (archaic)
                                            v5aru: Godan verb - -aru special class
                                            v5b: Godan verb with 'bu' ending
                                            v5g: Godan verb with 'gu' ending
                                            v5k: Godan verb with 'ku' ending
                                            v5k-s: Godan verb - Iku/Yuku special class
                                            v5m: Godan verb with 'mu' ending
                                            v5n: Godan verb with 'nu' ending
                                            v5r: Godan verb with 'ru' ending
                                            v5r-i: Godan verb with 'ru' ending (irregular verb)
                                            v5s: Godan verb with 'su' ending
                                            v5t: Godan verb with 'tsu' ending
                                            v5u: Godan verb with 'u' ending
                                            v5u-s: Godan verb with 'u' ending (special class)
                                            vk: Kuru verb - special class
                                            vs-c: su verb - precursor to the modern suru (limited support)
                                            vs-i: suru verb - included
                                            vs-s: suru verb - special class
                                            vz: Ichidan verb - zuru verb (alternative form of -jiru verbs)
                                            """;

        InfoWindow infoWindow = new()
        {
            Owner = this,
            Title = "Supported Word Classes",
            InfoTextBox = { Text = supportedWordClasses },
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        _ = infoWindow.ShowDialog();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _ = Activate();
        if (string.IsNullOrEmpty(SpellingsTextBox.Text))
        {
            _ = SpellingsTextBox.Focus();
        }
        else // if (string.IsNullOrEmpty(ReadingsTextBox.Text))
        {
            _ = ReadingsTextBox.Focus();
        }
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter && InputMethod.Current?.ImeState is not InputMethodState.On)
        {
            e.Handled = true;
            SaveButton_Click(null, null);
        }
    }
}
