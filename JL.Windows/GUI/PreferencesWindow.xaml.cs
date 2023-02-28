using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using JL.Core;
using JL.Core.Anki;
using JL.Core.Network;
using JL.Core.Utilities;
using JL.Windows.Utilities;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace JL.Windows.GUI;

/// <summary>
/// Interaction logic for PreferenceWindow.xaml
/// </summary>
internal sealed partial class PreferencesWindow : Window
{
    private static PreferencesWindow? s_instance;
    public bool SetAnkiConfig { get; private set; } = false;

    public static PreferencesWindow Instance => s_instance ??= new PreferencesWindow();

    public PreferencesWindow()
    {
        InitializeComponent();
    }

    public static bool IsItVisible()
    {
        return s_instance?.IsVisible ?? false;
    }

    private const string WordJLFieldsInfo = @"• Primary Spelling: It's the spelling you click to mine the word. e.g. If you look up ""わかりました"", its primary spelling will be ""分かる"".
• Readings: Readings of the mined word.
• Alternative Spellings: Alternative spellings of the mined word. e.g. If you look up ""わかりました"", its alternative spellings will be ""解る, 判る, 分る"".
• Definitions: Definitions of the mined word.
• Dictionary Name: Name of the dictionary. e.g. JMDict.
• Audio: Audio for the first reading of the mined word.
• Source Text: Whole text in which the mined word appears in.
• Sentence: Sentence in which the mined word appears in.
• Matched Text: Text the mined word found as. e.g. わかりました.
• Deconjugated Matched Text: ""Matched Text""'s deconjugated form. e.g. If the matched text is ""わかりました"", Deconjugated Matched Text will be ""わかる"".
• Deconjugation Process: Deconjugation path from the Matched Text to Deconjugated Matched Text.
• Frequencies: Frequency info for the mined word. e.g. ""VN: #77, JPDB: 666"".
• EDICT ID: JMDict entry ID.
• Local Time: Mining date and time expressed in local timezone.";

    private const string KanjiJLFieldsInfo = @"• Primary Spelling: It's the spelling you click to mine the kanji. e.g. 妹.
• Readings: Kun+On+Nanori readings of the kanji.
• Kun Readings: Kun readings of the mined kanji.
• On Readings: On readings of the mined kanji.
• Nanori Readings: Nanori readings of the mined kanji.
• Stroke Count: Stroke count of the kanji.
• Kanji Grade: The kanji grade level.
• Kanji Composition: Kanji composition info e.g. ⿰女未 for 妹.
• Definitions: Definitions of the mined kanji.
• Dictionary Name: Name of the dictionary. e.g. Kanjidic.
• Source Text: Whole text in which the mined kanji appears in.
• Sentence: Sentence in which the mined kanji appears in.
• Frequencies: Frequency info for the kanji.
• EDICT ID: KANJIDIC2 entry ID.
• Local Time: Mining date and time expressed in local timezone.";

    private const string NameJLFieldsInfo = @"• Primary Spelling: It's the spelling you click to mine the name.
• Readings: Readings of the name.
• Alternative Spellings: Alternative spellings of the mined name.
• Definitions: Translations of the name.
• Dictionary Name: Name of the dictionary. e.g. JMnedict.
• Source Text: Whole text in which the mined name appears in.
• Sentence: Sentence in which the mined name appears in.
• EDICT ID: JMnedict entry ID.
• Local Time: Mining date and time expressed in local timezone.";

    #region EventHandlers

    private void ShowColorPicker(object sender, RoutedEventArgs e)
    {
        WindowsUtils.ShowColorPicker(sender, e);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Storage.Frontend.InvalidateDisplayCache();
        await ConfigManager.Instance.SavePreferences(this).ConfigureAwait(true);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        WindowsUtils.UpdateMainWindowVisibility();
        _ = MainWindow.Instance.Focus();
        s_instance = null;
    }

