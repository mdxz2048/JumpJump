namespace SweetJumpJump.Core;

public static class BoardLayout
{
    private static readonly List<HexCoord> AllCellsInternal = GenerateAllCells();
    private static readonly HashSet<HexCoord> CellSet = new(AllCellsInternal);
    private static readonly Dictionary<SlotId, List<HexCoord>> Camps = GenerateCamps();
    private static readonly Dictionary<SlotId, SlotId> OppositeSlots = new()
    {
        { SlotId.Top, SlotId.Bottom },
        { SlotId.Bottom, SlotId.Top },
        { SlotId.TopLeft, SlotId.BottomRight },
        { SlotId.BottomRight, SlotId.TopLeft },
        { SlotId.TopRight, SlotId.BottomLeft },
        { SlotId.BottomLeft, SlotId.TopRight }
    };

    private static readonly Dictionary<SlotId, HexCoord> TargetAnchors = new()
    {
        { SlotId.Top, new HexCoord(-4, 8) },
        { SlotId.Bottom, new HexCoord(4, -8) },
        { SlotId.TopLeft, new HexCoord(4, 4) },
        { SlotId.BottomRight, new HexCoord(-4, -4) },
        { SlotId.TopRight, new HexCoord(-8, 4) },
        { SlotId.BottomLeft, new HexCoord(8, -4) }
    };

    public static readonly HexCoord[] Directions =
    {
        new(1, 0),
        new(1, -1),
        new(0, -1),
        new(-1, 0),
        new(-1, 1),
        new(0, 1)
    };

    public static IReadOnlyList<HexCoord> AllCells => AllCellsInternal;

    public static bool IsValidCell(HexCoord coord)
    {
        return CellSet.Contains(coord);
    }

    public static IReadOnlyList<HexCoord> GetCamp(SlotId slotId)
    {
        return Camps[slotId];
    }

    public static IReadOnlyList<HexCoord> GetTargetCamp(SlotId slotId)
    {
        return Camps[OppositeSlots[slotId]];
    }

    public static HexCoord GetTargetAnchor(SlotId slotId)
    {
        return TargetAnchors[slotId];
    }

    public static SlotId GetOppositeSlot(SlotId slotId)
    {
        return OppositeSlots[slotId];
    }

    public static string GetSlotLabel(SlotId slotId)
    {
        return slotId switch
        {
            SlotId.Top => "上方",
            SlotId.TopRight => "右上",
            SlotId.BottomRight => "右下",
            SlotId.Bottom => "下方",
            SlotId.BottomLeft => "左下",
            SlotId.TopLeft => "左上",
            _ => slotId.ToString()
        };
    }

    public static SlotId[] GetSlotsInDisplayOrder()
    {
        return new[]
        {
            SlotId.Top,
            SlotId.TopRight,
            SlotId.BottomRight,
            SlotId.Bottom,
            SlotId.BottomLeft,
            SlotId.TopLeft
        };
    }

    public static bool IsAi(PlayerKind playerKind)
    {
        return playerKind is PlayerKind.AiBeginner or PlayerKind.AiNormal or PlayerKind.AiAdvanced;
    }

    public static bool ValidateRoom(RoomConfig room, out string message)
    {
        message = string.Empty;
        if (room == null)
        {
            message = "房间不存在。";
            return false;
        }

        Dictionary<SlotId, PlayerKind> slots = GetSlotMap(room);
        int playerCount = slots.Values.Count(kind => kind != PlayerKind.None);
        int humanCount = slots.Values.Count(kind => kind == PlayerKind.Human);
        if (playerCount < 2)
        {
            message = "至少需要 2 个有效玩家。";
            return false;
        }

        if (humanCount < 1)
        {
            message = "至少需要 1 个真人玩家。";
            return false;
        }

        foreach (SlotId slotId in GetSlotsInDisplayOrder())
        {
            bool active = slots[slotId] != PlayerKind.None;
            bool oppositeActive = slots[GetOppositeSlot(slotId)] != PlayerKind.None;
            if (active != oppositeActive)
            {
                message = "玩家位置必须成对出现。";
                return false;
            }
        }

        return true;
    }

