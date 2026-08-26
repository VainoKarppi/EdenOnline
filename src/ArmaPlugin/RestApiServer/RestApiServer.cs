using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static EdenOnline.ArmaLog;

namespace EdenOnline;


// TODO Add ability to get data from arma 3
// TODO Add ability to set Invoke now, or just recompile arma 3 function
public static class ArmaApiServer {
    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;

    public static bool IsRunning => _listener?.IsListening == true;

    public static Task StartAsync(int port = 8765) {
        if (IsRunning) return Task.CompletedTask;
        

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        _cts = new CancellationTokenSource();
        _listener.Start();

        Log($"Started ArmaApiServer on port: {port}");

        _ = Task.Run(() => ListenLoopAsync(_cts.Token));

        return Task.CompletedTask;
    }

    public static void Stop() {
        if (_listener == null) return;
        
        Log("Stopping ArmaApiServer");

        _cts?.Cancel();

        _listener.Stop();
        _listener.Close();

        _listener = null;

        _cts?.Dispose();
        _cts = null;
    }

    private static async Task ListenLoopAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            var listener = _listener;

            if (listener == null) break;

            HttpListenerContext context;

            try {
                context = await listener.GetContextAsync();
            } catch (HttpListenerException) {
                break;
            } catch (ObjectDisposedException) {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context));
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context) {
        try {
            if (context.Request.HttpMethod == "POST" && context.Request.Url?.AbsolutePath == "/api/command") {

                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);

                var json = await reader.ReadToEndAsync();

                ArmaCommand? command;

                try {
                    using var document = JsonDocument.Parse(json);
                    var root = document.RootElement;

                    if (!root.TryGetProperty("type", out var typeElement) || !root.TryGetProperty("code", out var codeElement)) {

                        await WriteResponseAsync(context, 400, """{"error":"Missing required properties"}""");

                        return;
                    }

                    var type = typeElement.GetString()?.ToLowerInvariant();
                    var code = codeElement.GetString();

                    if (type is not ("code" or "recompile")) {
                        await WriteResponseAsync(context, 400, """{"error":"Type must be 'code' or 'recompile'"}""");

                        return;
                    }

                    if (code == null) {
                        await WriteResponseAsync(context, 400, """{"error":"Code is required"}""");

                        return;
                    }

                    string method = "";

                    if (type == "recompile") {
                        if (!root.TryGetProperty("method", out var methodElement)) {
                            await WriteResponseAsync(context, 400, """{"error":"Method is required for function recompile commands"}""");

                            return;
                        }

                        method = methodElement.GetString() ?? "";

                        if (string.IsNullOrWhiteSpace(method)) {
                            await WriteResponseAsync(context, 400, """{"error":"Method is required for function recompile commands"}""");

                            return;
                        }
                    } else if (root.TryGetProperty("method", out var methodElement)) {
                        method = methodElement.GetString() ?? "";
                    }

                    command = new ArmaCommand(type, method, code);
                } catch (JsonException) {
                    await WriteResponseAsync(context, 400, """{"error":"Invalid JSON"}""");

                    return;
                }

                Log($"Type: {command.Type}");
                Log($"Method: {command.Method}");

                Extension.SendToArma("ApiServerCommand", [command.Type, command.Method]);

                await WriteResponseAsync(context, 200, """{"success":true}""");

                return;
            }

            await WriteResponseAsync(context, 404, """{"error":"Not found"}""");
        } catch (Exception ex) {
            Log(ex);

            try {
                await WriteResponseAsync(context, 500, """{"error":"Internal server error"}""");
            }
            catch {
                // Connection may already be closed.
            }
        } finally {
            context.Response.Close();
        }
    }

    private static async Task WriteResponseAsync(HttpListenerContext context, int statusCode, string content) {

        var bytes = Encoding.UTF8.GetBytes(content);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = bytes.Length;

        await context.Response.OutputStream.WriteAsync(bytes);
    }
}

public sealed record ArmaCommand(string Type, string Method, string Code);