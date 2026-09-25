using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DesktopPet.Models;

namespace DesktopPet.Services;

public class NamedPipeIpcServer : IDisposable
{
    public const string DefaultPipeName = "DesktopPetIpc";

    private readonly string _pipeName;
    private volatile bool _isRunning;
    private Thread? _listenerThread;
    private NamedPipeServerStream? _currentServer;
    private readonly ManualResetEventSlim _startedEvent = new(false);

    public bool IsRunning => _isRunning;

    public event Action<PetEventMessage>? MessageReceived;

    public NamedPipeIpcServer(string pipeName = DefaultPipeName)
    {
        _pipeName = pipeName;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _startedEvent.Reset();
        _listenerThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "DesktopPet_NamedPipeListener"
        };
        _listenerThread.Start();
        _startedEvent.Wait(2000);
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        try
        {
            using var dummy = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            dummy.Connect(100);
        }
        catch { }

        try
        {
            _currentServer?.Dispose();
        }
        catch { }
        finally
        {
            _currentServer = null;
        }

        try
        {
            _listenerThread?.Join(500);
        }
        catch { }
        finally
        {
            _listenerThread = null;
        }
    }

    private void ListenLoop()
    {
        while (_isRunning)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096
                );
                _currentServer = server;
                _startedEvent.Set();

                server.WaitForConnection();
                _currentServer = null;

                if (!_isRunning)
                {
                    server.Dispose();
                    break;
                }

                var clientServer = server;
                new Thread(() => HandleClient(clientServer)) { IsBackground = true }.Start();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                server?.Dispose();
                if (!_isRunning) break;
                Thread.Sleep(100);
            }
        }
    }

    private void HandleClient(NamedPipeServerStream server)
    {
        using (server)
        {
            try
            {
                var utf8NoBom = new UTF8Encoding(false);
                using var reader = new StreamReader(server, utf8NoBom, false, 4096, leaveOpen: true);
                string? line = reader.ReadLine();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    var msg = JsonSerializer.Deserialize<PetEventMessage>(line);
                    if (msg != null)
                    {
                        MessageReceived?.Invoke(msg);
                        var response = new PetEventResponse { Status = "ok", Message = "Event processed" };
                        using var writer = new StreamWriter(server, utf8NoBom, 4096, leaveOpen: true) { AutoFlush = true };
                        writer.WriteLine(JsonSerializer.Serialize(response));
                        server.Flush();
                    }
                    else
                    {
                        var response = new PetEventResponse { Status = "error", Message = "Invalid message payload" };
                        using var writer = new StreamWriter(server, utf8NoBom, 4096, leaveOpen: true) { AutoFlush = true };
                        writer.WriteLine(JsonSerializer.Serialize(response));
                        server.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                DebugWriteLine($"IPC Client error: {ex.Message}");
            }
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void DebugWriteLine(string text) => System.Diagnostics.Debug.WriteLine(text);

    public void Dispose()
    {
        Stop();
    }
}
