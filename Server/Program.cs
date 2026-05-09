using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

const int defaultPort = 53333;
TimeSpan inactiveRoomLifetime = TimeSpan.FromMinutes(30);
int port = args.Length > 0 && int.TryParse(args[0], out int parsedPort) ? parsedPort : defaultPort;

TcpListener listener = new TcpListener(IPAddress.Any, port);
ConcurrentDictionary<string, Room> rooms = new ConcurrentDictionary<string, Room>(StringComparer.OrdinalIgnoreCase);
ConcurrentDictionary<string, ClientPeer> clients = new ConcurrentDictionary<string, ClientPeer>(StringComparer.OrdinalIgnoreCase);
Console.WriteLine($"SweetJumpJump TCP server listening on 0.0.0.0:{port}");
listener.Start();

_ = Task.Run(async () =>
{
    while (true)
    {
        foreach (Room room in rooms.Values)
        {
            await room.RetryPendingAsync();
        }

        await Task.Delay(250);
    }
});

_ = Task.Run(async () =>
{
    while (true)
    {
        DateTime now = DateTime.UtcNow;
        foreach (Room room in rooms.Values)
        {
            if (room.Count == 0 || now - room.LastActivityUtc > inactiveRoomLifetime)
            {
                await ClearRoomAsync(room, "房间长时间不活跃，已自动清空。");
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(30));
    }
});

while (true)
{
    TcpClient tcpClient = await listener.AcceptTcpClientAsync();
    _ = Task.Run(() => HandleClientAsync(new ClientPeer(tcpClient)));
}

async Task HandleClientAsync(ClientPeer peer)
{
    clients[peer.Id] = peer;
    Console.WriteLine($"client connected: {peer.Id}");
    await peer.SendAsync(new { type = "WELCOME", clientId = peer.Id });
    await SendDiscoveriesAsync(peer);

    try
    {
        while (true)
        {
            string? line = await peer.Reader.ReadLineAsync();
            if (line == null)
            {
                break;
            }

            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            string type = GetString(root, "type").ToUpperInvariant();

            if (type == "CLIENT_ACK")
            {
                peer.Room?.Acknowledge(peer.Id, GetString(root, "ackId"));
                continue;
            }

            await AcknowledgeCommandAsync(peer, root);
            if (!peer.MarkCommandIfNew(GetString(root, "messageId")))
            {
                continue;
            }

            switch (type)
            {
                case "AUTH":
                    peer.PlayerToken = GetString(root, "playerToken");
                    peer.PlayerName = NormalizePlayerName(GetString(root, "playerName"), peer.Id);
                    break;
                case "CREATE":
                    peer.PlayerToken = string.IsNullOrWhiteSpace(peer.PlayerToken) ? GetString(root, "playerToken") : peer.PlayerToken;
                    peer.PlayerName = NormalizePlayerName(GetString(root, "playerName"), peer.Id);
                    await CreateRoomAsync(peer);
                    break;
                case "REJOIN":
                    await RejoinRoomAsync(peer, GetString(root, "roomKey"), GetString(root, "playerToken"), GetString(root, "playerName"), GetInt(root, "lastActionSeq"));
                    break;
                case "JOIN":
                    await JoinRoomAsync(peer, GetString(root, "roomKey"));
                    break;
                case "JOIN_REQUEST":
                    peer.PlayerToken = string.IsNullOrWhiteSpace(peer.PlayerToken) ? GetString(root, "playerToken") : peer.PlayerToken;
                    await RequestJoinRoomAsync(peer, GetString(root, "roomKey"), GetString(root, "playerName"));
                    break;
                case "JOIN_APPROVE":
                    await ApproveJoinRoomAsync(peer, GetString(root, "requestClientId"));
                    break;
                case "JOIN_REJECT":
                    await RejectJoinRoomAsync(peer, GetString(root, "requestClientId"));
                    break;
                case "READY":
                    await SetReadyAsync(peer, GetBool(root, "ok"));
                    break;
                case "SET_AI":
                    await SetAiAsync(peer, GetString(root, "slot"), GetBool(root, "ok"));
                    break;
                case "START":
                    await StartRoomAsync(peer);
                    break;
                case "SELECT":
                    await BroadcastActionAsync(peer, new
                    {
                        type = "SELECT",
                        roomKey = peer.Room?.Key ?? string.Empty,
                        clientId = peer.Id,
                        pieceId = GetInt(root, "pieceId"),
                        q = GetInt(root, "q"),
                        r = GetInt(root, "r"),
                        ok = GetBool(root, "ok"),
                        message = GetString(root, "message")
                    });
                    break;
                case "MOVE":
                    await BroadcastActionAsync(peer, new
                    {
                        type = "MOVE",
                        roomKey = peer.Room?.Key ?? string.Empty,
                        clientId = peer.Id,
                        pieceId = GetInt(root, "pieceId"),
                        q = GetInt(root, "q"),
                        r = GetInt(root, "r")
                    });
                    break;
                case "FINISH":
                    await BroadcastActionAsync(peer, new { type = "FINISH", roomKey = peer.Room?.Key ?? string.Empty, clientId = peer.Id });
                    break;
                case "PASS":
                    await BroadcastActionAsync(peer, new { type = "PASS", roomKey = peer.Room?.Key ?? string.Empty, clientId = peer.Id });
                    break;
            }
        }
    }
    catch (Exception exception)
    {
        Console.WriteLine($"client {peer.Id} error: {exception.Message}");
    }
    finally
    {
        clients.TryRemove(peer.Id, out _);
        await MarkDisconnectedAsync(peer);
        peer.Dispose();
        Console.WriteLine($"client disconnected: {peer.Id}");
    }
}

async Task AcknowledgeCommandAsync(ClientPeer peer, JsonElement root)
{
    string messageId = GetString(root, "messageId");
    if (!string.IsNullOrEmpty(messageId))
    {
        await peer.SendAsync(new { type = "ACK", ackId = messageId });
    }
}

async Task CreateRoomAsync(ClientPeer peer)
{
    await RemoveFromRoomAsync(peer);
    string key;
    do
    {
        key = Random.Shared.Next(1000, 10000).ToString();
    }
    while (rooms.ContainsKey(key));

    peer.PlayerName = NormalizePlayerName(peer.PlayerName, peer.Id);
    Room room = new Room(key, peer.Id);
    rooms[key] = room;
    room.Add(peer);
    await SendRoomAssignedAsync(peer, room);
    await BroadcastLobbyAsync(room);
    await BroadcastDiscoveriesAsync();
}

async Task JoinRoomAsync(ClientPeer peer, string key)
{
    await RemoveFromRoomAsync(peer);
    if (!rooms.TryGetValue(key, out Room? room))
    {
        await peer.SendAsync(new { type = "ERROR", message = "房间不存在。" });
        return;
    }

    if (!room.Add(peer))
    {
        await peer.SendAsync(new { type = "ERROR", message = "房间已满。" });
        return;
    }

    await SendRoomAssignedAsync(peer, room);
    await BroadcastLobbyAsync(room);
    await BroadcastDiscoveriesAsync();
}

async Task RequestJoinRoomAsync(ClientPeer peer, string key, string playerName)
{
    if (peer.Room != null)
    {
        await peer.SendAsync(new { type = "ERROR", message = "你已经在房间里。" });
        return;
    }

    if (!rooms.TryGetValue(key, out Room? room))
    {
        await peer.SendAsync(new { type = "ERROR", message = "房间不存在。" });
        await SendDiscoveriesAsync(peer);
        return;
    }

    ClientPeer? host = room.HostPeer();
    if (host == null)
    {
        await peer.SendAsync(new { type = "ERROR", message = "房主不在线。" });
        return;
    }

    peer.PlayerName = NormalizePlayerName(playerName, peer.Id);
    room.Touch();
    room.AddJoinRequest(peer);
    await peer.SendAsync(new { type = "JOIN_PENDING", roomKey = room.Key, message = "已发送加入申请，等待房主同意。" });
    await host.SendAsync(new
    {
        type = "JOIN_REQUEST",
        roomKey = room.Key,
        requestClientId = peer.Id,
        requestPlayerName = peer.PlayerName,
        message = $"{peer.PlayerName} 想加入房间。"
    });
}

async Task RejoinRoomAsync(ClientPeer peer, string key, string playerToken, string playerName, int lastActionSeq)
{
    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(playerToken))
    {
        return;
    }

    if (!rooms.TryGetValue(key, out Room? room))
    {
        await peer.SendAsync(new { type = "ROOM_CLEARED", message = "房间已经不存在。" });
        return;
    }

    if (!room.Reconnect(peer, playerToken, NormalizePlayerName(playerName, peer.Id)))
    {
        await peer.SendAsync(new { type = "ERROR", message = "无法恢复座位，请重新加入房间。" });
        return;
    }

    await SendRoomAssignedAsync(peer, room);
    await BroadcastLobbyAsync(room);
    foreach (object action in room.ActionsAfter(lastActionSeq))
    {
        await peer.SendAsync(action);
    }
}

