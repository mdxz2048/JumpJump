using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using SweetJumpJump.Core;

const string adminPassword = "xiaozhi2048-admin";
const int defaultPort = 53333;
TimeSpan createRoomCooldown = TimeSpan.FromSeconds(10);

int port = args.Length > 0 && int.TryParse(args[0], out int parsedPort) ? parsedPort : defaultPort;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

WebApplication app = builder.Build();
JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter() }
};

ConcurrentDictionary<string, GameRoom> rooms = new(StringComparer.OrdinalIgnoreCase);
ConcurrentDictionary<string, ClientPeer> clients = new(StringComparer.OrdinalIgnoreCase);
ConcurrentDictionary<string, PlayerAccount> playerAccounts = new(StringComparer.OrdinalIgnoreCase);
playerAccounts["tian"] = new PlayerAccount("tian", "tian", "tian");
playerAccounts["mdxz"] = new PlayerAccount("mdxz", "mdxz", "mdxz");
string webRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "Web"));
if (!Directory.Exists(webRoot))
{
    webRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "Web"));
}

app.UseWebSockets();
if (Directory.Exists(webRoot))
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(webRoot) });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(webRoot) });
}

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
    ClientPeer peer = new(socket);
    clients[peer.Id] = peer;
    await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("WELCOME") { ClientId = peer.Id }, jsonOptions);
    await HandleClientAsync(peer);
});

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "SweetJumpJumpServer" }));
Console.WriteLine($"SweetJumpJump WebSocket server listening on http://0.0.0.0:{port}");
await app.RunAsync();

async Task HandleClientAsync(ClientPeer peer)
{
    byte[] buffer = new byte[32 * 1024];
    try
    {
        while (peer.Socket.State == WebSocketState.Open)
        {
            string? json = await ReceiveTextAsync(peer.Socket, buffer);
            if (json == null)
            {
                break;
            }

            ClientCommand? command = JsonSerializer.Deserialize<ClientCommand>(json, jsonOptions);
            if (command == null || string.IsNullOrWhiteSpace(command.Type))
            {
                continue;
            }

            await HandleCommandAsync(peer, command);
        }
    }
    catch (Exception exception)
    {
        Console.WriteLine($"client {peer.Id} error: {exception.Message}");
    }
    finally
    {
        clients.TryRemove(peer.Id, out _);
        GameRoom? room = peer.Room;
        if (room != null)
        {
            await room.RemovePeerAsync(peer, jsonOptions);
            if (room.IsEmpty)
            {
                rooms.TryRemove(room.Key, out _);
            }
            await SendRoomListToAllAsync();
        }
    }
}

