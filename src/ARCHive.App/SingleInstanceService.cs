using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace ARCHive.App;

internal sealed class SingleInstanceService : IDisposable
{
    private const string DefaultMutexName = "Local\\ARCHive_SingleInstance";
    private const string DefaultPipeName = "ARCHive_SingleInstance_Pipe";
    private const int MaxArgumentCount = 4096;

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly int _connectTimeoutMilliseconds;
    private Mutex? _mutex;
    private CancellationTokenSource? _listenCts;

    public SingleInstanceService(
        string mutexName = DefaultMutexName,
        string pipeName = DefaultPipeName,
        int connectTimeoutMilliseconds = 10_000)
    {
        _mutexName = mutexName;
        _pipeName = pipeName;
        _connectTimeoutMilliseconds = connectTimeoutMilliseconds;
    }

    public event Action<string[]>? ArgumentsReceived;

    public bool IsFirstInstance { get; private set; }
    public bool LastForwardSucceeded { get; private set; } = true;
    public string? LastForwardError { get; private set; }

    public bool TryAcquire(string[] args)
    {
        _mutex = new Mutex(true, _mutexName, out bool createdNew);
        IsFirstInstance = createdNew;

        if (!createdNew && args.Length > 0)
        {
            LastForwardSucceeded = SendArgsToRunningInstance(args);
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

        if (IsFirstInstance)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex?.Dispose();
    }

    private bool SendArgsToRunningInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.CurrentUserOnly);
            client.Connect(_connectTimeoutMilliseconds);

            using var writer = new StreamWriter(client, Encoding.UTF8);
            writer.WriteLine(args.Length);
            foreach (var arg in args)
            {
                writer.WriteLine(arg);
            }
            writer.Flush();
            return true;
        }
        catch (Exception ex)
        {
            LastForwardError = ex.Message;
            return false;
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
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var countLine = await reader.ReadLineAsync(ct);
                if (!int.TryParse(countLine, out var count) ||
                    count <= 0 ||
                    count > MaxArgumentCount)
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