async Task ApproveJoinRoomAsync(ClientPeer hostPeer, string requestClientId)
{
    Room? room = hostPeer.Room;
    if (room == null)
    {
        return;
    }

    if (room.HostId != hostPeer.Id)
    {
        await hostPeer.SendAsync(new { type = "ERROR", message = "只有房主可以同意加入。" });
        return;
    }

    ClientPeer? requester = room.TakeJoinRequest(requestClientId);
    if (requester == null || !clients.ContainsKey(requester.Id))
    {
        await hostPeer.SendAsync(new { type = "ERROR", message = "申请人已经离线。" });
        return;
    }

    await RemoveFromRoomAsync(requester);
    if (!room.Add(requester))
    {
        await requester.SendAsync(new { type = "ERROR", message = "房间已满。" });
        await hostPeer.SendAsync(new { type = "ERROR", message = "房间已满，无法加入。" });
        return;
    }

    await SendRoomAssignedAsync(requester, room);
    await BroadcastLobbyAsync(room);
    await BroadcastDiscoveriesAsync();
}

async Task RejectJoinRoomAsync(ClientPeer hostPeer, string requestClientId)
{
    Room? room = hostPeer.Room;
    if (room == null)
    {
        return;
    }

    if (room.HostId != hostPeer.Id)
    {
        await hostPeer.SendAsync(new { type = "ERROR", message = "只有房主可以拒绝加入。" });
        return;
    }

    ClientPeer? requester = room.TakeJoinRequest(requestClientId);
    if (requester != null && clients.ContainsKey(requester.Id))
    {
        await requester.SendAsync(new { type = "JOIN_REJECTED", roomKey = room.Key, message = "房主拒绝了加入申请。" });
    }
}

