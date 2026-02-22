using System.Buffers;
using System.Net.Sockets;
using System.Text;

namespace RoslynQuery.Core.IpcProtocol;

/// <summary>
/// Unix Domain Socket client for IPC communication with the daemon.
/// </summary>
public sealed class IpcClient : IAsyncDisposable
{
    private readonly string _socketPath;
    private Socket? _socket;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public IpcClient(string socketPath)
    {
        _socketPath = socketPath ?? throw new ArgumentNullException(nameof(socketPath));
    }

    /// <summary>
    /// Connect to the daemon.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_socket != null)
        {
            throw new InvalidOperationException("Already connected");
        }

        _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var endpoint = new UnixDomainSocketEndPoint(_socketPath);

        try
        {
            await _socket.ConnectAsync(endpoint, cancellationToken);
            _stream = new NetworkStream(_socket, ownsSocket: false);
        }
        catch
        {
            _socket.Dispose();
            _socket = null;
            throw;
        }
    }

    /// <summary>
    /// Check if connected to the daemon.
    /// </summary>
    public bool IsConnected => _socket?.Connected ?? false;

    /// <summary>
    /// Send a request and receive the response.
    /// </summary>
    public async Task<JsonRpcResponse> SendRequestAsync<TParams>(
        string method,
        TParams parameters,
        CancellationToken cancellationToken = default)
    {
        if (_stream == null)
        {
            throw new InvalidOperationException("Not connected");
        }

        var request = IpcSerializer.CreateRequest(method, parameters);
        return await SendAndReceiveAsync(request, cancellationToken);
    }

    /// <summary>
    /// Send a request without parameters and receive the response.
    /// </summary>
    public async Task<JsonRpcResponse> SendRequestAsync(
        string method,
        CancellationToken cancellationToken = default)
    {
        if (_stream == null)
        {
            throw new InvalidOperationException("Not connected");
        }

        var request = new JsonRpcRequest { Method = method };
        return await SendAndReceiveAsync(request, cancellationToken);
    }

    private async Task<JsonRpcResponse> SendAndReceiveAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            // Serialize and send
            var json = IpcSerializer.Serialize(request);
            var messageBytes = Encoding.UTF8.GetBytes(json);

            // Send length-prefixed message
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);
            await _stream!.WriteAsync(lengthBytes, cancellationToken);
            await _stream.WriteAsync(messageBytes, cancellationToken);
            await _stream.FlushAsync(cancellationToken);

            // Receive response
            var responseLengthBytes = new byte[4];
            await ReadExactlyAsync(_stream, responseLengthBytes, cancellationToken);
            var responseLength = BitConverter.ToInt32(responseLengthBytes);

            if (responseLength <= 0 || responseLength > 10 * 1024 * 1024) // 10MB max
            {
                throw new InvalidOperationException($"Invalid response length: {responseLength}");
            }

            var responseBytes = ArrayPool<byte>.Shared.Rent(responseLength);
            try
            {
                await ReadExactlyAsync(_stream, responseBytes.AsMemory(0, responseLength), cancellationToken);
                var response = IpcSerializer.Deserialize<JsonRpcResponse>(responseBytes.AsSpan(0, responseLength));
                return response ?? throw new InvalidOperationException("Failed to deserialize response");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(responseBytes);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Connection closed unexpectedly");
            }
            totalRead += read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }

        _socket?.Dispose();
        _socket = null;

        _sendLock.Dispose();
    }
}
