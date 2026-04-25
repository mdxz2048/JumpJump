#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweetJumpJump.Editor
{
    public static class StageOneVerifier
    {
        public static void Run()
        {
            GameOptions options = BoardLayout.CreateDefaultOptions();
            RoomConfig room = BoardLayout.CreateDefaultRoom(options);
            GameSession session = new GameSession(room);

            Assert(BoardLayout.AllCells.Count == 121, "棋盘格点数量必须为 121。");
            Assert(Enum.GetValues(typeof(SlotId)).Cast<SlotId>().All(slot => BoardLayout.GetCamp(slot).Count == 10), "每个阵营营地都必须是 10 格。");
            Assert(session.CurrentPlayerSlot == SlotId.Bottom, "默认房间应该由 Bottom 先手。");
            Assert(session.CurrentPlayerKind == PlayerKind.Human, "默认房间第一手应该是 Bottom 真人玩家。");

            PieceState movablePiece = session.Pieces.FirstOrDefault(piece => piece.Owner == SlotId.Bottom && session.TrySelectPiece(piece.Position, out _));
            Assert(movablePiece != null, "Bottom 真人方至少应有一个可行动棋子。");
            Assert(session.LegalTargets.Count > 0, "选中棋子后应该出现可落点。");

            session.TryPassTurn(out _);
            MoveOption aiMove = session.GetBestAiMove();
            Assert(aiMove != null, "普通 AI 应该能找到至少一个合法行动。");

            Debug.Log("StageOneVerifier passed: board=121, camps=10, default room, human turn, AI move.");
        }

        [MenuItem("Tools/SweetJumpJump/Verify Stage One")]
        public static void VerifyFromMenu()
        {
            Run();
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
