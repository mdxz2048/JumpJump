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
const string persistencePath = "data/accounts.json";

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

// Seed built-in accounts then overlay with persisted data
playerAccounts["tian"] = new PlayerAccount("tian", "tian", "tian");
playerAccounts["mdxz"] = new PlayerAccount("mdxz", "mdxz", "mdxz");
LoadAccounts();
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
        peer.IsDualDevice = command.DualDevice;
        peer.PreferredSlots = playerAccount.PreferredSlots ?? new List<string>();
        peer.Name = NormalizeName(playerAccount.DisplayName, account);
        await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("AUTH_OK") { ClientId = peer.Id, Account = peer.Account, Name = peer.Name, DualDevice = peer.IsDualDevice, PreferredSlots = playerAccount.PreferredSlots ?? new(), Message = "进入成功。" }, jsonOptions);
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
        case "SELECT_SLOTS":
            await RequireRoom(peer, room => room.SelectSlotsAsync(peer, command.Slots, jsonOptions, playerAccounts, PersistAccounts));
            break;
        case "LEAVE_ROOM":
            await LeaveCurrentRoomExplicitAsync(peer);
            break;
        case "KICK_PEER":
            await RequireRoom(peer, room => room.KickPeerAsync(peer, command.TargetClientId, jsonOptions, rooms, SendRoomListToAllAsync));
            break;
        case "UPDATE_NICKNAME":
            await UpdateNicknameAsync(peer, command.Name);
            break;
        case "START":
            await RequireRoom(peer, room => room.StartAsync(peer, jsonOptions));
            break;
        case "RESTART_GAME":
            await RequireRoom(peer, room => room.RestartAsync(peer, jsonOptions));
            break;
        case "DISBAND_ROOM":
            await RequireRoom(peer, room => room.DisbandAsync(peer, jsonOptions, rooms, SendRoomListToAllAsync));
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

    PersistAccounts();
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

    PersistAccounts();
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
    PersistAccounts();
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

    PersistAccounts();
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

    GameRoom room = new(key, string.IsNullOrWhiteSpace(command.RuleVariant) ? RuleVariant.SpaceJump : Enum.Parse<RuleVariant>(command.RuleVariant, true));
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

async Task LeaveCurrentRoomExplicitAsync(ClientPeer peer)
{
    if (peer.Room == null)
    {
        await SendErrorAsync(peer, "你还没有加入房间。");
        return;
    }

    GameRoom room = peer.Room;
    await room.RemovePeerAsync(peer, jsonOptions);
    if (room.IsEmpty) rooms.TryRemove(room.Key, out _);
    await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("LOBBY_RETURN") { Message = "已退出房间。" }, jsonOptions);
    await SendRoomListAsync(peer);
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
            IsHost = value.Room != null && value.Room.IsHost(value),
            IsDualDevice = value.IsDualDevice,
            ControlledSlots = value.ControlledSlots().Select(s => s.ToString()).ToList()
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

