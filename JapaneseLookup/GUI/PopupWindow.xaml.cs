﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JapaneseLookup.Anki;

namespace JapaneseLookup.GUI
{
    /// <summary>
    /// Interaction logic for PopupWindow.xaml
    /// </summary>
    public partial class PopupWindow : Window
    {
        private static PopupWindow _instance;
        private static int _playAudioIndex;

        public static PopupWindow Instance
        {
            get { return _instance ??= new PopupWindow(); }
        }

        public PopupWindow()
        {
            InitializeComponent();
        }

        public static void UpdatePosition(Point position)
        {
            Instance.Left = position.X + 10;
            Instance.Top = position.Y + 20;
        }

        internal static void DisplayResults(string sentence,
            List<Dictionary<string, List<string>>> results)
        {
            for (var index = 0; index < results.Count; index++)
            {
                var result = results[index];

                var innerStackPanel = new StackPanel
                {
                    Margin = new Thickness(4, 2, 4, 2),
                };
                var top = new WrapPanel();
                var bottom = new StackPanel();


                var textBlockFoundSpelling = new TextBlock
                {
                    Name = "foundSpelling",
                    Text = result["foundSpelling"][0],
                    Tag = index, // for audio
                    Foreground = ConfigManager.FoundSpellingColor,
                    FontSize = ConfigManager.FoundSpellingFontSize,
                };
                textBlockFoundSpelling.MouseEnter += FoundSpelling_MouseEnter; // for audio
                textBlockFoundSpelling.MouseLeave += FoundSpelling_MouseLeave; // for audio
                textBlockFoundSpelling.PreviewMouseUp += FoundSpelling_PreviewMouseUp; // for mining

                // var textBlockKanaSpellings = new TextBlock
                // {
                //     Name = "kanaSpellings",
                //     Text = string.Join(" ", result["kanaSpellings"]),
                //     TextWrapping = TextWrapping.Wrap,
                //     Foreground = Brushes.White
                // };

                var textBlockReadings = new TextBlock
                {
                    Name = "readings",
                    Text = string.Join(", ", result["readings"]),
                    Foreground = ConfigManager.ReadingsColor,
                    FontSize = ConfigManager.ReadingsFontSize,
                    Margin = new Thickness(5, 0, 0, 0),
                };

                var textBlockAlternativeSpellings = new TextBlock
                {
                    Name = "alternativeSpellings",
                    Text = "(" + string.Join(", ", result["alternativeSpellings"]) + ")",
                    Foreground = ConfigManager.AlternativeSpellingsColor,
                    FontSize = ConfigManager.AlternativeSpellingsFontSize,
                    Margin = new Thickness(5, 0, 0, 0),
                };

                var textBlockProcess = new TextBlock
                {
                    Name = "process",
                    Text = string.Join(", ", result["process"]),
                    Foreground = ConfigManager.ProcessColor,
                    FontSize = ConfigManager.ProcessFontSize,
                    Margin = new Thickness(5, 0, 0, 0),
                };

                var textBlockFrequency = new TextBlock
                {
                    Name = "frequency",
                    Text = "#" + string.Join(", ", result["frequency"]),
                    Foreground = ConfigManager.FrequencyColor,
                    FontSize = ConfigManager.FrequencyFontSize,
                    Margin = new Thickness(5, 0, 0, 0),
                };

                var textBlockContext = new TextBlock
                {
                    Name = "context",
                    Text = sentence,
                    Visibility = Visibility.Collapsed
                };

                var textBlockFoundForm = new TextBlock
                {
                    Name = "foundForm",
                    Text = string.Join("", result["foundForm"]),
                    Visibility = Visibility.Collapsed
                };

                var textBlockJmdictID = new TextBlock
                {
                    Name = "jmdictID",
                    Text = string.Join(", ", result["jmdictID"]),
                    Visibility = Visibility.Collapsed
                };

                var textBlockDefinitions = new TextBlock
                {
                    Name = "definitions",
                    Text = string.Join("", result["definitions"]),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ConfigManager.DefinitionsColor,
                    FontSize = ConfigManager.DefinitionsFontSize,
                    Margin = new Thickness(2, 2, 2, 2),
                };


                TextBlock[] babies =
                {
                    textBlockFoundSpelling, textBlockReadings, textBlockAlternativeSpellings, textBlockProcess,
                    textBlockFrequency, textBlockContext, textBlockFoundForm, textBlockJmdictID
                };
                foreach (var baby in babies)
                {
                    // general check, alternativespellings check, frequency check
                    if (baby.Text != "" && baby.Text != "()" && baby.Text != ("#" + MainWindowUtilities.FakeFrequency))
                    {
                        top.Children.Add(baby);
                    }
                }

                bottom.Children.Add(textBlockDefinitions);

                innerStackPanel.Children.Add(top);
                innerStackPanel.Children.Add(bottom);
                innerStackPanel.Children.Add(new Separator());

                Instance.StackPanel.Children.Add(innerStackPanel);
            }
        }

