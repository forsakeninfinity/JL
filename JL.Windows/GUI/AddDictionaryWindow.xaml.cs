using System.IO;
using System.Windows;
using System.Windows.Media;
using JL.Core.Dicts;
using JL.Core.Dicts.Options;
using JL.Core.Utilities;
using JL.Windows.GUI.UserControls;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;

namespace JL.Windows.GUI;

/// <summary>
/// Interaction logic for AddDictionaryWindow.xaml
/// </summary>
internal sealed partial class AddDictionaryWindow : Window
{
    private readonly DictOptionsControl _dictOptionsControl;

    public AddDictionaryWindow()
    {
        InitializeComponent();
        _dictOptionsControl = new DictOptionsControl();
        _ = DictStackPanel.Children.Add(_dictOptionsControl);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        bool isValid = true;

        string? typeString = ComboBoxDictType.SelectionBoxItem.ToString();
        if (string.IsNullOrEmpty(typeString))
        {
            ComboBoxDictType.BorderBrush = Brushes.Red;
            isValid = false;
        }
        else if (ComboBoxDictType.BorderBrush == Brushes.Red)
        {
            ComboBoxDictType.ClearValue(BorderBrushProperty);
        }

        string path = TextBlockPath.Text;
        string fullPath = Path.GetFullPath(path, Utils.ApplicationPath);

        if (string.IsNullOrWhiteSpace(path)
            || (!Directory.Exists(fullPath) && !File.Exists(fullPath))
            || DictUtils.Dicts.Values.Select(static dict => dict.Path).Contains(path))
        {
            TextBlockPath.BorderBrush = Brushes.Red;
            isValid = false;
        }
        else if (TextBlockPath.BorderBrush == Brushes.Red)
        {
            TextBlockPath.ClearValue(BorderBrushProperty);
        }

        string name = NameTextBox.Text;
        if (string.IsNullOrEmpty(name) || DictUtils.Dicts.Values.Select(static dict => dict.Name).Contains(name))
        {
            NameTextBox.BorderBrush = Brushes.Red;
            isValid = false;
        }
        else if (NameTextBox.BorderBrush == Brushes.Red)
        {
            NameTextBox.ClearValue(BorderBrushProperty);
        }

        if (isValid)
        {
            DictType type = typeString!.GetEnum<DictType>();

            DictOptions options = _dictOptionsControl.GetDictOptions(type);
            Dict dict = new(type, name, path, true, DictUtils.Dicts.Count + 1, 0, false, options);
            DictUtils.Dicts.Add(name, dict);

            if (dict.Type is DictType.PitchAccentYomichan)
            {
                DictUtils.SingleDictTypeDicts[DictType.PitchAccentYomichan] = dict;
            }

            Close();
        }
    }

    private void BrowseForDictionaryFile(string filter)
    {
        OpenFileDialog openFileDialog = new() { InitialDirectory = Utils.ApplicationPath, Filter = filter };

        if (openFileDialog.ShowDialog() is true)
        {
            TextBlockPath.Text = Utils.GetPath(openFileDialog.FileName);
        }
    }

