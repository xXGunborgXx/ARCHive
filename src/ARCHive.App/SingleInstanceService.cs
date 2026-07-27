using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace ARCHive.App;

internal sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\ARCHive_SingleInstance";
    private const string PipeName = "Local\\ARCHive_SingleInstance_Pipe";

    private Mutex? _mutex;
    private CancellationTokenSource? _listenCts;

    public event Action<string[]>? ArgumentsReceived;

    public bool IsFirstInstance { get; private set; }

    public bool TryAcquire(string[] args)
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        IsFirstInstance = createdNew;

        if (!createdNew && args.Length > 0)
        {
            SendArgsToRunningInstance(args);
        }

        return createdNew;
    }

    public void StartListening()
    {
        _listenCts = new CancellationTokenSource();
        _ = ListenLoop(_listenCts.Token);
    }

    public void Dispose()
    {
        _listenCts?.Cancel();
        _listenCts?.Dispose();

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex?.Dispose();
    }

    private void SendArgsToRunningInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out);
            client.Connect(3000);

            using var writer = new StreamWriter(client, Encoding.UTF8);
            writer.WriteLine(args.Length);
            foreach (var arg in args)
            {
                writer.WriteLine(arg);
            }
            writer.Flush();
        }
        catch
        {
        }
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var countLine = await reader.ReadLineAsync(ct);
                if (!int.TryParse(countLine, out var count) || count <= 0)
                {
                    continue;
                }

                var args = new string[count];
                for (var i = 0; i < count; i++)
                {
                    var line = await reader.ReadLineAsync(ct);
                    args[i] = line ?? string.Empty;
                }

                if (args.Length > 0)
                {
                    ArgumentsReceived?.Invoke(args);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }
            }
            finally
            {
                server?.Dispose();
            }
        }
    }
}