        private static void FoundSpelling_MouseEnter(object sender, MouseEventArgs e)
        {
            var textBlock = (TextBlock) sender;
            _playAudioIndex = (int) textBlock.Tag;
        }

        private static void FoundSpelling_MouseLeave(object sender, MouseEventArgs e)
        {
            _playAudioIndex = 0;
        }

        private static void FoundSpelling_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.MiningMode = false;
            Instance.Hide();

            string foundSpelling = null;
            string readings = null;
            string definitions = "";
            string context = null;
            string foundForm = null;
            string jmdictID = null;
            var timeLocal = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
            string alternativeSpellings = null;
            string frequency = null;

            var textBlock = (TextBlock) sender;
            var top = (WrapPanel) textBlock.Parent;

            foreach (TextBlock child in top.Children)
            {
                switch (child.Name)
                {
                    case "foundSpelling":
                        foundSpelling = child.Text;
                        break;
                    case "readings":
                        readings = child.Text;
                        break;
                    case "context":
                        context = child.Text;
                        break;
                    case "foundForm":
                        foundForm = child.Text;
                        break;
                    case "jmdictID":
                        jmdictID = child.Text;
                        break;
                    case "alternativeSpellings":
                        alternativeSpellings = child.Text;
                        break;
                    case "frequency":
                        frequency = child.Text;
                        break;
                }
            }

            var innerStackPanel = (StackPanel) top.Parent;
            var bottom = (StackPanel) innerStackPanel.Children[1];
            // For multiple definitions (multiple dictionaries enabled at the same time)
            foreach (TextBlock child in bottom.Children)
            {
                definitions += child.Text;
            }

            Mining.Mine(
                foundSpelling,
                readings,
                definitions,
                context,
                foundForm,
                jmdictID,
                timeLocal,
                alternativeSpellings,
                frequency
            );
        }

        static void PlayAudio(string foundSpelling, string reading)
        {
            Debug.WriteLine("Attempting to play audio: " + foundSpelling + " " + reading);

            if (reading == "") reading = foundSpelling;

            Uri uri = new(
                "http://assets.languagepod101.com/dictionary/japanese/audiomp3.php?kanji=" +
                foundSpelling +
                "&kana=" +
                reading
            );

            // var sound = AnkiConnect.GetAudio("猫", "ねこ").Result;
            var test = new MediaElement
                { Source = uri, Volume = 1, Visibility = Visibility.Collapsed };
            Instance.StackPanel.Children.Add(test);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.M:
                {
                    MainWindow.MiningMode = true;
                    PopUpScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    // TODO: Tell the user that they are in mining mode
                    Instance.Activate();
                    Instance.Focus();

                    break;
                }

                case Key.P:
                {
                    string foundSpelling = null;
                    string reading = null;

                    var innerStackPanel = (StackPanel) StackPanel.Children[_playAudioIndex];
                    var top = (WrapPanel) innerStackPanel.Children[0];

                    foreach (TextBlock child in top.Children)
                    {
                        switch (child.Name)
                        {
                            case "foundSpelling":
                                foundSpelling = child.Text;
                                break;
                            case "readings":
                                reading = child.Text.Split(",")[0];
                                break;
                        }
                    }

                    PlayAudio(foundSpelling, reading);

                    break;
                }

                case Key.Escape:
                {
                    if (MainWindow.MiningMode)
                    {
                        MainWindow.MiningMode = false;
                        PopUpScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    }

                    break;
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}