using System.Net.WebSockets;
using System.Text;
using JL.Core.Statistics;
using JL.Core.Utilities;

namespace JL.Core.Network;

public static class WebSocketUtils
{
    private static Task? s_webSocketTask = null;
    private static CancellationTokenSource? s_webSocketCancellationTokenSource = null;

    private static readonly Encoding s_utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static bool Connected => !s_webSocketTask?.IsCompleted ?? false;

    public static void HandleWebSocket()
    {
        if (!CoreConfig.CaptureTextFromWebSocket)
        {
            s_webSocketTask = null;
        }
        else if (s_webSocketTask is null)
        {
            s_webSocketCancellationTokenSource?.Dispose();
            s_webSocketCancellationTokenSource = new CancellationTokenSource();
            ListenWebSocket(s_webSocketCancellationTokenSource.Token);
        }
        else
        {
            s_webSocketCancellationTokenSource!.Cancel();
            s_webSocketCancellationTokenSource.Dispose();
            s_webSocketCancellationTokenSource = new CancellationTokenSource();
            ListenWebSocket(s_webSocketCancellationTokenSource.Token);
        }
    }

    private static void ListenWebSocket(CancellationToken cancellationToken)
    {
        s_webSocketTask = Task.Run(async () =>
        {
            try
            {
                using ClientWebSocket webSocketClient = new();
                await webSocketClient.ConnectAsync(CoreConfig.WebSocketUri, CancellationToken.None).ConfigureAwait(false);
                byte[] buffer = new byte[1024];

                while (CoreConfig.CaptureTextFromWebSocket && !cancellationToken.IsCancellationRequested && webSocketClient.State is WebSocketState.Open)
                {
                    try
                    {
                        WebSocketReceiveResult result = await webSocketClient.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);

                        if (!CoreConfig.CaptureTextFromWebSocket || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (result.MessageType is WebSocketMessageType.Text)
                        {
                            using MemoryStream memoryStream = new();
                            await memoryStream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);

                            while (!result.EndOfMessage)
                            {
                                result = await webSocketClient.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                                await memoryStream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
                            }

                            _ = memoryStream.Seek(0, SeekOrigin.Begin);

                            string text = s_utf8NoBom.GetString(memoryStream.ToArray());
                            _ = Task.Run(async () => await Utils.Frontend.CopyFromWebSocket(text).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (WebSocketException webSocketException)
                    {
                        if (!CoreConfig.CaptureTextFromClipboard)
                        {
                            StatsUtils.StatsStopWatch.Stop();
                            StatsUtils.StopStatsTimer();
                        }

                        Utils.Logger.Warning(webSocketException, "WebSocket server is closed unexpectedly");
                        Utils.Frontend.Alert(AlertLevel.Error, "WebSocket server is closed");

                        break;
                    }
                }
            }

            catch (WebSocketException webSocketException)
            {
                if (!CoreConfig.CaptureTextFromClipboard)
                {
                    StatsUtils.StatsStopWatch.Stop();
                    StatsUtils.StopStatsTimer();
                }

                Utils.Logger.Warning(webSocketException, "Couldn't connect to the WebSocket server, probably because it is not running");
                Utils.Frontend.Alert(AlertLevel.Error, "Couldn't connect to the WebSocket server, probably because it is not running");
            }
        }, cancellationToken);
    }
}
