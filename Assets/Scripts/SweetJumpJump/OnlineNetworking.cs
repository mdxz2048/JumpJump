using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace SweetJumpJump
{
    [Serializable]
    public sealed class OnlineMessage
    {
        // Common fields
        public string type;
        public string clientId;
        public string account;
        public string password;
        public string name;
        public string message;
        public string roomKey;
        public string slot;
        public string ruleVariant;
        public bool isHost;
        public int version;

        // Game action fields
        public int pieceId;
        public int q;
        public int r;

        // Nested data (populated manually after deserialization)
        [NonSerialized] public OnlineGameSnapshot snapshot;
        [NonSerialized] public OnlineSeatSummary[] seats;
        [NonSerialized] public OnlineRoomSummary[] rooms;
        [NonSerialized] public OnlineRoomSummary room;
    }

    // Raw deserialization wrapper (JsonUtility-friendly flat envelope)
    [Serializable]
    internal sealed class RawEnvelope
    {
        public string type;
        public string clientId;
        public string account;
        public string name;
        public string message;
        public string roomKey;
        public string slot;
        public string ruleVariant;
        public bool isHost;
        public int version;
        public int pieceId;
        public int q;
        public int r;
    }

    public sealed class OnlineClient : IDisposable
    {
        private readonly Queue<OnlineMessage> inbound = new Queue<OnlineMessage>();
        private ClientWebSocket ws;
        private Thread receiveThread;
        private volatile bool running;
        private readonly object sendLock = new object();

        public bool IsConnected
        {
            get { return ws != null && ws.State == WebSocketState.Open; }
        }

        public string ClientId { get; private set; }

        // url: e.g. "ws://jump.mddxz.top:53333/ws"
        public void Connect(string url)
        {
            Dispose();
            ws = new ClientWebSocket();
            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            ws.ConnectAsync(new Uri(url), cts.Token).GetAwaiter().GetResult();
            running = true;
            receiveThread = new Thread(() => ReceiveLoop());
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        public void Send(OnlineMessage message)
        {
            if (ws == null || ws.State != WebSocketState.Open)
            {
                return;
            }

            string json = BuildJson(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            lock (sendLock)
            {
                try
                {
                    ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, System.Threading.CancellationToken.None)
                      .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Online] Send failed: " + ex.Message);
                }
            }
        }

        // Legacy compat: relay to Send (no retry needed with WebSocket)
        public void SendReliable(OnlineMessage message)
        {
            Send(message);
        }

        // No-op for WebSocket (no retry system)
        public void UpdateRetries() { }

        public bool TryDequeue(out OnlineMessage message)
        {
            lock (inbound)
            {
                if (inbound.Count == 0)
                {
                    message = null;
                    return false;
                }

                message = inbound.Dequeue();
                return true;
            }
        }

        public void Dispose()
        {
            running = false;

            try
            {
                if (ws != null)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, System.Threading.CancellationToken.None)
                          .GetAwaiter().GetResult();
                    }

                    ws.Dispose();
                }
            }
            catch
            {
            }

            ws = null;
        }

        private void ReceiveLoop()
        {
            try
            {
                byte[] buffer = new byte[65536];
                StringBuilder sb = new StringBuilder();

                while (running && ws != null && ws.State == WebSocketState.Open)
                {
                    sb.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        var seg = new ArraySegment<byte>(buffer);
                        result = ws.ReceiveAsync(seg, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    string json = sb.ToString();
                    if (string.IsNullOrEmpty(json))
                    {
                        continue;
                    }

                    OnlineMessage msg = ParseMessage(json);
                    if (msg != null)
                    {
                        if (msg.type == "WELCOME" && !string.IsNullOrEmpty(msg.clientId))
                        {
                            ClientId = msg.clientId;
                        }

                        lock (inbound)
                        {
                            inbound.Enqueue(msg);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                lock (inbound)
                {
                    inbound.Enqueue(new OnlineMessage { type = "ERROR", message = exception.Message });
                }
            }
            finally
            {
                running = false;
                lock (inbound)
                {
                    inbound.Enqueue(new OnlineMessage { type = "DISCONNECTED", message = "已断开服务器连接。" });
                }
            }
        }

        // Parse server envelope — flat fields via RawEnvelope, nested via dedicated parsers
        private static OnlineMessage ParseMessage(string json)
        {
            try
            {
                RawEnvelope raw = JsonUtility.FromJson<RawEnvelope>(json);
                if (raw == null)
                {
                    return null;
                }

                OnlineMessage msg = new OnlineMessage
                {
                    type = raw.type,
                    clientId = raw.clientId,
                    account = raw.account,
                    name = raw.name,
                    message = raw.message,
                    roomKey = raw.roomKey,
                    slot = raw.slot,
                    ruleVariant = raw.ruleVariant,
                    isHost = raw.isHost,
                    version = raw.version,
                    pieceId = raw.pieceId,
                    q = raw.q,
                    r = raw.r
                };

                if (raw.type == "STATE")
                {
                    msg.snapshot = ParseNested<OnlineGameSnapshot>(json, "\"snapshot\"");
                    msg.seats = ParseNestedArray<OnlineSeatSummary>(json, "\"seats\"");
                }
                else if (raw.type == "ROOM" || raw.type == "LOBBY")
                {
                    msg.room = ParseNested<OnlineRoomSummary>(json, "\"room\"");
                    // LOBBY sends seats inside room.players; ROOM has no seats array
                    msg.seats = msg.room?.players;
                }
                else if (raw.type == "ROOM_LIST")
                {
                    msg.rooms = ParseNestedArray<OnlineRoomSummary>(json, "\"rooms\"");
                }

                return msg;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Online] ParseMessage failed: " + ex.Message + "\n" + json);
                return null;
            }
        }

        // Extract a nested JSON object and deserialize it
        private static T ParseNested<T>(string json, string key)
        {
            int keyIdx = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIdx < 0) return default;

            int colonIdx = json.IndexOf(':', keyIdx + key.Length);
            if (colonIdx < 0) return default;

            int start = json.IndexOf('{', colonIdx);
            if (start < 0) return default;

            int depth = 0, end = -1;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { depth--; if (depth == 0) { end = i; break; } }
            }

            if (end < 0) return default;
            return JsonUtility.FromJson<T>(json.Substring(start, end - start + 1));
        }

        // Extract a nested JSON array and deserialize each element
        private static T[] ParseNestedArray<T>(string json, string key)
        {
            int keyIdx = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIdx < 0) return null;

            int colonIdx = json.IndexOf(':', keyIdx + key.Length);
            if (colonIdx < 0) return null;

            int start = json.IndexOf('[', colonIdx);
            if (start < 0) return null;

            int depth = 0, end = -1;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']') { depth--; if (depth == 0) { end = i; break; } }
            }

            if (end < 0) return null;

            // JsonUtility can't deserialize arrays directly; wrap in object
            string wrapped = "{\"items\":" + json.Substring(start, end - start + 1) + "}";
            ArrayWrapper<T> wrapper = JsonUtility.FromJson<ArrayWrapper<T>>(wrapped);
            return wrapper?.items;
        }

        // Build outbound JSON from OnlineMessage
        private static string BuildJson(OnlineMessage msg)
        {
            var sb = new StringBuilder("{");
            AppendStr(sb, "type", msg.type);
            AppendStr(sb, "account", msg.account);
            AppendStr(sb, "password", msg.password);
            AppendStr(sb, "name", msg.name);
            AppendStr(sb, "roomKey", msg.roomKey);
            AppendStr(sb, "ruleVariant", msg.ruleVariant);
            if (msg.pieceId != 0)
                sb.Append("\"pieceId\":").Append(msg.pieceId).Append(",");
            if (msg.q != 0 || msg.r != 0)
                sb.Append("\"q\":").Append(msg.q).Append(",\"r\":").Append(msg.r).Append(",");
            if (sb[sb.Length - 1] == ',')
                sb.Remove(sb.Length - 1, 1);
            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendStr(StringBuilder sb, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                sb.Append("\"").Append(key).Append("\":\"")
                  .Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""))
                  .Append("\",");
            }
        }
    }

    // Helper for JsonUtility array deserialization
    [Serializable]
    internal sealed class ArrayWrapper<T>
    {
        public T[] items;
    }
}
