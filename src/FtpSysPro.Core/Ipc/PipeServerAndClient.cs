using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FtpSysPro.Core.Ipc
{
    public class PipeServer : IDisposable
    {
        private readonly string _pipeName;
        private readonly Func<IpcMessage, Task<IpcResponse>> _handler;
        private CancellationTokenSource _cts;
        private Task _listenTask;

        public PipeServer(Func<IpcMessage, Task<IpcResponse>> handler, string pipeName = IpcConstants.PipeName)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _pipeName = pipeName;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listenTask?.Wait(3000); } catch { }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = CreateServerPipe();
                    await Task.Factory.FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null);

                    // Handle connection in background so server can accept next client
                    var clientPipe = pipe;
                    _ = Task.Run(() => HandleClientAsync(clientPipe, cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    pipe?.Dispose();
                    break;
                }
                catch (Exception)
                {
                    pipe?.Dispose();
                    await Task.Delay(500, cancellationToken);
                }
            }
        }

        private NamedPipeServerStream CreateServerPipe()
        {
            PipeSecurity pipeSecurity = null;

            try
            {
                pipeSecurity = new PipeSecurity();

                // SYSTEM: FullControl
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));

                // Builtin Administrators: FullControl
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));

                // Authenticated users: ReadWrite (so Manager running elevated or standard can connect if allowed)
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));

                return new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    8192,
                    8192,
                    pipeSecurity);
            }
            catch
            {
                // Fallback without explicit security if restricted
                return new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
        {
            using (pipe)
            using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 8192, true))
            using (var writer = new StreamWriter(pipe, Encoding.UTF8, 8192, true) { AutoFlush = true })
            {
                try
                {
                    while (pipe.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync();
                        if (line == null) break;

                        IpcMessage msg;
                        try
                        {
                            msg = JsonConvert.DeserializeObject<IpcMessage>(line);
                        }
                        catch (Exception ex)
                        {
                            var errResp = IpcResponse.Fail("unknown", "Malformed request: " + ex.Message);
                            await writer.WriteLineAsync(JsonConvert.SerializeObject(errResp));
                            continue;
                        }

                        IpcResponse resp;
                        try
                        {
                            resp = await _handler(msg);
                        }
                        catch (Exception ex)
                        {
                            resp = IpcResponse.Fail(msg.MessageId, ex.Message);
                        }

                        var respJson = JsonConvert.SerializeObject(resp);
                        await writer.WriteLineAsync(respJson);
                    }
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }

    public class PipeClient : IDisposable
    {
        private readonly string _pipeName;

        public PipeClient(string pipeName = IpcConstants.PipeName)
        {
            _pipeName = pipeName;
        }

        public async Task<IpcResponse> SendAsync(IpcMessage message, int timeoutMs = IpcConstants.DefaultTimeoutMs)
        {
            using (var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await pipe.ConnectAsync(timeoutMs);

                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 8192, true))
                using (var writer = new StreamWriter(pipe, Encoding.UTF8, 8192, true) { AutoFlush = true })
                {
                    var reqJson = JsonConvert.SerializeObject(message);
                    await writer.WriteLineAsync(reqJson);

                    var respJson = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(respJson))
                        throw new IOException("Empty response from service");

                    return JsonConvert.DeserializeObject<IpcResponse>(respJson);
                }
            }
        }

        public async Task<bool> IsServiceAvailableAsync(int timeoutMs = 1000)
        {
            try
            {
                var resp = await SendAsync(new IpcMessage { Action = IpcActions.Ping }, timeoutMs);
                return resp != null && resp.Success;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose() { }
    }
}
