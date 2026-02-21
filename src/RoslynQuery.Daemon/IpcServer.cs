using System.Buffers;
using System.Net.Sockets;
using System.Text;
using RoslynQuery.Core.Contracts;
using RoslynQuery.Core.Contracts.Requests;
using RoslynQuery.Core.IpcProtocol;

namespace RoslynQuery.Daemon;

/// <summary>
/// Unix Domain Socket server for handling JSON-RPC requests.
/// </summary>
public sealed class IpcServer : IAsyncDisposable
{
    private readonly string _socketPath;
    private readonly IQueryService _queryService;
    private readonly TimeSpan _idleTimeout;
    private DateTime _lastActivity = DateTime.UtcNow;
    private readonly object _activityLock = new();
    private Socket? _listenSocket;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _clientTasks = [];
    private readonly object _clientLock = new();
    private bool _disposed;

    public IpcServer(string socketPath, IQueryService queryService, TimeSpan? idleTimeout = null)
    {
        _socketPath = socketPath ?? throw new ArgumentNullException(nameof(socketPath));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _idleTimeout = idleTimeout ?? Timeout.InfiniteTimeSpan;
    }

    /// <summary>
    /// Record activity to reset the idle timer.
    /// </summary>
    public void RecordActivity()
    {
        lock (_activityLock) { _lastActivity = DateTime.UtcNow; }
    }

    /// <summary>
    /// Start listening for connections.
    /// </summary>
    public void Start()
    {
        if (_listenSocket != null)
        {
            throw new InvalidOperationException("Server already started");
        }

        // Remove existing socket file if it exists
        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }

        // Create the directory if needed
        var socketDir = Path.GetDirectoryName(_socketPath);
        if (!string.IsNullOrEmpty(socketDir) && !Directory.Exists(socketDir))
        {
            Directory.CreateDirectory(socketDir);
        }

        _listenSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listenSocket.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listenSocket.Listen(5);

