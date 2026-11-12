using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Parchwell.LoadGen.Tests.Abrupt;

/// <summary>Delays broker connections and replies so the offline probes exercise unacknowledged sends.</summary>
internal sealed class B10AbruptKafkaProxy : IAsyncDisposable
{
    internal static readonly TimeSpan ConnectDelay = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan ResponseDelay = TimeSpan.FromMilliseconds(500);
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource shutdown = new();
    private readonly ConcurrentBag<Task> connections = [];
    private readonly Task accepting;
    private IPEndPoint? upstream;
    private int acceptedConnections;

    internal B10AbruptKafkaProxy()
    {
        listener.Start();
        accepting = AcceptAsync();
    }

    internal string BootstrapServers => "127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture);
    internal int AcceptedConnections => Volatile.Read(ref acceptedConnections);

    internal void SetUpstream(string address)
    {
        string[] parts = address.Split(':');
        upstream = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(shutdown.Token).ConfigureAwait(false);
                Interlocked.Increment(ref acceptedConnections);
                connections.Add(ForwardAsync(client));
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        catch (SocketException) when (shutdown.IsCancellationRequested) { }
    }

    private async Task ForwardAsync(TcpClient client)
    {
        using (client)
        using (var broker = new TcpClient())
        {
            try
            {
                await Task.Delay(ConnectDelay, shutdown.Token).ConfigureAwait(false);
                IPEndPoint endpoint = upstream ?? throw new InvalidOperationException("Proxy upstream is missing.");
                await broker.ConnectAsync(endpoint, shutdown.Token).ConfigureAwait(false);
                NetworkStream downstream = client.GetStream();
                NetworkStream upstreamStream = broker.GetStream();
                Task replies = CopyAsync(upstreamStream, downstream, ResponseDelay);
                // A closed client can still have a final produce request buffered in its receive stream.
                // Drain that stream before closing the broker's write side, even if replies have failed.
                await CopyAsync(downstream, upstreamStream, TimeSpan.Zero).ConfigureAwait(false);
                try { broker.Client.Shutdown(SocketShutdown.Send); }
                catch (SocketException) { }
                await replies.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
            catch (SocketException) { }
            catch (IOException) { }
        }
    }

    private async Task CopyAsync(NetworkStream source, NetworkStream destination, TimeSpan delay)
    {
        byte[] buffer = new byte[65536];
        try
        {
            int count;
            while ((count = await source.ReadAsync(buffer, shutdown.Token).ConfigureAwait(false)) != 0)
            {
                if (delay != TimeSpan.Zero) await Task.Delay(delay, shutdown.Token).ConfigureAwait(false);
                await destination.WriteAsync(buffer.AsMemory(0, count), shutdown.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        catch (IOException) { }
        catch (SocketException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await shutdown.CancelAsync().ConfigureAwait(false);
        listener.Stop();
        await accepting.ConfigureAwait(false);
        await Task.WhenAll(connections).ConfigureAwait(false);
        shutdown.Dispose();
    }
}
