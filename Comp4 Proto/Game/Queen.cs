using System;
using System.Collections.Generic;
using System.Drawing;

namespace ChessClient.Game {
    /// <summary>
    /// The Queen Piece
    /// </summary>
    [Serializable]
    public class Queen : Piece {

        /// <summary>
        /// Create a new Queen
        /// </summary>
        /// <param name="x">The x coord of the piece</param>
        /// <param name="y">The y coord of the piece</param>
        /// <param name="colour">The colour of the piece</param>
        public Queen(int x, int y, Color colour)
            : base(x, y, colour, Pieces.Queen) { }

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
            // Right
            for (var i = X + 1; i < 9; i++)
                if (board.IsEmptyAndValid(i, Y))
                    Moves.Add(Tuple.Create(i, (int)Y));
                else if (board.IsNotFriendlyAndValid(i, Y, Owner)) {
                    Moves.Add(Tuple.Create(i, (int)Y));
                    break;
                } else
                    break;
            // Left
            for (var i = X - 1; i > 0; i--)
                if (board.IsEmptyAndValid(i, Y))
                    Moves.Add(Tuple.Create(i, (int)Y));
                else if (board.IsNotFriendlyAndValid(i, Y, Owner)) {
                    Moves.Add(Tuple.Create(i, (int)Y));
                    break;
                } else
                    break;
            // Down
            for (var i = Y + 1; i < 9; i++)
                if (board.IsEmptyAndValid(X, i))
                    Moves.Add(Tuple.Create((int)X, i));
                else if (board.IsNotFriendlyAndValid(X, i, Owner)) {
                    Moves.Add(Tuple.Create((int)X, i));
                    break;
                } else
                    break;
            // Up
            for (var i = Y - 1; i > 0; i--)
                if (board.IsEmptyAndValid(X, i))
                    Moves.Add(Tuple.Create((int)X, i));
                else if (board.IsNotFriendlyAndValid(X, i, Owner)) {
                    Moves.Add(Tuple.Create((int)X, i));
                    break;
                } else
                    break;
            if (check) RemoveBad(board);
        }
    }
}