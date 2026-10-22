using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinDesk.App;

internal sealed class SingleInstanceCommands : IDisposable
{
    internal static string UserPipeName => "WinDesk.Commands." + WindowsIdentity.GetCurrent().User!.Value;
    internal static bool IsValid(string command) => command is "new-group" or "toggle-panels" or "settings";
    private readonly CancellationTokenSource _stop = new();
    private readonly string _name;
    private readonly Action<string> _receive;
    private readonly Task _listener;

    internal SingleInstanceCommands(Action<string> receive, string? name = null)
    {
        _name = name ?? UserPipeName; _receive = receive;
        _listener = Listen();
    }

    private async Task Listen()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                string command = await ReadCommand(pipe, timeout.Token).ConfigureAwait(false);
                bool valid = IsValid(command);
                if (valid) _receive(command);
                await pipe.WriteAsync(Encoding.ASCII.GetBytes(valid ? "ok\n" : "error\n"), timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (IOException ex) { System.Diagnostics.Trace.WriteLine($"WinDesk command connection: {ex.Message}"); }
            catch (UnauthorizedAccessException ex) { System.Diagnostics.Trace.WriteLine($"WinDesk command access: {ex.Message}"); break; }
        }
    }

    private static async Task<string> ReadCommand(Stream stream, CancellationToken cancellation)
    {
        byte[] buffer = new byte[64]; int length = 0;
        while (length < buffer.Length)
        {
            int count = await stream.ReadAsync(buffer.AsMemory(length, 1), cancellation).ConfigureAwait(false);
            if (count == 0) return "";
            if (buffer[length] == '\n') return Encoding.ASCII.GetString(buffer, 0, length);
            length++;
        }
        return "";
    }

    internal static async Task<bool> SendAsync(string command, string? name = null)
    {
        if (!IsValid(command)) return false;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var pipe = new NamedPipeClientStream(".", name ?? UserPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            await pipe.WriteAsync(Encoding.ASCII.GetBytes(command + "\n"), timeout.Token).ConfigureAwait(false);
            return await ReadCommand(pipe, timeout.Token).ConfigureAwait(false) == "ok";
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException) { return false; }
    }

    public void Dispose() { _stop.Cancel(); }
}
