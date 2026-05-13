using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweetJumpJump
{
    public enum SlotId
    {
        Top,
        TopRight,
        BottomRight,
        Bottom,
        BottomLeft,
        TopLeft
    }

    public enum PlayerKind
    {
        None,
        Human,
        AiBeginner,
        AiNormal,
        AiAdvanced
    }

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

    [Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        public int Q;
        public int R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int S
        {
            get { return -Q - R; }
        }

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
            return (Mathf.Abs(delta.Q) + Mathf.Abs(delta.R) + Mathf.Abs(delta.S)) / 2;
        }

        public bool Equals(HexCoord other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Q * 397) ^ R;
            }
        }

        public override string ToString()
        {
            return string.Format("({0}, {1})", Q, R);
        }
    }

    [Serializable]
    public sealed class TurnStep
    {
        public int PieceId;
        public HexCoord From;
        public HexCoord To;
        public bool IsJump;
    }

    [Serializable]
    public sealed class SlotConfig
    {
        public SlotId SlotId;
        public PlayerKind PlayerKind;
    }

    [Serializable]
    public sealed class RoomConfig
    {
        public string RoomId = "default-room";
        public string RoomName = "默认房间";
        public RuleVariant RuleVariant = RuleVariant.OnePieceJump;
        public bool PromptEnabled;
        public int PromptIntervalSeconds = 30;
        public bool SoundEnabled = true;
        public bool MusicEnabled = true;
        public string ThemeId = "pink";
        public List<SlotConfig> Slots = new List<SlotConfig>();
    }

    [Serializable]
    public sealed class GameOptions
    {
        public RuleVariant DefaultRule = RuleVariant.OnePieceJump;
        public bool PromptEnabled;
        public int PromptIntervalSeconds = 30;
        public bool SoundEnabled = true;
        public bool MusicEnabled = true;
        public string ThemeId = "pink";
        public string CustomMusicPath = string.Empty;
        public string OnlinePlayerToken = string.Empty;
        public string OnlinePlayerName = string.Empty;
        public string OnlinePlayerAccount = string.Empty;
        public string OnlinePlayerPassword = string.Empty;
    }

    // ---- Online protocol data models (WebSocket / new server) ----

    [Serializable]
    public sealed class OnlineHexCoord
    {
        public int q;
        public int r;
    }

    [Serializable]
    public sealed class OnlinePieceState
    {
        public int pieceId;
        public string owner;
        public OnlineHexCoord position;
    }

    [Serializable]
    public sealed class OnlinePlayerEntry
    {
        public string slotId;
        public string playerKind;
    }

    [Serializable]
    public sealed class OnlineGameSnapshot
    {
        public string currentPlayerSlot;
        public string currentPlayerKind;
        public int selectedPieceId;
        public bool hasMovedThisTurn;
        public bool isGameOver;
        public string statusMessage;
        public string winnerLabel;
        public OnlinePieceState[] pieces;
        public OnlineHexCoord[] legalTargets;
        public OnlinePlayerEntry[] players;
    }

    [Serializable]
    public sealed class OnlineSeatSummary
    {
        public string slot;
        public string name;
        public string kind;
        public bool isHost;
    }

    [Serializable]
    public sealed class OnlineRoomSummary
    {
        public string roomKey;
        public bool started;
        public string ruleVariant;
        public string hostClientId;
        public OnlineSeatSummary[] players;
    }

    // Wrapper types for JsonUtility array deserialization
    [Serializable]
    public sealed class OnlineSeatList { public OnlineSeatSummary[] seats; }
    [Serializable]
    public sealed class OnlineRoomList { public OnlineRoomSummary[] rooms; }
    [Serializable]
    public sealed class OnlineRoomSummaryWrapper { public OnlineRoomSummary room; }

    [Serializable]
    public sealed class AppSaveData
    {
        public int SaveVersion = 4;
        public bool Initialized;
        public GameOptions Options = new GameOptions();
        public List<RoomConfig> Rooms = new List<RoomConfig>();
    }
}