async Task SetReadyAsync(ClientPeer peer, bool ready)
{
    if (peer.Room == null)
    {
        return;
    }

    peer.Ready = ready;
    await BroadcastLobbyAsync(peer.Room);
}

async Task SetAiAsync(ClientPeer peer, string slot, bool enabled)
{
    Room? room = peer.Room;
    if (room == null)
    {
        return;
    }

    if (room.HostId != peer.Id)
    {
        await peer.SendAsync(new { type = "ERROR", message = "只有房主可以设置人机位置。" });
        return;
    }

    room.SetAi(slot, enabled);
    await BroadcastLobbyAsync(room);
}

async Task StartRoomAsync(ClientPeer peer)
{
    Room? room = peer.Room;
    if (room == null)
    {
        return;
    }

    if (room.HostId != peer.Id)
    {
        await peer.SendAsync(new { type = "ERROR", message = "只有房主可以开始。" });
        return;
    }

    ClientPeer[] peers = room.ConnectedPeersSnapshot();
    if (peers.Length < 1)
    {
        await peer.SendAsync(new { type = "ERROR", message = "至少需要 1 位玩家。" });
        return;
    }

    if (peers.Any(value => !value.Ready))
    {
        await peer.SendAsync(new { type = "ERROR", message = "还有玩家没有准备。" });
        return;
    }

    string slots = string.Join(",", peers.Select(value => value.Slot));
    string aiSlots = string.Join(",", room.AiSlotsSnapshot());
    room.ClearActionLog();
    await room.BroadcastReliableAsync(new { type = "START", roomKey = room.Key, slots, aiSlots });
}

