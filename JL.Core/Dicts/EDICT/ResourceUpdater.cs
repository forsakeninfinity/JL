using System.IO.Compression;
using System.Net;
using System.Runtime;
using JL.Core.Dicts.EDICT.JMdict;
using JL.Core.Dicts.EDICT.JMnedict;
using JL.Core.Dicts.EDICT.KANJIDIC;
using JL.Core.Utilities;
using JL.Core.WordClass;

namespace JL.Core.Dicts.EDICT;

public static class ResourceUpdater
{
    internal static async Task<bool> UpdateResource(string resourcePath, Uri resourceDownloadUri, string resourceName,
        bool isUpdate, bool noPrompt)
    {
        try
        {
            if (!isUpdate || Storage.Frontend.ShowYesNoDialog($"Do you want to download the latest version of {resourceName}?",
                "Update dictionary?"))
            {
                HttpRequestMessage request = new(HttpMethod.Get, resourceDownloadUri);

                if (File.Exists(resourcePath))
                {
                    request.Headers.IfModifiedSince = File.GetLastWriteTime(resourcePath);
                }

                if (!noPrompt)
                {
                    Storage.Frontend.ShowOkDialog(
                        $"This may take a while. Please don't shut down the program until {resourceName} is downloaded.",
                        "Info");
                }

                HttpResponseMessage response = await Storage.Client.SendAsync(request).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    await using (responseStream.ConfigureAwait(false))
                    {
                        await DecompressGzipStream(responseStream, resourcePath).ConfigureAwait(false);
                    }

                    if (!noPrompt)
                    {
                        Storage.Frontend.ShowOkDialog($"{resourceName} has been downloaded successfully.",
                            "Info");
                    }

                    return true;
                }

                if (response.StatusCode is HttpStatusCode.NotModified && !noPrompt)
                {
                    Storage.Frontend.ShowOkDialog($"{resourceName} is up to date.",
                        "Info");
                }

                else
                {
                    Utils.Logger.Error("Unexpected error while downloading {ResourceName. Status code: {StatusCode}}",
                        resourceName, response.StatusCode);

                    if (!noPrompt)
                    {
                        Storage.Frontend.ShowOkDialog($"Unexpected error while downloading {resourceName}.", "Info");
                    }
                }
            }
        }

        catch (Exception ex)
        {
            Storage.Frontend.ShowOkDialog($"Unexpected error while downloading {resourceName}.", "Info");
            Utils.Logger.Error(ex, "Unexpected error while downloading {ResourceName}", resourceName);
        }

        return false;
    }

    private static async Task DecompressGzipStream(Stream stream, string filePath)
    {
        FileStream decompressedFileStream = File.Create(filePath);
        await using (decompressedFileStream.ConfigureAwait(false))
        {
            GZipStream decompressionStream = new(stream, CompressionMode.Decompress);
            await using (decompressionStream.ConfigureAwait(false))
            {
                await decompressionStream.CopyToAsync(decompressedFileStream).ConfigureAwait(false);
            }
        }
    }

    public static async Task UpdateJmdict()
    {
        Storage.UpdatingJMdict = true;

        Dict dict = Storage.Dicts.Values.First(static dict => dict.Type is DictType.JMdict);
        bool isDownloaded = await UpdateResource(dict.Path,
                Storage.JmdictUrl,
                DictType.JMdict.ToString(), true, false)
            .ConfigureAwait(false);

        if (isDownloaded)
        {
            dict.Contents.Clear();

            await Task.Run(async () => await JmdictLoader
                .Load(dict).ConfigureAwait(false)).ConfigureAwait(false);

            await JmdictWordClassUtils.SerializeJmdictWordClass().ConfigureAwait(false);

            Storage.WordClassDictionary.Clear();

            await JmdictWordClassUtils.Load().ConfigureAwait(false);

            if (!dict.Active)
            {
                dict.Contents.Clear();
            }

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
        }

        Storage.UpdatingJMdict = false;
    }

    public static async Task UpdateJmnedict()
    {
        Storage.UpdatingJMnedict = true;

        Dict dict = Storage.Dicts.Values.First(static dict => dict.Type is DictType.JMnedict);
        bool isDownloaded = await UpdateResource(dict.Path,
                Storage.JmnedictUrl,
                DictType.JMnedict.ToString(), true, false)
            .ConfigureAwait(false);

        if (isDownloaded)
        {
            dict.Contents.Clear();

            await Task.Run(async () => await JmnedictLoader
                .Load(dict).ConfigureAwait(false)).ConfigureAwait(false);

            if (!dict.Active)
            {
                dict.Contents.Clear();
            }

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
        }

        Storage.UpdatingJMnedict = false;
    }

    public static async Task UpdateKanjidic()
    {
        Storage.UpdatingKanjidic = true;

        Dict dict = Storage.Dicts.Values.First(static dict => dict.Type is DictType.Kanjidic);
        bool isDownloaded = await UpdateResource(dict.Path,
                Storage.KanjidicUrl,
                DictType.Kanjidic.ToString(), true, false)
            .ConfigureAwait(false);

        if (isDownloaded)
        {
            dict.Contents.Clear();

            await Task.Run(async () => await KanjidicLoader
                .Load(dict).ConfigureAwait(false)).ConfigureAwait(false);

            if (!dict.Active)
            {
                dict.Contents.Clear();
            }

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
        }

        Storage.UpdatingKanjidic = false;
    }
}