    public static Dictionary<SlotId, PlayerKind> GetSlotMap(RoomConfig room)
    {
        Dictionary<SlotId, PlayerKind> slots = new();
        foreach (SlotId slotId in GetSlotsInDisplayOrder())
        {
            slots[slotId] = PlayerKind.None;
        }

        foreach (SlotConfig slot in room.Slots)
        {
            slots[slot.SlotId] = slot.PlayerKind;
        }

        return slots;
    }

    private static List<HexCoord> GenerateAllCells()
    {
        List<HexCoord> cells = new();
        Dictionary<int, (int Min, int Max)> rowRanges = new()
        {
            { -8, (4, 4) },
            { -7, (3, 4) },
            { -6, (2, 4) },
            { -5, (1, 4) },
            { -4, (-4, 8) },
            { -3, (-4, 7) },
            { -2, (-4, 6) },
            { -1, (-4, 5) },
            { 0, (-4, 4) },
            { 1, (-5, 4) },
            { 2, (-6, 4) },
            { 3, (-7, 4) },
            { 4, (-8, 4) },
            { 5, (-4, -1) },
            { 6, (-4, -2) },
            { 7, (-4, -3) },
            { 8, (-4, -4) }
        };

        foreach (KeyValuePair<int, (int Min, int Max)> row in rowRanges)
        {
            for (int q = row.Value.Min; q <= row.Value.Max; q++)
            {
                cells.Add(new HexCoord(q, row.Key));
            }
        }

        return cells;
    }

    private static Dictionary<SlotId, List<HexCoord>> GenerateCamps()
    {
        Dictionary<SlotId, List<HexCoord>> camps = new()
        {
            { SlotId.Top, new List<HexCoord>() },
            { SlotId.TopRight, new List<HexCoord>() },
            { SlotId.BottomRight, new List<HexCoord>() },
            { SlotId.Bottom, new List<HexCoord>() },
            { SlotId.BottomLeft, new List<HexCoord>() },
            { SlotId.TopLeft, new List<HexCoord>() }
        };

        for (int r = -8; r <= -5; r++)
        {
            for (int q = -r - 4; q <= 4; q++)
            {
                camps[SlotId.Top].Add(new HexCoord(q, r));
            }
        }

        for (int r = -4; r <= -1; r++)
        {
            for (int q = -4; q <= -r - 5; q++)
            {
                camps[SlotId.TopLeft].Add(new HexCoord(q, r));
            }

            for (int q = 5; q <= 4 - r; q++)
            {
                camps[SlotId.TopRight].Add(new HexCoord(q, r));
            }
        }

        for (int r = 1; r <= 4; r++)
        {
            for (int q = -4 - r; q <= -5; q++)
            {
                camps[SlotId.BottomLeft].Add(new HexCoord(q, r));
            }

            for (int q = 5 - r; q <= 4; q++)
            {
                camps[SlotId.BottomRight].Add(new HexCoord(q, r));
            }
        }

        for (int r = 5; r <= 8; r++)
        {
            for (int q = -4; q <= 4 - r; q++)
            {
                camps[SlotId.Bottom].Add(new HexCoord(q, r));
            }
        }

        return camps;
    }
}

public sealed class GameSession
{
    private static readonly SlotId[] PreferredTurnOrder =
    {
        SlotId.Bottom,
        SlotId.BottomLeft,
        SlotId.TopLeft,
        SlotId.Top,
        SlotId.TopRight,
        SlotId.BottomRight
    };

    private readonly Dictionary<int, PieceState> piecesById = new();
    private readonly Dictionary<HexCoord, PieceState> piecesByCoord = new();
    private readonly List<PlayerState> players = new();
    private readonly List<HexCoord> legalTargets = new();
    private readonly HashSet<HexCoord> visitedJumpCells = new();
    private readonly List<TurnStep> currentTurnSteps = new();
    private readonly HashSet<SlotId> completedSlots = new();
    private int selectedPieceId = -1;
    private int currentPlayerIndex;
    private MoveMode moveMode;

    public GameSession(RoomConfig roomConfig)
    {
        RoomConfig = roomConfig;
        StatusMessage = "欢迎来到甜姐的跳跳棋";
        WinnerLabel = string.Empty;
        BuildPlayers(roomConfig);
        BuildPieces();
    }

