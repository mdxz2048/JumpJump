using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace SweetJumpJump
{
    [Serializable]
    public sealed class OnlineMessage
    {
        public string type;
        public string messageId;
        public string ackId;
        public string roomKey;
        public string clientId;
        public string hostId;
        public string playerName;
        public string playerToken;
        public string requestClientId;
        public string requestPlayerName;
        public string slot;
        public string slots;
        public string aiSlots;
        public string ready;
        public string message;
        public int pieceId;
        public int q;
        public int r;
        public int count;
        public int actionSeq;
        public int lastActionSeq;
        public bool ok;
    }

    public sealed class OnlineClient : IDisposable
    {
        private sealed class PendingMessage
        {
            public OnlineMessage Message;
            public float LastSentAt;
            public int Attempts;
        }

        private const float RetryIntervalSeconds = 0.75f;
        private const int MaxRetryAttempts = 20;

        private readonly Queue<OnlineMessage> inbound = new Queue<OnlineMessage>();
        private readonly Dictionary<string, PendingMessage> pendingReliable = new Dictionary<string, PendingMessage>();
        private readonly HashSet<string> receivedReliable = new HashSet<string>();
        private TcpClient tcpClient;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread receiveThread;
        private volatile bool running;
        private int nextMessageId = 1;

        public bool IsConnected
        {
            get { return tcpClient != null && tcpClient.Connected; }
        }

        public string ClientId { get; private set; }

        public void Connect(string host, int port)
        {
            Dispose();
            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            tcpClient.Connect(host, port);
            NetworkStream stream = tcpClient.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            running = true;
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        public void Send(OnlineMessage message)
        {
            if (writer == null)
            {
                return;
            }

            writer.WriteLine(JsonUtility.ToJson(message));
        }

        public void SendReliable(OnlineMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(message.messageId))
            {
                message.messageId = string.Format("{0}-{1}", ClientId, nextMessageId++);
            }

            lock (pendingReliable)
            {
                pendingReliable[message.messageId] = new PendingMessage
                {
                    Message = message,
                    LastSentAt = Time.realtimeSinceStartup,
                    Attempts = 1
                };
            }
            Send(message);
        }

        public void UpdateRetries()
        {
            if (writer == null || pendingReliable.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            List<string> expired = null;
            List<KeyValuePair<string, PendingMessage>> snapshot;
            lock (pendingReliable)
            {
                snapshot = pendingReliable.ToList();
            }

            foreach (KeyValuePair<string, PendingMessage> entry in snapshot)
            {
                PendingMessage pending = entry.Value;
                if (now - pending.LastSentAt < RetryIntervalSeconds)
                {
                    continue;
                }

                if (pending.Attempts >= MaxRetryAttempts)
                {
                    if (expired == null)
                    {
                        expired = new List<string>();
                    }

                    expired.Add(entry.Key);
                    lock (inbound)
                    {
                        inbound.Enqueue(new OnlineMessage { type = "ERROR", message = "网络消息多次重传失败，请重新进入房间。" });
                    }
                    continue;
                }

                pending.Attempts++;
                pending.LastSentAt = now;
                Send(pending.Message);
            }

            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                {
                    lock (pendingReliable)
                    {
                        pendingReliable.Remove(expired[i]);
                    }
                }
            }
        }

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
                if (tcpClient != null)
                {
                    tcpClient.Close();
                }
            }
            catch
            {
            }

            tcpClient = null;
            reader = null;
            writer = null;
        }

        private void ReceiveLoop()
        {
            try
            {
                while (running && reader != null)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    OnlineMessage message = JsonUtility.FromJson<OnlineMessage>(line);
                    if (message != null)
                    {
                        if (message.type == "ACK")
                        {
                            lock (pendingReliable)
                            {
                                pendingReliable.Remove(message.ackId);
                            }
                            continue;
                        }

                        if (message.type == "WELCOME")
                        {
                            ClientId = message.clientId;
                        }

                        if (!string.IsNullOrEmpty(message.messageId))
                        {
                            Send(new OnlineMessage { type = "CLIENT_ACK", ackId = message.messageId, roomKey = message.roomKey });
                            lock (receivedReliable)
                            {
                                if (receivedReliable.Contains(message.messageId))
                                {
                                    continue;
                                }

                                receivedReliable.Add(message.messageId);
                            }
                        }

                        lock (inbound)
                        {
                            inbound.Enqueue(message);
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
    }
}