async Task BroadcastActionAsync(ClientPeer peer, object message)
{
    if (peer.Room == null)
    {
        return;
    }

    await peer.Room.BroadcastActionReliableAsync(message);
}

async Task SendRoomAssignedAsync(ClientPeer peer, Room room)
{
    await peer.SendAsync(new
    {
        type = "ROOM",
        roomKey = room.Key,
        clientId = peer.Id,
        hostId = room.HostId,
        slot = peer.Slot,
        aiSlots = string.Join(",", room.AiSlotsSnapshot()),
        lastActionSeq = room.LastActionSeq,
        message = room.Summary()
    });
}

async Task BroadcastLobbyAsync(Room room)
{
    await room.BroadcastReliableAsync(new
    {
        type = "LOBBY",
        roomKey = room.Key,
        hostId = room.HostId,
        count = room.Count,
        aiSlots = string.Join(",", room.AiSlotsSnapshot()),
        message = room.Summary()
    });
}

async Task RemoveFromRoomAsync(ClientPeer peer)
{
    Room? room = peer.Room;
    if (room == null)
    {
        return;
    }

    room.Remove(peer);
    peer.Room = null;
    peer.Ready = false;

    if (room.Count == 0)
    {
        rooms.TryRemove(room.Key, out _);
    }
    else
    {
        room.EnsureHost();
        await BroadcastLobbyAsync(room);
    }

    await BroadcastDiscoveriesAsync();
}

async Task MarkDisconnectedAsync(ClientPeer peer)
{
    Room? room = peer.Room;
    if (room == null)
    {
        return;
    }

    room.MarkDisconnected(peer);
    await BroadcastLobbyAsync(room);
    await BroadcastDiscoveriesAsync();
}

async Task BroadcastDiscoveriesAsync()
{
    foreach (ClientPeer peer in clients.Values)
    {
        if (peer.Room == null)
        {
            try
            {
                await SendDiscoveriesAsync(peer);
            }
            catch
            {
            }
        }
    }
}

async Task SendDiscoveriesAsync(ClientPeer peer)
{
    Room[] openRooms = rooms.Values
        .Where(room => room.CanDiscover)
        .OrderByDescending(room => room.LastActivityUtc)
        .Take(6)
        .ToArray();

    string roomKeys = string.Join(",", openRooms.Select(room => room.Key));
    string summaries = string.Join("\n\n", openRooms.Select(room => $"房间 {room.Key} · {room.Count}/6 人\n{room.Summary()}"));
    await peer.SendAsync(new
    {
        type = "DISCOVER",
        roomKey = openRooms.Length > 0 ? openRooms[0].Key : string.Empty,
        count = openRooms.Length,
        slots = roomKeys,
        message = summaries
    });
}

async Task ClearRoomAsync(Room room, string message)
{
    if (!rooms.TryRemove(room.Key, out _))
    {
        return;
    }

    foreach (ClientPeer peer in room.ConnectedPeersSnapshot())
    {
        peer.Room = null;
        peer.Ready = false;
        await peer.SendAsync(new { type = "ROOM_CLEARED", message });
    }

    room.ClearJoinRequests();
    await BroadcastDiscoveriesAsync();
}

static string GetString(JsonElement root, string name)
{
    return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? string.Empty
        : string.Empty;
}

static bool GetBool(JsonElement root, string name)
{
    return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}

static int GetInt(JsonElement root, string name)
{
    return root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result) ? result : 0;
}

static string NormalizePlayerName(string value, string fallback)
{
    string name = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    return name.Length > 14 ? name[..14] : name;
}

sealed class ClientPeer : IDisposable
{
    private readonly TcpClient tcpClient;
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private readonly StreamWriter writer;

