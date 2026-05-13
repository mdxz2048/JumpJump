using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SweetJumpJump
{
    public sealed class PieceState
    {
        public int PieceId;
        public SlotId Owner;
        public HexCoord Position;
    }

    public sealed class PlayerState
    {
        public SlotId SlotId;
        public PlayerKind PlayerKind;
    }

    public sealed class MoveOption
    {
        public int PieceId;
        public HexCoord From;
        public bool IsJump;
        public List<HexCoord> Path = new List<HexCoord>();
        public string DebugLabel;

        public HexCoord FinalPosition
        {
            get { return Path.Count == 0 ? From : Path[Path.Count - 1]; }
        }
    }

    public static class BoardLayout
    {
        private static readonly List<HexCoord> AllCellsInternal = GenerateAllCells();
        private static readonly HashSet<HexCoord> CellSet = new HashSet<HexCoord>(AllCellsInternal);
        private static readonly Dictionary<SlotId, List<HexCoord>> Camps = GenerateCamps();
        private static readonly Dictionary<SlotId, SlotId> OppositeSlots = new Dictionary<SlotId, SlotId>
        {
            { SlotId.Top, SlotId.Bottom },
            { SlotId.Bottom, SlotId.Top },
            { SlotId.TopLeft, SlotId.BottomRight },
            { SlotId.BottomRight, SlotId.TopLeft },
            { SlotId.TopRight, SlotId.BottomLeft },
            { SlotId.BottomLeft, SlotId.TopRight }
        };

        private static readonly Dictionary<SlotId, HexCoord> TargetAnchors = new Dictionary<SlotId, HexCoord>
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
            new HexCoord(1, 0),
            new HexCoord(1, -1),
            new HexCoord(0, -1),
            new HexCoord(-1, 0),
            new HexCoord(-1, 1),
            new HexCoord(0, 1)
        };

        public static IReadOnlyList<HexCoord> AllCells
        {
            get { return AllCellsInternal; }
        }

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
            switch (slotId)
            {
                case SlotId.Top:
                    return "上方";
                case SlotId.TopRight:
                    return "右上";
                case SlotId.BottomRight:
                    return "右下";
                case SlotId.Bottom:
                    return "下方";
                case SlotId.BottomLeft:
                    return "左下";
                case SlotId.TopLeft:
                    return "左上";
                default:
                    return slotId.ToString();
            }
        }

        public static string GetRuleLabel(RuleVariant ruleVariant)
        {
            return ruleVariant == RuleVariant.SpaceJump ? "空跳" : "一子跳";
        }

        public static string GetPlayerKindLabel(PlayerKind playerKind)
        {
            switch (playerKind)
            {
                case PlayerKind.Human:
                    return "真人玩家";
                case PlayerKind.AiBeginner:
                    return "人机 - 初学者";
                case PlayerKind.AiNormal:
                    return "人机 - 普通";
                case PlayerKind.AiAdvanced:
                    return "人机 - 高级";
                default:
                    return "无玩家";
            }
        }

        public static PlayerKind GetNextPlayerKind(PlayerKind playerKind)
        {
            switch (playerKind)
            {
                case PlayerKind.None:
                    return PlayerKind.Human;
                case PlayerKind.Human:
                    return PlayerKind.AiBeginner;
                case PlayerKind.AiBeginner:
                    return PlayerKind.AiNormal;
                case PlayerKind.AiNormal:
                    return PlayerKind.AiAdvanced;
                default:
                    return PlayerKind.None;
            }
        }

        public static bool IsAi(PlayerKind playerKind)
        {
            return playerKind == PlayerKind.AiBeginner || playerKind == PlayerKind.AiNormal || playerKind == PlayerKind.AiAdvanced;
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

        public static Color GetPieceColor(SlotId slotId)
        {
            switch (slotId)
            {
                case SlotId.Top:
                    // 顶部阵营棋子颜色：橙棕色。
                    return new Color(0.88f, 0.65f, 0.48f);
                case SlotId.TopRight:
                    // 右上阵营棋子颜色：蓝色。
                    return new Color(0.47f, 0.66f, 0.96f);
                case SlotId.BottomRight:
                    // 右下阵营棋子颜色：黄绿色。
                    return new Color(0.78f, 0.91f, 0.42f);
                case SlotId.Bottom:
                    // 底部阵营棋子颜色：粉红。
                    return new Color(0.92f, 0.55f, 0.68f);
                case SlotId.BottomLeft:
                    // 左下阵营棋子颜色：紫色。
                    return new Color(0.64f, 0.48f, 0.96f);
                case SlotId.TopLeft:
                    // 左上阵营棋子颜色：青绿色。
                    return new Color(0.58f, 0.73f, 0.68f);
                default:
                    // 兜底颜色，一般不会显示。
                    return Color.white;
            }
        }

        public static RoomConfig CreateDefaultRoom(GameOptions options)
        {
            RoomConfig room = new RoomConfig();
            room.RoomId = "default-room";
            room.RoomName = "默认房间";
            room.RuleVariant = RuleVariant.OnePieceJump;
            room.PromptEnabled = options != null && options.PromptEnabled;
            room.PromptIntervalSeconds = options != null ? options.PromptIntervalSeconds : 30;
            room.SoundEnabled = options == null || options.SoundEnabled;
            room.MusicEnabled = options == null || options.MusicEnabled;
            room.ThemeId = options == null ? "pink" : options.ThemeId;
            room.Slots = new List<SlotConfig>
            {
                new SlotConfig { SlotId = SlotId.Bottom, PlayerKind = PlayerKind.Human },
                new SlotConfig { SlotId = SlotId.Top, PlayerKind = PlayerKind.AiNormal }
            };
            return room;
        }

        public static RoomConfig CreateNewRoom(GameOptions options, int roomNumber)
        {
            RoomConfig room = CreateDefaultRoom(options);
            room.RoomId = Guid.NewGuid().ToString("N");
            room.RoomName = string.Format("新房间 {0}", roomNumber);
            room.RuleVariant = options == null ? RuleVariant.OnePieceJump : options.DefaultRule;
            return room;
        }

        public static GameOptions CreateDefaultOptions()
        {
            return new GameOptions
            {
                DefaultRule = RuleVariant.OnePieceJump,
                PromptEnabled = false,
                PromptIntervalSeconds = 30,
                SoundEnabled = true,
                MusicEnabled = true,
                ThemeId = "pink",
                CustomMusicPath = string.Empty
            };
        }

        public static bool ValidateRoom(RoomConfig room, out string message)
        {
            message = string.Empty;
            if (room == null)
            {
                message = "房间不存在。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(room.RoomName))
            {
                message = "房间名不能为空。";
                return false;
            }

            if (room.PromptIntervalSeconds <= 0)
            {
                message = "催促时间必须大于 0。";
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
            Dictionary<SlotId, PlayerKind> slots = new Dictionary<SlotId, PlayerKind>();
            foreach (SlotId slotId in GetSlotsInDisplayOrder())
            {
                slots[slotId] = PlayerKind.None;
            }

            if (room == null || room.Slots == null)
            {
                return slots;
            }

            for (int i = 0; i < room.Slots.Count; i++)
            {
                slots[room.Slots[i].SlotId] = room.Slots[i].PlayerKind;
            }

            return slots;
        }

        public static void NormalizeRoomSlots(RoomConfig room)
        {
            Dictionary<SlotId, PlayerKind> slotMap = GetSlotMap(room);
            room.Slots = GetSlotsInDisplayOrder()
                .Where(slot => slotMap[slot] != PlayerKind.None)
                .Select(slot => new SlotConfig { SlotId = slot, PlayerKind = slotMap[slot] })
                .ToList();
        }

        public static RoomConfig CloneRoom(RoomConfig room)
        {
            RoomConfig clone = new RoomConfig
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                RuleVariant = room.RuleVariant,
                PromptEnabled = room.PromptEnabled,
                PromptIntervalSeconds = room.PromptIntervalSeconds,
                SoundEnabled = room.SoundEnabled,
                MusicEnabled = room.MusicEnabled,
                ThemeId = room.ThemeId,
                Slots = new List<SlotConfig>()
            };

            if (room.Slots != null)
            {
                for (int i = 0; i < room.Slots.Count; i++)
                {
                    clone.Slots.Add(new SlotConfig
                    {
                        SlotId = room.Slots[i].SlotId,
                        PlayerKind = room.Slots[i].PlayerKind
                    });
                }
            }

            return clone;
        }

        private static List<HexCoord> GenerateAllCells()
        {
            List<HexCoord> cells = new List<HexCoord>();
            Dictionary<int, Vector2Int> rowRanges = new Dictionary<int, Vector2Int>
            {
                { -8, new Vector2Int(4, 4) },
                { -7, new Vector2Int(3, 4) },
                { -6, new Vector2Int(2, 4) },
                { -5, new Vector2Int(1, 4) },
                { -4, new Vector2Int(-4, 8) },
                { -3, new Vector2Int(-4, 7) },
                { -2, new Vector2Int(-4, 6) },
                { -1, new Vector2Int(-4, 5) },
                { 0, new Vector2Int(-4, 4) },
                { 1, new Vector2Int(-5, 4) },
                { 2, new Vector2Int(-6, 4) },
                { 3, new Vector2Int(-7, 4) },
                { 4, new Vector2Int(-8, 4) },
                { 5, new Vector2Int(-4, -1) },
                { 6, new Vector2Int(-4, -2) },
                { 7, new Vector2Int(-4, -3) },
                { 8, new Vector2Int(-4, -4) }
            };

            foreach (KeyValuePair<int, Vector2Int> row in rowRanges)
            {
                for (int q = row.Value.x; q <= row.Value.y; q++)
                {
                    cells.Add(new HexCoord(q, row.Key));
                }
            }

            return cells;
        }

        private static Dictionary<SlotId, List<HexCoord>> GenerateCamps()
        {
            Dictionary<SlotId, List<HexCoord>> camps = new Dictionary<SlotId, List<HexCoord>>
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

        private readonly Dictionary<int, PieceState> piecesById = new Dictionary<int, PieceState>();
        private readonly Dictionary<HexCoord, PieceState> piecesByCoord = new Dictionary<HexCoord, PieceState>();
        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly List<HexCoord> legalTargets = new List<HexCoord>();
        private readonly HashSet<HexCoord> visitedJumpCells = new HashSet<HexCoord>();
        private readonly List<TurnStep> currentTurnSteps = new List<TurnStep>();
        private readonly HashSet<SlotId> completedSlots = new HashSet<SlotId>();
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

        public RoomConfig RoomConfig { get; private set; }

        public string StatusMessage { get; private set; }

        public bool HasMovedThisTurn { get; private set; }

        public bool IsGameOver { get; private set; }

        public string WinnerLabel { get; private set; }

        public SlotId CurrentPlayerSlot
        {
            get { return players[currentPlayerIndex].SlotId; }
        }

        public PlayerKind CurrentPlayerKind
        {
            get { return players[currentPlayerIndex].PlayerKind; }
        }

        public bool CanPass
        {
            get { return !IsGameOver && CurrentPlayerKind == PlayerKind.Human && !HasMovedThisTurn; }
        }

        public bool CanFinishTurn
        {
            get { return !IsGameOver && CurrentPlayerKind == PlayerKind.Human && HasMovedThisTurn; }
        }

        public bool CanUndo
        {
            get { return !IsGameOver && CurrentPlayerKind == PlayerKind.Human && currentTurnSteps.Count > 0; }
        }

        public int SelectedPieceId
        {
            get { return selectedPieceId; }
        }

        public IReadOnlyList<HexCoord> AllCells
        {
            get { return BoardLayout.AllCells; }
        }

        public IReadOnlyList<HexCoord> LegalTargets
        {
            get { return legalTargets; }
        }

        public IEnumerable<PieceState> Pieces
        {
            get { return piecesById.Values; }
        }

        public string CurrentPlayerLabel
        {
            get
            {
                string role = BoardLayout.GetPlayerKindLabel(CurrentPlayerKind);
                return string.Format("{0} · {1}", BoardLayout.GetSlotLabel(CurrentPlayerSlot), role);
            }
        }

        public PieceState GetPieceAt(HexCoord coord)
        {
            PieceState piece;
            return piecesByCoord.TryGetValue(coord, out piece) ? piece : null;
        }

        public PieceState GetPieceById(int pieceId)
        {
            PieceState piece;
            return piecesById.TryGetValue(pieceId, out piece) ? piece : null;
        }

        public bool TryMovePieceById(int pieceId, HexCoord target, out string message)
        {
            PieceState piece = GetPieceById(pieceId);
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

            if (selectedPieceId != pieceId)
            {
                if (!TrySelectPiece(piece.Position, out message))
                {
                    return false;
                }
            }

            return TryMoveSelectedPiece(target, out message);
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

            PieceState piece = GetPieceAt(coord);
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
            StatusMessage = string.Format("已选中 {0} 的棋子。", BoardLayout.GetSlotLabel(piece.Owner));
            return true;
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
            piecesById[piece.PieceId] = piece;

            HasMovedThisTurn = true;
            currentTurnSteps.Add(new TurnStep
            {
                PieceId = piece.PieceId,
                From = from,
                To = target,
                IsJump = isJump
            });

            if (!isJump)
            {
                moveMode = MoveMode.AdjacentDone;
                legalTargets.Clear();
                StatusMessage = "相邻移动完成，点击“完成移动”结束回合，或点击“悔棋”重走。";
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

            if (legalTargets.Count > 0)
            {
                StatusMessage = "可以继续跳跃，或点击“完成移动”结束回合。";
            }
            else
            {
                StatusMessage = "没有更多可跳位置了，点击“完成移动”结束回合。";
            }

            return true;
        }

        public bool TryUndo(out string message)
        {
            message = string.Empty;

            if (!CanUndo)
            {
                message = "当前没有可悔棋的步骤。";
                return false;
            }

            TurnStep step = currentTurnSteps[currentTurnSteps.Count - 1];
            currentTurnSteps.RemoveAt(currentTurnSteps.Count - 1);

            PieceState piece = piecesById[step.PieceId];
            piecesByCoord.Remove(piece.Position);
            piece.Position = step.From;
            piecesByCoord[piece.Position] = piece;
            piecesById[piece.PieceId] = piece;

            selectedPieceId = piece.PieceId;
            legalTargets.Clear();
            visitedJumpCells.Clear();

            if (currentTurnSteps.Count == 0)
            {
                HasMovedThisTurn = false;
                moveMode = MoveMode.None;
                selectedPieceId = -1;
                StatusMessage = "已悔棋到本回合起点，请重新选择棋子。";
            }
            else
            {
                HasMovedThisTurn = true;
                bool jumpChain = currentTurnSteps.Any(value => value.IsJump);
                moveMode = jumpChain ? MoveMode.JumpChain : MoveMode.AdjacentDone;

                if (moveMode == MoveMode.JumpChain)
                {
                    visitedJumpCells.Add(currentTurnSteps[0].From);
                    for (int i = 0; i < currentTurnSteps.Count; i++)
                    {
                        visitedJumpCells.Add(currentTurnSteps[i].To);
                    }

                    legalTargets.AddRange(GetJumpTargets(piece.Position, null));
                    StatusMessage = legalTargets.Count > 0
                        ? "已撤回上一步，可以继续跳跃或完成移动。"
                        : "已撤回上一步，可以完成移动。";
                }
                else
                {
                    StatusMessage = "已撤回到相邻移动后，可以完成移动。";
                }
            }

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

        public MoveOption GetBestAiMove()
        {
            List<MoveOption> moves = GetAllMovesForSlot(CurrentPlayerSlot);
            if (moves.Count == 0)
            {
                return null;
            }

            if (CurrentPlayerKind == PlayerKind.AiBeginner)
            {
                return moves
                    .OrderBy(move => move.IsJump ? 1 : 0)
                    .ThenBy(move => move.From.Q)
                    .ThenBy(move => move.From.R)
                    .First();
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

        public void ApplyAiMove(MoveOption move)
        {
            if (move == null)
            {
                StatusMessage = "AI 没有合法行动，自动放弃移动。";
                FinishTurnInternal(false);
                return;
            }

            PieceState piece = piecesById[move.PieceId];
            piecesByCoord.Remove(piece.Position);

            for (int i = 0; i < move.Path.Count; i++)
            {
                piece.Position = move.Path[i];
            }

            piecesByCoord[piece.Position] = piece;
            piecesById[piece.PieceId] = piece;
            HasMovedThisTurn = true;
            StatusMessage = string.Format("{0} 完成了 {1}。", CurrentPlayerLabel, move.IsJump ? "跳跃行动" : "相邻移动");
            FinishTurnInternal();
        }

        public HexCoord ApplyAiMoveStep(MoveOption move, int pathIndex)
        {
            if (move == null)
            {
                StatusMessage = "AI 没有合法行动，自动放弃移动。";
                FinishTurnInternal(false);
                return default(HexCoord);
            }

            if (pathIndex < 0 || pathIndex >= move.Path.Count)
            {
                throw new ArgumentOutOfRangeException("pathIndex");
            }

            PieceState piece = piecesById[move.PieceId];
            piecesByCoord.Remove(piece.Position);
            piece.Position = move.Path[pathIndex];
            piecesByCoord[piece.Position] = piece;
            piecesById[piece.PieceId] = piece;
            HasMovedThisTurn = true;

            bool isLastStep = pathIndex == move.Path.Count - 1;
            StatusMessage = isLastStep
                ? string.Format("{0} 完成了 {1}。", CurrentPlayerLabel, move.IsJump ? "跳跃行动" : "相邻移动")
                : string.Format("{0} 正在连续跳跃。", CurrentPlayerLabel);

            if (isLastStep)
            {
                FinishTurnInternal();
            }

            return piece.Position;
        }

        // Apply a server-authoritative STATE snapshot to this session.
        // Used in online mode where the server is the rules authority.
        public void ApplySnapshot(OnlineGameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            // Rebuild piece maps
            piecesById.Clear();
            piecesByCoord.Clear();
            if (snapshot.pieces != null)
            {
                for (int i = 0; i < snapshot.pieces.Length; i++)
                {
                    OnlinePieceState ps = snapshot.pieces[i];
                    SlotId owner;
                    if (!Enum.TryParse(ps.owner, true, out owner))
                    {
                        continue;
                    }

                    HexCoord pos = ps.position != null ? new HexCoord(ps.position.q, ps.position.r) : default;
                    PieceState piece = new PieceState
                    {
                        PieceId = ps.pieceId,
                        Owner = owner,
                        Position = pos
                    };
                    piecesById[piece.PieceId] = piece;
                    piecesByCoord[piece.Position] = piece;
                }
            }

            // Rebuild player list from snapshot.players (preserves turn order)
            players.Clear();
            if (snapshot.players != null)
            {
                foreach (SlotId preferred in PreferredTurnOrder)
                {
                    for (int i = 0; i < snapshot.players.Length; i++)
                    {
                        OnlinePlayerEntry entry = snapshot.players[i];
                        SlotId entrySlot;
                        if (!Enum.TryParse(entry.slotId, true, out entrySlot))
                        {
                            continue;
                        }

                        if (entrySlot == preferred)
                        {
                            PlayerKind kind;
                            if (!Enum.TryParse(entry.playerKind, true, out kind))
                            {
                                kind = PlayerKind.Human;
                            }

                            players.Add(new PlayerState { SlotId = entrySlot, PlayerKind = kind });
                            break;
                        }
                    }
                }
            }

            // Set current player index
            SlotId currentSlot;
            if (!string.IsNullOrEmpty(snapshot.currentPlayerSlot) && Enum.TryParse(snapshot.currentPlayerSlot, true, out currentSlot))
            {
                currentPlayerIndex = 0;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].SlotId == currentSlot)
                    {
                        currentPlayerIndex = i;
                        break;
                    }
                }
            }

            // Legal targets
            legalTargets.Clear();
            if (snapshot.legalTargets != null)
            {
                for (int i = 0; i < snapshot.legalTargets.Length; i++)
                {
                    OnlineHexCoord c = snapshot.legalTargets[i];
                    legalTargets.Add(new HexCoord(c.q, c.r));
                }
            }

            selectedPieceId = snapshot.selectedPieceId > 0 ? snapshot.selectedPieceId : -1;
            HasMovedThisTurn = snapshot.hasMovedThisTurn;
            IsGameOver = snapshot.isGameOver;
            StatusMessage = snapshot.statusMessage ?? string.Empty;
            WinnerLabel = snapshot.winnerLabel ?? string.Empty;

            // Infer move mode
            moveMode = selectedPieceId >= 0
                ? (HasMovedThisTurn ? MoveMode.JumpChain : MoveMode.None)
                : MoveMode.None;

            // Keep completedSlots in sync
            completedSlots.Clear();
            foreach (PlayerState player in players)
            {
                if (HasPlayerWon(player.SlotId))
                {
                    completedSlots.Add(player.SlotId);
                }
            }

            // Clear transient state
            visitedJumpCells.Clear();
            currentTurnSteps.Clear();
        }

        private void BuildPlayers(RoomConfig roomConfig)
        {
            Dictionary<SlotId, PlayerKind> configuredKinds = new Dictionary<SlotId, PlayerKind>();
            for (int i = 0; i < roomConfig.Slots.Count; i++)
            {
                configuredKinds[roomConfig.Slots[i].SlotId] = roomConfig.Slots[i].PlayerKind;
            }

            foreach (SlotId slotId in PreferredTurnOrder)
            {
                PlayerKind kind;
                if (!configuredKinds.TryGetValue(slotId, out kind) || kind == PlayerKind.None)
                {
                    continue;
                }

                players.Add(new PlayerState
                {
                    SlotId = slotId,
                    PlayerKind = kind
                });
            }

            currentPlayerIndex = 0;
        }

        private void BuildPieces()
        {
            int nextPieceId = 1;
            foreach (PlayerState player in players)
            {
                IReadOnlyList<HexCoord> camp = BoardLayout.GetCamp(player.SlotId);
                for (int i = 0; i < camp.Count; i++)
                {
                    PieceState piece = new PieceState
                    {
                        PieceId = nextPieceId++,
                        Owner = player.SlotId,
                        Position = camp[i]
                    };
                    piecesById[piece.PieceId] = piece;
                    piecesByCoord[piece.Position] = piece;
                }
            }

            StatusMessage = string.Format("{0} 先手。", CurrentPlayerLabel);
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
            List<MoveOption> moves = new List<MoveOption>();

            foreach (PieceState piece in piecesById.Values.Where(value => value.Owner == slotId))
            {
                List<HexCoord> adjacentTargets = GetAdjacentTargets(piece.Position);
                for (int i = 0; i < adjacentTargets.Count; i++)
                {
                    MoveOption move = new MoveOption
                    {
                        PieceId = piece.PieceId,
                        From = piece.Position,
                        IsJump = false
                    };
                    move.Path.Add(adjacentTargets[i]);
                    moves.Add(move);
                }

                List<HexCoord> path = new List<HexCoord>();
                HashSet<HexCoord> visited = new HashSet<HexCoord> { piece.Position };
                CollectJumpMoves(piece.PieceId, piece.Position, piece.Position, visited, path, moves);
            }

            return moves;
        }

        private void CollectJumpMoves(
            int pieceId,
            HexCoord origin,
            HexCoord current,
            HashSet<HexCoord> visited,
            List<HexCoord> path,
            List<MoveOption> output)
        {
            List<HexCoord> jumpTargets = GetJumpTargets(current, visited);

            for (int i = 0; i < jumpTargets.Count; i++)
            {
                HexCoord target = jumpTargets[i];
                visited.Add(target);
                path.Add(target);

                MoveOption move = new MoveOption
                {
                    PieceId = pieceId,
                    From = origin,
                    IsJump = true,
                    Path = new List<HexCoord>(path)
                };
                output.Add(move);

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
            List<HexCoord> targets = new List<HexCoord>();
            for (int i = 0; i < BoardLayout.Directions.Length; i++)
            {
                HexCoord target = from + BoardLayout.Directions[i];
                if (BoardLayout.IsValidCell(target) && !piecesByCoord.ContainsKey(target))
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private List<HexCoord> GetJumpTargets(HexCoord from, HashSet<HexCoord> visited)
        {
            if (RoomConfig.RuleVariant == RuleVariant.SpaceJump)
            {
                return GetSpaceJumpTargets(from, visited);
            }

            List<HexCoord> targets = new List<HexCoord>();

            for (int i = 0; i < BoardLayout.Directions.Length; i++)
            {
                HexCoord direction = BoardLayout.Directions[i];
                HexCoord middle = from + direction;
                HexCoord landing = from + (direction * 2);

                if (!BoardLayout.IsValidCell(landing))
                {
                    continue;
                }

                if (!piecesByCoord.ContainsKey(middle) || piecesByCoord.ContainsKey(landing))
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

        private List<HexCoord> GetSpaceJumpTargets(HexCoord from, HashSet<HexCoord> visited)
        {
            List<HexCoord> targets = new List<HexCoord>();

            for (int i = 0; i < BoardLayout.Directions.Length; i++)
            {
                HexCoord direction = BoardLayout.Directions[i];
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
                WinnerLabel = string.Format("{0} 已完成", BoardLayout.GetSlotLabel(previousSlot));
                completionMessage = string.Format("{0} 的棋子已经全部到达目标营地。", BoardLayout.GetSlotLabel(previousSlot));
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
                StatusMessage = string.IsNullOrEmpty(completionMessage)
                    ? "所有玩家都已完成。"
                    : completionMessage + "\n所有玩家都已完成。";
                return;
            }

            AdvanceToNextUnfinishedPlayer();
            StatusMessage = string.IsNullOrEmpty(completionMessage)
                ? string.Format("轮到 {0}。", CurrentPlayerLabel)
                : completionMessage + "\n轮到 " + CurrentPlayerLabel + "。";
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
            IReadOnlyList<HexCoord> targetCamp = BoardLayout.GetTargetCamp(slotId);
            HashSet<HexCoord> targetSet = new HashSet<HexCoord>(targetCamp);
            List<PieceState> playerPieces = piecesById.Values.Where(value => value.Owner == slotId).ToList();

            for (int i = 0; i < playerPieces.Count; i++)
            {
                if (!targetSet.Contains(playerPieces[i].Position))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
