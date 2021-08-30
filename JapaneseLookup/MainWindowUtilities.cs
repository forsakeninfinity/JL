﻿using JapaneseLookup.Deconjugation;
using JapaneseLookup.EDICT;
using JapaneseLookup.GUI;
using JapaneseLookup.KANJIDIC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace JapaneseLookup
{
    public static class MainWindowUtilities
    {
        public static readonly List<string> Backlog = new();
        public const string FakeFrequency = "1000000";
        private static DateTime _lastLookupTime;

        public static readonly Regex JapaneseRegex =
            new(
                @"[\u2e80-\u2eff\u3000-\u303f\u3040-\u309f\u30a0-\u30ff\u31c0-\u31ef\u31f0-\u31ff\u3200-\u32ff\u3300-\u33ff\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\ufe30-\ufe4f\uff00-\uffef]|[\ud82c-\ud82c][\udc00-\udcff]|[\ud840-\ud869][\udc00-\udedf]|[\ud869-\ud86d][\udf00-\udf3f]|[\ud86e-\ud873][\udc20-\udeaf]|[\ud873-\ud87a][\udeb0-\udfef]|[\ud87e-\ude1f][\udc00-\ude1f]|[\ud880-\ud884][\udc00-\udf4f]");

        // Consider checking for \t, \r, "　", " ", ., !, ?, –, —, ―, ‒, ~, ‥, ♪, ～, ♡, ♥, ☆, ★
        private static readonly List<string> JapanesePunctuation =
            new() { "。", "！", "？", "…", "―", ".", "＆", "、", "「", "」", "『", "』", "（", "）", "\n" };

        public static void MainWindowInitializer()
        {
            // init AnkiConnect so that it doesn't block later
            // Task.Run(AnkiConnect.GetDeckNames);
        }

        public static int FindWordBoundary(string text, int position)
        {
            int endPosition = -1;

            foreach (string punctuation in JapanesePunctuation)
            {
                int tempIndex = text.IndexOf(punctuation, position, StringComparison.Ordinal);

                if (tempIndex != -1 && (endPosition == -1 || tempIndex < endPosition))
                    endPosition = tempIndex;
            }

            if (endPosition == -1)
                endPosition = text.Length;

            return endPosition;
        }

        public static List<Dictionary<LookupResult, List<string>>> Lookup(string text)
        {
            var preciseTimeNow = new DateTime(Stopwatch.GetTimestamp());
            if ((preciseTimeNow - _lastLookupTime).Milliseconds < ConfigManager.LookupRate)
                return null;

            _lastLookupTime = preciseTimeNow;

            Dictionary<string, (List<KanjiResult> KanjiResult, List<string> processList, string foundForm)>
                kanjiResult = new();

            if (ConfigManager.KanjiMode)
            {
                if (KanjiInfoLoader.KanjiDictionary.TryGetValue(
                    text.UnicodeIterator().DefaultIfEmpty(string.Empty).First(), out KanjiResult kResult))
                {
                    kanjiResult.Add(text.UnicodeIterator().First(),
                        (new List<KanjiResult> { kResult }, new List<string>(), text.UnicodeIterator().First()));

                    return KanjiResultBuilder(kanjiResult);
                }

                else return null;
            }

            Dictionary<string, (List<JMdictResult> jMdictResults, List<string> processList, string foundForm)>
                wordResults = new();
            Dictionary<string, (List<JMnedictResult> jMnedictResults, List<string> processList, string foundForm)>
                nameResults = new();

            int succAttempt = 0;
            for (int i = 0; i < text.Length; i++)
            {
                string textInHiragana = Kana.KatakanaToHiraganaConverter(text[..^i]);

                bool tryLongVowelConversion = true;

                if (JMdictLoader.JMdictDictionary.TryGetValue(textInHiragana, out var tempResult))
                {
                    wordResults.TryAdd(textInHiragana, (tempResult, new List<string>(), text[..^i]));
                    tryLongVowelConversion = false;
                }

                if (ConfigManager.UseJMnedict)
                {
                    if (JMnedictLoader.jMnedictDictionary.TryGetValue(textInHiragana, out var tempNameResult))
                    {
                        nameResults.TryAdd(textInHiragana, (tempNameResult, new List<string>(), text[..^i]));
                    }
                }


                if (succAttempt < 3)
                {
                    var deconjugationResults = Deconjugator.Deconjugate(textInHiragana);
                    foreach (var result in deconjugationResults)
                    {
                        if (wordResults.ContainsKey(result.Text))
                            continue;

                        if (JMdictLoader.JMdictDictionary.TryGetValue(result.Text, out var temp))
                        {
                            List<JMdictResult> resultsList = new();

                            foreach (var rslt in temp)
                            {
                                if (rslt.WordClasses.SelectMany(pos => pos).Intersect(result.Tags).Any())
                                {
                                    resultsList.Add(rslt);
                                }
                            }

                            if (resultsList.Any())
                            {
                                wordResults.Add(result.Text,
                                    (resultsList, result.Process, text[..result.OriginalText.Length]));
                                ++succAttempt;
                                tryLongVowelConversion = false;
                            }
                        }
                    }
                }

                if (tryLongVowelConversion && textInHiragana.Contains("ー") && textInHiragana[0] != 'ー')
                {
                    string textWithoutLongVowelMark = Kana.LongVowelMarkConverter(textInHiragana);
                    if (JMdictLoader.JMdictDictionary.TryGetValue(textWithoutLongVowelMark, out var tmpResult))
                    {
                        wordResults.Add(textInHiragana, (tmpResult, new List<string>(), text[..^i]));
                    }
                }
            }

            if (!wordResults.Any() && !nameResults.Any())
            {
                if (KanjiInfoLoader.KanjiDictionary.TryGetValue(
                    text.UnicodeIterator().DefaultIfEmpty(string.Empty).First(), out KanjiResult kResult))
                {
                    kanjiResult.Add(text.UnicodeIterator().First(),
                        (new List<KanjiResult> { kResult }, new List<string>(), text.UnicodeIterator().First()));
                }
            }

            // don't display an empty popup if there are no results
            if (!wordResults.Any() && !nameResults.Any() && !kanjiResult.Any())
            {
                return null;
            }

            List<Dictionary<LookupResult, List<string>>> results = new();

            if (wordResults.Any())
                results.AddRange(WordResultBuilder(wordResults));

            if (nameResults.Any())
                results.AddRange(NameResultBuilder(nameResults));

            if (kanjiResult.Any())
                results.AddRange(KanjiResultBuilder(kanjiResult));

            results = results
                .OrderByDescending(dict => dict[LookupResult.FoundForm][0].Length)
                .ThenBy(dict => Convert.ToInt32(dict[LookupResult.Frequency][0])).ToList();
            return results;
        }

        private static List<Dictionary<LookupResult, List<string>>> KanjiResultBuilder
            (Dictionary<string, (List<KanjiResult> kanjiResult, List<string> processList, string foundForm)> kanjiResults)
        {
            var results = new List<Dictionary<LookupResult, List<string>>>();
            var result = new Dictionary<LookupResult, List<string>>();

            var kanjiResult = kanjiResults.First().Value.kanjiResult.First();

            result.Add(LookupResult.FoundSpelling, new List<string> { kanjiResults.First().Key });
            result.Add(LookupResult.Definitions, kanjiResult.Meanings);
            result.Add(LookupResult.OnReadings, kanjiResult.OnReadings);
            result.Add(LookupResult.KunReadings, kanjiResult.KunReadings);
            result.Add(LookupResult.Nanori, kanjiResult.Nanori);
            result.Add(LookupResult.StrokeCount, new List<string> { kanjiResult.StrokeCount.ToString() });
            result.Add(LookupResult.Grade, new List<string> { kanjiResult.Grade.ToString() });
            result.Add(LookupResult.Composition, new List<string> { kanjiResult.Composition });
            result.Add(LookupResult.Frequency, new List<string> { kanjiResult.Frequency.ToString() });

            var foundForm = new List<string> { kanjiResults.First().Value.foundForm };
            result.Add(LookupResult.FoundForm, foundForm);

            results.Add(result);
            return results;
        }

        private static List<Dictionary<LookupResult, List<string>>> NameResultBuilder
            (Dictionary<string, (List<JMnedictResult> jMdictResults, List<string> processList, string foundForm)> nameResults)
        {
            var results = new List<Dictionary<LookupResult, List<string>>>();

            foreach (var nameResult in nameResults)
            {
                foreach (var jMDictResult in nameResult.Value.jMdictResults)
                {
                    var result = new Dictionary<LookupResult, List<string>>();

                    var foundSpelling = new List<string> { jMDictResult.PrimarySpelling };

                    var readings = jMDictResult.Readings != null ? jMDictResult.Readings.ToList() : new List<string>();

                    var foundForm = new List<string> { nameResult.Value.foundForm };

                    var edictID = new List<string> { jMDictResult.Id };

                    var alternativeSpellings = jMDictResult.AlternativeSpellings ?? new List<string>();

                    var definitions = new List<string> { BuildNameDefinition(jMDictResult) };

                    result.Add(LookupResult.EdictID, edictID);
                    result.Add(LookupResult.FoundSpelling, foundSpelling);
                    result.Add(LookupResult.AlternativeSpellings, alternativeSpellings);
                    result.Add(LookupResult.Readings, readings);
                    result.Add(LookupResult.Definitions, definitions);

                    result.Add(LookupResult.FoundForm, foundForm);
                    result.Add(LookupResult.Frequency, new List<string> { FakeFrequency });

                    results.Add(result);
                }
            }

            return results;
        }

        private static List<Dictionary<LookupResult, List<string>>> WordResultBuilder
            (Dictionary<string, (List<JMdictResult> jMdictResults, List<string> processList, string foundForm)> wordResults)
        {
            var results = new List<Dictionary<LookupResult, List<string>>>();

            foreach (var wordResult in wordResults)
            {
                foreach (var jMDictResult in wordResult.Value.jMdictResults)
                {
                    var result = new Dictionary<LookupResult, List<string>>();

                    var foundSpelling = new List<string> { jMDictResult.PrimarySpelling };

                    var kanaSpellings = jMDictResult.KanaSpellings ?? new List<string>();

                    var readings = jMDictResult.Readings.ToList();
                    var foundForm = new List<string> { wordResult.Value.foundForm };
                    var edictID = new List<string> { jMDictResult.Id };

                    List<string> alternativeSpellings;
                    if (jMDictResult.AlternativeSpellings != null)
                        alternativeSpellings = jMDictResult.AlternativeSpellings.ToList();
                    else
                        alternativeSpellings = new List<string>();
                    var process = wordResult.Value.processList;

                    List<string> frequency;
                    if (jMDictResult.FrequencyDict != null)
                    {
                        jMDictResult.FrequencyDict.TryGetValue(ConfigManager.FrequencyList, out var freq);
                        frequency = new List<string> { freq.ToString() };
                    }

                    else frequency = new List<string> { FakeFrequency };

                    var definitions = new List<string> { BuildWordDefinition(jMDictResult) };

                    var pOrthographyInfoList = jMDictResult.POrthographyInfoList ?? new List<string>();

                    var rList = jMDictResult.ROrthographyInfoList ?? new List<List<string>>();
                    var aList = jMDictResult.AOrthographyInfoList ?? new List<List<string>>();
                    var rOrthographyInfoList = new List<string>();
                    var aOrthographyInfoList = new List<string>();

                    foreach (var list in rList)
                    {
                        var final = "";
                        foreach (var str in list)
                        {
                            final += str + ", ";
                        }

                        final = final.TrimEnd(", ".ToCharArray());

                        rOrthographyInfoList.Add(final);
                    }

                    foreach (var list in aList)
                    {
                        var final = "";
                        foreach (var str in list)
                        {
                            final += str + ", ";
                        }

                        final = final.TrimEnd(", ".ToCharArray());

                        aOrthographyInfoList.Add(final);
                    }

                    result.Add(LookupResult.FoundSpelling, foundSpelling);
                    result.Add(LookupResult.KanaSpellings, kanaSpellings);
                    result.Add(LookupResult.Readings, readings);
                    result.Add(LookupResult.Definitions, definitions);
                    result.Add(LookupResult.FoundForm, foundForm);
                    result.Add(LookupResult.EdictID, edictID);
                    result.Add(LookupResult.AlternativeSpellings, alternativeSpellings);
                    result.Add(LookupResult.Process, process);
                    result.Add(LookupResult.Frequency, frequency);
                    result.Add(LookupResult.POrthographyInfoList, pOrthographyInfoList);
                    result.Add(LookupResult.ROrthographyInfoList, rOrthographyInfoList);
                    result.Add(LookupResult.AOrthographyInfoList, aOrthographyInfoList);

                    results.Add(result);
                }
            }

            return results;
        }

        private static string BuildNameDefinition(JMnedictResult jMDictResult)
        {
            int count = 1;
            string defResult = "";

            if (jMDictResult.NameTypes != null &&
                (jMDictResult.NameTypes.Count > 1 || !jMDictResult.NameTypes.Contains("unclass")))
            {
                foreach (var nameType in jMDictResult.NameTypes)
                {
                    defResult += "(";
                    defResult += nameType;
                    defResult += ") ";
                }
            }

            for (int i = 0; i < jMDictResult.Definitions.Count; i++)
            {
                if (jMDictResult.Definitions.Any())
                {
                    if (jMDictResult.Definitions.Count > 0)
                        defResult += "(" + count + ") ";

                    defResult += string.Join("; ", jMDictResult.Definitions[i]) + " ";
                    ++count;
                }
            }

            return defResult;
        }

        private static string BuildWordDefinition(JMdictResult jMDictResult)
        {
            int count = 1;
            string defResult = "";
            for (int i = 0; i < jMDictResult.Definitions.Count; i++)
            {
                if (jMDictResult.WordClasses.Any() && jMDictResult.WordClasses[i].Any())
                {
                    defResult += "(";
                    defResult += string.Join(", ", jMDictResult.WordClasses[i]);
                    defResult += ") ";
                }

                if (jMDictResult.Definitions.Any())
                {
                    defResult += "(" + count + ") ";

                    if (jMDictResult.SpellingInfo.Any() && jMDictResult.SpellingInfo[i] != null)
                    {
                        defResult += "(";
                        defResult += jMDictResult.SpellingInfo[i];
                        defResult += ") ";
                    }

                    if (jMDictResult.MiscList.Any() && jMDictResult.MiscList[i].Any())
                    {
                        defResult += "(";
                        defResult += string.Join(", ", jMDictResult.MiscList[i]);
                        defResult += ") ";
                    }

                    defResult += string.Join("; ", jMDictResult.Definitions[i]) + " ";

                    if (jMDictResult.RRestrictions != null && jMDictResult.RRestrictions[i].Any()
                        || jMDictResult.KRestrictions != null && jMDictResult.KRestrictions[i].Any())
                    {
                        defResult += "(only applies to ";

                        if (jMDictResult.KRestrictions != null && jMDictResult.KRestrictions[i].Any())
                            defResult += string.Join("; ", jMDictResult.KRestrictions[i]);

                        if (jMDictResult.RRestrictions != null && jMDictResult.RRestrictions[i].Any())
                            defResult += string.Join("; ", jMDictResult.RRestrictions[i]);

                        defResult += ") ";
                    }

                    ++count;
                }
            }

            return defResult;
        }

        public static IEnumerable<string> UnicodeIterator(this string s)
        {
            for (int i = 0; i < s.Length; ++i)
            {
                yield return char.ConvertFromUtf32(char.ConvertToUtf32(s, i));
                if (char.IsHighSurrogate(s, i))
                    i++;
            }
        }

        public static void ShowAddNameWindow()
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().First();
            var addNameWindowInstance = AddNameWindow.Instance;
            addNameWindowInstance.SpellingTextBox.Text = mainWindow.MainTextBox.SelectedText;
            addNameWindowInstance.ShowDialog();
        }

        public static void ShowAddWordWindow()
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().First();
            var addWordWindowInstance = AddWordWindow.Instance;
            addWordWindowInstance.SpellingsTextBox.Text = mainWindow.MainTextBox.SelectedText;
            addWordWindowInstance.ShowDialog();
        }

        public static void ShowPreferencesWindow()
        {
            ConfigManager.LoadPreferences(PreferencesWindow.Instance);
            PreferencesWindow.Instance.ShowDialog();
        }

        public static void SearchWithBrowser()
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().First();
            if (mainWindow.MainTextBox.SelectedText.Length > 0)
                Process.Start(new ProcessStartInfo("cmd",
                        $"/c start https://www.google.com/search?q={mainWindow.MainTextBox.SelectedText}^&hl=ja")
                    { CreateNoWindow = true });
        }
    }
}