    public ClientPeer(TcpClient tcpClient)
    {
        this.tcpClient = tcpClient;
        tcpClient.NoDelay = true;
        NetworkStream stream = tcpClient.GetStream();
        Reader = new StreamReader(stream, Encoding.UTF8);
        writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        Id = Guid.NewGuid().ToString("N")[..8];
    }

    public string Id { get; }
    public StreamReader Reader { get; }
    public Room? Room { get; set; }
    public string Slot { get; set; } = string.Empty;
    public string PlayerToken { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public bool Ready { get; set; }
    public bool Connected { get; set; } = true;
    public DateTime DisconnectedAtUtc { get; set; } = DateTime.MinValue;

    private readonly HashSet<string> handledCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool MarkCommandIfNew(string messageId)
    {
        if (string.IsNullOrEmpty(messageId))
        {
            return true;
        }

        lock (handledCommands)
        {
            if (handledCommands.Contains(messageId))
            {
                return false;
            }

            handledCommands.Add(messageId);
            return true;
        }
    }

    public async Task SendAsync(object message)
    {
        string json = JsonSerializer.Serialize(message);
        await sendLock.WaitAsync();
        try
        {
            await writer.WriteLineAsync(json);
        }
        finally
        {
            sendLock.Release();
        }
    }

    public void Dispose()
    {
        tcpClient.Dispose();
        sendLock.Dispose();
    }
}

sealed class Room
{
    private sealed class ReliableEnvelope
    {
        public string MessageId = string.Empty;
        public object Message = new();
        public HashSet<string> PendingClientIds = new HashSet<string>();
        public DateTime LastSentAtUtc = DateTime.MinValue;
        public int Attempts;
    }

    private sealed class ActionLogEntry
    {
        public int Seq;
        public object Message = new();
    }

    private static readonly string[] SlotOrder =
    {
        "Bottom",
        "Top",
        "BottomLeft",
        "TopRight",
        "TopLeft",
        "BottomRight"
    };

    private readonly object gate = new object();
    private readonly List<ClientPeer> peers = new List<ClientPeer>();
    private readonly HashSet<string> aiSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClientPeer> joinRequests = new Dictionary<string, ClientPeer>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReliableEnvelope> pendingReliable = new Dictionary<string, ReliableEnvelope>();
    private readonly List<ActionLogEntry> actionLog = new List<ActionLogEntry>();
    private int nextBroadcastId = 1;
    private int nextActionSeq = 1;

    public Room(string key, string hostId)
    {
        Key = key;
        HostId = hostId;
        foreach (string slot in SlotOrder)
        {
            aiSlots.Add(slot);
        }
    }

    public string Key { get; }
    public string HostId { get; private set; }
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;
    public int LastActionSeq
    {
        get
        {
            lock (gate)
            {
                return nextActionSeq - 1;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return peers.Count;
            }
        }
    }

    public bool CanDiscover
    {
        get
        {
            lock (gate)
            {
                peers.RemoveAll(value => !value.Connected && IsSeatExpired(value));
                return peers.Any(value => value.Connected) && peers.Count < SlotOrder.Length;
            }
        }
    }

    public void Touch()
    {
        lock (gate)
        {
            LastActivityUtc = DateTime.UtcNow;
        }
    }

    public bool Add(ClientPeer peer)
    {
        lock (gate)
        {
            string? slot = SlotOrder.FirstOrDefault(value => peers.All(peerValue => peerValue.Slot != value || !peerValue.Connected && IsSeatExpired(peerValue)));
            if (slot == null)
            {
                return false;
            }

            peers.RemoveAll(value => !value.Connected && IsSeatExpired(value));
            peer.Room = this;
            peer.Slot = slot;
            peer.Ready = false;
            peer.Connected = true;
            peer.DisconnectedAtUtc = DateTime.MinValue;
            aiSlots.Remove(slot);
            peers.Add(peer);
            joinRequests.Remove(peer.Id);
            LastActivityUtc = DateTime.UtcNow;
            return true;
        }
    }

    public void Remove(ClientPeer peer)
    {
        lock (gate)
        {
            peers.Remove(peer);
            if (!string.IsNullOrEmpty(peer.Slot))
            {
                aiSlots.Add(peer.Slot);
            }
            joinRequests.Remove(peer.Id);
            LastActivityUtc = DateTime.UtcNow;
        }
    }