async Task HandleCommandAsync(ClientPeer peer, ClientCommand command)
{
    string type = command.Type.ToUpperInvariant();
    if (type == "AUTH")
    {
        string account = command.Account.Trim();
        if (string.IsNullOrWhiteSpace(account) || !playerAccounts.TryGetValue(account, out PlayerAccount? playerAccount) || playerAccount.Password != command.Password)
        {
            await SendErrorAsync(peer, "账号或密码不正确。");
            return;
        }

        if (playerAccount.Disabled)
        {
            await SendErrorAsync(peer, "账号已被禁用，请联系管理员。");
            return;
        }

        if (clients.Values.Any(value => value.Id != peer.Id && value.Authenticated && !value.IsAdmin && value.Account.Equals(account, StringComparison.OrdinalIgnoreCase) && value.Socket.State == WebSocketState.Open))
        {
            await SendErrorAsync(peer, "这个账号已经在其他网页登录。");
            return;
        }

        peer.Authenticated = true;
        peer.Account = account;
        peer.Name = NormalizeName(playerAccount.DisplayName, account);
        await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("AUTH_OK") { ClientId = peer.Id, Account = peer.Account, Name = peer.Name, Message = "进入成功。" }, jsonOptions);
        await SendRoomListAsync(peer);
        return;
    }

    if (type == "ADMIN_AUTH")
    {
        if (command.Password != adminPassword)
        {
            await SendErrorAsync(peer, "管理员密钥不正确。");
            return;
        }

        peer.Authenticated = true;
        peer.IsAdmin = true;
        peer.Name = NormalizeName(command.Name, "管理员");
        await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ADMIN_OK") { ClientId = peer.Id, Message = "管理员已进入。" }, jsonOptions);
        await SendAdminSnapshotAsync(peer);
        return;
    }

    if (!peer.Authenticated)
    {
        await SendErrorAsync(peer, "请先输入访问密码。");
        return;
    }

    switch (type)
    {
        case "CREATE":
            await CreateRoomAsync(peer, command);
            break;
        case "JOIN":
            await JoinRoomAsync(peer, command.RoomKey);
            break;
        case "LIST":
            await SendRoomListAsync(peer);
            break;
        case "ADMIN_SNAPSHOT":
            await SendAdminSnapshotAsync(peer);
            break;
        case "ADD_PLAYER":
            await AddPlayerAsync(peer, command);
            break;
        case "REMOVE_PLAYER":
            await RemovePlayerAsync(peer, command);
            break;
        case "DISABLE_PLAYER":
            await DisablePlayerAsync(peer, command);
            break;
        case "ENABLE_PLAYER":
            await EnablePlayerAsync(peer, command);
            break;
        case "SELECT_SLOT":
            await RequireRoom(peer, room => room.SelectSlotAsync(peer, command.Slot, jsonOptions));
            break;
        case "UPDATE_NICKNAME":
            await UpdateNicknameAsync(peer, command.Name);
            break;
        case "START":
            await RequireRoom(peer, room => room.StartAsync(peer, jsonOptions));
            break;
        case "SELECT":
            await RequireRoom(peer, room => room.SelectAsync(peer, command.PieceId, jsonOptions));
            break;
        case "MOVE":
            await RequireRoom(peer, room => room.MoveAsync(peer, command.PieceId, new HexCoord(command.Q, command.R), jsonOptions));
            break;
        case "FINISH":
            await RequireRoom(peer, room => room.FinishAsync(peer, jsonOptions));
            break;
        case "PASS":
            await RequireRoom(peer, room => room.PassAsync(peer, jsonOptions));
            break;
        default:
            await SendErrorAsync(peer, "未知指令。");
            break;
    }
}

async Task RemovePlayerAsync(ClientPeer peer, ClientCommand command)
{
    if (!peer.IsAdmin)
    {
        await SendErrorAsync(peer, "需要管理员权限。");
        return;
    }

    string account = command.Account.Trim();
    if (!playerAccounts.TryRemove(account, out _))
    {
        await SendErrorAsync(peer, "账号不存在。");
        return;
    }

    // Kick any connected session for this account
    foreach (ClientPeer target in clients.Values.Where(c => c.Account.Equals(account, StringComparison.OrdinalIgnoreCase) && c.Socket.State == WebSocketState.Open))
    {
        await SocketJson.SendAsync(target.Socket, new ServerEnvelope("ERROR") { Message = "你的账号已被管理员删除。" }, jsonOptions);
        await target.Socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "account removed", CancellationToken.None);
    }

    await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ADMIN_NOTICE") { Message = $"已删除账号 {account}。" }, jsonOptions);
    await SendAdminSnapshotAsync(peer);
}

async Task DisablePlayerAsync(ClientPeer peer, ClientCommand command)
{
    if (!peer.IsAdmin)
    {
        await SendErrorAsync(peer, "需要管理员权限。");
        return;
    }

    string account = command.Account.Trim();
    if (!playerAccounts.TryGetValue(account, out PlayerAccount? playerAccount))
    {
        await SendErrorAsync(peer, "账号不存在。");
        return;
    }

    playerAccount.Disabled = true;

    // Kick active sessions for this account
    foreach (ClientPeer target in clients.Values.Where(c => c.Account.Equals(account, StringComparison.OrdinalIgnoreCase) && c.Socket.State == WebSocketState.Open))
    {
        await SocketJson.SendAsync(target.Socket, new ServerEnvelope("ERROR") { Message = "你的账号已被管理员禁用。" }, jsonOptions);
        await target.Socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "account disabled", CancellationToken.None);
    }

    await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ADMIN_NOTICE") { Message = $"已禁用账号 {account}。" }, jsonOptions);
    await SendAdminSnapshotAsync(peer);
}

