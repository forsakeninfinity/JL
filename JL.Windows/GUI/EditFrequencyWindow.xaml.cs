using System.IO;
using System.Windows;
using System.Windows.Media;
using JL.Core.Freqs;
using JL.Core.Utilities;
using JL.Windows.GUI.UserControls;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace JL.Windows.GUI;

/// <summary>
/// Interaction logic for EditFrequencyWindow.xaml
/// </summary>
internal sealed partial class EditFrequencyWindow : Window
{
    private readonly Freq _freq;
    private readonly FreqOptionsControl _freqOptionsControl;

    public EditFrequencyWindow(Freq freq)
    {
        _freq = freq;
        _freqOptionsControl = new FreqOptionsControl();
        InitializeComponent();
        _ = FreqStackPanel.Children.Add(_freqOptionsControl);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        bool isValid = true;

        string path = TextBlockPath.Text;
        string fullPath = Path.GetFullPath(path, Utils.ApplicationPath);

        if (string.IsNullOrWhiteSpace(path)
            || (_freq.Path != path
                && ((!Directory.Exists(fullPath) && !File.Exists(fullPath))
                    || FreqUtils.FreqDicts.Values.Any(dict => dict.Path == path))))
        {
            TextBlockPath.BorderBrush = Brushes.Red;
            isValid = false;
        }
        else if (TextBlockPath.BorderBrush == Brushes.Red)
        {
            TextBlockPath.ClearValue(BorderBrushProperty);
        }

        string name = NameTextBox.Text;
        if (string.IsNullOrEmpty(name)
            || (_freq.Name != name && FreqUtils.FreqDicts.Values.Any(dict => dict.Name == name)))
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
            string dbPath = FreqUtils.GetDBPath(_freq.Name);
            bool dbExists = File.Exists(dbPath);

            if (_freq.Path != path)
            {
                _freq.Path = path;
                _freq.Contents.Clear();
                _freq.Ready = false;

                if (dbExists)
                {
                    SqliteConnection.ClearAllPools();
                    File.Delete(dbPath);
                    dbExists = false;
                }
            }

            Core.Freqs.Options.FreqOptions options = _freqOptionsControl.GetFreqOptions(_freq.Type);

            if (_freq.Options?.UseDB?.Value != options.UseDB?.Value)
            {
                _freq.Ready = false;
                //if (dbExists && !(options.UseDB?.Value ?? false))
                //{
                //    SqliteConnection.ClearAllPools();
                //    File.Delete(dbPath);
                //    dbExists = false;
                //}
            }

            if (_freq.Name != name)
            {
                if (dbExists)
                {
                    SqliteConnection.ClearAllPools();
                    File.Move(dbPath, FreqUtils.GetDBPath(name));
                }

                _freq.Name = name;
            }

            _freq.Options = options;
            Utils.Frontend.InvalidateDisplayCache();

            Close();
        }
    }

    private void BrowseForFrequencyFile(string filter)
    {
        OpenFileDialog openFileDialog = new() { InitialDirectory = Utils.ApplicationPath, Filter = filter };
        if (openFileDialog.ShowDialog() is true)
        {
            TextBlockPath.Text = Utils.GetPath(openFileDialog.FileName);
        }
    }

    private void BrowseForFrequencyFolder()
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
        string type = _freq.Type.GetDescription() ?? _freq.Type.ToString();
        _ = FreqTypeComboBox.Items.Add(type);
        FreqTypeComboBox.SelectedValue = type;
        TextBlockPath.Text = _freq.Path;
        NameTextBox.Text = _freq.Name;

        _freqOptionsControl.GenerateFreqOptionsElements(_freq);
    }

    private void BrowsePathButton_OnClick(object sender, RoutedEventArgs e)
    {
        string typeString = FreqTypeComboBox.SelectionBoxItem.ToString()!;
        FreqType selectedFreqType = typeString.GetEnum<FreqType>();

        switch (selectedFreqType)
        {
            // not providing a description for the filter causes the filename returned to be empty
            case FreqType.Yomichan:
            case FreqType.YomichanKanji:
                BrowseForFrequencyFolder();
                break;

            case FreqType.Nazeka:
                BrowseForFrequencyFile("Nazeka file|*.json");
                break;

            default:
                throw new ArgumentOutOfRangeException(null, "Invalid FreqType (Add)");
        }
    }
}
