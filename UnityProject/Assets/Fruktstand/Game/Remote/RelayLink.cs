// The Quest's connection to the relay on the MacBook (EPIC-004). Optional: without a relay the game is exactly the
// usual game, and nothing here ever blocks a frame or throws into the game.
//
// Finding the relay: an address in <persistentDataPath>/relay.txt ("ws://192.168.1.10:8787/ws", put there with
// adb for development), otherwise the relay's UDP beacon (broadcast to port 47811 once a second; the Quest takes the
// sender's address and the port in the beacon). Then a WebSocket (System.Net.WebSockets.ClientWebSocket: part of
// the runtime, no package). On any failure or silence (no message for 15 s; the relay pings every 5 s) it waits a
// moment and starts over.
//
// All socket work runs on a background thread; messages cross to the main thread through queues, and RemoteSession
// (the rules) only ever runs on the main thread inside Update.
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Fruktstand.Game
{
    public enum LinkState { Off, Searching, Connecting, Connected }

    public sealed class RelayLink : MonoBehaviour
    {
        public const int BeaconPort = 47811;
        const int ConnectTimeoutMs = 4000;
        const int SilenceTimeoutMs = 15000;
        const int RetryDelayMs = 2000;
        const int MaxIncomingBytes = 128 * 1024;

        readonly ConcurrentQueue<string> inbox = new();
        readonly ConcurrentQueue<string> outbox = new();
        readonly ConcurrentQueue<string> logs = new();
        readonly SemaphoreSlim outboxSignal = new(0);
        CancellationTokenSource stop;
        RemoteSession session;
        string fixedAddress;
        volatile LinkState state = LinkState.Off;
        volatile string address = "";
        volatile int connectedEpoch;
        int helloEpoch;

        public LinkState State => state;
        public string Address => address;

        // Set by tests (or a debugging build) to skip discovery.
        public static string OverrideAddress { get; set; }

        public void Begin(RemoteSession remoteSession)
        {
            if (stop != null) return;
            session = remoteSession;
            fixedAddress = OverrideAddress;
            if (string.IsNullOrEmpty(fixedAddress))
            {
                try
                {
                    var file = Path.Combine(Application.persistentDataPath, "relay.txt");
                    if (File.Exists(file)) fixedAddress = File.ReadAllText(file).Trim();
                }
                catch (Exception) { fixedAddress = null; }
            }
            stop = new CancellationTokenSource();
            var token = stop.Token;
            state = LinkState.Searching;
            Task.Run(() => Run(token), token);
        }

        void Update()
        {
            while (logs.TryDequeue(out var line)) Debug.Log("[Fruktstand] relay " + line);
            if (session == null) return;
            if (state == LinkState.Connected && helloEpoch != connectedEpoch)
            {
                helloEpoch = connectedEpoch;
                Send(session.HelloMessage());
                session.ResendMarket(); // the relay (and phones) learn the market's state after every (re)connect
            }
            var budget = 16; // a burst of messages never stalls a frame
            while (budget-- > 0 && inbox.TryDequeue(out var json))
                foreach (var reply in session.Handle(json)) Send(reply);
            if (session.Pump() is string status) Send(status);
            if (session.PumpMarket() is string market) Send(market);
        }

        void Send(string json)
        {
            if (state != LinkState.Connected) return; // statuses are re-asked on reconnect (sync), nothing is queued up
            outbox.Enqueue(json);
            outboxSignal.Release();
        }

        void OnDestroy() => End();
        void OnApplicationQuit() => End();

        public void End()
        {
            if (stop == null) return;
            stop.Cancel();
            stop = null;
            state = LinkState.Off;
        }

        async Task Run(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    state = LinkState.Searching;
                    var url = !string.IsNullOrEmpty(fixedAddress) ? fixedAddress : await Discover(token);
                    if (url == null) continue;
                    address = url;
                    state = LinkState.Connecting;
                    await Connect(url, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception e) { logs.Enqueue($"error: {e.GetType().Name}: {e.Message}"); }
                state = LinkState.Searching;
                try { await Task.Delay(RetryDelayMs, token); }
                catch (OperationCanceledException) { return; }
            }
        }

        // Listens for the relay's beacon: {"fruktstand":"relay","port":8787}. Null after a quiet while (loop again).
        async Task<string> Discover(CancellationToken token)
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, BeaconPort));
            var receive = udp.ReceiveAsync();
            _ = receive.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted); // disposed while waiting
            var done = await Task.WhenAny(receive, Task.Delay(5000, token));
            if (done != receive) return null;
            var packet = receive.Result;
            var text = Encoding.UTF8.GetString(packet.Buffer);
            if (!text.Contains("\"fruktstand\":\"relay\"")) return null;
            var port = Beacon.Port(text);
            if (port <= 0) return null;
            logs.Enqueue($"beacon from {packet.RemoteEndPoint.Address}:{port}");
            return $"ws://{packet.RemoteEndPoint.Address}:{port}/ws";
        }

        async Task Connect(string url, CancellationToken token)
        {
            using var ws = new ClientWebSocket();
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(ConnectTimeoutMs);
                await ws.ConnectAsync(new Uri(url + "?role=quest"), timeout.Token);
            }
            logs.Enqueue($"connected {url}");
            while (outbox.TryDequeue(out _)) { } // nothing stale from a previous connection
            state = LinkState.Connected;
            connectedEpoch++;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
            var sender = SendLoop(ws, linked.Token);
            try { await ReceiveLoop(ws, linked.Token); }
            finally
            {
                state = LinkState.Searching;
                linked.Cancel();
                try { await sender; } catch (Exception) { /* closing */ }
                logs.Enqueue("disconnected");
            }
        }

        async Task ReceiveLoop(ClientWebSocket ws, CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            var message = new MemoryStream();
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                using var silence = CancellationTokenSource.CreateLinkedTokenSource(token);
                silence.CancelAfter(SilenceTimeoutMs);
                WebSocketReceiveResult r;
                try { r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), silence.Token); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    logs.Enqueue("silent for 15 s, reconnecting");
                    return;
                }
                if (r.MessageType == WebSocketMessageType.Close) return;
                message.Write(buffer, 0, r.Count);
                if (message.Length > MaxIncomingBytes)
                {
                    logs.Enqueue("message too large, reconnecting");
                    return;
                }
                if (!r.EndOfMessage) continue;
                if (r.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                    if (!text.StartsWith("{\"t\":\"ping\"")) inbox.Enqueue(text);
                }
                message.SetLength(0);
            }
        }

        async Task SendLoop(ClientWebSocket ws, CancellationToken token)
        {
            while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                await outboxSignal.WaitAsync(token);
                while (outbox.TryDequeue(out var json))
                    await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, token);
            }
        }
    }

    // Beacon parsing kept separate and pure (tested).
    public static class Beacon
    {
        public static int Port(string json)
        {
            const string key = "\"port\":";
            var i = json?.IndexOf(key, StringComparison.Ordinal) ?? -1;
            if (i < 0) return -1;
            i += key.Length;
            var n = 0;
            var digits = 0;
            while (i < json.Length && char.IsDigit(json[i]) && digits < 6)
            {
                n = n * 10 + (json[i++] - '0');
                digits++;
            }
            return digits > 0 && n is > 0 and < 65536 ? n : -1;
        }
    }
}