async Task EnablePlayerAsync(ClientPeer peer, ClientCommand command)
{
    if (!peer.IsAdmin)
    {
        await SendErrorAsync(peer, "需要管理员权限。");
        return;
    }

    string account = command.Account.Trim();
    if (!playerAccounts.TryGetValue(account, out PlayerAccount? playerAccount))
    {
        await SendErrorAsync(peer, "账号不存在。");
        return;
    }

    playerAccount.Disabled = false;
    await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ADMIN_NOTICE") { Message = $"已启用账号 {account}。" }, jsonOptions);
    await SendAdminSnapshotAsync(peer);
}

async Task AddPlayerAsync(ClientPeer peer, ClientCommand command)
{
    if (!peer.IsAdmin)
    {
        await SendErrorAsync(peer, "需要管理员权限。");
        return;
    }

    string account = command.Account.Trim();
    string password = command.Password.Trim();
    if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
    {
        await SendErrorAsync(peer, "账号和密码不能为空。");
        return;
    }

    if (!playerAccounts.TryAdd(account, new PlayerAccount(account, password, NormalizeName(command.Name, account))))
    {
        await SendErrorAsync(peer, "账号已存在。");
        return;
    }

    await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ADMIN_NOTICE") { Message = $"已新增棋手 {account}。" }, jsonOptions);
    await SendAdminSnapshotAsync(peer);
}

async Task UpdateNicknameAsync(ClientPeer peer, string name)
{
    if (peer.IsAdmin)
    {
        await SendErrorAsync(peer, "管理员不参与棋手昵称设置。");
        return;
    }

    peer.Name = NormalizeName(name, peer.Account);
    if (!string.IsNullOrEmpty(peer.Account) && playerAccounts.TryGetValue(peer.Account, out PlayerAccount? account))
    {
        account.DisplayName = peer.Name;
    }

    if (peer.Room != null)
    {
        await peer.Room.UpdatePeerNameAsync(peer, jsonOptions);
    }

    await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("PROFILE") { Account = peer.Account, Name = peer.Name, Message = "昵称已更新。" }, jsonOptions);
    await SendRoomListToAllAsync();
}

async Task CreateRoomAsync(ClientPeer peer, ClientCommand command)
{
    if (peer.Room != null && !peer.Room.Started)
    {
        await SendErrorAsync(peer, "你已经在一个未开始的房间里。");
        return;
    }

    DateTime now = DateTime.UtcNow;
    if (now - peer.LastRoomCreatedAtUtc < createRoomCooldown)
    {
        int seconds = Math.Max(1, (int)Math.Ceiling((createRoomCooldown - (now - peer.LastRoomCreatedAtUtc)).TotalSeconds));
        await SendErrorAsync(peer, $"创建太频繁，请 {seconds} 秒后再试。");
        return;
    }

    await LeaveCurrentRoomAsync(peer);
    string key;
    do
    {
        key = Random.Shared.Next(1000, 10000).ToString();
    }
    while (rooms.ContainsKey(key));

    GameRoom room = new(key, string.IsNullOrWhiteSpace(command.RuleVariant) ? RuleVariant.OnePieceJump : Enum.Parse<RuleVariant>(command.RuleVariant, true));
    rooms[key] = room;
    peer.LastRoomCreatedAtUtc = now;
    await room.AddPeerAsync(peer, jsonOptions);
    await room.BroadcastLobbyAsync(jsonOptions);
    await SendRoomListToAllAsync();
}

async Task JoinRoomAsync(ClientPeer peer, string roomKey)
{
    if (string.IsNullOrWhiteSpace(roomKey) || !rooms.TryGetValue(roomKey.Trim(), out GameRoom? room))
    {
        await SendErrorAsync(peer, "房间不存在。");
        return;
    }

    await LeaveCurrentRoomAsync(peer);
    if (!await room.AddPeerAsync(peer, jsonOptions))
    {
        await SendErrorAsync(peer, "房间已满或已经开始。");
        return;
    }

    await room.BroadcastLobbyAsync(jsonOptions);
    await SendRoomListToAllAsync();
}

async Task LeaveCurrentRoomAsync(ClientPeer peer)
{
    GameRoom? room = peer.Room;
    if (room == null)
    {
        return;
    }

    await room.RemovePeerAsync(peer, jsonOptions);
    if (room.IsEmpty)
    {
        rooms.TryRemove(room.Key, out _);
    }
}

async Task RequireRoom(ClientPeer peer, Func<GameRoom, Task> action)
{
    if (peer.Room == null)
    {
        await SendErrorAsync(peer, "你还没有加入房间。");
        return;
    }

    await action(peer.Room);
}

