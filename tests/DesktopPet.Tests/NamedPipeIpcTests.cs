using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DesktopPet.Models;
using DesktopPet.Services;
using Xunit;

namespace DesktopPet.Tests;

public class NamedPipeIpcTests
{
    [Fact]
    public void ServerStartAndStop()
    {
        string pipeName = $"DesktopPetTestPipe_{Guid.NewGuid():N}";
        using var server = new NamedPipeIpcServer(pipeName);
        server.Start();
        Assert.True(server.IsRunning);
        server.Stop();
        Assert.False(server.IsRunning);
    }

    [Fact]
    public void ClientConnectsAndSendsMessage()
    {
        string pipeName = $"DesktopPetTestPipe_{Guid.NewGuid():N}";
        using var server = new NamedPipeIpcServer(pipeName);

        PetEventMessage? received = null;
        using var ev = new ManualResetEventSlim(false);

        server.MessageReceived += msg =>
        {
            received = msg;
            ev.Set();
        };

        server.Start();

        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        client.Connect(2000);

        var utf8 = new UTF8Encoding(false);
        using var writer = new StreamWriter(client, utf8, 4096, leaveOpen: true) { AutoFlush = true };
        writer.WriteLine("{\"event\":\"success\",\"title\":\"Build\",\"message\":\"Success!\",\"actionLabel\":\"Open\",\"timeoutSeconds\":5}");

        using var reader = new StreamReader(client, utf8, false, 4096, leaveOpen: true);
        string? resp = reader.ReadLine();
        Assert.NotNull(resp);

        var parsed = JsonSerializer.Deserialize<PetEventResponse>(resp);
        Assert.NotNull(parsed);
        Assert.Equal("ok", parsed.Status);

        Assert.True(ev.Wait(2000));
        Assert.NotNull(received);
        Assert.Equal("success", received.Event);
        Assert.Equal("Build", received.Title);
        Assert.Equal("Success!", received.Message);
        Assert.Equal("Open", received.ActionLabel);
        Assert.Equal(5, received.TimeoutSeconds);

        server.Stop();
    }

    [Fact]
    public void MultipleSequentialClientsHandled()
    {
        string pipeName = $"DesktopPetTestPipe_{Guid.NewGuid():N}";
        using var server = new NamedPipeIpcServer(pipeName);

        int count = 0;
        using var ev = new CountdownEvent(3);

        server.MessageReceived += _ =>
        {
            Interlocked.Increment(ref count);
            ev.Signal();
        };

        server.Start();

        var utf8 = new UTF8Encoding(false);
        for (int i = 0; i < 3; i++)
        {
            using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
            {
                client.Connect(2000);
                using var writer = new StreamWriter(client, utf8, 4096, leaveOpen: true) { AutoFlush = true };
                writer.WriteLine($"{{\"event\":\"step_{i}\"}}");

                using var reader = new StreamReader(client, utf8, false, 4096, leaveOpen: true);
                string? resp = reader.ReadLine();
                Assert.NotNull(resp);
            }
            Thread.Sleep(30);
        }

        Assert.True(ev.Wait(2000));
        Assert.Equal(3, count);

        server.Stop();
    }
}