    private void BrowseForDictionaryFolder()
    {
        using System.Windows.Forms.FolderBrowserDialog fbd = new();
        fbd.SelectedPath = Utils.ApplicationPath;

        if (fbd.ShowDialog() is System.Windows.Forms.DialogResult.OK &&
            !string.IsNullOrWhiteSpace(fbd.SelectedPath))
        {
            TextBlockPath.Text = Utils.GetPath(fbd.SelectedPath);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RadioButtonYomichanImport.IsChecked = true;
        FillDictTypesCombobox(DictUtils.YomichanDictTypes);
    }

    private void RadioButtonYomichanImport_OnClick(object sender, RoutedEventArgs e)
    {
        FillDictTypesCombobox(DictUtils.YomichanDictTypes);
        HideDictOptions();
    }

    private void RadioButtonNazekaEpwingConverter_OnClick(object sender, RoutedEventArgs e)
    {
        FillDictTypesCombobox(DictUtils.NazekaDictTypes);
        HideDictOptions();
    }

    private void FillDictTypesCombobox(DictType[] types)
    {
        IEnumerable<DictType> loadedDictTypes = DictUtils.Dicts.Values.Select(static dict => dict.Type);
        IEnumerable<DictType> validTypes = types.Except(loadedDictTypes.Except(DictUtils.NonspecificDictTypes));

        ComboBoxDictType.ItemsSource = validTypes.Select(static d => d.GetDescription() ?? d.ToString());
    }

    private void BrowsePathButton_OnClick(object sender, RoutedEventArgs e)
    {
        string typeString = ComboBoxDictType.SelectionBoxItem.ToString()!;
        DictType selectedDictType = typeString.GetEnum<DictType>();

        switch (selectedDictType)
        {
            // not providing a description for the filter causes the filename returned to be empty
            case DictType.Kenkyuusha:
            case DictType.Daijirin:
            case DictType.Daijisen:
            case DictType.Koujien:
            case DictType.Meikyou:
            case DictType.Gakken:
            case DictType.Kotowaza:
            case DictType.IwanamiYomichan:
            case DictType.JitsuyouYomichan:
            case DictType.ShinmeikaiYomichan:
            case DictType.NikkokuYomichan:
            case DictType.ShinjirinYomichan:
            case DictType.OubunshaYomichan:
            case DictType.ZokugoYomichan:
            case DictType.WeblioKogoYomichan:
            case DictType.GakkenYojijukugoYomichan:
            case DictType.ShinmeikaiYojijukugoYomichan:
            case DictType.KanjigenYomichan:
            case DictType.KireiCakeYomichan:
            case DictType.NonspecificWordYomichan:
            case DictType.NonspecificKanjiYomichan:
            case DictType.NonspecificKanjiWithWordSchemaYomichan:
            case DictType.NonspecificNameYomichan:
            case DictType.NonspecificYomichan:
                BrowseForDictionaryFolder();
                break;

            case DictType.PitchAccentYomichan:
                BrowseForDictionaryFolder();
                break;

            case DictType.DaijirinNazeka:
                BrowseForDictionaryFile("Daijirin file|*.json");
                break;
            case DictType.KenkyuushaNazeka:
                BrowseForDictionaryFile("Kenkyuusha file|*.json");
                break;
            case DictType.ShinmeikaiNazeka:
                BrowseForDictionaryFile("Shinmeikai file|*.json");
                break;
            case DictType.NonspecificWordNazeka:
            case DictType.NonspecificKanjiNazeka:
            case DictType.NonspecificNameNazeka:
            case DictType.NonspecificNazeka:
                BrowseForDictionaryFile("Nazeka file|*.json");
                break;

            case DictType.JMdict:
            case DictType.JMnedict:
            case DictType.Kanjidic:
            case DictType.CustomWordDictionary:
            case DictType.CustomNameDictionary:
            case DictType.ProfileCustomWordDictionary:
            case DictType.ProfileCustomNameDictionary:
                break;

            default:
                throw new ArgumentOutOfRangeException(null, "Invalid DictType (Add)");
        }
    }

    private void ComboBoxDictType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        GenerateDictOptions();
    }

    private void GenerateDictOptions()
    {
        string? typeString = ComboBoxDictType.SelectedItem?.ToString();
        if (!string.IsNullOrEmpty(typeString))
        {
            DictType type = typeString.GetEnum<DictType>();
            _dictOptionsControl.GenerateDictOptionsElements(type);
        }

        else
        {
            HideDictOptions();
        }
    }

    private void HideDictOptions()
    {
        _dictOptionsControl.OptionsStackPanel.Visibility = Visibility.Collapsed;
        _dictOptionsControl.OptionsTextBlock.Visibility = Visibility.Collapsed;
    }
}