void PersistAccounts()
{
    try
    {
        string dir = Path.GetDirectoryName(persistencePath)!;
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string json = JsonSerializer.Serialize(playerAccounts.Values.Select(a => new PersistedAccount
        {
            Account = a.Account, Password = a.Password, DisplayName = a.DisplayName,
            Disabled = a.Disabled, PreferDualDevice = a.PreferDualDevice, PreferredSlots = a.PreferredSlots
        }).ToList(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(persistencePath, json);
    }
    catch (Exception ex) { Console.WriteLine($"[persistence] save error: {ex.Message}"); }
}

void LoadAccounts()
{
    try
    {
        if (!File.Exists(persistencePath)) return;
        List<PersistedAccount>? list = JsonSerializer.Deserialize<List<PersistedAccount>>(File.ReadAllText(persistencePath));
        if (list == null) return;
        foreach (PersistedAccount pa in list)
        {
            PlayerAccount acct = new(pa.Account, pa.Password, pa.DisplayName);
            acct.Disabled = pa.Disabled;
            acct.PreferDualDevice = pa.PreferDualDevice;
            acct.PreferredSlots = pa.PreferredSlots;
            playerAccounts[pa.Account] = acct;
        }
        Console.WriteLine($"[persistence] loaded {list.Count} accounts.");
    }
    catch (Exception ex) { Console.WriteLine($"[persistence] load error: {ex.Message}"); }
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
    public bool IsDualDevice { get; set; }
    /// <summary>Preferred slots loaded from account at AUTH time.</summary>
    public List<string> PreferredSlots { get; set; } = new();
    /// <summary>Primary slot (first slot for dual-device, or the single slot for single-device).</summary>
    public SlotId? Slot { get; set; }
    /// <summary>Secondary slot — only set when IsDualDevice and a pair was allocated.</summary>
    public SlotId? Slot2 { get; set; }
    public GameRoom? Room { get; set; }
    public DateTime LastRoomCreatedAtUtc { get; set; } = DateTime.MinValue;

    /// <summary>Returns all slots controlled by this peer.</summary>
    public IEnumerable<SlotId> ControlledSlots()
    {
        if (Slot.HasValue) yield return Slot.Value;
        if (Slot2.HasValue) yield return Slot2.Value;
    }

    public bool ControlsSlot(SlotId slot) => Slot == slot || Slot2 == slot;
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
            else if (peers.Any(p => p.ControlsSlot(slot) && p.Id != peer.Id))
            {
                error = new ServerEnvelope("ERROR") { Message = "该位置已被占用。" };
            }
            else
            {
                // Release old slots
                if (peer.Slot.HasValue) playerNames.Remove(peer.Slot.Value);
                if (peer.Slot2.HasValue) playerNames.Remove(peer.Slot2.Value);
                peer.Slot2 = null;
                peer.IsDualDevice = false;

                peer.Slot = slot;
                playerNames[slot] = peer.Name;
                notification = new ServerEnvelope("ROOM")
                {
                    RoomKey = Key,
                    Slot = slot,
                    ControlledSlots = new List<string> { slot.ToString() },
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

    public async Task SelectSlotsAsync(ClientPeer peer, List<string> slotNames, JsonSerializerOptions jsonOptions, ConcurrentDictionary<string, PlayerAccount> accounts, Action persistCallback)
    {
        if (slotNames == null || slotNames.Count == 0)
        {
            await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ERROR") { Message = "未指定位置。" }, jsonOptions);
            return;
        }

        // Parse slots
        List<SlotId> requestedSlots = new();
        foreach (string name in slotNames)
        {
            if (!Enum.TryParse<SlotId>(name, ignoreCase: true, out SlotId s))
            {
                await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ERROR") { Message = $"无效的位置：{name}。" }, jsonOptions);
                return;
            }
            requestedSlots.Add(s);
        }

        bool isDual = peer.IsDualDevice;
        if (isDual)
        {
            if (requestedSlots.Count != 2)
            {
                await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ERROR") { Message = "双人端需要选择两个对家位置。" }, jsonOptions);
                return;
            }
            if (BoardLayout.GetOppositeSlot(requestedSlots[0]) != requestedSlots[1])
            {
                await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ERROR") { Message = "双人端必须选择一对对家位置（例如上方+下方）。" }, jsonOptions);
                return;
            }
        }
        else
        {
            if (requestedSlots.Count != 1)
            {
                await SocketJson.SendAsync(peer.Socket, new ServerEnvelope("ERROR") { Message = "单人端只能选择一个位置。" }, jsonOptions);
                return;
            }
        }

        ServerEnvelope? notification = null;
        ServerEnvelope? error = null;
        lock (gate)
        {
            if (session != null)
            {
                error = new ServerEnvelope("ERROR") { Message = "棋局已开始，不能切换位置。" };
            }
            else
            {
                // Check none of the target slots is held by another peer
                foreach (SlotId s in requestedSlots)
                {
                    if (peers.Any(p => p.Id != peer.Id && p.ControlsSlot(s)))
                    {
                        error = new ServerEnvelope("ERROR") { Message = $"{BoardLayout.GetSlotLabel(s)} 已被其他玩家占用。" };
                        break;
                    }
                }

                if (error == null)
                {
                    // Release old slots
                    if (peer.Slot.HasValue) playerNames.Remove(peer.Slot.Value);
                    if (peer.Slot2.HasValue) playerNames.Remove(peer.Slot2.Value);

                    peer.Slot = requestedSlots[0];
                    playerNames[requestedSlots[0]] = Helpers.GenerateName(peer.Name, true, isDual);
                    if (isDual)
                    {
                        peer.Slot2 = requestedSlots[1];
                        playerNames[requestedSlots[1]] = Helpers.GenerateName(peer.Name, false, isDual);
                    }
                    else
                    {
                        peer.Slot2 = null;
                    }

                    notification = new ServerEnvelope("ROOM")
                    {
                        RoomKey = Key,
                        Slot = peer.Slot,
                        Slot2 = peer.Slot2,
                        DualDevice = isDual,
                        ControlledSlots = peer.ControlledSlots().Select(s => s.ToString()).ToList(),
                        IsHost = IsHostLocked(peer),
                        Message = isDual
                            ? $"已选择 {BoardLayout.GetSlotLabel(requestedSlots[0])} 和 {BoardLayout.GetSlotLabel(requestedSlots[1])} 位置。"
                            : $"已切换到{BoardLayout.GetSlotLabel(requestedSlots[0])}位置。"
                    };
                }
            }
        }

        if (error != null)
        {
            await SocketJson.SendAsync(peer.Socket, error, jsonOptions);
            return;
        }

        // Persist slot preference
        if (!string.IsNullOrEmpty(peer.Account) && accounts.TryGetValue(peer.Account, out PlayerAccount? acct))
        {
            acct.PreferredSlots = requestedSlots.Select(s => s.ToString()).ToList();
            persistCallback();
        }

        await SocketJson.SendAsync(peer.Socket, notification!, jsonOptions);
        await BroadcastLobbyAsync(jsonOptions);
    }

    public async Task KickPeerAsync(ClientPeer requestor, string targetClientId, JsonSerializerOptions jsonOptions,
        ConcurrentDictionary<string, GameRoom> rooms, Func<Task> sendRoomListToAll)
    {
        ClientPeer? target = null;
        ServerEnvelope? error = null;
        lock (gate)
        {
            if (!IsHostLocked(requestor))
            {
                error = new ServerEnvelope("ERROR") { Message = "只有房主可以踢人。" };
            }
            else if (requestor.Id == targetClientId)
            {
                error = new ServerEnvelope("ERROR") { Message = "房主不能踢自己。" };
            }
            else
            {
                target = peers.FirstOrDefault(p => p.Id == targetClientId);
                if (target == null) error = new ServerEnvelope("ERROR") { Message = "找不到该成员。" };
            }
        }

        if (error != null)
        {
            await SocketJson.SendAsync(requestor.Socket, error, jsonOptions);
            return;
        }

        // Notify the kicked peer first
        await SocketJson.SendAsync(target!.Socket, new ServerEnvelope("KICKED") { Message = "你已被房主移出房间。" }, jsonOptions);

        await RemovePeerAsync(target, jsonOptions);
        if (IsEmpty) rooms.TryRemove(Key, out _);

        await sendRoomListToAll();
    }

    public async Task<bool> AddPeerAsync(ClientPeer peer, JsonSerializerOptions jsonOptions)
    {
        ServerEnvelope envelope;
        lock (gate)
        {
            if (session != null) return false;

            List<SlotId> freeSlots = SlotOrder.Where(s => !peers.Any(p => p.ControlsSlot(s))).ToList();

            if (peer.IsDualDevice)
            {
                // Need a free pair
                if (freeSlots.Count < 2) return false;

                // Try preferred pair first
                SlotId? pref1 = null, pref2 = null;
                if (peer.PreferredSlots.Count == 2
                    && Enum.TryParse<SlotId>(peer.PreferredSlots[0], out SlotId ps1)
                    && Enum.TryParse<SlotId>(peer.PreferredSlots[1], out SlotId ps2)
                    && freeSlots.Contains(ps1) && freeSlots.Contains(ps2)
                    && BoardLayout.GetOppositeSlot(ps1) == ps2)
                {
                    pref1 = ps1;
                    pref2 = ps2;
                }

                if (pref1 == null)
                {
                    // Find any free pair
                    foreach (SlotId s in freeSlots)
                    {
                        SlotId opp = BoardLayout.GetOppositeSlot(s);
                        if (freeSlots.Contains(opp))
                        {
                            pref1 = s;
                            pref2 = opp;
                            break;
                        }
                    }
                }

                if (pref1 == null)
                {
                    // No free pair available
                    return false;
                }

                peer.Room = this;
                peer.Slot = pref1;
                peer.Slot2 = pref2;
                peers.Add(peer);
                if (string.IsNullOrEmpty(hostClientId)) hostClientId = peer.Id;
                playerNames[pref1.Value] = Helpers.GenerateName(peer.Name, true, true);
                playerNames[pref2!.Value] = Helpers.GenerateName(peer.Name, false, true);

                envelope = new ServerEnvelope("ROOM")
                {
                    RoomKey = Key,
                    Slot = pref1,
                    Slot2 = pref2,
                    DualDevice = true,
                    ControlledSlots = peer.ControlledSlots().Select(s => s.ToString()).ToList(),
                    IsHost = IsHostLocked(peer),
                    Message = $"已加入房间 {Key}（双人端）：{BoardLayout.GetSlotLabel(pref1.Value)} 和 {BoardLayout.GetSlotLabel(pref2.Value)}。"
                };
            }
            else
            {
                if (freeSlots.Count == 0) return false;

                // Try preferred single slot
                SlotId slot = freeSlots[0];
                if (peer.PreferredSlots.Count >= 1
                    && Enum.TryParse<SlotId>(peer.PreferredSlots[0], out SlotId preferred)
                    && freeSlots.Contains(preferred))
                {
                    slot = preferred;
                }

                peer.Room = this;
                peer.Slot = slot;
                peer.Slot2 = null;
                peers.Add(peer);
                if (string.IsNullOrEmpty(hostClientId)) hostClientId = peer.Id;
                playerNames[slot] = peer.Name;
                envelope = new ServerEnvelope("ROOM")
                {
                    RoomKey = Key,
                    Slot = slot,
                    DualDevice = false,
                    ControlledSlots = new List<string> { slot.ToString() },
                    IsHost = IsHostLocked(peer),
                    Message = $"已加入房间 {Key}，你是{BoardLayout.GetSlotLabel(slot)}。"
                };
            }
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
            if (peer.Slot.HasValue) playerNames.Remove(peer.Slot.Value);
            if (peer.Slot2.HasValue) playerNames.Remove(peer.Slot2.Value);
            if (hostClientId == peer.Id)
            {
                hostClientId = peers.FirstOrDefault(value => value.Id != peer.Id)?.Id ?? string.Empty;
            }

            peer.Room = null;
            peer.Slot = null;
            peer.Slot2 = null;
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
            if (peer.Slot.HasValue) playerNames[peer.Slot.Value] = Helpers.GenerateName(peer.Name, true, peer.IsDualDevice);
            if (peer.Slot2.HasValue) playerNames[peer.Slot2.Value] = Helpers.GenerateName(peer.Name, false, peer.IsDualDevice);
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

    public async Task RestartAsync(ClientPeer peer, JsonSerializerOptions jsonOptions)
    {
        ServerEnvelope? error = null;
        lock (gate)
        {
            if (!IsHostLocked(peer))
            {
                error = new ServerEnvelope("ERROR") { Message = "只有房主可以重开本局。" };
            }
            else if (session == null)
            {
                error = new ServerEnvelope("ERROR") { Message = "棋局还没有开始。" };
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
            await SocketJson.SendAsync(peer.Socket, error, jsonOptions);
            return;
        }

        await BroadcastStateAsync(jsonOptions);
    }

    public async Task DisbandAsync(ClientPeer peer, JsonSerializerOptions jsonOptions,
        ConcurrentDictionary<string, GameRoom> rooms, Func<Task> sendRoomListToAll)
    {
        ClientPeer[] targets = Array.Empty<ClientPeer>();
        ServerEnvelope? error = null;
        lock (gate)
        {
            if (!IsHostLocked(peer))
            {
                error = new ServerEnvelope("ERROR") { Message = "只有房主可以解散房间。" };
            }
            else
            {
                targets = peers.ToArray();
                foreach (ClientPeer target in targets)
                {
                    target.Room = null;
                    target.Slot = null;
                    target.Slot2 = null;
                }

                peers.Clear();
                playerNames.Clear();
                session = null;
                hostClientId = string.Empty;
                version++;
            }
        }

        if (error != null)
        {
            await SocketJson.SendAsync(peer.Socket, error, jsonOptions);
            return;
        }

        rooms.TryRemove(Key, out _);
        foreach (ClientPeer target in targets)
        {
            await SocketJson.SendAsync(target.Socket, new ServerEnvelope("ROOM_DISBANDED") { Message = "房间已被房主解散。" }, jsonOptions);
        }

        await sendRoomListToAll();
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
                    .Select(slot =>
                    {
                        ClientPeer? holder = peers.FirstOrDefault(p => p.ControlsSlot(slot));
                        return new SeatSummary
                        {
                            Slot = slot,
                            Name = playerNames[slot],
                            Kind = PlayerKind.Human,
                            IsHost = holder != null && holder.Id == hostClientId,
                            ClientId = holder?.Id ?? string.Empty,
                            IsDualDevice = holder?.IsDualDevice ?? false
                        };
                    })
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
        // All slots controlled by human peers are human slots
        HashSet<SlotId> humanSlots = peers.SelectMany(p => p.ControlledSlots()).ToHashSet();
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

        if (!peer.ControlsSlot(session.CurrentPlayerSlot))
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
                .Select(slot =>
                {
                    ClientPeer? holder = peers.FirstOrDefault(p => p.ControlsSlot(slot));
                    return new SeatSummary
                    {
                        Slot = slot,
                        Name = playerNames[slot],
                        Kind = PlayerKind.Human,
                        IsHost = holder != null && holder.Id == hostClientId,
                        ClientId = holder?.Id ?? string.Empty,
                        IsDualDevice = holder?.IsDualDevice ?? false
                    };
                })
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
            // Set per-peer controlled slots info
            envelope.ControlledSlots = peer.ControlledSlots().Select(s => s.ToString()).ToList();
            envelope.DualDevice = peer.IsDualDevice;
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
    public List<string> Slots { get; set; } = new();
    public bool DualDevice { get; set; }
    public string TargetClientId { get; set; } = string.Empty;
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
    public SlotId? Slot2 { get; set; }
    public List<string> ControlledSlots { get; set; } = new();
    public bool DualDevice { get; set; }
    public List<string> PreferredSlots { get; set; } = new();
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
    public bool PreferDualDevice { get; set; }
    /// <summary>Persisted slot preference. 1 slot for single-device; 2 slots for dual-device preference.</summary>
    public List<string> PreferredSlots { get; set; } = new();
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
    public string ClientId { get; set; } = string.Empty;
    public bool IsDualDevice { get; set; }
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
    public bool IsDualDevice { get; set; }
    public List<string> ControlledSlots { get; set; } = new();
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

static class Helpers
{
    public static string GenerateName(string baseName, bool isPrimary, bool isDual)
    {
        if (!isDual) return baseName;
        return isPrimary ? baseName + "-A" : baseName + "-B";
    }
}

/// <summary>DTO used for JSON file persistence of account data.</summary>
sealed class PersistedAccount
{
    public string Account { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Disabled { get; set; }
    public bool PreferDualDevice { get; set; }
    public List<string> PreferredSlots { get; set; } = new();
}
