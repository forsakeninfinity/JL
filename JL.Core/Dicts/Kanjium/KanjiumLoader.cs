﻿using System.Text.Json;

namespace JL.Core.Dicts.Kanjium;

public static class KanjiumLoader
{
    public static async Task Load(Dict dict)
    {
        if (!Directory.Exists(dict.Path) && !File.Exists(dict.Path))
            return;

        Dictionary<string, List<IResult>> kanjiumDict = dict.Contents;

        string[] jsonFiles = Directory.GetFiles(dict.Path, "term_meta_bank_*.json");

        foreach (string jsonFile in jsonFiles)
        {
            List<List<JsonElement>>? jsonObjects;

            FileStream openStream = File.OpenRead(jsonFile);
            await using (openStream.ConfigureAwait(false))
            {
                jsonObjects = await JsonSerializer.DeserializeAsync<List<List<JsonElement>>>(openStream)
                    .ConfigureAwait(false);
            }

            if (jsonObjects == null)
                continue;

            foreach (List<JsonElement> jsonObject in jsonObjects)
            {
                KanjiumResult newEntry = new(jsonObject);

                string spellingInHiragana = Kana.KatakanaToHiraganaConverter(newEntry.Spelling);

                if (kanjiumDict.TryGetValue(spellingInHiragana, out List<IResult>? result))
                {
                    result.Add(newEntry);
                }

                else
                {
                    kanjiumDict[spellingInHiragana] = new List<IResult> { newEntry };
                }

                if (!string.IsNullOrEmpty(newEntry.Reading))
                {
                    string readingInHiragana = Kana.KatakanaToHiraganaConverter(newEntry.Reading);

                    if (kanjiumDict.TryGetValue(readingInHiragana, out List<IResult>? readingResult))
                    {
                        readingResult.Add(newEntry);
                    }

                    else
                    {
                        kanjiumDict[readingInHiragana] = new List<IResult> { newEntry };
                    }
                }
            }
        }

        kanjiumDict.TrimExcess();
    }
}