    public RoomConfig RoomConfig { get; }
    public string StatusMessage { get; private set; }
    public bool HasMovedThisTurn { get; private set; }
    public bool IsGameOver { get; private set; }
    public string WinnerLabel { get; private set; }
    public SlotId CurrentPlayerSlot => players[currentPlayerIndex].SlotId;
    public PlayerKind CurrentPlayerKind => players[currentPlayerIndex].PlayerKind;
    public int SelectedPieceId => selectedPieceId;
    public IReadOnlyList<HexCoord> LegalTargets => legalTargets;
    public IEnumerable<PieceState> Pieces => piecesById.Values;
    public IReadOnlyList<PlayerState> Players => players;
    public bool CanFinishTurn => !IsGameOver && CurrentPlayerKind == PlayerKind.Human && HasMovedThisTurn;
    public bool CanPass => !IsGameOver && CurrentPlayerKind == PlayerKind.Human && !HasMovedThisTurn;

    public PieceState? GetPieceAt(HexCoord coord)
    {
        return piecesByCoord.TryGetValue(coord, out PieceState? piece) ? piece : null;
    }

    public PieceState? GetPieceById(int pieceId)
    {
        return piecesById.TryGetValue(pieceId, out PieceState? piece) ? piece : null;
    }

    public bool TrySelectPiece(int pieceId, out string message)
    {
        PieceState? piece = GetPieceById(pieceId);
        if (piece == null)
        {
            message = "没有找到要移动的棋子。";
            return false;
        }

        return TrySelectPiece(piece.Position, out message);
    }

    public bool TrySelectPiece(HexCoord coord, out string message)
    {
        message = string.Empty;
        if (IsGameOver)
        {
            message = "当前对局已经结束。";
            return false;
        }

        if (CurrentPlayerKind != PlayerKind.Human)
        {
            message = "现在是 AI 回合。";
            return false;
        }

        PieceState? piece = GetPieceAt(coord);
        if (piece == null)
        {
            message = "这里没有可选中的棋子。";
            return false;
        }

        if (piece.Owner != CurrentPlayerSlot)
        {
            message = "只能移动自己的棋子。";
            return false;
        }

        if (moveMode == MoveMode.JumpChain && piece.PieceId != selectedPieceId)
        {
            message = "连续跳跃中只能继续使用当前棋子。";
            return false;
        }

        List<HexCoord> nextTargets = moveMode == MoveMode.JumpChain
            ? GetJumpTargets(piece.Position, null)
            : moveMode == MoveMode.AdjacentDone
                ? new List<HexCoord>()
                : GetOpeningTargets(piece.Position);

        if (nextTargets.Count == 0)
        {
            message = "这个棋子当前没有合法落点。";
            return false;
        }

        selectedPieceId = piece.PieceId;
        legalTargets.Clear();
        legalTargets.AddRange(nextTargets);
        StatusMessage = $"已选中 {BoardLayout.GetSlotLabel(piece.Owner)} 的棋子。";
        return true;
    }

    public bool TryMovePieceById(int pieceId, HexCoord target, out string message)
    {
        PieceState? piece = GetPieceById(pieceId);
        if (piece == null)
        {
            message = "没有找到要移动的棋子。";
            return false;
        }

        if (piece.Owner != CurrentPlayerSlot)
        {
            message = "收到的走子不属于当前玩家。";
            return false;
        }

        if (selectedPieceId != pieceId && !TrySelectPiece(piece.Position, out message))
        {
            return false;
        }

        return TryMoveSelectedPiece(target, out message);
    }

    public bool TryMoveSelectedPiece(HexCoord target, out string message)
    {
        message = string.Empty;
        if (selectedPieceId < 0)
        {
            message = "请先选中一个棋子。";
            return false;
        }

        if (!legalTargets.Contains(target))
        {
            message = "这个位置当前不能落子。";
            return false;
        }

        PieceState piece = piecesById[selectedPieceId];
        HexCoord from = piece.Position;
        bool isJump = !IsAdjacentMove(from, target);
        piecesByCoord.Remove(from);
        piece.Position = target;
        piecesByCoord[target] = piece;

        HasMovedThisTurn = true;
        currentTurnSteps.Add(new TurnStep { PieceId = piece.PieceId, From = from, To = target, IsJump = isJump });

        if (!isJump)
        {
            moveMode = MoveMode.AdjacentDone;
            legalTargets.Clear();
            StatusMessage = "相邻移动完成，请完成移动结束回合。";
            return true;
        }

        if (moveMode == MoveMode.None)
        {
            moveMode = MoveMode.JumpChain;
            visitedJumpCells.Clear();
            visitedJumpCells.Add(from);
        }

        visitedJumpCells.Add(target);
        legalTargets.Clear();
        legalTargets.AddRange(GetJumpTargets(target, null));
        StatusMessage = legalTargets.Count > 0 ? "可以继续跳跃，或完成移动。" : "没有更多可跳位置了，请完成移动。";
        return true;
    }

