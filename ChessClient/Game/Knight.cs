using System;
using System.Collections.Generic;
using System.Drawing;

namespace ChessClient.Game {
    /// <summary>
    /// The Knight piece
    /// </summary>
    [Serializable]
    public class Knight : Piece {

        /// <summary>
        /// Create a new Knight
        /// </summary>
        /// <param name="x">The x coord of the piece</param>
        /// <param name="y">The y coord of the piece</param>
        /// <param name="colour">The colour of the piece</param>
        public Knight(int x, int y, Color colour)
            : base(x, y, colour, Pieces.Knight) { }

        /// <summary>
        /// Get all possible moves including moves that cause friendly king to be in check
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        /// <param name="check">Whether to remove moves that endanger king (can cause recursion errors)</param>
        public override void GetMoves(Board board, bool check) {
            Moves = new List<Tuple<int, int>>();
            // L-shapes
            for (var i = -2; i < 3; i++)
                for (var j = -2; j < 3; j++)
                    if (Math.Abs(i) != Math.Abs(j) && i != 0 && j != 0)
                        if (board.IsNotFriendlyAndValid(X + i, Y + j, Owner))
                            Moves.Add(Tuple.Create(X + i, Y + j));
            if (check) RemoveBad(board);
        }
    }
}