async Task SendRoomListAsync(ClientPeer peer)
{
    List<RoomSummary> summaries = rooms.Values
        .Where(room => !room.Started)
        .Select(room => room.ToSummary())
        .OrderBy(summary => summary.RoomKey)
        .ToList();
    await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ROOM_LIST") { Rooms = summaries }, jsonOptions);
}

async Task SendRoomListToAllAsync()
{
    foreach (ClientPeer peer in clients.Values.Where(peer => peer.Authenticated && peer.Socket.State == WebSocketState.Open))
    {
        await SendRoomListAsync(peer);
    }
}

async Task SendAdminSnapshotAsync(ClientPeer peer)
{
    if (!peer.IsAdmin)
    {
        await SendErrorAsync(peer, "需要管理员权限。");
        return;
    }

    List<MemberSummary> members = clients.Values
        .Where(value => value.Authenticated)
        .OrderBy(value => value.Name)
        .Select(value => new MemberSummary
        {
            ClientId = value.Id,
            Account = value.Account,
            Name = value.Name,
            RoomKey = value.Room?.Key ?? string.Empty,
            Slot = value.Slot,
            IsAdmin = value.IsAdmin,
            IsHost = value.Room != null && value.Room.IsHost(value)
        })
        .ToList();

    List<AccountSummary> accounts = playerAccounts.Values
        .OrderBy(value => value.Account)
        .Select(value => new AccountSummary
        {
            Account = value.Account,
            Name = value.DisplayName,
            Disabled = value.Disabled,
            Online = clients.Values.Any(peer => peer.Authenticated && !peer.IsAdmin && peer.Account.Equals(value.Account, StringComparison.OrdinalIgnoreCase) && peer.Socket.State == WebSocketState.Open)
        })
        .ToList();

    List<RoomSummary> roomSummaries = rooms.Values
        .Select(room => room.ToSummary())
        .OrderBy(room => room.RoomKey)
        .ToList();

    await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ADMIN_SNAPSHOT") { Members = members, Rooms = roomSummaries, Accounts = accounts }, jsonOptions);
}

Task SendErrorAsync(ClientPeer peer, string message)
{
    return SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ERROR") { Message = message }, jsonOptions);
}

static string NormalizeName(string value, string fallback)
{
    string name = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    return name.Length > 14 ? name[..14] : name;
}

static async Task<string?> ReceiveTextAsync(WebSocket socket, byte[] buffer)
{
    StringBuilder builder = new();
    while (true)
    {
        WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            return null;
        }

        builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        if (result.EndOfMessage)
        {
            return builder.ToString();
        }
    }
}

sealed class ClientPeer
{
    public ClientPeer(WebSocket socket)
    {
        Socket = socket;
        Id = Guid.NewGuid().ToString("N")[..8];
    }

    public string Id { get; }
    public WebSocket Socket { get; }
    public string Account { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Authenticated { get; set; }
    public bool IsAdmin { get; set; }
    public SlotId? Slot { get; set; }
    public GameRoom? Room { get; set; }
    public DateTime LastRoomCreatedAtUtc { get; set; } = DateTime.MinValue;
}

sealed class GameRoom
{
    private static readonly SlotId[] SlotOrder =
    {
        SlotId.Bottom,
        SlotId.Top,
        SlotId.BottomLeft,
        SlotId.TopRight,
        SlotId.TopLeft,
        SlotId.BottomRight
    };

    private readonly object gate = new();
    private readonly List<ClientPeer> peers = new();
    private readonly Dictionary<SlotId, string> playerNames = new();
    private readonly RuleVariant ruleVariant;
    private string hostClientId = string.Empty;
    private GameSession? session;
    private int version;

    public GameRoom(string key, RuleVariant ruleVariant)
    {
        Key = key;
        this.ruleVariant = ruleVariant;
    }