    public bool TryFinishTurn(out string message)
    {
        message = string.Empty;
        if (!CanFinishTurn)
        {
            message = "当前还不能完成移动。";
            return false;
        }

        FinishTurnInternal();
        return true;
    }

    public bool TryPassTurn(out string message)
    {
        message = string.Empty;
        if (!CanPass)
        {
            message = "已经走子后不能放弃移动。";
            return false;
        }

        StatusMessage = "已放弃移动，切换到下一位。";
        FinishTurnInternal(false);
        return true;
    }

    public MoveOption? GetBestAiMove()
    {
        List<MoveOption> moves = GetAllMovesForSlot(CurrentPlayerSlot);
        if (moves.Count == 0)
        {
            return null;
        }

        if (CurrentPlayerKind == PlayerKind.AiBeginner)
        {
            return moves.OrderBy(move => move.IsJump ? 1 : 0).ThenBy(move => move.From.Q).ThenBy(move => move.From.R).First();
        }

        MoveOption bestMove = moves[0];
        float bestScore = ScoreMove(CurrentPlayerSlot, bestMove, CurrentPlayerKind);
        for (int i = 1; i < moves.Count; i++)
        {
            float score = ScoreMove(CurrentPlayerSlot, moves[i], CurrentPlayerKind);
            if (score > bestScore)
            {
                bestMove = moves[i];
                bestScore = score;
            }
        }

        return bestMove;
    }

    public void ApplyAiMove(MoveOption? move)
    {
        if (move == null)
        {
            StatusMessage = "AI 没有合法行动，自动放弃移动。";
            FinishTurnInternal(false);
            return;
        }

        PieceState piece = piecesById[move.PieceId];
        piecesByCoord.Remove(piece.Position);
        foreach (HexCoord step in move.Path)
        {
            piece.Position = step;
        }

        piecesByCoord[piece.Position] = piece;
        HasMovedThisTurn = true;
        StatusMessage = $"{BoardLayout.GetSlotLabel(CurrentPlayerSlot)} 完成了 {(move.IsJump ? "跳跃行动" : "相邻移动")}。";
        FinishTurnInternal();
    }

