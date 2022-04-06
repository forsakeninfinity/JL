﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using JL.Core;
using JL.Core.Anki;
using JL.Core.Dicts;
using JL.Core.Dicts.Kanjium;
using JL.Core.Lookup;
using JL.Core.Utilities;
using JL.Windows.Utilities;

namespace JL.Windows.GUI
{
    /// <summary>
    /// Interaction logic for PopupWindow.xaml
    /// </summary>
    public partial class PopupWindow : Window
    {
        private readonly PopupWindow _parentPopupWindow;
        public PopupWindow ChildPopupWindow { get; set; }

        private TextBox _lastTextBox;

        private int _playAudioIndex;

        private int _currentCharPosition;

        private string _currentText;

        private string _lastSelectedText;

        private List<LookupResult> _lastLookupResults = new();

        public bool UnavoidableMouseEnter { get; set; }

        public string LastText { get; set; }

        public bool MiningMode { get; set; }

        public ObservableCollection<StackPanel> ResultStackPanels { get; } = new();

        public PopupWindow()
        {
            InitializeComponent();
            Init();

            // need to initialize window (position) for later
            Show();
            Hide();
        }

        public PopupWindow(PopupWindow parentPopUp) : this()
        {
            _parentPopupWindow = parentPopUp;
        }

        public void Init()
        {
            Background = ConfigManager.PopupBackgroundColor;
            FontFamily = ConfigManager.PopupFont;

            MaxHeight = ConfigManager.PopupMaxHeight;
            MaxWidth = ConfigManager.PopupMaxWidth;

            if (ConfigManager.PopupDynamicWidth && ConfigManager.PopupDynamicHeight)
            {
                SizeToContent = SizeToContent.WidthAndHeight;
            }

            else if (ConfigManager.PopupDynamicWidth)
            {
                SizeToContent = SizeToContent.Width;
                Height = ConfigManager.PopupMaxHeight;
            }

            else if (ConfigManager.PopupDynamicHeight)
            {
                SizeToContent = SizeToContent.Height;
                Width = ConfigManager.PopupMaxWidth;
            }

            else
            {
                SizeToContent = SizeToContent.Manual;
                Height = ConfigManager.PopupMaxHeight;
                Width = ConfigManager.PopupMaxWidth;
            }


            WindowsUtils.SetInputGestureText(AddNameButton, ConfigManager.ShowAddNameWindowKeyGesture);
            WindowsUtils.SetInputGestureText(AddWordButton, ConfigManager.ShowAddWordWindowKeyGesture);
            WindowsUtils.SetInputGestureText(SearchButton, ConfigManager.SearchWithBrowserKeyGesture);
            WindowsUtils.SetInputGestureText(ManageDictionariesButton,
                ConfigManager.ShowManageDictionariesWindowKeyGesture);
            WindowsUtils.SetInputGestureText(StatsButton, ConfigManager.ShowStatsKeyGesture);

            TextBlockMiningModeReminder.Text =
                $"Click on an entry's main spelling to mine it," + Environment.NewLine +
                $"or press {ConfigManager.ClosePopupKeyGesture.Key} or click on the main window to exit.";
        }

        private void AddName(object sender, RoutedEventArgs e)
        {
            WindowsUtils.ShowAddNameWindow(_lastSelectedText);
        }

        private void AddWord(object sender, RoutedEventArgs e)
        {
            WindowsUtils.ShowAddWordWindow(_lastSelectedText);
        }

        private void SearchWithBrowser(object sender, RoutedEventArgs e)
        {
            WindowsUtils.SearchWithBrowser(_lastSelectedText);
        }

        private void ShowManageDictionariesWindow(object sender, RoutedEventArgs e)
        {
            WindowsUtils.ShowManageDictionariesWindow();
        }

        private void ShowStats(object sender, RoutedEventArgs e)
        {
            WindowsUtils.ShowStatsWindow();
        }

        public void TextBox_MouseMove(TextBox tb)
        {
            if (MiningMode || ConfigManager.InactiveLookupMode
                           || (ConfigManager.RequireLookupKeyPress
                               && !Keyboard.Modifiers.HasFlag(ConfigManager.LookupKey))
                           || (ConfigManager.FixedPopupPositioning && _parentPopupWindow != null)
               )
                return;

            _lastTextBox = tb;

            int charPosition = tb.GetCharacterIndexFromPoint(Mouse.GetPosition(tb), false);
            if (charPosition != -1)
            {
                if (charPosition > 0 && char.IsHighSurrogate(tb.Text[charPosition - 1]))
                    --charPosition;

                _currentText = tb.Text;
                _currentCharPosition = charPosition;

                int endPosition = Utils.FindWordBoundary(tb.Text, charPosition);

                string text;
                if (endPosition - charPosition <= ConfigManager.MaxSearchLength)
                    text = tb.Text[charPosition..endPosition];
                else
                    text = tb.Text[charPosition..(charPosition + ConfigManager.MaxSearchLength)];

                if (text == LastText) return;
                LastText = text;

                ResultStackPanels.Clear();
                List<LookupResult> lookupResults = Lookup.LookupText(text);

                if (lookupResults != null && lookupResults.Any())
                {
                    _lastSelectedText = lookupResults[0].FoundForm;
                    if (ConfigManager.HighlightLongestMatch)
                    {
                        double verticalOffset = tb.VerticalOffset;

                        if (ConfigManager.PopupFocusOnLookup)
                        {
                            tb.Focus();
                        }

                        tb.Select(charPosition, lookupResults[0].FoundForm.Length);
                        tb.ScrollToVerticalOffset(verticalOffset);
                    }

                    Init();
                    Visibility = Visibility.Visible;

                    if (ConfigManager.PopupFocusOnLookup)
                    {
                        Activate();
                        Focus();
                    }

                    _lastLookupResults = lookupResults;
                    DisplayResults(false);
                }
                else
                {
                    Visibility = Visibility.Hidden;

                    if (ConfigManager.HighlightLongestMatch)
                    {
                        //Unselect(tb);
                    }
                }
            }
            else
            {
                LastText = "";
                Visibility = Visibility.Hidden;

                if (ConfigManager.HighlightLongestMatch)
                {
                    Unselect(tb);
                }
            }
        }