    public string Key { get; }
    public bool Started
    {
        get
        {
            lock (gate)
            {
                return session != null;
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (gate)
            {
                return peers.Count == 0;
            }
        }
    }

    public async Task SelectSlotAsync(ClientPeer peer, string slotName, JsonSerializerOptions jsonOptions)
    {
        if (!Enum.TryParse<SlotId>(slotName, ignoreCase: true, out SlotId slot))
        {
            await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ERROR") { Message = "无效的位置。" }, jsonOptions);
            return;
        }

        ServerEnvelope? notification = null;
        ServerEnvelope? error = null;
        lock (gate)
        {
            if (session != null)
            {
                error = new ServerEnvelope("ERROR") { Message = "棋局已开始，不能切换位置。" };
            }
            else if (peers.Any(p => p.Slot == slot && p.Id != peer.Id))
            {
                error = new ServerEnvelope("ERROR") { Message = "该位置已被占用。" };
            }
            else
            {
                if (peer.Slot.HasValue)
                {
                    playerNames.Remove(peer.Slot.Value);
                }

                peer.Slot = slot;
                playerNames[slot] = peer.Name;
                notification = new ServerEnvelope("ROOM")
                {
                    RoomKey = Key,
                    Slot = slot,
                    IsHost = IsHostLocked(peer),
                    Message = $"已切换到{BoardLayout.GetSlotLabel(slot)}位置。"
                };
            }
        }

        if (error != null)
        {
            await SocketJson.SendAsync(peer.Socket, error, jsonOptions);
            return;
        }

        await SocketJson.SendAsync(peer.Socket, notification!, jsonOptions);
        await BroadcastLobbyAsync(jsonOptions);
    }

    public async Task<bool> AddPeerAsync(ClientPeer peer, JsonSerializerOptions jsonOptions)
    {
        ServerEnvelope envelope;
        lock (gate)
        {
            if (session != null || peers.Count >= SlotOrder.Length)
            {
                return false;
            }

            SlotId slot = SlotOrder.First(slotValue => peers.All(value => value.Slot != slotValue));
            peer.Room = this;
            peer.Slot = slot;
            peers.Add(peer);
            if (string.IsNullOrEmpty(hostClientId))
            {
                hostClientId = peer.Id;
            }
            playerNames[slot] = peer.Name;
            envelope = new ServerEnvelope("ROOM")
            {
                RoomKey = Key,
                Slot = slot,
                IsHost = IsHostLocked(peer),
                Message = $"已加入房间 {Key}，你是{BoardLayout.GetSlotLabel(slot)}。"
            };
        }

        await SocketJson.SendAsync(peer.Socket, envelope, jsonOptions);
        return true;
    }

    public async Task RemovePeerAsync(ClientPeer peer, JsonSerializerOptions jsonOptions)
    {
        bool changed;
        lock (gate)
        {
            changed = peers.Remove(peer);
            if (peer.Slot.HasValue)
            {
                playerNames.Remove(peer.Slot.Value);
            }
            if (hostClientId == peer.Id)
            {
                hostClientId = peers.FirstOrDefault(value => value.Id != peer.Id)?.Id ?? string.Empty;
            }

            peer.Room = null;
            peer.Slot = null;
        }

        if (changed)
        {
            await BroadcastLobbyAsync(jsonOptions);
        }
    }

    public async Task UpdatePeerNameAsync(ClientPeer peer, JsonSerializerOptions jsonOptions)
    {
        lock (gate)
        {
            if (peer.Slot.HasValue && playerNames.ContainsKey(peer.Slot.Value))
            {
                playerNames[peer.Slot.Value] = peer.Name;
            }
        }

        await BroadcastLobbyAsync(jsonOptions);
        if (session != null)
        {
            await BroadcastStateAsync(jsonOptions);
        }
    }

    public async Task StartAsync(ClientPeer peer, JsonSerializerOptions jsonOptions)
    {
        ServerEnvelope? error = null;
        lock (gate)
        {
            if (!IsHostLocked(peer))
            {
                error = new ServerEnvelope("ERROR") { Message = "只有创建房间的人可以开始棋局。" };
            }
            else if (session != null)
            {
                error = new ServerEnvelope("ERROR") { Message = "房间已经开始。" };
            }
            else
            {
                RoomConfig config = BuildRoomConfig();
                if (!BoardLayout.ValidateRoom(config, out string message))
                {
                    error = new ServerEnvelope("ERROR") { Message = message };
                }
                else
                {
                    session = new GameSession(config);
                    RunAiTurnsLocked();
                    version++;
                }
            }
        }

        if (error != null)
        {
            await BroadcastAsync(error, jsonOptions);
            return;
        }

        await BroadcastStateAsync(jsonOptions);
    }

    public async Task SelectAsync(ClientPeer peer, int pieceId, JsonSerializerOptions jsonOptions)
    {
        ServerEnvelope? error = null;
        lock (gate)
        {
            if (!CanActLocked(peer, out string message))
            {
                error = new ServerEnvelope("ERROR") { Message = message };
            }
            else if (!session!.TrySelectPiece(pieceId, out message))
            {
                error = new ServerEnvelope("ERROR") { Message = message };
            }
            else
            {
                version++;
            }
        }

        if (error != null)
        {
            await SocketJson.SendAsync(peer.Socket, error, jsonOptions);
            return;
        }

        await BroadcastStateAsync(jsonOptions);
    }

    public async Task MoveAsync(ClientPeer peer, int pieceId, HexCoord target, JsonSerializerOptions jsonOptions)
    {
        ServerEnvelope? error = null;
        lock (gate)
        {
            if (!CanActLocked(peer, out string message))
            {
                error = new ServerEnvelope("ERROR") { Message = message };
            }
            else if (!session!.TryMovePieceById(pieceId, target, out message))
            {
                error = new ServerEnvelope("ERROR") { Message = message };
            }
            else
            {
                version++;
            }
        }

        if (error != null)
        {
            await SocketJson.SendAsync(peer.Socket, error, jsonOptions);
            return;
        }

        await BroadcastStateAsync(jsonOptions);
    }

    public async Task FinishAsync(ClientPeer peer, JsonSerializerOptions jsonOptions)
    {
        await ApplyTurnCommandAsync(peer, session => session.TryFinishTurn(out string message) ? null : message, jsonOptions);
    }

    public async Task PassAsync(ClientPeer peer, JsonSerializerOptions jsonOptions)
    {
        await ApplyTurnCommandAsync(peer, session => session.TryPassTurn(out string message) ? null : message, jsonOptions);
    }

    public async Task BroadcastLobbyAsync(JsonSerializerOptions jsonOptions)
    {
        await BroadcastAsync(new ServerEnvelope("LOBBY") { Room = ToSummary() }, jsonOptions);
    }

    public RoomSummary ToSummary()
    {
        lock (gate)
        {
            return new RoomSummary
            {
                RoomKey = Key,
                Started = session != null,
                RuleVariant = ruleVariant,
                HostClientId = hostClientId,
                Players = SlotOrder
                    .Where(slot => playerNames.ContainsKey(slot))
                    .Select(slot => new SeatSummary { Slot = slot, Name = playerNames[slot], Kind = PlayerKind.Human, IsHost = peers.Any(peer => peer.Id == hostClientId && peer.Slot == slot) })
                    .ToList()
            };
        }
    }

    public bool IsHost(ClientPeer peer)
    {
        lock (gate)
        {
            return IsHostLocked(peer);
        }
    }

    public ClientPeer[] PeersSnapshot()
    {
        lock (gate)
        {
            return peers.ToArray();
        }
    }

    private async Task ApplyTurnCommandAsync(ClientPeer peer, Func<GameSession, string?> command, JsonSerializerOptions jsonOptions)
    {
        ServerEnvelope? error = null;
        lock (gate)
        {
            if (!CanActLocked(peer, out string message))
            {
                error = new ServerEnvelope("ERROR") { Message = message };
            }
            else
            {
                string? result = command(session!);
                if (result != null)
                {
                    error = new ServerEnvelope("ERROR") { Message = result };
                }
                else
                {
                    RunAiTurnsLocked();
                    version++;
                }
            }
        }

        if (error != null)
        {
            await SocketJson.SendAsync(peer.Socket, error, jsonOptions);
            return;
        }

        await BroadcastStateAsync(jsonOptions);
    }

    private RoomConfig BuildRoomConfig()
    {
        HashSet<SlotId> humanSlots = peers.Where(peer => peer.Slot.HasValue).Select(peer => peer.Slot!.Value).ToHashSet();
        HashSet<SlotId> activeSlots = new(humanSlots);
        foreach (SlotId slot in humanSlots.ToArray())
        {
            activeSlots.Add(BoardLayout.GetOppositeSlot(slot));
        }

        return new RoomConfig
        {
            RoomId = Key,
            RoomName = "网页房间 " + Key,
            RuleVariant = ruleVariant,
            Slots = SlotOrder
                .Where(activeSlots.Contains)
                .Select(slot => new SlotConfig
                {
                    SlotId = slot,
                    PlayerKind = humanSlots.Contains(slot) ? PlayerKind.Human : PlayerKind.AiAdvanced
                })
                .ToList()
        };
    }

    private bool CanActLocked(ClientPeer peer, out string message)
    {
        message = string.Empty;
        if (session == null)
        {
            message = "房间还没有开始。";
            return false;
        }

        if (peer.Slot != session.CurrentPlayerSlot)
        {
            message = "还没轮到你。";
            return false;
        }

        return true;
    }

    private void RunAiTurnsLocked()
    {
        int guard = 0;
        while (session != null && !session.IsGameOver && BoardLayout.IsAi(session.CurrentPlayerKind) && guard++ < 24)
        {
            session.ApplyAiMove(session.GetBestAiMove());
            version++;
        }
    }

    private async Task BroadcastStateAsync(JsonSerializerOptions jsonOptions)
    {
        GameSnapshot? snapshot;
        List<SeatSummary> seats;
        int stateVersion;
        lock (gate)
        {
            snapshot = session?.ToSnapshot();
            seats = SlotOrder
                .Where(slot => playerNames.ContainsKey(slot))
                .Select(slot => new SeatSummary { Slot = slot, Name = playerNames[slot], Kind = PlayerKind.Human, IsHost = peers.Any(peer => peer.Id == hostClientId && peer.Slot == slot) })
                .ToList();
            stateVersion = version;
        }

        await BroadcastAsync(new ServerEnvelope("STATE") { RoomKey = Key, Snapshot = snapshot, Seats = seats, Version = stateVersion }, jsonOptions);
    }

    private async Task BroadcastAsync(ServerEnvelope envelope, JsonSerializerOptions jsonOptions)
    {
        ClientPeer[] targets = PeersSnapshot();
        foreach (ClientPeer peer in targets)
        {
            envelope.IsHost = IsHost(peer);
            await SocketJson.SendAsync(peer.Socket, envelope, jsonOptions);
        }
    }

    private bool IsHostLocked(ClientPeer peer)
    {
        return !string.IsNullOrEmpty(hostClientId) && peer.Id == hostClientId;
    }
}

sealed class ClientCommand
{
    public string Type { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoomKey { get; set; } = string.Empty;
    public string RuleVariant { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public int PieceId { get; set; }
    public int Q { get; set; }
    public int R { get; set; }
}

sealed class ServerEnvelope
{
    public ServerEnvelope(string type)
    {
        Type = type;
    }

    public string Type { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoomKey { get; set; } = string.Empty;
    public SlotId? Slot { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Version { get; set; }
    public GameSnapshot? Snapshot { get; set; }
    public RoomSummary? Room { get; set; }
    public List<RoomSummary> Rooms { get; set; } = new();
    public List<SeatSummary> Seats { get; set; } = new();
    public List<MemberSummary> Members { get; set; } = new();
    public List<AccountSummary> Accounts { get; set; } = new();
    public bool IsHost { get; set; }
}

sealed class PlayerAccount
{
    public PlayerAccount(string account, string password, string displayName)
    {
        Account = account;
        Password = password;
        DisplayName = displayName;
    }

    public string Account { get; }
    public string Password { get; set; }
    public string DisplayName { get; set; }
    public bool Disabled { get; set; }
}

sealed class RoomSummary
{
    public string RoomKey { get; set; } = string.Empty;
    public bool Started { get; set; }
    public RuleVariant RuleVariant { get; set; }
    public string HostClientId { get; set; } = string.Empty;
    public List<SeatSummary> Players { get; set; } = new();
}

sealed class SeatSummary
{
    public SlotId Slot { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlayerKind Kind { get; set; }
    public bool IsHost { get; set; }
}

sealed class MemberSummary
{
    public string ClientId { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoomKey { get; set; } = string.Empty;
    public SlotId? Slot { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsHost { get; set; }
}

sealed class AccountSummary
{
    public string Account { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Online { get; set; }
    public bool Disabled { get; set; }
}

static class SocketJson
{
    public static async Task SendAsync(WebSocket socket, object value, JsonSerializerOptions jsonOptions)
    {
        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions);
        await socket.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