    public HexCoord ApplyAiMoveStep(MoveOption? move, int pathIndex)
    {
        if (move == null)
        {
            StatusMessage = "AI 没有合法行动，自动放弃移动。";
            FinishTurnInternal(false);
            return default;
        }

        if (pathIndex < 0 || pathIndex >= move.Path.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pathIndex));
        }

        PieceState piece = piecesById[move.PieceId];
        piecesByCoord.Remove(piece.Position);
        piece.Position = move.Path[pathIndex];
        piecesByCoord[piece.Position] = piece;
        HasMovedThisTurn = true;

        bool isLastStep = pathIndex == move.Path.Count - 1;
        StatusMessage = isLastStep
            ? $"{BoardLayout.GetSlotLabel(CurrentPlayerSlot)} 完成了 {(move.IsJump ? "跳跃行动" : "相邻移动")}。"
            : $"{BoardLayout.GetSlotLabel(CurrentPlayerSlot)} 正在连续跳跃。";

        if (isLastStep)
        {
            FinishTurnInternal();
        }

        return piece.Position;
    }

    public GameSnapshot ToSnapshot()
    {
        return new GameSnapshot
        {
            RoomId = RoomConfig.RoomId,
            RoomName = RoomConfig.RoomName,
            RuleVariant = RoomConfig.RuleVariant,
            CurrentPlayerSlot = CurrentPlayerSlot,
            CurrentPlayerKind = CurrentPlayerKind,
            SelectedPieceId = SelectedPieceId,
            HasMovedThisTurn = HasMovedThisTurn,
            IsGameOver = IsGameOver,
            StatusMessage = StatusMessage,
            WinnerLabel = WinnerLabel,
            Pieces = piecesById.Values.OrderBy(piece => piece.PieceId).Select(ClonePiece).ToList(),
            LegalTargets = legalTargets.ToList(),
            Players = players.Select(player => new PlayerState { SlotId = player.SlotId, PlayerKind = player.PlayerKind }).ToList()
        };
    }

    private void BuildPlayers(RoomConfig roomConfig)
    {
        Dictionary<SlotId, PlayerKind> configuredKinds = roomConfig.Slots.ToDictionary(slot => slot.SlotId, slot => slot.PlayerKind);
        foreach (SlotId slotId in PreferredTurnOrder)
        {
            if (!configuredKinds.TryGetValue(slotId, out PlayerKind kind) || kind == PlayerKind.None)
            {
                continue;
            }

            players.Add(new PlayerState { SlotId = slotId, PlayerKind = kind });
        }
    }

    private void BuildPieces()
    {
        int nextPieceId = 1;
        foreach (PlayerState player in players)
        {
            foreach (HexCoord coord in BoardLayout.GetCamp(player.SlotId))
            {
                PieceState piece = new() { PieceId = nextPieceId++, Owner = player.SlotId, Position = coord };
                piecesById[piece.PieceId] = piece;
                piecesByCoord[piece.Position] = piece;
            }
        }

        StatusMessage = $"轮到 {BoardLayout.GetSlotLabel(CurrentPlayerSlot)}。";
    }

    private float ScoreMove(SlotId slotId, MoveOption move, PlayerKind playerKind)
    {
        HexCoord anchor = BoardLayout.GetTargetAnchor(slotId);
        int startDistance = move.From.DistanceTo(anchor);
        int endDistance = move.FinalPosition.DistanceTo(anchor);
        bool endsInTarget = BoardLayout.GetTargetCamp(slotId).Contains(move.FinalPosition);
        bool leavesHome = BoardLayout.GetCamp(slotId).Contains(move.From) && !BoardLayout.GetCamp(slotId).Contains(move.FinalPosition);
        float score = (startDistance - endDistance) * 10f;
        score += move.IsJump ? 2f * move.Path.Count : 0.5f;
        score += endsInTarget ? 30f : 0f;
        score += leavesHome ? 8f : 0f;
        if (playerKind == PlayerKind.AiAdvanced)
        {
            score += move.IsJump ? 6f * move.Path.Count : 0f;
            score += endDistance < startDistance ? 4f : -3f;
            score += BoardLayout.GetTargetCamp(slotId).Contains(move.From) && !endsInTarget ? -50f : 0f;
        }

        return score;
    }

    private List<MoveOption> GetAllMovesForSlot(SlotId slotId)
    {
        List<MoveOption> moves = new();
        foreach (PieceState piece in piecesById.Values.Where(value => value.Owner == slotId))
        {
            foreach (HexCoord adjacent in GetAdjacentTargets(piece.Position))
            {
                moves.Add(new MoveOption { PieceId = piece.PieceId, From = piece.Position, IsJump = false, Path = new List<HexCoord> { adjacent } });
            }

            CollectJumpMoves(piece.PieceId, piece.Position, piece.Position, new HashSet<HexCoord> { piece.Position }, new List<HexCoord>(), moves);
        }

        return moves;
    }

    private void CollectJumpMoves(int pieceId, HexCoord origin, HexCoord current, HashSet<HexCoord> visited, List<HexCoord> path, List<MoveOption> output)
    {
        foreach (HexCoord target in GetJumpTargets(current, visited))
        {
            visited.Add(target);
            path.Add(target);
            output.Add(new MoveOption { PieceId = pieceId, From = origin, IsJump = true, Path = new List<HexCoord>(path) });
            CollectJumpMoves(pieceId, origin, target, visited, path, output);
            path.RemoveAt(path.Count - 1);
            visited.Remove(target);
        }
    }

    private List<HexCoord> GetOpeningTargets(HexCoord from)
    {
        List<HexCoord> targets = GetAdjacentTargets(from);
        targets.AddRange(GetJumpTargets(from, null));
        return targets;
    }

    private List<HexCoord> GetAdjacentTargets(HexCoord from)
    {
        List<HexCoord> targets = new();
        foreach (HexCoord direction in BoardLayout.Directions)
        {
            HexCoord target = from + direction;
            if (BoardLayout.IsValidCell(target) && !piecesByCoord.ContainsKey(target))
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    private List<HexCoord> GetJumpTargets(HexCoord from, HashSet<HexCoord>? visited)
    {
        if (RoomConfig.RuleVariant == RuleVariant.SpaceJump)
        {
            return GetSpaceJumpTargets(from, visited);
        }

        List<HexCoord> targets = new();
        foreach (HexCoord direction in BoardLayout.Directions)
        {
            HexCoord middle = from + direction;
            HexCoord landing = from + direction * 2;
            if (!BoardLayout.IsValidCell(landing) || !piecesByCoord.ContainsKey(middle) || piecesByCoord.ContainsKey(landing))
            {
                continue;
            }

            if (visited != null && visited.Contains(landing))
            {
                continue;
            }

            targets.Add(landing);
        }

        return targets;
    }

    private List<HexCoord> GetSpaceJumpTargets(HexCoord from, HashSet<HexCoord>? visited)
    {
        List<HexCoord> targets = new();
        foreach (HexCoord direction in BoardLayout.Directions)
        {
            int emptyCountBeforeCenter = 0;
            HexCoord cursor = from + direction;
            while (BoardLayout.IsValidCell(cursor) && !piecesByCoord.ContainsKey(cursor))
            {
                emptyCountBeforeCenter++;
                cursor += direction;
            }

            if (!BoardLayout.IsValidCell(cursor) || !piecesByCoord.ContainsKey(cursor))
            {
                continue;
            }

            bool blocked = false;
            HexCoord afterCenter = cursor + direction;
            for (int emptyIndex = 0; emptyIndex < emptyCountBeforeCenter; emptyIndex++)
            {
                if (!BoardLayout.IsValidCell(afterCenter) || piecesByCoord.ContainsKey(afterCenter))
                {
                    blocked = true;
                    break;
                }

                afterCenter += direction;
            }

            if (blocked || !BoardLayout.IsValidCell(afterCenter) || piecesByCoord.ContainsKey(afterCenter))
            {
                continue;
            }

            if (visited != null && visited.Contains(afterCenter))
            {
                continue;
            }

            targets.Add(afterCenter);
        }

        return targets;
    }

    private static bool IsAdjacentMove(HexCoord from, HexCoord target)
    {
        return from.DistanceTo(target) == 1;
    }

    private void FinishTurnInternal(bool checkWin = true)
    {
        SlotId previousSlot = CurrentPlayerSlot;
        string completionMessage = string.Empty;
        if (checkWin && !completedSlots.Contains(previousSlot) && HasPlayerWon(previousSlot))
        {
            completedSlots.Add(previousSlot);
            WinnerLabel = $"{BoardLayout.GetSlotLabel(previousSlot)} 已完成";
            completionMessage = $"{BoardLayout.GetSlotLabel(previousSlot)} 的棋子已经全部到达目标营地。";
        }

        selectedPieceId = -1;
        legalTargets.Clear();
        visitedJumpCells.Clear();
        currentTurnSteps.Clear();
        moveMode = MoveMode.None;
        HasMovedThisTurn = false;

        if (completedSlots.Count >= players.Count)
        {
            IsGameOver = true;
            StatusMessage = string.IsNullOrEmpty(completionMessage) ? "所有玩家都已完成。" : completionMessage + "\n所有玩家都已完成。";
            return;
        }

        AdvanceToNextUnfinishedPlayer();
        StatusMessage = string.IsNullOrEmpty(completionMessage)
            ? $"轮到 {BoardLayout.GetSlotLabel(CurrentPlayerSlot)}。"
            : completionMessage + "\n轮到 " + BoardLayout.GetSlotLabel(CurrentPlayerSlot) + "。";
    }

    private void AdvanceToNextUnfinishedPlayer()
    {
        for (int i = 0; i < players.Count; i++)
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
            if (!completedSlots.Contains(CurrentPlayerSlot))
            {
                return;
            }
        }
    }

    private bool HasPlayerWon(SlotId slotId)
    {
        HashSet<HexCoord> targetSet = new(BoardLayout.GetTargetCamp(slotId));
        return piecesById.Values.Where(value => value.Owner == slotId).All(piece => targetSet.Contains(piece.Position));
    }

    private static PieceState ClonePiece(PieceState piece)
    {
        return new PieceState { PieceId = piece.PieceId, Owner = piece.Owner, Position = piece.Position };
    }
}