        public void LookupOnSelect(TextBox tb)
        {
            if (string.IsNullOrWhiteSpace(tb.SelectedText))
                return;

            _lastTextBox = tb;

            PopUpScrollViewer.ScrollToTop();

            List<LookupResult> lookupResults = Lookup.LookupText(tb.SelectedText);

            if (lookupResults?.Any() ?? false)
            {
                ResultStackPanels.Clear();

                Init();

                PopUpScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                Visibility = Visibility.Visible;

                if (ConfigManager.PopupFocusOnLookup)
                {
                    Activate();
                    Focus();
                }

                _lastLookupResults = lookupResults;
                DisplayResults(true);
            }
            else
            {
                PopUpScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                Visibility = Visibility.Hidden;
            }
        }

        public void UpdatePosition(Point cursorPosition)
        {
            double mouseX = cursorPosition.X / WindowsUtils.Dpi.DpiScaleX;
            double mouseY = cursorPosition.Y / WindowsUtils.Dpi.DpiScaleY;

            bool needsFlipX = ConfigManager.PopupFlipX && mouseX + Width > WindowsUtils.DpiAwareWorkAreaWidth;
            bool needsFlipY = ConfigManager.PopupFlipY && mouseY + Height > WindowsUtils.DpiAwareWorkAreaHeight;

            double newLeft;
            double newTop;

            UnavoidableMouseEnter = false;

            if (needsFlipX)
            {
                // flip Leftwards while preventing -OOB
                newLeft = mouseX - Width - WindowsUtils.DpiAwareXOffset * 2;
                if (newLeft < 0) newLeft = 0;
            }
            else
            {
                // no flip
                newLeft = mouseX + WindowsUtils.DpiAwareXOffset;
            }

            if (needsFlipY)
            {
                // flip Upwards while preventing -OOB
                newTop = mouseY - Height - WindowsUtils.DpiAwareYOffset * 2;
                if (newTop < 0) newTop = 0;
            }
            else
            {
                // no flip
                newTop = mouseY + WindowsUtils.DpiAwareYOffset;
            }

            // stick to edges if +OOB
            if (newLeft + Width > WindowsUtils.DpiAwareWorkAreaWidth)
            {
                newLeft = WindowsUtils.DpiAwareWorkAreaWidth - Width;
            }

            if (newTop + Height > WindowsUtils.DpiAwareWorkAreaHeight)
            {
                newTop = WindowsUtils.DpiAwareWorkAreaHeight - Height;
            }

            if (mouseX >= newLeft && mouseX <= newLeft + Width && mouseY >= newTop && mouseY <= newTop + Height)
            {
                UnavoidableMouseEnter = true;
            }

            Left = newLeft;
            Top = newTop;
        }

        public void UpdatePosition(double x, double y)
        {
            Left = x;
            Top = y;
        }

        private void DisplayResults(bool generateAllResults)
        {
            int resultCount = _lastLookupResults.Count;

            for (int index = 0; index < resultCount; index++)
            {
                // if (!generateAllResults && index > 0)
                // {
                //     PopupListBox.UpdateLayout();
                //
                //     if (PopupListBox.ActualHeight >= MaxHeight - 30)
                //         return;
                // }

                ResultStackPanels.Add(MakeResultStackPanel(_lastLookupResults[index], index, resultCount));
            }

            UpdateLayout();
        }

