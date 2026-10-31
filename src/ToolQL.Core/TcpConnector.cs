using System.Net.Sockets;

namespace ToolQL.Core;

/// <summary>Nối TCP tới server, trả luồng sở hữu socket (đóng luồng là đóng socket).</summary>
public static class TcpConnector
{
    public static Func<CancellationToken, Stream> Create(string host, int port, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(10);
        return cancel =>
        {
            var client = new TcpClient { NoDelay = true };
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                cts.CancelAfter(limit);
                client.ConnectAsync(host, port, cts.Token).AsTask().GetAwaiter().GetResult();
                return new NetworkStream(client.Client, ownsSocket: true);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        };
    }
}
