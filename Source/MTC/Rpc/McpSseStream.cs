using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MTC;

/// <summary>
/// Server-initiated SSE stream for one MCP client connection on <c>GET /mcp</c>. Carries
/// JSON-RPC notifications from the app to the agent, so a bidirectional game like Trade Wars
/// can push prompts, menu output, and state changes without the agent polling. Frames follow
/// the MCP Streamable HTTP transport: each notification is an <c>event: message</c> frame with
/// a JSON-RPC 2.0 body. Keepalive comments hold idle connections open.
/// </summary>
internal sealed class McpSseStream : IDisposable
{
    public const string GameEventNotification = "notifications/mtc/gameEvent";
    public const string ScriptStateNotification = "notifications/mtc/scriptState";
    private const int QueueLimit = 256;
    private static readonly TimeSpan KeepaliveInterval = TimeSpan.FromSeconds(15);

    private readonly HttpListenerResponse _response;
    private readonly Channel<string> _frames = Channel.CreateBounded<string>(
        new BoundedChannelOptions(QueueLimit)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writeLoop;
    private bool _disposed;

    /// <summary>Create an SSE stream and start its write loop on an open HTTP response.</summary>
    /// <param name="response">The HTTP response to stream notifications on; kept open by this stream</param>
    public McpSseStream(HttpListenerResponse response)
    {
        _response = response;
        WriteSseHeaders();
        _frames.Writer.TryWrite(": mtc mcp stream open\n\n");
        _writeLoop = Task.Run(() => WriteLoopAsync(_cts.Token));
    }

    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Pushes one observed game event to the connected agent as a JSON-RPC notification.</summary>
    /// <param name="evt">The game event observed by the MTC game agent</param>
    public void PublishGameEvent(GameAgentEvent evt)
        => PublishNotification(GameEventNotification, evt);

    /// <summary>Pushes a script runtime state change (started or stopped) to the connected agent.</summary>
    /// <param name="change">What happened to the script, e.g. "started" or "stopped"</param>
    /// <param name="script">The script name or id involved</param>
    /// <param name="result">The action result from the runtime</param>
    public void PublishScriptState(string change, string script, MtcRpcActionResult result)
        => PublishNotification(ScriptStateNotification, new
        {
            change,
            script,
            success = result.Success,
            message = result.Message,
        });

    /// <summary>Serializes a notification into an SSE frame and queues it for delivery.</summary>
    /// <param name="method">The JSON-RPC notification method name</param>
    /// <param name="parameters">The notification params payload</param>
    private void PublishNotification(string method, object parameters)
    {
        if (_disposed)
            return;

        string json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method,
            @params = parameters,
        }, McpJson.Options);
        _frames.Writer.TryWrite($"event: message\ndata: {json}\n\n");
    }

    private void WriteSseHeaders()
    {
        _response.StatusCode = (int)HttpStatusCode.OK;
        _response.ContentType = "text/event-stream";
        _response.Headers["Cache-Control"] = "no-cache";
        _response.SendChunked = true;
    }

    private async Task WriteLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(KeepaliveInterval);
                string frame;
                try
                {
                    frame = await _frames.Reader.ReadAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    frame = ": keepalive\n\n";
                }

                byte[] bytes = Encoding.UTF8.GetBytes(frame);
                await _response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await _response.OutputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or OperationCanceledException or ObjectDisposedException)
        {
            // Client disconnected or server stopping; the owning McpServer deregisters us.
        }
    }

    /// <summary>Stops the write loop and closes the HTTP response.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _frames.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            _writeLoop.Wait(2000);
        }
        catch
        {
            // Write loop may end in a transport exception after the client drops; nothing to do.
        }
        _cts.Dispose();
        try
        {
            _response.OutputStream.Close();
        }
        catch
        {
            // The listener may already have torn the connection down.
        }
    }
}
