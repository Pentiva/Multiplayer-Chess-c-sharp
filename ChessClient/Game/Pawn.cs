using System;
using System.Collections.Generic;
using System.Drawing;

namespace ChessClient.Game {
    /// <summary>
    /// The pawn piece
    /// </summary>
    [Serializable]
    public class Pawn : Piece {

        /// <summary>
        /// Create a new Pawn
        /// </summary>
        /// <param name="x">The x coord of the piece</param>
        /// <param name="y">The y coord of the piece</param>
        /// <param name="colour">The colour of the piece</param>
        public Pawn(int x, int y, Color colour)
            : base(x, y, colour, Pieces.Pawn) { }

        /// <summary>
        /// Get all possible moves including moves that cause friendly king to be in check
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        /// <param name="check">Whether to remove moves that endanger king (can cause recursion errors)</param>
        public override void GetMoves(Board board, bool check) {
            Moves = new List<Tuple<int, int>>();

            // Move Forward
            var colour = Owner == board.WhiteColour ? -1 : 1;
            if (board.IsEmptyAndValid(X, Y + colour)) {
                Moves.Add(Tuple.Create((int)X, Y + colour));
                if (Y == (Owner == board.BlackColour ? 2 : 7))
                    // Starting move
                    if (board.IsEmptyAndValid(X, Y + (colour * 2)))
                        Moves.Add(Tuple.Create((int)X, Y + (colour * 2)));
            }

            // Taking Pieces
            if (board.IsNotEmptyAndNotFriendly(X - 1, Y + colour, Owner))
                Moves.Add(Tuple.Create(X - 1, Y + colour));
            if (board.IsNotEmptyAndNotFriendly(X + 1, Y + colour, Owner))
                Moves.Add(Tuple.Create(X + 1, Y + colour));

            // En passant
            var lastStart = board.LastMove.Item1;
            var lastEnd = board.LastMove.Item2;

            if (board.GetPiece((lastEnd % 8) + 1, (lastEnd / 8) + 1).PieceType == Pieces.Pawn)
                if (!board.GetPiece((lastEnd % 8) + 1, (lastEnd / 8) + 1).Owner.Equals(Owner))
                    if ((lastStart / 8) - 2 == (lastEnd / 8) || (lastStart / 8) + 2 == (lastEnd / 8))
                        if ((lastEnd / 8) + 1 == Y)
                            if ((lastEnd % 8) == X)
                                Moves.Add(Tuple.Create(X + 1, Y + colour));
                            else if ((lastEnd % 8) + 2 == X)
                                Moves.Add(Tuple.Create(X - 1, Y + colour));

            if (check) RemoveBad(board);
        }
    }
}