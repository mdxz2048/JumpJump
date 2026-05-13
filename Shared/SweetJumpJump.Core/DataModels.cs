using System.Text.Json.Serialization;

namespace SweetJumpJump.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SlotId
{
    Top,
    TopRight,
    BottomRight,
    Bottom,
    BottomLeft,
    TopLeft
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlayerKind
{
    None,
    Human,
    AiBeginner,
    AiNormal,
    AiAdvanced
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleVariant
{
    OnePieceJump,
    SpaceJump
}

public enum MoveMode
{
    None,
    AdjacentDone,
    JumpChain
}

public readonly record struct HexCoord(int Q, int R)
{
    public int S => -Q - R;

    public static HexCoord operator +(HexCoord left, HexCoord right)
    {
        return new HexCoord(left.Q + right.Q, left.R + right.R);
    }

    public static HexCoord operator -(HexCoord left, HexCoord right)
    {
        return new HexCoord(left.Q - right.Q, left.R - right.R);
    }

    public static HexCoord operator *(HexCoord value, int multiplier)
    {
        return new HexCoord(value.Q * multiplier, value.R * multiplier);
    }

    public int DistanceTo(HexCoord other)
    {
        HexCoord delta = this - other;
        return (Math.Abs(delta.Q) + Math.Abs(delta.R) + Math.Abs(delta.S)) / 2;
    }

    public override string ToString()
    {
        return $"({Q}, {R})";
    }
}

public sealed class TurnStep
{
    public int PieceId { get; set; }
    public HexCoord From { get; set; }
    public HexCoord To { get; set; }
    public bool IsJump { get; set; }
}

public sealed class SlotConfig
{
    public SlotId SlotId { get; set; }
    public PlayerKind PlayerKind { get; set; }
}

public sealed class RoomConfig
{
    public string RoomId { get; set; } = "default-room";
    public string RoomName { get; set; } = "默认房间";
    public RuleVariant RuleVariant { get; set; } = RuleVariant.OnePieceJump;
    public List<SlotConfig> Slots { get; set; } = new();
}

public sealed class PieceState
{
    public int PieceId { get; set; }
    public SlotId Owner { get; set; }
    public HexCoord Position { get; set; }
}

public sealed class PlayerState
{
    public SlotId SlotId { get; set; }
    public PlayerKind PlayerKind { get; set; }
}

public sealed class MoveOption
{
    public int PieceId { get; set; }
    public HexCoord From { get; set; }
    public bool IsJump { get; set; }
    public List<HexCoord> Path { get; set; } = new();
    public HexCoord FinalPosition => Path.Count == 0 ? From : Path[^1];
}

public sealed class GameSnapshot
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public RuleVariant RuleVariant { get; set; }
    public SlotId CurrentPlayerSlot { get; set; }
    public PlayerKind CurrentPlayerKind { get; set; }
    public int SelectedPieceId { get; set; }
    public bool HasMovedThisTurn { get; set; }
    public bool IsGameOver { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string WinnerLabel { get; set; } = string.Empty;
    public List<PieceState> Pieces { get; set; } = new();
    public List<HexCoord> LegalTargets { get; set; } = new();
    public List<PlayerState> Players { get; set; } = new();
}