        // Start accepting clients
        _ = AcceptClientsAsync();
    }

    private async Task AcceptClientsAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var clientSocket = await _listenSocket!.AcceptAsync(_cts.Token);
                var clientTask = HandleClientAsync(clientSocket);

                lock (_clientLock)
                {
                    _clientTasks.Add(clientTask);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down
        }
        catch (Exception)
        {
            // TODO: Log error
        }
    }

    private async Task HandleClientAsync(Socket clientSocket)
    {
        await using var stream = new NetworkStream(clientSocket, ownsSocket: true);

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                // Read length prefix
                var lengthBytes = new byte[4];
                var bytesRead = await ReadExactlyAsync(stream, lengthBytes, _cts.Token);
                if (bytesRead == 0)
                {
                    break; // Client disconnected
                }

                var messageLength = BitConverter.ToInt32(lengthBytes);
                if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) // 10MB max
                {
                    break; // Invalid message
                }

                // Read message
                var messageBytes = ArrayPool<byte>.Shared.Rent(messageLength);
                try
                {
                    bytesRead = await ReadExactlyAsync(stream, messageBytes.AsMemory(0, messageLength), _cts.Token);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    // Parse and handle request
                    var request = IpcSerializer.Deserialize<JsonRpcRequest>(messageBytes.AsSpan(0, messageLength));
                    if (request == null)
                    {
                        var errorResponse = IpcSerializer.CreateErrorResponse(
                            "", JsonRpcErrorCodes.ParseError, "Failed to parse request");
                        await SendResponseAsync(stream, errorResponse, _cts.Token);
                        continue;
                    }

                    var response = await HandleRequestAsync(request, _cts.Token);
                    await SendResponseAsync(stream, response, _cts.Token);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(messageBytes);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down
        }
        catch (Exception)
        {
            // TODO: Log error
        }
    }

    private async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        RecordActivity();
        try
        {
            return request.Method switch
            {
                "definition" => await HandleDefinitionAsync(request, cancellationToken),
                "base-definition" => await HandleBaseDefinitionAsync(request, cancellationToken),
                "implementations" => await HandleImplementationsAsync(request, cancellationToken),
                "references" => await HandleReferencesAsync(request, cancellationToken),
                "callers" => await HandleCallersAsync(request, cancellationToken),
                "callees" => await HandleCalleesAsync(request, cancellationToken),
                "symbol" => await HandleSymbolInfoAsync(request, cancellationToken),
                "diagnostics" => await HandleDiagnosticsAsync(request, cancellationToken),
                "ping" => CreatePingResponse(request.Id),
                "shutdown" => HandleShutdown(request.Id),
                _ => IpcSerializer.CreateErrorResponse(
                    request.Id, JsonRpcErrorCodes.MethodNotFound, $"Unknown method: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            return IpcSerializer.CreateErrorResponse(
                request.Id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
    }

    private async Task<JsonRpcResponse> HandleDefinitionAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var req = ParseRequest<DefinitionRequest>(request);
        if (req == null)
        {
            return IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Invalid parameters");
        }

        var result = await _queryService.GetDefinitionAsync(req, cancellationToken);
        return result.Success
            ? IpcSerializer.CreateSuccessResponse(request.Id, result)
            : IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.SymbolNotFound, result.ErrorMessage ?? "Symbol not found");
    }

    private async Task<JsonRpcResponse> HandleBaseDefinitionAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var req = ParseRequest<BaseDefinitionRequest>(request);
        if (req == null)
        {
            return IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Invalid parameters");
        }

        var result = await _queryService.GetBaseDefinitionAsync(req, cancellationToken);
        return result.Success
            ? IpcSerializer.CreateSuccessResponse(request.Id, result)
            : IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.SymbolNotFound, result.ErrorMessage ?? "Symbol not found");
    }

    private async Task<JsonRpcResponse> HandleImplementationsAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var req = ParseRequest<ImplementationsRequest>(request);
        if (req == null)
        {
            return IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Invalid parameters");
        }

        var result = await _queryService.GetImplementationsAsync(req, cancellationToken);
        return result.Success
            ? IpcSerializer.CreateSuccessResponse(request.Id, result)
            : IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.SymbolNotFound, result.ErrorMessage ?? "Symbol not found");
    }

    private async Task<JsonRpcResponse> HandleReferencesAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var req = ParseRequest<ReferencesRequest>(request);
        if (req == null)
        {
            return IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Invalid parameters");
        }

        var result = await _queryService.GetReferencesAsync(req, cancellationToken);
        return result.Success
            ? IpcSerializer.CreateSuccessResponse(request.Id, result)
            : IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.SymbolNotFound, result.ErrorMessage ?? "Symbol not found");
    }

    private async Task<JsonRpcResponse> HandleCallersAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var req = ParseRequest<CallersRequest>(request);
        if (req == null)
        {
            return IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Invalid parameters");
        }

        var result = await _queryService.GetCallersAsync(req, cancellationToken);
        return result.Success
            ? IpcSerializer.CreateSuccessResponse(request.Id, result)
            : IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.SymbolNotFound, result.ErrorMessage ?? "Symbol not found");
    }

    private async Task<JsonRpcResponse> HandleCalleesAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var req = ParseRequest<CalleesRequest>(request);
        if (req == null)
        {
            return IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Invalid parameters");
        }

        var result = await _queryService.GetCalleesAsync(req, cancellationToken);
        return result.Success
            ? IpcSerializer.CreateSuccessResponse(request.Id, result)
            : IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.SymbolNotFound, result.ErrorMessage ?? "Symbol not found");
    }

    private async Task<JsonRpcResponse> HandleSymbolInfoAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var req = ParseRequest<SymbolRequest>(request);
        if (req == null)
        {
            return IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.InvalidParams, "Invalid parameters");
        }

        var result = await _queryService.GetSymbolInfoAsync(req, cancellationToken);
        return result.Success
            ? IpcSerializer.CreateSuccessResponse(request.Id, result)
            : IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.SymbolNotFound, result.ErrorMessage ?? "Symbol not found");
    }

    private async Task<JsonRpcResponse> HandleDiagnosticsAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        var req = ParseRequest<DiagnosticsRequest>(request);
        req ??= new DiagnosticsRequest(); // Default: whole solution

        var result = await _queryService.GetDiagnosticsAsync(req, cancellationToken);
        return result.Success
            ? IpcSerializer.CreateSuccessResponse(request.Id, result)
            : IpcSerializer.CreateErrorResponse(request.Id, JsonRpcErrorCodes.InternalError, result.ErrorMessage ?? "Failed to get diagnostics");
    }

    private JsonRpcResponse CreatePingResponse(string id)
    {
        DateTime lastActivity;
        lock (_activityLock) { lastActivity = _lastActivity; }
        return IpcSerializer.CreateSuccessResponse(id, new
        {
            status = "ok",
            idleTimeoutMinutes = _idleTimeout == Timeout.InfiniteTimeSpan ? 0 : _idleTimeout.TotalMinutes,
            idleSeconds = Math.Round((DateTime.UtcNow - lastActivity).TotalSeconds, 1)
        });
    }

    private JsonRpcResponse HandleShutdown(string id)
    {
        // Request shutdown asynchronously
        _ = Task.Run(async () =>
        {
            await Task.Delay(100); // Let the response be sent first
            await _cts.CancelAsync();
        });

        return IpcSerializer.CreateSuccessResponse(id, new { status = "shutting_down" });
    }

    private static T? ParseRequest<T>(JsonRpcRequest request) where T : class
    {
        if (request.Params == null)
        {
            return null;
        }

        return IpcSerializer.Deserialize<T>(request.Params.Value);
    }

    private static async Task SendResponseAsync(NetworkStream stream, JsonRpcResponse response, CancellationToken cancellationToken)
    {
        var responseBytes = IpcSerializer.SerializeToUtf8Bytes(response);
        var lengthBytes = BitConverter.GetBytes(responseBytes.Length);

        await stream.WriteAsync(lengthBytes, cancellationToken);
        await stream.WriteAsync(responseBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<int> ReadExactlyAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken);
            if (read == 0)
            {
                return totalRead == 0 ? 0 : throw new EndOfStreamException();
            }
            totalRead += read;
        }
        return totalRead;
    }

    /// <summary>
    /// Wait for server shutdown.
    /// </summary>
    public async Task WaitForShutdownAsync()
    {
        if (_idleTimeout == Timeout.InfiniteTimeSpan)
        {
            try { await Task.Delay(Timeout.Infinite, _cts.Token); }
            catch (OperationCanceledException) { }
            return;
        }

        var checkInterval = TimeSpan.FromSeconds(Math.Min(60, _idleTimeout.TotalSeconds));
        while (!_cts.IsCancellationRequested)
        {
            DateTime lastActivity;
            lock (_activityLock) { lastActivity = _lastActivity; }

            if (DateTime.UtcNow - lastActivity >= _idleTimeout)
            {
                Console.Error.WriteLine($"Idle timeout ({_idleTimeout.TotalMinutes:F0} min) reached. Shutting down.");
                try { await _cts.CancelAsync(); } catch { }
                break;
            }

            try { await Task.Delay(checkInterval, _cts.Token); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _cts.CancelAsync();

        _listenSocket?.Dispose();

        // Wait for client tasks to complete
        Task[] tasks;
        lock (_clientLock)
        {
            tasks = [.. _clientTasks];
        }

        if (tasks.Length > 0)
        {
            await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        // Clean up socket file
        if (File.Exists(_socketPath))
        {
            try
            {
                File.Delete(_socketPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _cts.Dispose();
    }
}
