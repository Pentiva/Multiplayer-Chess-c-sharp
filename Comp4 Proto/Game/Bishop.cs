using System;
using System.Collections.Generic;
using System.Drawing;

namespace ChessClient.Game {
    /// <summary>
    /// The Bishop piece
    /// </summary>
    [Serializable]
    public class Bishop : Piece {

        /// <summary>
        /// Create a new Bishop
        /// </summary>
        /// <param name="x">The x coord of the piece</param>
        /// <param name="y">The y coord of the piece</param>
        /// <param name="colour">The colour of the piece</param>
        public Bishop(int x, int y, Color colour)
            : base(x, y, colour, Pieces.Bishop) { }

        /// <summary>
        /// Get all possible moves including moves that cause friendly king to be in check
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        /// <param name="check">Whether to remove moves that endanger king (can cause recursion errors)</param>
        public override void GetMoves(Board board, bool check) {
            Moves = new List<Tuple<int, int>>();
            // Top Right
            var count = 1;
            for (var i = X + 1; i < 9; i++) {
                if (board.IsEmptyAndValid(i, Y + count)) Moves.Add(Tuple.Create(i, Y + count));
                else if (board.IsNotFriendlyAndValid(i, Y + count, Owner)) {
                    Moves.Add(Tuple.Create(i, Y + count));
                    break;
                } else break;
                count++;
            }
            // Bottom Right
            count = 1;
            for (var i = X + 1; i < 9; i++) {
                if (board.IsEmptyAndValid(i, Y - count)) Moves.Add(Tuple.Create(i, Y - count));
                else if (board.IsNotFriendlyAndValid(i, Y - count, Owner)) {
                    Moves.Add(Tuple.Create(i, Y - count));
                    break;
                } else break;
                count++;
            }
            // Top Left
            count = 1;
            for (var i = X - 1; i > 0; i--) {
                if (board.IsEmptyAndValid(i, Y + count)) Moves.Add(Tuple.Create(i, Y + count));
                else if (board.IsNotFriendlyAndValid(i, Y + count, Owner)) {
                    Moves.Add(Tuple.Create(i, Y + count));
                    break;
                } else break;
                count++;
            }
            // Bottom Left
            count = 1;
            for (var i = X - 1; i > 0; i--) {
                if (board.IsEmptyAndValid(i, Y - count)) Moves.Add(Tuple.Create(i, Y - count));
                else if (board.IsNotFriendlyAndValid(i, Y - count, Owner)) {
                    Moves.Add(Tuple.Create(i, Y - count));
                    break;
                } else break;
                count++;
            }
            if (check) RemoveBad(board);
        }
    }
}