    public void MarkDisconnected(ClientPeer peer)
    {
        lock (gate)
        {
            ClientPeer? seat = peers.FirstOrDefault(value => value.Id == peer.Id);
            if (seat == null)
            {
                return;
            }

            seat.Connected = false;
            seat.DisconnectedAtUtc = DateTime.UtcNow;
            LastActivityUtc = DateTime.UtcNow;
        }
    }

    public bool Reconnect(ClientPeer peer, string playerToken, string playerName)
    {
        lock (gate)
        {
            ClientPeer? oldPeer = peers.FirstOrDefault(value => !value.Connected && value.PlayerToken == playerToken && !IsSeatExpired(value));
            if (oldPeer == null)
            {
                return false;
            }

            string slot = oldPeer.Slot;
            bool ready = oldPeer.Ready;
            peers.Remove(oldPeer);
            peer.Room = this;
            peer.Slot = slot;
            peer.Ready = ready;
            peer.PlayerToken = playerToken;
            peer.PlayerName = playerName;
            peer.Connected = true;
            peer.DisconnectedAtUtc = DateTime.MinValue;
            peers.Add(peer);
            if (HostId == oldPeer.Id)
            {
                HostId = peer.Id;
            }
            LastActivityUtc = DateTime.UtcNow;
            return true;
        }
    }

