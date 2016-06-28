using System;
using System.Collections.Generic;
using System.Drawing;

namespace ChessClient.Game {
    /// <summary>
    /// The Rook Piece
    /// </summary>
    [Serializable]
    public class Rook : Piece {

        /// <summary>
        /// Create a new Rook
        /// </summary>
        /// <param name="x">The x coord of the piece</param>
        /// <param name="y">The y coord of the piece</param>
        /// <param name="colour">The colour of the piece</param>
        public Rook(int x, int y, Color colour)
            : base(x, y, colour, Pieces.Rook) { }

        /// <summary>
        /// Get all possible moves including moves that cause friendly king to be in check
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        /// <param name="check">Whether to remove moves that endanger king (can cause recursion errors)</param>
        public override void GetMoves(Board board, bool check) {
            Moves = new List<Tuple<int, int>>();
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