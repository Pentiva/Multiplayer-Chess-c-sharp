using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ChessClient.Game {
    /// <summary>
    /// The base for every piece
    /// </summary>
    [Serializable]
    public abstract class Piece {
        /// <summary>
        /// The X coordinate of the Piece
        /// </summary>
        public byte X { get; private set; }
        /// <summary>
        /// The Y coordinate of the Piece
        /// </summary>
        public byte Y { get; private set; }
        /// <summary>
        /// The colour of the pieces owner
        /// </summary>
        public Color Owner { get; private set; }
        /// <summary>
        /// Which piece it is
        /// </summary>
        public Pieces PieceType { get; private set; }
        /// <summary>
        /// List of possible moves this piece can do
        /// </summary>
        public List<Tuple<int, int>> Moves = new List<Tuple<int, int>>();
        /// <summary>
        /// The amount of times a piece this piece has moved
        /// </summary>
        public int MoveCount;

        /// <summary>
        /// Create a new piece
        /// </summary>
        /// <param name="x">The new X coordinate of the piece</param>
        /// <param name="y">The new Y coordinate of the piece</param>
        /// <param name="colour">The new colour of the piece</param>
        /// <param name="piece">What piece it is</param>
        protected Piece(int x, int y, Color colour, Pieces piece) {
            X = (byte)x;
            Y = (byte)y;
            Owner = colour;
            PieceType = piece;
        }

        /// <summary>
        /// Get all possible moves including moves that cause friendly king to be in check
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        /// <param name="check">Whether to remove moves that endanger king (can cause recursion errors)</param>
        public abstract void GetMoves(Board board, bool check);

        /// <summary>
        /// Removes all moves that endanger your sides king
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        protected void RemoveBad(Board board) {
            for (var i = Moves.Count - 1; i >= 0; i--)
                if (board.EndangerKing(this, Moves[i]))
                    Moves.RemoveAt(i);
        }

        /// <summary>
        /// Check if this piece takes an opposing king
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        /// <returns>BOOL: If this piece can take a king</returns>
        public bool CheckCheck(Board board) {
            return Moves != null && Moves.Any(move => board.GetPiece(move.Item1, move.Item2).PieceType == Pieces.King);
        }

        /// <summary>
        /// Move the piece to a new position
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        /// <param name="x">The new X coordinate of the Piece</param>
        /// <param name="y">The new Y coordinate of the Piece</param>
        public void Move(Board board, int x, int y) {
            board.FiftyMoveRule++;
            // Get the piece to be taken (can be null)
            var oldPiece = board.GetPiece(x, y);
            // Take Piece (Remove from list of Pieces)
            board.LastPiece = oldPiece;
            if (board.Pieces.Contains(oldPiece)) {
                board.TakenPieces.Add(oldPiece);
                board.Pieces.Remove(oldPiece);
                board.FiftyMoveRule = 0;
            }
            // If pawn is moving, reset counter
            if (PieceType == Pieces.Pawn) board.FiftyMoveRule = 0;

            // Move piece to new position
            X = (byte)x;
            Y = (byte)y;
            // Update amount of times moved
            MoveCount++;
        }

        /// <summary>
        /// Copy all relevant properties to a new Piece
        /// </summary>
        /// <param name="piece">The piece to copy from</param>
        /// <returns>PIECE: A new piece with the same values as parsed</returns>
        public static Piece Clone(Piece piece) {
            Piece newPiece = new Nullpiece(30, 30);
            switch (piece.PieceType) {
                case Pieces.Rook:
                    newPiece = new Rook(piece.X, piece.Y, piece.Owner);
                    break;
                case Pieces.Bishop:
                    newPiece = new Bishop(piece.X, piece.Y, piece.Owner);
                    break;
                case Pieces.Queen:
                    newPiece = new Queen(piece.X, piece.Y, piece.Owner);
                    break;
                case Pieces.King:
                    newPiece = new King(piece.X, piece.Y, piece.Owner);
                    break;
                case Pieces.Knight:
                    newPiece = new Knight(piece.X, piece.Y, piece.Owner);
                    break;
                case Pieces.Pawn:
                    newPiece = new Pawn(piece.X, piece.Y, piece.Owner);
                    break;
            }
            return newPiece;
        }

        /// <summary>
        /// Determines whether the specified Piece is equal to the current Piece.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object.</param>
        public override bool Equals(object obj) {
            if (!(obj is Piece)) return false;
            var piece = (Piece)obj;
            return (piece.X == X) && (piece.Y == Y) && (piece.Owner.Equals(Owner))
                   && (piece.PieceType.Equals(PieceType)) && (piece.MoveCount == MoveCount)
                   && (piece.Moves.SequenceEqual(Moves));
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current Piece.
        /// </returns>
        public override int GetHashCode() {
            return X * Y + Owner.R;
        }
    }

    /// <summary>
    /// A Piece only used instead of null.
    /// </summary>
    [Serializable]
    public class Nullpiece : Piece {
        /// <summary>
        /// Create a new Null piece
        /// </summary>
        /// <param name="a">It's fake x coord</param>
        /// <param name="b">It's fake y coord</param>
        public Nullpiece(int a, int b)
            : base(a, b, Color.IndianRed, Pieces.Null) { }

        /// <summary>
        /// Get all possible moves including moves that cause friendly king to be in check
        /// </summary>
        /// <param name="board">The board that this piece is on</param>
        /// <param name="check">Whether to remove moves that endanger king (can cause recursion errors)</param>
        public override void GetMoves(Board board, bool check) { }
    }

    /// <summary>
    /// An Enum of possible chess pieces
    /// </summary>
    public enum Pieces {
        /// <summary>
        /// The rook piece (castle)
        /// </summary>
        Rook = 0,
        /// <summary>
        /// The bishop piece (pointy hat)
        /// </summary>
        Bishop = 1,
        /// <summary>
        /// The queen piece (point crown)
        /// </summary>
        Queen = 2,
        /// <summary>
        /// The king piece (single point crown)
        /// </summary>
        King = 3,
        /// <summary>
        /// The knight piece (horse)
        /// </summary>
        Knight = 4,
        /// <summary>
        /// The pawn piece (front ones)
        /// </summary>
        Pawn = 5,
        /// <summary>
        /// Null piece
        /// </summary>
        Null = 6
    }
}