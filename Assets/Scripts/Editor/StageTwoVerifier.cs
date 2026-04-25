#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweetJumpJump.Editor
{
    public static class StageTwoVerifier
    {
        public static void Run()
        {
            VerifyRoomValidation();
            VerifyUndo();
            VerifySpaceJumpAndAiDifficulties();
            Debug.Log("StageTwoVerifier passed: room validation, undo, space jump, and AI difficulties.");
        }

        [MenuItem("Tools/SweetJumpJump/Verify Stage Two")]
        public static void VerifyFromMenu()
        {
            Run();
        }

        private static void VerifyRoomValidation()
        {
            GameOptions options = BoardLayout.CreateDefaultOptions();
            RoomConfig room = BoardLayout.CreateDefaultRoom(options);
            string message;
            Assert(BoardLayout.ValidateRoom(room, out message), "默认房间应通过校验。");

            RoomConfig fourPlayerRoom = BoardLayout.CreateNewRoom(options, 2);
            fourPlayerRoom.Slots = new List<SlotConfig>
            {
                new SlotConfig { SlotId = SlotId.Bottom, PlayerKind = PlayerKind.Human },
                new SlotConfig { SlotId = SlotId.Top, PlayerKind = PlayerKind.AiNormal },
                new SlotConfig { SlotId = SlotId.TopLeft, PlayerKind = PlayerKind.Human },
                new SlotConfig { SlotId = SlotId.BottomRight, PlayerKind = PlayerKind.AiBeginner }
            };
            Assert(BoardLayout.ValidateRoom(fourPlayerRoom, out message), "4 人成对房间应通过校验。");

            RoomConfig invalidRoom = BoardLayout.CloneRoom(room);
            invalidRoom.Slots = new List<SlotConfig>
            {
                new SlotConfig { SlotId = SlotId.Bottom, PlayerKind = PlayerKind.Human },
                new SlotConfig { SlotId = SlotId.TopRight, PlayerKind = PlayerKind.AiNormal }
            };
            Assert(!BoardLayout.ValidateRoom(invalidRoom, out message), "未成对房间应校验失败。");
        }

        private static void VerifyUndo()
        {
            RoomConfig room = BoardLayout.CreateDefaultRoom(BoardLayout.CreateDefaultOptions());
            GameSession session = new GameSession(room);
            PieceState piece = session.Pieces.First(value => value.Owner == SlotId.Bottom && session.TrySelectPiece(value.Position, out _));
            HexCoord origin = piece.Position;
            HexCoord target = session.LegalTargets[0];

            string message;
            Assert(session.TryMoveSelectedPiece(target, out message), "真人玩家应能执行合法移动。");
            Assert(session.CanUndo, "移动后应允许悔棋。");
            Assert(session.TryUndo(out message), "悔棋应成功。");
            Assert(session.GetPieceAt(origin) != null, "悔棋后棋子应回到原点。");
            Assert(!session.HasMovedThisTurn, "撤回本回合第一步后应回到未移动状态。");
        }

        private static void VerifySpaceJumpAndAiDifficulties()
        {
            foreach (PlayerKind kind in new[] { PlayerKind.AiBeginner, PlayerKind.AiNormal, PlayerKind.AiAdvanced })
            {
                RoomConfig room = BoardLayout.CreateDefaultRoom(BoardLayout.CreateDefaultOptions());
                room.RuleVariant = RuleVariant.SpaceJump;
                room.Slots = new List<SlotConfig>
                {
                    new SlotConfig { SlotId = SlotId.Bottom, PlayerKind = PlayerKind.Human },
                    new SlotConfig { SlotId = SlotId.Top, PlayerKind = kind }
                };

                GameSession session = new GameSession(room);
                string message;
                Assert(session.TryPassTurn(out message), "真人先手应能放弃移动以进入 AI 回合。");
                MoveOption move = session.GetBestAiMove();
                Assert(move != null, string.Format("{0} 应能在空跳规则下找到合法行动。", kind));
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
#endif