    private async void AnkiTabItem_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (ConfigManager.AnkiIntegration && !SetAnkiConfig)
        {
            await SetPreviousMiningConfig().ConfigureAwait(true);
            await PopulateDeckAndModelNames().ConfigureAwait(true);

            SetAnkiConfig = true;
        }
    }

    private async void CheckForJLUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        CheckForJLUpdatesButton.IsEnabled = false;
        await Networking.CheckForJLUpdates(false).ConfigureAwait(true);
        CheckForJLUpdatesButton.IsEnabled = true;
    }

    #endregion

    #region MiningSetup

    private async Task SetPreviousMiningConfig()
    {
        Dictionary<MineType, AnkiConfig>? ankiConfigDict = await AnkiConfig.ReadAnkiConfig().ConfigureAwait(true);

        if (ankiConfigDict is null)
        {
            return;
        }

        AnkiConfig? wordAnkiConfig = ankiConfigDict.GetValueOrDefault(MineType.Word);
        AnkiConfig? kanjiAnkiConfig = ankiConfigDict.GetValueOrDefault(MineType.Kanji);
        AnkiConfig? nameAnkiConfig = ankiConfigDict.GetValueOrDefault(MineType.Name);
        AnkiConfig? otherAnkiConfig = ankiConfigDict.GetValueOrDefault(MineType.Other);


        if (wordAnkiConfig is not null)
        {
            SetPreviousMiningConfig(WordMiningSetupComboBoxDeckNames, WordMiningSetupComboBoxModelNames, WordTagsTextBox, wordAnkiConfig);
            CreateFieldElements(wordAnkiConfig.Fields, Storage.JLFieldsForWordDicts, WordMiningSetupStackPanelFields);
        }

        if (kanjiAnkiConfig is not null)
        {
            SetPreviousMiningConfig(KanjiMiningSetupComboBoxDeckNames, KanjiMiningSetupComboBoxModelNames, KanjiTagsTextBox, kanjiAnkiConfig);
            CreateFieldElements(kanjiAnkiConfig.Fields, Storage.JLFieldsForKanjiDicts, KanjiMiningSetupStackPanelFields);
        }

        if (nameAnkiConfig is not null)
        {
            SetPreviousMiningConfig(NameMiningSetupComboBoxDeckNames, NameMiningSetupComboBoxModelNames, NameTagsTextBox, nameAnkiConfig);
            CreateFieldElements(nameAnkiConfig.Fields, Storage.JLFieldsForNameDicts, NameMiningSetupStackPanelFields);
        }

        if (otherAnkiConfig is not null)
        {
            SetPreviousMiningConfig(OtherMiningSetupComboBoxDeckNames, OtherMiningSetupComboBoxModelNames, OtherTagsTextBox, otherAnkiConfig);
            CreateFieldElements(otherAnkiConfig.Fields, Enum.GetValues<JLField>().ToList(), OtherMiningSetupStackPanelFields);
        }
    }

    private static void SetPreviousMiningConfig(Selector deckNamesSelector, Selector modelNamesComboBox, TextBox tagTextBox, AnkiConfig ankiConfig)
    {
        deckNamesSelector.ItemsSource = new List<string> { ankiConfig.DeckName };
        deckNamesSelector.SelectedItem = ankiConfig.DeckName;
        modelNamesComboBox.ItemsSource = new List<string> { ankiConfig.ModelName };
        modelNamesComboBox.SelectedItem = ankiConfig.ModelName;
        tagTextBox.Text = string.Join(",", ankiConfig.Tags);
    }

    private async Task PopulateDeckAndModelNames()
    {
        List<string>? deckNames = await AnkiUtils.GetDeckNames().ConfigureAwait(true);

        if (deckNames is not null)
        {
            List<string>? modelNames = await AnkiUtils.GetModelNames().ConfigureAwait(true);

            if (modelNames is not null)
            {
                WordMiningSetupComboBoxDeckNames.ItemsSource = deckNames.ToList();
                KanjiMiningSetupComboBoxDeckNames.ItemsSource = deckNames.ToList();
                NameMiningSetupComboBoxDeckNames.ItemsSource = deckNames.ToList();
                OtherMiningSetupComboBoxDeckNames.ItemsSource = deckNames.ToList();

                WordMiningSetupComboBoxModelNames.ItemsSource = modelNames.ToList();
                KanjiMiningSetupComboBoxModelNames.ItemsSource = modelNames.ToList();
                NameMiningSetupComboBoxModelNames.ItemsSource = modelNames.ToList();
                OtherMiningSetupComboBoxModelNames.ItemsSource = modelNames.ToList();
            }

            else
            {
                Storage.Frontend.Alert(AlertLevel.Error, "Error getting model names form Anki");
                Utils.Logger.Error("Error getting model names from Anki");
            }
        }

        else
        {
            Storage.Frontend.Alert(AlertLevel.Error, "Error getting deck names form Anki");
            Utils.Logger.Error("Error getting deck names from Anki");
        }
    }

    private async void MiningSetupButtonRefresh_Click(object sender, RoutedEventArgs e)
    {
        await PopulateDeckAndModelNames().ConfigureAwait(false);
    }

    private static async Task GetFields(ComboBox modelNamesComboBox, Panel miningPanel, IEnumerable<JLField> fieldList)
    {
        string modelName = modelNamesComboBox.SelectionBoxItem.ToString()!;

        List<string>? fieldNames = await AnkiUtils.GetFieldNames(modelName).ConfigureAwait(true);

        if (fieldNames is not null)
        {
            Dictionary<string, JLField> fields =
                fieldNames.ToDictionary(static fieldName => fieldName, static _ => JLField.Nothing);

            CreateFieldElements(fields, fieldList, miningPanel);
        }

        else
        {
            Storage.Frontend.Alert(AlertLevel.Error, "Error getting fields from AnkiConnect");
            Utils.Logger.Error("Error getting fields from AnkiConnect");
        }
    }

    private async void WordMiningSetupButtonGetFields_Click(object sender, RoutedEventArgs e)
    {
        await GetFields(WordMiningSetupComboBoxModelNames, WordMiningSetupStackPanelFields, Storage.JLFieldsForWordDicts).ConfigureAwait(false);
    }

    private async void KanjiMiningSetupButtonGetFields_Click(object sender, RoutedEventArgs e)
    {
        await GetFields(KanjiMiningSetupComboBoxModelNames, KanjiMiningSetupStackPanelFields, Storage.JLFieldsForKanjiDicts).ConfigureAwait(false);
    }

    private async void NameMiningSetupButtonGetFields_Click(object sender, RoutedEventArgs e)
    {
        await GetFields(NameMiningSetupComboBoxModelNames, NameMiningSetupStackPanelFields, Storage.JLFieldsForNameDicts).ConfigureAwait(false);
    }

    private async void OtherMiningSetupButtonGetFields_Click(object sender, RoutedEventArgs e)
    {
        await GetFields(OtherMiningSetupComboBoxModelNames, OtherMiningSetupStackPanelFields, Storage.JLFieldsForWordDicts).ConfigureAwait(false);
    }

    private static void CreateFieldElements(Dictionary<string, JLField> fields, IEnumerable<JLField> fieldList, Panel fieldPanel)
    {
        fieldPanel.Children.Clear();

        string[] descriptions = fieldList
            .Select(static jlFieldName => jlFieldName.GetDescription() ?? jlFieldName.ToString()).ToArray();

        foreach ((string fieldName, JLField jlField) in fields)
        {
            StackPanel stackPanel = new();
            var textBlockFieldName = new TextBlock { Text = fieldName };
            var comboBoxJLFields = new ComboBox
            {
                ItemsSource = descriptions,
                SelectedItem = jlField.GetDescription() ?? jlField.ToString()
            };

            _ = stackPanel.Children.Add(textBlockFieldName);
            _ = stackPanel.Children.Add(comboBoxJLFields);
            _ = fieldPanel.Children.Add(stackPanel);
        }
    }

    private static AnkiConfig? GetAnkiConfigFromPreferences(ComboBox deckNamesComboBox, ComboBox modelNamesComboBox, Panel miningPanel, TextBox tagsTextBox, IReadOnlyCollection<JLField> jlFieldList)
    {
        if (deckNamesComboBox.SelectedItem is null ||
            modelNamesComboBox.SelectedItem is null)
        {
            Storage.Frontend.Alert(AlertLevel.Error, "Save failed: Incomplete Anki config");
            Utils.Logger.Error("Save failed: Incomplete Anki config");
            return null;
        }

        string deckName = deckNamesComboBox.SelectedItem.ToString()!;
        string modelName = modelNamesComboBox.SelectedItem.ToString()!;

        Dictionary<string, JLField> dict = new();
        foreach (StackPanel stackPanel in miningPanel.Children)
        {
            var textBlock = (TextBlock)stackPanel.Children[0];
            var comboBox = (ComboBox)stackPanel.Children[1];

            string selectedDescription = comboBox.SelectionBoxItem.ToString()!;
            JLField result = jlFieldList.FirstOrDefault(jlFieldName =>
                    (jlFieldName.GetDescription() ?? jlFieldName.ToString()) == selectedDescription, JLField.Nothing);

            dict.Add(textBlock.Text, result);
        }

        string rawTags = tagsTextBox.Text;
        string[] tags = string.IsNullOrEmpty(rawTags)
            ? Array.Empty<string>()
            : rawTags.Split(',').Select(static s => s.Trim()).ToArray();

        return new AnkiConfig(deckName, modelName, dict, tags);
    }

    public async Task SaveMiningSetup()
    {
        if (!ConfigManager.AnkiIntegration)
        {
            return;
        }

        Dictionary<MineType, AnkiConfig> ankiConfigDict = new();

        AnkiConfig? ankiConfig = GetAnkiConfigFromPreferences(WordMiningSetupComboBoxDeckNames, WordMiningSetupComboBoxModelNames, WordMiningSetupStackPanelFields, WordTagsTextBox, Storage.JLFieldsForWordDicts);
        if (ankiConfig is not null)
        {
            ankiConfigDict.Add(MineType.Word, ankiConfig);
        }

        ankiConfig = GetAnkiConfigFromPreferences(KanjiMiningSetupComboBoxDeckNames, KanjiMiningSetupComboBoxModelNames, KanjiMiningSetupStackPanelFields, KanjiTagsTextBox, Storage.JLFieldsForKanjiDicts);
        if (ankiConfig is not null)
        {
            ankiConfigDict.Add(MineType.Kanji, ankiConfig);
        }

        ankiConfig = GetAnkiConfigFromPreferences(NameMiningSetupComboBoxDeckNames, NameMiningSetupComboBoxModelNames, NameMiningSetupStackPanelFields, NameTagsTextBox, Storage.JLFieldsForNameDicts);
        if (ankiConfig is not null)
        {
            ankiConfigDict.Add(MineType.Name, ankiConfig);
        }

        ankiConfig = GetAnkiConfigFromPreferences(OtherMiningSetupComboBoxDeckNames, OtherMiningSetupComboBoxModelNames, OtherMiningSetupStackPanelFields, OtherTagsTextBox, Storage.JLFieldsForWordDicts);
        if (ankiConfig is not null)
        {
            ankiConfigDict.Add(MineType.Other, ankiConfig);
        }

        if (ankiConfigDict.Count > 0)
        {
            _ = await AnkiConfig.WriteAnkiConfig(ankiConfigDict).ConfigureAwait(false);
        }

        else
        {
            Storage.Frontend.Alert(AlertLevel.Error, "Error saving AnkiConfig");
            Utils.Logger.Error("Error saving AnkiConfig");
            ConfigManager.AnkiIntegration = false;
        }
    }

    #endregion

    #region Keys

    private void KeyGestureToText(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        Key key = e.Key is Key.System
            ? e.SystemKey
            : e.Key;

        if (key is Key.LWin or Key.RWin)
        {
            return;
        }

        StringBuilder hotkeyTextBuilder = new();

        if (key is Key.LeftShift or Key.RightShift
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt)
        {
            _ = hotkeyTextBuilder.Append(key.ToString());
        }

        else
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) is not 0)
            {
                _ = hotkeyTextBuilder.Append("Ctrl+");
            }

            if ((Keyboard.Modifiers & ModifierKeys.Alt) is not 0)
            {
                _ = hotkeyTextBuilder.Append("Alt+");
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) is not 0 && hotkeyTextBuilder.Length > 0)
            {
                _ = hotkeyTextBuilder.Append("Shift+");
            }

            _ = hotkeyTextBuilder.Append(key.ToString());
        }

        ((TextBox)sender).Text = hotkeyTextBuilder.ToString();
    }

    private void ClearKeyGestureButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var dockPanel = (DockPanel)button.Parent;
        TextBox textBox = dockPanel.Children.OfType<TextBox>().ToArray()[0];
        textBox.Text = "None";
    }

    #endregion

    private void ApplyAnkiConnectUrlButton_Click(object sender, RoutedEventArgs e)
    {
        if (Uri.IsWellFormedUriString(AnkiUriTextBox.Text, UriKind.Absolute))
        {
            ConfigManager.Instance.AnkiConnectUri = new Uri(AnkiUriTextBox.Text);
        }

        else
        {
            Storage.Frontend.Alert(AlertLevel.Error, "Couldn't save AnkiConnect server address, invalid URL");
        }
    }

    private void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        string title = "JL Fields for ";
        string text;

        switch (((Button)sender).Name)
        {
            case "WordInfoButton":
                title += "Words";
                text = WordJLFieldsInfo;
                break;

            case "KanjiInfoButton":
                title += "Kanjis";
                text = KanjiJLFieldsInfo;
                break;

            case "NameInfoButton":
                title += "Names";
                text = NameJLFieldsInfo;
                break;

            default:
                title += "Others";
                text = WordJLFieldsInfo;
                break;
        }

        InfoWindow infoWindow = new()
        {
            Owner = this,
            Title = title,
            InfoTextBox = { Text = text }
        };

        _ = infoWindow.ShowDialog();
    }
}