        private StackPanel MakeResultStackPanel(LookupResult result,
            int index, int resultsCount)
        {
            var innerStackPanel = new StackPanel { Margin = new Thickness(4, 2, 4, 2), };
            WrapPanel top = new();
            StackPanel bottom = new();

            innerStackPanel.Children.Add(top);
            innerStackPanel.Children.Add(bottom);

            // top
            TextBlock textBlockFoundSpelling = null;
            TextBlock textBlockPOrthographyInfo = null;
            UIElement uiElementReadings = null;
            UIElement uiElementAlternativeSpellings = null;
            TextBlock textBlockProcess = null;
            TextBlock textBlockFrequency = null;
            TextBlock textBlockFoundForm = null;
            TextBlock textBlockDictType = null;
            TextBlock textBlockEdictID = null;

            // bottom
            UIElement uiElementDefinitions = null;
            TextBlock textBlockNanori = null;
            TextBlock textBlockOnReadings = null;
            TextBlock textBlockKunReadings = null;
            TextBlock textBlockStrokeCount = null;
            TextBlock textBlockGrade = null;
            TextBlock textBlockComposition = null;

            if (result.FoundForm != null)
            {
                textBlockFoundForm = new TextBlock
                {
                    Name = nameof(result.FoundForm),
                    Text = result.FoundForm,
                    Visibility = Visibility.Collapsed,
                };
            }

            if (result.Frequency != int.MaxValue && result.Frequency > 0)
            {
                textBlockFrequency = new TextBlock
                {
                    Name = nameof(result.Frequency),
                    Text = "#" + result.Frequency,
                    Foreground = ConfigManager.FrequencyColor,
                    FontSize = ConfigManager.FrequencyFontSize,
                    Margin = new Thickness(5, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            if (result.DictType != null)
            {
                IEnumerable<DictType> dictTypeNames = Enum.GetValues(typeof(DictType)).Cast<DictType>();
                DictType dictType = dictTypeNames.First(dictTypeName => dictTypeName.ToString() == result.DictType);

                textBlockDictType = new TextBlock
                {
                    Name = nameof(result.DictType),
                    Text = dictType.GetDescription() ?? result.DictType,
                    Foreground = ConfigManager.DictTypeColor,
                    FontSize = ConfigManager.DictTypeFontSize,
                    Margin = new Thickness(5, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            if (result.FoundSpelling != null)
            {
                textBlockFoundSpelling = new TextBlock
                {
                    Name = nameof(result.FoundSpelling),
                    Text = result.FoundSpelling,
                    Tag = index, // for audio
                    Foreground = ConfigManager.PrimarySpellingColor,
                    FontSize = ConfigManager.PrimarySpellingFontSize,
                    TextWrapping = TextWrapping.Wrap,
                };
                textBlockFoundSpelling.MouseEnter += FoundSpelling_MouseEnter; // for audio
                textBlockFoundSpelling.MouseLeave += FoundSpelling_MouseLeave; // for audio
                textBlockFoundSpelling.PreviewMouseUp += FoundSpelling_PreviewMouseUp; // for mining
            }

            if (result.Readings != null && result.Readings.Any())
            {
                List<string> rOrthographyInfoList = result.ROrthographyInfoList ??= new();
                List<string> readings = result.Readings;
                string readingsText =
                    PopupWindowUtilities.MakeUiElementReadingsText(readings, rOrthographyInfoList);

                if (readingsText != "")
                {
                    if (MiningMode || ConfigManager.LookupOnSelectOnly)
                    {
                        uiElementReadings = new TextBox()
                        {
                            Name = nameof(result.Readings),
                            Text = readingsText,
                            TextWrapping = TextWrapping.Wrap,
                            Background = Brushes.Transparent,
                            Foreground = ConfigManager.ReadingsColor,
                            FontSize = ConfigManager.ReadingsFontSize,
                            BorderThickness = new Thickness(0, 0, 0, 0),
                            Margin = new Thickness(5, 0, 0, 0),
                            Padding = new Thickness(0),
                            IsReadOnly = true,
                            IsUndoEnabled = false,
                            Cursor = Cursors.Arrow,
                            SelectionBrush = ConfigManager.HighlightColor,
                            IsInactiveSelectionHighlightEnabled = true,
                            ContextMenu = PopupContextMenu,
                        };

                        uiElementReadings.PreviewMouseLeftButtonUp += UiElement_PreviewMouseLeftButtonUp;
                        uiElementReadings.MouseMove += PopupMouseMove;
                        uiElementReadings.LostFocus += Unselect;
                        uiElementReadings.PreviewMouseRightButtonUp += TextBoxPreviewMouseRightButtonUp;
                    }
                    else
                    {
                        uiElementReadings = new TextBlock
                        {
                            Name = nameof(result.Readings),
                            Text = readingsText,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = ConfigManager.ReadingsColor,
                            FontSize = ConfigManager.ReadingsFontSize,
                            Margin = new Thickness(5, 0, 0, 0),
                        };
                    }
                }
            }

            if (result.FormattedDefinitions != null && result.FormattedDefinitions.Any())
            {
                if (MiningMode || ConfigManager.LookupOnSelectOnly)
                {
                    uiElementDefinitions = new TextBox
                    {
                        Name = nameof(result.FormattedDefinitions),
                        Text = result.FormattedDefinitions,
                        TextWrapping = TextWrapping.Wrap,
                        Background = Brushes.Transparent,
                        Foreground = ConfigManager.DefinitionsColor,
                        FontSize = ConfigManager.DefinitionsFontSize,
                        BorderThickness = new Thickness(0, 0, 0, 0),
                        Margin = new Thickness(2, 2, 2, 2),
                        Padding = new Thickness(0),
                        IsReadOnly = true,
                        IsUndoEnabled = false,
                        Cursor = Cursors.Arrow,
                        SelectionBrush = ConfigManager.HighlightColor,
                        IsInactiveSelectionHighlightEnabled = true,
                        ContextMenu = PopupContextMenu,
                    };

                    uiElementDefinitions.PreviewMouseLeftButtonUp += UiElement_PreviewMouseLeftButtonUp;

                    uiElementDefinitions.MouseMove += PopupMouseMove;
                    uiElementDefinitions.LostFocus += Unselect;
                    uiElementDefinitions.PreviewMouseRightButtonUp += TextBoxPreviewMouseRightButtonUp;
                }
                else
                {
                    uiElementDefinitions = new TextBlock
                    {
                        Name = nameof(result.FormattedDefinitions),
                        Text = result.FormattedDefinitions,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = ConfigManager.DefinitionsColor,
                        FontSize = ConfigManager.DefinitionsFontSize,
                        Margin = new Thickness(2, 2, 2, 2),
                    };
                }
            }

            if (result.EdictID != null)
            {
                textBlockEdictID = new TextBlock
                {
                    Name = nameof(result.EdictID),
                    Text = result.EdictID,
                    Visibility = Visibility.Collapsed,
                };
            }

            if (result.AlternativeSpellings != null && result.AlternativeSpellings.Any())
            {
                List<string> aOrthographyInfoList = result.AOrthographyInfoList ??= new List<string>();
                List<string> alternativeSpellings = result.AlternativeSpellings;
                string alternativeSpellingsText =
                    PopupWindowUtilities.MakeUiElementAlternativeSpellingsText(alternativeSpellings,
                        aOrthographyInfoList);

                if (alternativeSpellingsText != "")
                {
                    if (MiningMode || ConfigManager.LookupOnSelectOnly)
                    {
                        uiElementAlternativeSpellings = new TextBox()
                        {
                            Name = nameof(result.AlternativeSpellings),
                            Text = alternativeSpellingsText,
                            TextWrapping = TextWrapping.Wrap,
                            Background = Brushes.Transparent,
                            Foreground = ConfigManager.AlternativeSpellingsColor,
                            FontSize = ConfigManager.AlternativeSpellingsFontSize,
                            BorderThickness = new Thickness(0, 0, 0, 0),
                            Margin = new Thickness(5, 0, 0, 0),
                            Padding = new Thickness(0),
                            IsReadOnly = true,
                            IsUndoEnabled = false,
                            Cursor = Cursors.Arrow,
                            SelectionBrush = ConfigManager.HighlightColor,
                            IsInactiveSelectionHighlightEnabled = true,
                            ContextMenu = PopupContextMenu,
                        };

                        uiElementAlternativeSpellings.PreviewMouseLeftButtonUp +=
                            UiElement_PreviewMouseLeftButtonUp;
                        uiElementAlternativeSpellings.MouseMove += PopupMouseMove;
                        uiElementAlternativeSpellings.LostFocus += Unselect;
                        uiElementAlternativeSpellings.PreviewMouseRightButtonUp += TextBoxPreviewMouseRightButtonUp;
                    }
                    else
                    {
                        uiElementAlternativeSpellings = new TextBlock
                        {
                            Name = nameof(result.AlternativeSpellings),
                            Text = alternativeSpellingsText,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = ConfigManager.AlternativeSpellingsColor,
                            FontSize = ConfigManager.AlternativeSpellingsFontSize,
                            Margin = new Thickness(5, 0, 0, 0),
                        };
                    }
                }
            }

            if (result.Process != null)
            {
                textBlockProcess = new TextBlock
                {
                    Name = nameof(result.Process),
                    Text = result.Process,
                    Foreground = ConfigManager.DeconjugationInfoColor,
                    FontSize = ConfigManager.DeconjugationInfoFontSize,
                    Margin = new Thickness(5, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            if (result.POrthographyInfoList != null && result.POrthographyInfoList.Any())
            {
                textBlockPOrthographyInfo = new TextBlock
                {
                    Name = nameof(result.POrthographyInfoList),
                    Text = $"({string.Join(",", result.POrthographyInfoList)})",
                    Foreground = ConfigManager.PrimarySpellingColor,
                    FontSize = ConfigManager.PrimarySpellingFontSize,
                    Margin = new Thickness(5, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            // KANJIDIC
            if (result.OnReadings != null && result.OnReadings.Any())
            {
                textBlockOnReadings = new TextBlock
                {
                    Name = nameof(result.OnReadings),
                    Text = "On" + ": " + string.Join(", ", result.OnReadings),
                    Foreground = ConfigManager.ReadingsColor,
                    FontSize = ConfigManager.ReadingsFontSize,
                    Margin = new Thickness(2, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            if (result.KunReadings != null && result.KunReadings.Any())
            {
                textBlockKunReadings = new TextBlock
                {
                    Name = nameof(result.KunReadings),
                    Text = "Kun" + ": " + string.Join(", ", result.KunReadings),
                    Foreground = ConfigManager.ReadingsColor,
                    FontSize = ConfigManager.ReadingsFontSize,
                    Margin = new Thickness(2, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            if (result.Nanori != null && result.Nanori.Any())
            {
                textBlockNanori = new TextBlock
                {
                    Name = nameof(result.Nanori),
                    Text = nameof(result.Nanori) + ": " + string.Join(", ", result.Nanori),
                    Foreground = ConfigManager.ReadingsColor,
                    FontSize = ConfigManager.ReadingsFontSize,
                    Margin = new Thickness(2, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            if (result.StrokeCount > 0)
            {
                textBlockStrokeCount = new TextBlock
                {
                    Name = nameof(result.StrokeCount),
                    Text = "Strokes" + ": " + result.StrokeCount,
                    Foreground = ConfigManager.DefinitionsColor,
                    FontSize = ConfigManager.DefinitionsFontSize,
                    Margin = new Thickness(2, 2, 2, 2),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            if (result.Grade > 0)
            {
                string gradeString = "";
                int gradeInt = result.Grade;
                switch (gradeInt)
                {
                    case 0:
                        gradeString = "Hyougai";
                        break;
                    case <= 6:
                        gradeString = $"{gradeInt} (Kyouiku)";
                        break;
                    case 8:
                        gradeString = $"{gradeInt} (Jouyou)";
                        break;
                    case <= 10:
                        gradeString = $"{gradeInt} (Jinmeiyou)";
                        break;
                }

                textBlockGrade = new TextBlock
                {
                    Name = nameof(result.Grade),
                    Text = nameof(result.Grade) + ": " + gradeString,
                    Foreground = ConfigManager.DefinitionsColor,
                    FontSize = ConfigManager.DefinitionsFontSize,
                    Margin = new Thickness(2, 2, 2, 2),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            if (result.Composition != null && result.Composition.Any())
            {
                textBlockComposition = new TextBlock
                {
                    Name = nameof(result.Composition),
                    Text = nameof(result.Composition) + ": " + result.Composition,
                    Foreground = ConfigManager.DefinitionsColor,
                    FontSize = ConfigManager.DefinitionsFontSize,
                    Margin = new Thickness(2, 2, 2, 2),
                    TextWrapping = TextWrapping.Wrap,
                };
            }

            UIElement[] babies =
            {
                textBlockFoundSpelling, textBlockPOrthographyInfo, uiElementReadings, uiElementAlternativeSpellings,
                textBlockProcess, textBlockFoundForm, textBlockEdictID, textBlockFrequency, textBlockDictType
            };

            for (int i = 0; i < babies.Length; i++)
            {
                UIElement baby = babies[i];

                if (baby is TextBlock textBlock)
                {
                    if (textBlock == null) continue;

                    // common emptiness check
                    if (textBlock.Text == "")
                        continue;

                    // POrthographyInfo check
                    if (textBlock.Text == "()")
                        continue;

                    textBlock.MouseLeave += OnMouseLeave;

                    if ((textBlock.Name is "FoundSpelling" or "Readings") &&
                        Storage.Dicts.TryGetValue(DictType.Kanjium, out Dict kanjiumDict) && kanjiumDict.Active)
                    {
                        List<string> readings = result.Readings;

                        if (textBlock.Name is "FoundSpelling" && readings.Any())
                        {
                            top.Children.Add(textBlock);
                        }
                        else
                        {
                            Grid pitchAccentGrid = CreatePitchAccentGrid(result.FoundSpelling,
                                result.AlternativeSpellings,
                                readings,
                                textBlock.Text.Split(", ").ToList(),
                                textBlock.Margin.Left);

                            if (pitchAccentGrid.Children.Count == 0)
                            {
                                top.Children.Add(textBlock);
                            }
                            else
                            {
                                pitchAccentGrid.Children.Add(textBlock);
                                top.Children.Add(pitchAccentGrid);
                            }
                        }
                    }
                    else
                        top.Children.Add(textBlock);
                }
                else if (baby is TextBox textBox)
                {
                    if (textBox == null) continue;

                    // common emptiness check
                    if (textBox.Text == "")
                        continue;

                    textBox.MouseLeave += OnMouseLeave;

                    if ((textBox.Name is "FoundSpelling" or "Readings") &&
                        Storage.Dicts.TryGetValue(DictType.Kanjium, out Dict kanjiumDict) && kanjiumDict.Active)
                    {
                        List<string> readings = result.Readings;

                        if (textBox.Name is "FoundSpelling" && readings.Any())
                        {
                            top.Children.Add(textBox);
                        }
                        else
                        {
                            Grid pitchAccentGrid = CreatePitchAccentGrid(result.FoundSpelling,
                                result.AlternativeSpellings,
                                readings,
                                textBox.Text.Split(", ").ToList(),
                                textBox.Margin.Left);

                            if (pitchAccentGrid.Children.Count == 0)
                            {
                                top.Children.Add(textBox);
                            }
                            else
                            {
                                pitchAccentGrid.Children.Add(textBox);
                                top.Children.Add(pitchAccentGrid);
                            }
                        }
                    }
                    else
                        top.Children.Add(textBox);
                }
            }

            if (uiElementDefinitions != null)
                bottom.Children.Add(uiElementDefinitions);

            TextBlock[] babiesKanji =
            {
                textBlockOnReadings, textBlockKunReadings, textBlockNanori, textBlockGrade, textBlockStrokeCount,
                textBlockComposition,
            };
            foreach (TextBlock baby in babiesKanji)
            {
                if (baby == null) continue;

                // common emptiness check
                if (baby.Text == "")
                    continue;

                baby.MouseLeave += OnMouseLeave;

                bottom.Children.Add(baby);
            }

            if (index != resultsCount - 1)
            {
                bottom.Children.Add(new Separator
                {
                    // TODO: Fix thickness' differing from one separator to another
                    // Width = PopupWindow.Width,
                    Background = ConfigManager.SeparatorColor
                });
            }

            innerStackPanel.MouseLeave += OnMouseLeave;
            top.MouseLeave += OnMouseLeave;
            bottom.MouseLeave += OnMouseLeave;

            return innerStackPanel;
        }

        private static Grid CreatePitchAccentGrid(string foundSpelling, List<string> alternativeSpellings,
            List<string> readings, List<string> splitReadingsWithRInfo, double leftMargin)
        {
            Dictionary<string, List<IResult>> kanjiumDict = Storage.Dicts[DictType.Kanjium].Contents;
            Grid pitchAccentGrid = new();

            bool hasReading = readings.Any();

            int fontSize = hasReading
                ? ConfigManager.ReadingsFontSize
                : ConfigManager.PrimarySpellingFontSize;

            List<string> expressions = hasReading ? readings : new List<string> { foundSpelling };

            double horizontalOffsetForReading = leftMargin;

            for (int i = 0; i < expressions.Count; i++)
            {
                string normalizedExpression = Kana.KatakanaToHiraganaConverter(expressions[i]);
                List<string> combinedFormList = Kana.CreateCombinedForm(expressions[i]);

                if (i > 0)
                {
                    horizontalOffsetForReading +=
                        WindowsUtils.MeasureTextSize(splitReadingsWithRInfo[i - 1] + ", ", fontSize).Width;
                }

                if (kanjiumDict.TryGetValue(normalizedExpression, out List<IResult> kanjiumResultList))
                {
                    KanjiumResult chosenKanjiumResult = null;

                    for (int j = 0; j < kanjiumResultList.Count; j++)
                    {
                        var kanjiumResult = (KanjiumResult)kanjiumResultList[j];

                        if (!hasReading || normalizedExpression == Kana.KatakanaToHiraganaConverter(kanjiumResult.Reading))
                        {
                            if (foundSpelling == kanjiumResult.Spelling)
                            {
                                chosenKanjiumResult = kanjiumResult;
                                break;
                            }

                            else if (alternativeSpellings?.Contains(kanjiumResult.Spelling) ?? false)
                            {
                                if (chosenKanjiumResult == null)
                                    chosenKanjiumResult = kanjiumResult;
                            }
                        }
                    }

                    if (chosenKanjiumResult != null)
                    {
                        Polyline polyline = new()
                        {
                            StrokeThickness = 2,
                            Stroke = ConfigManager.PitchAccentMarkerColor,
                            StrokeDashArray = new DoubleCollection { 1, 1 }
                        };

                        bool lowPitch = false;
                        double horizontalOffsetForChar = horizontalOffsetForReading;
                        for (int j = 0; j < combinedFormList.Count; j++)
                        {
                            Size charSize = WindowsUtils.MeasureTextSize(combinedFormList[j], fontSize);

                            if (chosenKanjiumResult.Position - 1 == j)
                            {
                                polyline.Points.Add(new Point(horizontalOffsetForChar, 0));
                                polyline.Points.Add(new Point(horizontalOffsetForChar + charSize.Width, 0));
                                polyline.Points.Add(new Point(horizontalOffsetForChar + charSize.Width,
                                    charSize.Height));

                                lowPitch = true;
                            }

                            else if (j == 0)
                            {
                                polyline.Points.Add(new Point(horizontalOffsetForChar, charSize.Height));
                                polyline.Points.Add(new Point(horizontalOffsetForChar + charSize.Width,
                                    charSize.Height));
                                polyline.Points.Add(new Point(horizontalOffsetForChar + charSize.Width, 0));
                            }

                            else
                            {
                                double charHeight = lowPitch ? charSize.Height : 0;
                                polyline.Points.Add(new Point(horizontalOffsetForChar, charHeight));
                                polyline.Points.Add(new Point(horizontalOffsetForChar + charSize.Width,
                                    charHeight));
                            }

                            horizontalOffsetForChar += charSize.Width;
                        }

                        pitchAccentGrid.Children.Add(polyline);
                    }
                }
            }

            return pitchAccentGrid;
        }

        private void Unselect(object sender, RoutedEventArgs e)
        {
            Unselect((TextBox)sender);
            //((TextBox)sender).Select(0, 0);
        }

        private static void Unselect(TextBox tb)
        {
            double verticalOffset = tb.VerticalOffset;
            tb.Select(0, 0);
            tb.ScrollToVerticalOffset(verticalOffset);
        }

        private void TextBoxPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ManageDictionariesButton.IsEnabled = Storage.Ready
                                                 && !Storage.UpdatingJMdict
                                                 && !Storage.UpdatingJMnedict
                                                 && !Storage.UpdatingKanjidic;

            AddNameButton.IsEnabled = Storage.Ready;
            AddWordButton.IsEnabled = Storage.Ready;

            _lastSelectedText = ((TextBox)sender).SelectedText;
        }

        private void PopupMouseMove(object sender, MouseEventArgs e)
        {
            if (ConfigManager.LookupOnSelectOnly
                || (ConfigManager.RequireLookupKeyPress
                && !Keyboard.Modifiers.HasFlag(ConfigManager.LookupKey)))
                return;

            ChildPopupWindow ??= new PopupWindow(this);

            if (ChildPopupWindow.MiningMode)
                return;


            // prevents stray PopupWindows being created when you move your mouse too fast
            if (MiningMode)
            {
                ChildPopupWindow.Definitions_MouseMove((TextBox)sender);

                if (!ChildPopupWindow.MiningMode)
                {
                    if (ConfigManager.FixedPopupPositioning)
                    {
                        ChildPopupWindow.UpdatePosition(WindowsUtils.DpiAwareFixedPopupXPosition, WindowsUtils.DpiAwareFixedPopupYPosition);
                    }

                    else
                    {
                        ChildPopupWindow.UpdatePosition(PointToScreen(Mouse.GetPosition(this)));
                    }
                }
            }
        }

        private void FoundSpelling_MouseEnter(object sender, MouseEventArgs e)
        {
            var textBlock = (TextBlock)sender;
            _playAudioIndex = (int)textBlock.Tag;
        }

        private void FoundSpelling_MouseLeave(object sender, MouseEventArgs e)
        {
            _playAudioIndex = 0;
        }

        private async void FoundSpelling_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!MiningMode && !ConfigManager.LookupOnSelectOnly)
                return;

            MiningMode = false;
            TextBlockMiningModeReminder.Visibility = Visibility.Collapsed;
            Hide();

            var miningParams = new Dictionary<JLField, string>();
            foreach (JLField jlf in Enum.GetValues(typeof(JLField)))
            {
                miningParams[jlf] = "";
            }

            var textBlock = (TextBlock)sender;

            WrapPanel top;

            if (textBlock.Parent is Grid foundSpellingGrid)
            {
                top = (WrapPanel)foundSpellingGrid.Parent;
            }

            else
            {
                top = (WrapPanel)textBlock.Parent;
            }

            foreach (UIElement child in top.Children)
            {
                switch (child)
                {
                    case TextBox chi:
                        switch (chi.Name)
                        {
                            case nameof(LookupResult.Readings):
                                miningParams[JLField.Readings] = chi.Text;
                                break;
                            case nameof(LookupResult.AlternativeSpellings):
                                miningParams[JLField.AlternativeSpellings] = chi.Text;
                                break;
                        }

                        break;
                    case TextBlock ch:
                        switch (ch.Name)
                        {
                            case nameof(LookupResult.FoundSpelling):
                                miningParams[JLField.FoundSpelling] = ch.Text;
                                break;
                            case nameof(LookupResult.FoundForm):
                                miningParams[JLField.FoundForm] = ch.Text;
                                break;
                            case nameof(LookupResult.EdictID):
                                miningParams[JLField.EdictID] = ch.Text;
                                break;
                            case nameof(LookupResult.Frequency):
                                miningParams[JLField.Frequency] = ch.Text;
                                break;
                            case nameof(LookupResult.DictType):
                                miningParams[JLField.DictType] = ch.Text;
                                break;
                            case nameof(LookupResult.Process):
                                miningParams[JLField.Process] = ch.Text;
                                break;
                        }

                        break;
                    case Grid grid:
                        {
                            foreach (UIElement uiElement in grid.Children)
                            {
                                if (uiElement is TextBlock textBlockCg)
                                {
                                    switch (textBlockCg.Name)
                                    {
                                        case nameof(LookupResult.FoundSpelling):
                                            miningParams[JLField.FoundSpelling] = textBlockCg.Text;
                                            break;
                                    }
                                }
                                else if (uiElement is TextBox textBoxCg)
                                {
                                    switch (textBoxCg.Name)
                                    {
                                        case nameof(LookupResult.Readings):
                                            miningParams[JLField.Readings] = textBoxCg.Text;
                                            break;
                                    }
                                }
                            }

                            break;
                        }
                }
            }

            var innerStackPanel = (StackPanel)top.Parent;
            var bottom = (StackPanel)innerStackPanel.Children[1];
            foreach (object child in bottom.Children)
            {
                if (child is TextBox textBox)
                {
                    miningParams[JLField.Definitions] += textBox.Text.Replace("\n", "<br/>");
                    continue;
                }

                if (child is not TextBlock)
                    continue;

                textBlock = (TextBlock)child;

                switch (textBlock.Name)
                {
                    case nameof(LookupResult.StrokeCount):
                        miningParams[JLField.StrokeCount] += textBlock.Text;
                        break;
                    case nameof(LookupResult.Grade):
                        miningParams[JLField.Grade] += textBlock.Text;
                        break;
                    case nameof(LookupResult.Composition):
                        miningParams[JLField.Composition] += textBlock.Text;
                        break;
                    case nameof(LookupResult.OnReadings):
                    case nameof(LookupResult.KunReadings):
                    case nameof(LookupResult.Nanori):
                        if (!miningParams[JLField.Readings].EndsWith("<br/>"))
                        {
                            miningParams[JLField.Readings] += "<br/>";
                        }

                        miningParams[JLField.Readings] += textBlock.Text + "<br/>";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(null, "Invalid LookupResult type");
                }
            }

            miningParams[JLField.Context] = Utils.FindSentence(_currentText, _currentCharPosition);
            miningParams[JLField.TimeLocal] = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);

            bool miningResult = await Mining.Mine(miningParams).ConfigureAwait(false);

            if (miningResult)
            {
                Storage.SessionStats.CardsMined += 1;
            }
        }

        private void Definitions_MouseMove(TextBox tb)
        {
            if (Storage.JapaneseRegex.IsMatch(tb.Text))
                TextBox_MouseMove(tb);
        }

        private void PopupListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            MouseWheelEventArgs e2 = new(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = e.Source
            };
            PopupListBox.RaiseEvent(e2);
        }

        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //int keyVal = (int)e.Key;
            //int numericKeyValue = -1;
            //if ((keyVal >= (int)Key.D1 && keyVal <= (int)Key.D9))
            //{
            //    numericKeyValue = (int)e.Key - (int)Key.D0 - 1;
            //}
            //else if (keyVal >= (int)Key.NumPad1 && keyVal <= (int)Key.NumPad9)
            //{
            //    numericKeyValue = (int)e.Key - (int)Key.NumPad0 - 1;
            //}

            if (WindowsUtils.KeyGestureComparer(e, ConfigManager.MiningModeKeyGesture))
            {
                MiningMode = true;
                TextBlockMiningModeReminder.Visibility = Visibility.Visible;

                PopUpScrollViewer.ScrollToTop();
                PopUpScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                Activate();
                Focus();

                ResultStackPanels.Clear();
                DisplayResults(true);
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.PlayAudioKeyGesture))
            {
                //int index = numericKeyValue != -1 ? numericKeyValue : _playAudioIndex;
                //if (index > PopupListBox.Items.Count - 1)
                //{
                //    WindowsUtils.Alert(AlertLevel.Error, "Index out of range");
                //    return;
                //}

                //var innerStackPanel = (StackPanel)PopupListBox.Items[index];

                string foundSpelling = null;
                string reading = null;

                var innerStackPanel = (StackPanel)PopupListBox.Items[_playAudioIndex];
                var top = (WrapPanel)innerStackPanel.Children[0];

                foreach (UIElement child in top.Children)
                {
                    if (child is TextBox chi)
                    {
                        switch (chi.Name)
                        {
                            case nameof(LookupResult.Readings):
                                reading = chi.Text.Split(",")[0];
                                break;
                        }
                    }

                    else if (child is TextBlock ch)
                    {
                        switch (ch.Name)
                        {
                            case nameof(LookupResult.FoundSpelling):
                                foundSpelling = ch.Text;
                                break;
                            case nameof(LookupResult.Readings):
                                reading = ch.Text.Split(",")[0];
                                break;
                        }
                    }

                    else if (child is Grid grid)
                    {
                        foreach (UIElement uiElement in grid.Children)
                        {
                            if (uiElement is TextBlock textBlockCg)
                            {
                                switch (textBlockCg.Name)
                                {
                                    case nameof(LookupResult.FoundSpelling):
                                        foundSpelling = textBlockCg.Text;
                                        break;
                                    case nameof(LookupResult.Readings):
                                        reading = textBlockCg.Text.Split(",")[0];
                                        break;
                                }
                            }
                            else if (uiElement is TextBox textBoxCg)
                            {
                                switch (textBoxCg.Name)
                                {
                                    case nameof(LookupResult.Readings):
                                        reading = textBoxCg.Text.Split(",")[0];
                                        break;
                                }
                            }
                        }
                    }
                }

                await Utils.GetAndPlayAudioFromJpod101(foundSpelling, reading, 1).ConfigureAwait(false);
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ClosePopupKeyGesture))
            {
                MiningMode = false;
                TextBlockMiningModeReminder.Visibility = Visibility.Collapsed;

                PopUpScrollViewer.ScrollToTop();
                PopUpScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

                if (ConfigManager.LookupOnSelectOnly && _parentPopupWindow == null)
                {
                    Unselect(MainWindow.Instance.MainTextBox);
                }

                else if (ConfigManager.LookupOnSelectOnly && _lastTextBox != null)
                {
                    Unselect(_lastTextBox);
                }

                if (_parentPopupWindow != null)
                {
                    _parentPopupWindow.Focus();
                }

                else
                {
                    MainWindow.Instance.Focus();
                }

                Hide();
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.KanjiModeKeyGesture))
            {
                ConfigManager.Instance.KanjiMode = !ConfigManager.Instance.KanjiMode;
                LastText = "";
                //todo will only work for the FirstPopupWindow
                MainWindow.Instance.MainTextBox_MouseMove(null, null);
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ShowAddNameWindowKeyGesture))
            {
                if (Storage.Ready)
                    WindowsUtils.ShowAddNameWindow(_lastSelectedText);
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ShowAddWordWindowKeyGesture))
            {
                if (Storage.Ready)
                    WindowsUtils.ShowAddWordWindow(_lastSelectedText);
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ShowManageDictionariesWindowKeyGesture))
            {
                if (Storage.Ready)
                    WindowsUtils.ShowManageDictionariesWindow();
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.SearchWithBrowserKeyGesture))
            {
                WindowsUtils.SearchWithBrowser(_lastSelectedText);
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.InactiveLookupModeKeyGesture))
            {
                ConfigManager.InactiveLookupMode = !ConfigManager.InactiveLookupMode;
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.MotivationKeyGesture))
            {
                WindowsUtils.Motivate($"{Storage.ResourcesPath}/Motivation");
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ShowStatsKeyGesture))
            {
                WindowsUtils.ShowStatsWindow();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (!ConfigManager.LookupOnSelectOnly && !ConfigManager.FixedPopupPositioning &&
                ChildPopupWindow is { MiningMode: false })
            {
                ChildPopupWindow.Hide();
                ChildPopupWindow.LastText = "";
            }

            if (MiningMode || ConfigManager.LookupOnSelectOnly || ConfigManager.FixedPopupPositioning || UnavoidableMouseEnter) return;

            Hide();
            LastText = "";
        }

        private void UiElement_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!ConfigManager.LookupOnSelectOnly
                || Background.Opacity == 0
                || ConfigManager.InactiveLookupMode
                || (ConfigManager.FixedPopupPositioning && _parentPopupWindow != null))
                return;

            //if (ConfigManager.RequireLookupKeyPress
            //    && !Keyboard.Modifiers.HasFlag(ConfigManager.LookupKey))
            //    return;

            ChildPopupWindow ??= new PopupWindow(this);

            ChildPopupWindow.LookupOnSelect((TextBox)sender);

            if (ConfigManager.FixedPopupPositioning)
            {
                ChildPopupWindow.UpdatePosition(WindowsUtils.DpiAwareFixedPopupXPosition, WindowsUtils.DpiAwareFixedPopupYPosition);
            }

            else
            {
                ChildPopupWindow.UpdatePosition(PointToScreen(Mouse.GetPosition(this)));
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!ConfigManager.LookupOnSelectOnly && !ConfigManager.FixedPopupPositioning &&
                ChildPopupWindow is { MiningMode: false, UnavoidableMouseEnter: false })
            {
                ChildPopupWindow.Hide();
                ChildPopupWindow.LastText = "";
            }

            if (MiningMode || ConfigManager.LookupOnSelectOnly || ConfigManager.FixedPopupPositioning || IsMouseOver) return;

            Hide();
            LastText = "";

            if (ConfigManager.HighlightLongestMatch)
            {
                Unselect(MainWindow.Instance.MainTextBox);
            }
        }
    }
}