    public void SetAi(string slot, bool enabled)
    {
        lock (gate)
        {
            if (!SlotOrder.Contains(slot) || peers.Any(peer => peer.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (enabled)
            {
                aiSlots.Add(slot);
            }
            else
            {
                aiSlots.Remove(slot);
            }
            LastActivityUtc = DateTime.UtcNow;
        }
    }

    public void AddJoinRequest(ClientPeer peer)
    {
        lock (gate)
        {
            joinRequests[peer.Id] = peer;
            LastActivityUtc = DateTime.UtcNow;
        }
    }

    public ClientPeer? TakeJoinRequest(string clientId)
    {
        lock (gate)
        {
            if (!joinRequests.TryGetValue(clientId, out ClientPeer? peer))
            {
                return null;
            }

            joinRequests.Remove(clientId);
            LastActivityUtc = DateTime.UtcNow;
            return peer;
        }
    }

    public void ClearJoinRequests()
    {
        lock (gate)
        {
            joinRequests.Clear();
        }
    }

    public ClientPeer? HostPeer()
    {
        lock (gate)
        {
            return peers.FirstOrDefault(peer => peer.Id == HostId && peer.Connected);
        }
    }

    public string[] AiSlotsSnapshot()
    {
        lock (gate)
        {
            return SlotOrder.Where(slot => aiSlots.Contains(slot)).ToArray();
        }
    }

    public void EnsureHost()
    {
        lock (gate)
        {
            if (peers.All(value => value.Id != HostId || !value.Connected) && peers.Any(value => value.Connected))
            {
                HostId = peers.First(value => value.Connected).Id;
            }
            LastActivityUtc = DateTime.UtcNow;
        }
    }

    public ClientPeer[] PeersSnapshot()
    {
        lock (gate)
        {
            return peers.ToArray();
        }
    }

    public ClientPeer[] ConnectedPeersSnapshot()
    {
        lock (gate)
        {
            return peers.Where(peer => peer.Connected).ToArray();
        }
    }

    public async Task BroadcastReliableAsync(object message)
    {
        ReliableEnvelope envelope;
        ClientPeer[] targets;
        lock (gate)
        {
            targets = peers.Where(peer => peer.Connected).ToArray();
            string messageId = $"{Key}-{nextBroadcastId++}";
            object stamped = StampMessage(message, messageId);
            envelope = new ReliableEnvelope
            {
                MessageId = messageId,
                Message = stamped,
                PendingClientIds = targets.Select(peer => peer.Id).ToHashSet(StringComparer.OrdinalIgnoreCase),
                LastSentAtUtc = DateTime.UtcNow,
                Attempts = 1
            };
            pendingReliable[messageId] = envelope;
            LastActivityUtc = DateTime.UtcNow;
        }

        foreach (ClientPeer peer in targets)
        {
            await peer.SendAsync(envelope.Message);
        }
    }

    public async Task BroadcastActionReliableAsync(object message)
    {
        object stamped;
        lock (gate)
        {
            int seq = nextActionSeq++;
            stamped = StampMessage(message, string.Empty, seq);
            actionLog.Add(new ActionLogEntry { Seq = seq, Message = stamped });
            if (actionLog.Count > 200)
            {
                actionLog.RemoveRange(0, actionLog.Count - 200);
            }
        }

        await BroadcastReliableAsync(stamped);
    }

    public object[] ActionsAfter(int seq)
    {
        lock (gate)
        {
            return actionLog.Where(entry => entry.Seq > seq).Select(entry => entry.Message).ToArray();
        }
    }

    public void ClearActionLog()
    {
        lock (gate)
        {
            actionLog.Clear();
            nextActionSeq = 1;
        }
    }

    public void Acknowledge(string clientId, string messageId)
    {
        if (string.IsNullOrEmpty(messageId))
        {
            return;
        }

        lock (gate)
        {
            if (!pendingReliable.TryGetValue(messageId, out ReliableEnvelope? envelope))
            {
                return;
            }

            envelope.PendingClientIds.Remove(clientId);
            if (envelope.PendingClientIds.Count == 0)
            {
                pendingReliable.Remove(messageId);
            }
        }
    }

    public async Task RetryPendingAsync()
    {
        List<(ClientPeer Peer, object Message)> sends = new List<(ClientPeer, object)>();
        lock (gate)
        {
            DateTime now = DateTime.UtcNow;
            foreach (ReliableEnvelope envelope in pendingReliable.Values.ToArray())
            {
                if ((now - envelope.LastSentAtUtc).TotalMilliseconds < 750)
                {
                    continue;
                }

                if (envelope.Attempts >= 20)
                {
                    pendingReliable.Remove(envelope.MessageId);
                    continue;
                }

                envelope.Attempts++;
                envelope.LastSentAtUtc = now;
                foreach (ClientPeer peer in peers.Where(peer => peer.Connected))
                {
                    if (envelope.PendingClientIds.Contains(peer.Id))
                    {
                        sends.Add((peer, envelope.Message));
                    }
                }
            }
        }

        foreach ((ClientPeer peer, object message) in sends)
        {
            await peer.SendAsync(message);
        }
    }

    public string Summary()
    {
        lock (gate)
        {
            IEnumerable<string> humanLines = peers.Select(peer => $"{peer.Slot}: {DisplayName(peer)} {(peer.Connected ? (peer.Ready ? "已准备" : "未准备") : "离线")}{(peer.Id == HostId ? " 房主" : string.Empty)}");
            IEnumerable<string> aiLines = SlotOrder.Where(slot => aiSlots.Contains(slot)).Select(slot => $"{slot}: 高级人机");
            IEnumerable<string> emptyLines = SlotOrder
                .Where(slot => peers.All(peer => peer.Slot != slot) && !aiSlots.Contains(slot))
                .Select(slot => $"{slot}: 空位");
            return string.Join("\n", humanLines.Concat(aiLines).Concat(emptyLines));
        }
    }

    private static bool IsSeatExpired(ClientPeer peer)
    {
        return peer.DisconnectedAtUtc != DateTime.MinValue && DateTime.UtcNow - peer.DisconnectedAtUtc > TimeSpan.FromMinutes(3);
    }

    private static string DisplayName(ClientPeer peer)
    {
        return string.IsNullOrWhiteSpace(peer.PlayerName) ? peer.Id : peer.PlayerName;
    }

    private static object StampMessage(object source, string messageId, int actionSeq = 0)
    {
        string json = JsonSerializer.Serialize(source);
        Dictionary<string, object?>? values = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        values ??= new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(messageId))
        {
            values["messageId"] = messageId;
        }
        if (actionSeq > 0)
        {
            values["actionSeq"] = actionSeq;
        }
        return values;
    }
}
