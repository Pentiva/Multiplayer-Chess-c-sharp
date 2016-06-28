using System;
using System.Collections.Generic;
using System.Drawing;

namespace ChessClient.Game {
    /// <summary>
    /// The King piece
    /// </summary>
    [Serializable]
    public class King : Piece {

        /// <summary>
        /// Create a new King
        /// </summary>
        /// <param name="x">The x coord of the piece</param>
        /// <param name="y">The y coord of the piece</param>
        /// <param name="colour">The colour of the piece</param>
        public King(int x, int y, Color colour)
            : base(x, y, colour, Pieces.King) { }

        /// <summary>
        /// Get all possible moves including moves that cause friendly king to be in check
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        /// <param name="check">Whether to remove moves that endanger king (can cause recursion errors)</param>
        public override void GetMoves(Board board, bool check) {
            Moves = new List<Tuple<int, int>>();
            // All squares around the king
            for (var i = X - 1; i < X + 2; i++)
                for (var j = Y - 1; j < Y + 2; j++)
                    if (board.IsNotFriendlyAndValid(i, j, Owner))
                        Moves.Add(Tuple.Create(i, j));

            // Castling
            if (MoveCount == 0) {
                var colour = (Owner.Equals(board.WhiteColour) ? 8 : 1);
                if (X == 5 && Y == colour) {
                    if (board.GetPiece(8, colour).PieceType == Pieces.Rook && board.GetPiece(8, colour).Owner == Owner)
                        if (board.IsEmptyAndValid(6, colour) && board.IsEmptyAndValid(7, colour))
                            Moves.Add(Tuple.Create(7, colour));
                    if (board.GetPiece(1, colour).PieceType == Pieces.Rook && board.GetPiece(1, colour).Owner == Owner)
                        if (board.IsEmptyAndValid(2, colour) && board.IsEmptyAndValid(3, colour) && board.IsEmptyAndValid(4, colour))
                            Moves.Add(Tuple.Create(3, colour));
                }
            }

            if (check) RemoveBad(board);
        }
    }
}