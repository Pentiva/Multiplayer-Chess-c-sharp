using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO.Compression;

namespace ChessClient.Game {
    /// <summary>
    /// Board containing all game information
    /// </summary>
    [Serializable]
    public class Board {
        /// <summary>
        /// Returned if no piece available
        /// </summary>
        public static readonly Nullpiece Null = new Nullpiece(30, 30);
        /// <summary>
        /// Returned if there was an error trying to get a piece
        /// </summary>
        private static readonly Nullpiece Error = new Nullpiece(40, 40);
        /// <summary>
        /// The colour of the "Black" Pieces
        /// </summary>
        public Color BlackColour { get; private set; }
        /// <summary>
        /// The colour of the "White" Pieces
        /// </summary>
        public Color WhiteColour { get; private set; }
        /// <summary>
        /// If it is currently player one's turn to move
        /// </summary>
        public bool PlayerOneTurn;
        /// <summary>
        /// Which mode this game is being played in
        /// </summary>
        private readonly Gametype _mode;
        /// <summary>
        /// List of Pieces on the Board
        /// </summary>
        public List<Piece> Pieces;
        /// <summary>
        /// First index and last index of the last move
        /// </summary>
        public Tuple<int, int> LastMove = Tuple.Create(0, 0);
        /// <summary>
        /// The last piece that was taken
        /// </summary>
        public Piece LastPiece = Null;
        /// <summary>
        /// List of piece that have been taken
        /// </summary>
        public List<Piece> TakenPieces = new List<Piece>(32);
        /// <summary>
        /// Keeps track of how many moves since a pawn moved or a piece was taken
        /// </summary>
        public int FiftyMoveRule;

        /// <summary>
        /// Create a new Board
        /// </summary>
        /// <param name="colour1">The new black colour</param>
        /// <param name="colour2">The new white colour</param>
        /// <param name="mode">The new game mode</param>
        public Board(Color colour1, Color colour2, Gametype mode) {
            // Assign Board properties
            _mode = mode;
            PlayerOneTurn = true;
            WhiteColour = Color.FromArgb(colour1.ToArgb());
            BlackColour = Color.FromArgb(colour2.ToArgb());

            // Setup the board based on Game type
            CreateBoard();

            // Get all the moves for the pieces
            if (Pieces == null) return;
            foreach (var piece in Pieces.Where(piece => piece != null))
                piece.GetMoves(this, true);
        }

        /// <summary>
        /// Create a new Board
        /// </summary>
        /// <param name="colour1">The new black colour</param>
        /// <param name="colour2">The new White colour</param>
        /// <param name="mode">The new game mode</param>
        /// <param name="pieces">Use these pieces instead of creating new ones</param>
        private Board(Color colour1, Color colour2, Gametype mode, List<Piece> pieces) {
            // Assign Board properties
            _mode = mode;
            WhiteColour = Color.FromArgb(colour1.ToArgb());
            BlackColour = Color.FromArgb(colour2.ToArgb());

            // Add all the pieces
            Pieces = pieces;
        }

        /// <summary>
        /// Populate the board with pieces based on game mode
        /// </summary>
        private void CreateBoard() {
            switch (_mode) {
                // A normal game
                case Gametype.Normal:
                    Pieces = new List<Piece>(32) {
                        new Rook(1, 1, BlackColour),
                        new Rook(8, 1, BlackColour),
                        new Rook(1, 8, WhiteColour),
                        new Rook(8, 8, WhiteColour),
                        new Knight(2, 1, BlackColour),
                        new Knight(7, 1, BlackColour),
                        new Knight(2, 8, WhiteColour),
                        new Knight(7, 8, WhiteColour),
                        new Bishop(3, 1, BlackColour),
                        new Bishop(6, 1, BlackColour),
                        new Bishop(3, 8, WhiteColour),
                        new Bishop(6, 8, WhiteColour),
                        new Queen(4, 1, BlackColour),
                        new King(5, 1, BlackColour),
                        new Queen(4, 8, WhiteColour),
                        new King(5, 8, WhiteColour)
                    };
                    // Pawns
                    for (var i = 1; i < 9; i++) {
                        Pieces.Add(new Pawn(i, 2, BlackColour));
                        Pieces.Add(new Pawn(i, 7, WhiteColour));
                    }
                    break;
                // Chess 960, randomize the position of pieces other than pawns
                case Gametype.Chess960:
                    var random = new Random();
                    var alreadyChosen = new List<int>();
                    var king = random.Next(2, 8);
                    alreadyChosen.Add(king);
                    var rook1 = random.Next(1, 9);
                    var rook2 = random.Next(1, 9);
                    while (((rook1 < king && rook2 < king) ||
                        (rook1 > king && rook2 > king)) && (rook1 != king && rook2 != king)) {
                        rook1 = random.Next(1, 9);
                        rook2 = random.Next(1, 9);
                    }
                    alreadyChosen.Add(rook1);
                    alreadyChosen.Add(rook2);
                    var bishop1 = random.Next(1, 9);
                    var bishop2 = random.Next(1, 9);
                    while (((bishop1 % 2 == 0 && bishop2 % 2 == 0) ||
                        (bishop1 % 2 == 1 && bishop2 % 2 == 1)) &&
                        (alreadyChosen.Contains(bishop1) && alreadyChosen.Contains(bishop2) && bishop1 != bishop2)) {
                        bishop1 = random.Next(1, 9);
                        bishop2 = random.Next(1, 9);
                    }
                    alreadyChosen.Add(bishop1);
                    alreadyChosen.Add(bishop2);

                    var queen = random.Next(1, 9);
                    while (alreadyChosen.Contains(queen))
                        queen = random.Next(1, 9);
                    alreadyChosen.Add(queen);
                    var knight1 = random.Next(1, 9);
                    while (alreadyChosen.Contains(knight1))
                        knight1 = random.Next(1, 9);
                    alreadyChosen.Add(knight1);
                    var knight2 = random.Next(1, 9);
                    while (alreadyChosen.Contains(knight2))
                        knight2 = random.Next(1, 9);
                    alreadyChosen.Add(knight2);

                    Pieces = new List<Piece>(32){
                        new King(alreadyChosen[0], 1, WhiteColour),
                        new King(9-alreadyChosen[0], 8, BlackColour),
                        new Rook(alreadyChosen[1], 1, WhiteColour),
                        new Rook(alreadyChosen[2], 1,WhiteColour),
                        new Rook(9-alreadyChosen[1], 8,BlackColour),
                        new Rook(9-alreadyChosen[2], 8,BlackColour),
                        new Bishop(alreadyChosen[3], 1, WhiteColour),
                        new Bishop(alreadyChosen[4], 1, WhiteColour),
                        new Bishop(9-alreadyChosen[3], 8, BlackColour),
                        new Bishop(9-alreadyChosen[4], 8, BlackColour),
                        new Queen(alreadyChosen[5], 1, WhiteColour),
                        new Queen(9-alreadyChosen[5], 8, BlackColour),
                        new Knight(alreadyChosen[6] , 1, WhiteColour),
                        new Knight(alreadyChosen[7] , 1, WhiteColour),
                        new Knight(9-alreadyChosen[6] , 8, BlackColour),
                        new Knight(9-alreadyChosen[7] , 8, BlackColour)
                    };

                    for (var i = 1; i < 9; i++) {
                        Pieces.Add(new Pawn(i, 2, WhiteColour));
                        Pieces.Add(new Pawn(i, 7, BlackColour));
                    }
                    break;
                case Gametype.Siege:
                    Pieces = new List<Piece>(32) {
                        new Rook(1, 1, BlackColour),
                        new Rook(8, 1, BlackColour),
                        new Knight(2, 1, BlackColour),
                        new Knight(7, 1, BlackColour),
                        new Bishop(3, 1, BlackColour),
                        new Bishop(6, 1, BlackColour),
                        new Queen(4, 1, BlackColour),
                        new King(5, 1, BlackColour)
                    };

                    for (var i = 1; i < 9; i++) {
                        Pieces.Add(new Pawn(i, 8, WhiteColour));
                        Pieces.Add(new Pawn(i, 7, WhiteColour));
                        Pieces.Add(new Pawn(i, 6, WhiteColour));
                        Pieces.Add(new Pawn(i, 5, WhiteColour));
                        Pieces.Add(new Pawn(i, 2, BlackColour));
                    }
                    break;
                // Don't set up the pieces
                case Gametype.DontCreate:
                    Pieces = new List<Piece>();
                    break;
            }
        }

        /// <summary>
        /// Loops through every piece in Pieces, checks if they match the colour parsed, 
        /// and if any of them can take the other colours king.
        /// </summary>
        /// <param name="colour">The colour of the piece to check if can take opponents King</param>
        /// <returns>BOOL: If the other colour (not parsed), is in check</returns>
        public bool CheckCheck(Color colour) {
            return Pieces.Where(piece => piece.Owner.Equals(colour)).Any(piece => piece.CheckCheck(this));
        }

        /// <summary>
        /// Loops through every piece in Pieces, checks if they match the colour parsed, 
        /// and if all of them have no moves to make.
        /// </summary>
        /// <param name="colour">The colour of the piece to check if in checkmate</param>
        /// <returns>BOOL: If the colour parsed can make any moves (checkmate)</returns>
        public bool CheckCheckMate(Color colour) {
            return Pieces.Where(piece => piece.Owner.Equals(colour)).All(piece => piece.Moves == null || piece.Moves.Count <= 0);
        }

        /// <summary>
        /// Checks if the move parsed will cause the piece parsed colours king to be in check.
        /// </summary>
        /// <param name="piece">The piece to move</param>
        /// <param name="move">The destination coordinate to check</param>
        /// <returns>BOOL: If moving the piece to the destination will cause the same colour piece to be in check</returns>
        public bool EndangerKing(Piece piece, Tuple<int, int> move) {
            // Create a fake board to move the piece in
            var fakeBoard = new Board(WhiteColour, BlackColour, Gametype.DontCreate);
            Piece copyPiece = Null;
            // Copy across all of the pieces, and assign the copy of piece to copyPiece
            foreach (var piece1 in Pieces) {
                fakeBoard.Pieces.Add(Piece.Clone(piece1));
                if (!piece1.Equals(piece)) continue;
                copyPiece = fakeBoard.Pieces.Last();
            }
            // Move the copied piece
            copyPiece.Move(fakeBoard, move.Item1, move.Item2);
            // Get the moves for all the opposite colour pieces
            foreach (var piece1 in fakeBoard.Pieces.Where(piece2 => piece2.Owner != copyPiece.Owner))
                piece1.GetMoves(fakeBoard, false);
            // Check if the king is in check
            return fakeBoard.CheckCheck(copyPiece.Owner.Equals(BlackColour) ? WhiteColour : BlackColour);
        }

        /// <summary>
        /// Loop through every Piece and return one that matches the x and y coords parsed
        /// </summary>
        /// <param name="x">X coordinate of piece</param>
        /// <param name="y">Y coordinate of piece</param>
        /// <returns>PIECE: The piece in Pieces which is at the same coordinate as parsed</returns>
        public Piece GetPiece(int x, int y) {
            if (!IsValid(x, y)) return Error;
            foreach (var piece in Pieces.Where(piece => piece.X == (byte)x && piece.Y == (byte)y))
                return piece;
            return Null;
        }

        /// <summary>
        /// Checks if the square is empty and if it is valid
        /// </summary>
        /// <param name="x">The X coordinate to check</param>
        /// <param name="y">The Y coordinate to check</param>
        /// <returns>BOOL: If the chosen square is empty and within the board</returns>
        public bool IsEmptyAndValid(int x, int y) {
            return IsSquareEmpty(x, y) && IsValid(x, y);
        }

        /// <summary>
        /// Checks if the square has a friendly piece and if it is valid
        /// </summary>
        /// <param name="x">The X coordinate to check</param>
        /// <param name="y">The Y coordinate to check</param>
        /// <param name="colour">The colour to check if friendly to</param>
        /// <returns>BOOL: If the chosen square does not have a friendly piece and is within the board</returns>
        public bool IsNotFriendlyAndValid(int x, int y, Color colour) {
            return !IsSquareFriendly(x, y, colour) && IsValid(x, y);
        }

        /// <summary>
        /// Checks if the square is empty and friendly
        /// </summary>
        /// <param name="x">The X coordinate to check</param>
        /// <param name="y">The X coordinate to check</param>
        /// <param name="colour">The colour to check if friendly to</param>
        /// <returns>BOOL: If the chosen square has a piece in and it is not friendly</returns>
        public bool IsNotEmptyAndNotFriendly(int x, int y, Color colour) {
            return !IsSquareEmpty(x, y) && !IsSquareFriendly(x, y, colour) && IsValid(x, y);
        }

        /// <summary>
        /// Checks if the square is valid
        /// </summary>
        /// <param name="x">The X coordinate to check</param>
        /// <param name="y">The Y coordinate to check</param>
        /// <returns>BOOL: If the chosen square is within the board</returns>
        private static bool IsValid(int x, int y) {
            return (x > 0 && x < 9 && y > 0 && y < 9);
        }

        /// <summary>
        /// Checks if the square is empty
        /// </summary>
        /// <param name="x">The X coordinate to check</param>
        /// <param name="y">The Y coordinate to check</param>
        /// <returns>BOOL: If the chosen square does not contain a piece</returns>
        private bool IsSquareEmpty(int x, int y) {
            return GetPiece(x, y).Equals(Null);
        }

        /// <summary>
        /// Checks if the square is friendly
        /// </summary>
        /// <param name="x">The X coordinate to check</param>
        /// <param name="y">The Y coordinate to check</param>
        /// <param name="colour">The colour to check if friendly to</param>
        /// <returns>BOOL: If the chosen square's piece is the same as parsed</returns>
        private bool IsSquareFriendly(int x, int y, Color colour) {
            return (GetPiece(x, y).Owner == colour);
        }

        /// <summary>
        /// Convert the Board object to an array of bytes
        /// </summary>
        /// <returns>A compressed array of Bytes containing the current board object</returns>
        public byte[] Serialize() {
            var result = new List<byte>();

            // Black Colour 4-Bytes
            var intBytes = BitConverter.GetBytes(BlackColour.ToArgb());
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            result.AddRange(intBytes);

            // White Colour 4-Bytes
            intBytes = BitConverter.GetBytes(WhiteColour.ToArgb());
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            result.AddRange(intBytes);

            // PlayerOneTurn 1-Bytes
            result.Add(Convert.ToByte(PlayerOneTurn));

            // GameMode 1-Bytes
            result.Add((byte)_mode);

            // LastMove 8-Bytes
            var lastMove = BitConverter.GetBytes(LastMove.Item1);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lastMove);
            result.AddRange(lastMove);
            lastMove = BitConverter.GetBytes(LastMove.Item2);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lastMove);
            result.AddRange(lastMove);

            // FiftyMoveRule
            intBytes = BitConverter.GetBytes(FiftyMoveRule);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            result.AddRange(intBytes);

            // Add lastPiece to pieces temporarily
            Pieces.Add(LastPiece);

            var takenPieceCount = (byte)TakenPieces.Count;
            result.Add(takenPieceCount);
            Pieces.AddRange(TakenPieces);

            // All Pieces ?-Bytes
            // Convert to byte array
            byte[] msgOut;
            using (var seri = new MemoryStream()) {
                var bin = new BinaryFormatter();
                bin.Serialize(seri, Pieces);
                msgOut = seri.ToArray();
            }
            // Compress Byte array
            using (var compr = new MemoryStream()) {
                using (var gzip = new GZipStream(compr, CompressionMode.Compress, true))
                    gzip.Write(msgOut, 0, msgOut.Length);
                msgOut = compr.ToArray();
            }
            result.AddRange(msgOut);

            // Remove the temporary piece
            var startOfTakenPieces = (Pieces.Count - takenPieceCount - 1);
            for (var i = Pieces.Count - 1; i > startOfTakenPieces; i--)
                Pieces.RemoveAt(i);
            Pieces.Remove(LastPiece);
            // Return all of the bytes
            return result.ToArray();
        }

        /// <summary>
        /// Returns a new Board object
        /// </summary>
        /// <param name="board">A previously serialized and compressed board object</param>
        /// <returns>BOARD: A board from the parsed byte array</returns>
        public static Board UnSerialize(IEnumerable<byte> board) {
            var bytes = new Queue<byte>(board);

            // Remove the first 4 bytes for Black colour
            var intBytes = new byte[4];
            for (var i = 0; i < 4; i++)
                intBytes[i] = bytes.Dequeue();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            var bCol = Color.FromArgb(BitConverter.ToInt32(intBytes.ToArray(), 0));

            // Remove the next 4 bytes for White colour
            intBytes = new byte[4];
            for (var i = 0; i < 4; i++)
                intBytes[i] = bytes.Dequeue();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            var wCol = Color.FromArgb(BitConverter.ToInt32(intBytes.ToArray(), 0));

            // Remove next byte for if it is player one's turn
            var playerOne = Convert.ToBoolean(bytes.Dequeue());
            // Remove next byte for game mode
            var mode = (Gametype)bytes.Dequeue();

            // Remove 8 byte for the Last Move
            var lastMove = new byte[4];
            for (var i = 0; i < 4; i++)
                lastMove[i] = bytes.Dequeue();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lastMove);
            var lastMoveStart = BitConverter.ToInt32(lastMove.ToArray(), 0);
            lastMove = new byte[4];
            for (var i = 0; i < 4; i++)
                lastMove[i] = bytes.Dequeue();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(lastMove);
            var lastMoveEnd = BitConverter.ToInt32(lastMove.ToArray(), 0);

            intBytes = new byte[4];
            for (var i = 0; i < 4; i++)
                intBytes[i] = bytes.Dequeue();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            var fifty = BitConverter.ToInt32(intBytes.ToArray(), 0);

            var takenCount = bytes.Dequeue();

            // Uncompress the byte array
            List<Piece> pieces;
            byte[] deserialize;
            using (var stream = new GZipStream(new MemoryStream(bytes.ToArray()), CompressionMode.Decompress)) {
                const int size = 4096;
                var buffer = new byte[size];
                using (var memory = new MemoryStream()) {
                    int count;
                    do {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                            memory.Write(buffer, 0, count);
                    }
                    while (count > 0);
                    deserialize = memory.ToArray();
                }
            }

            // Deserialize uncompressed byte array to a list of Pieces
            using (var stream = new MemoryStream(deserialize))
                pieces = (List<Piece>)new BinaryFormatter().Deserialize(stream);

            var takenpieces = pieces.Skip(pieces.Count - takenCount).ToList();
            pieces.RemoveRange(pieces.Count - takenCount, takenCount);

            // Create a new board from all the information
            var b = new Board(wCol, bCol, mode, pieces) {
                PlayerOneTurn = playerOne,
                LastMove = Tuple.Create(lastMoveStart, lastMoveEnd),
                LastPiece = pieces[pieces.Count - 1],
                TakenPieces = takenpieces,
                FiftyMoveRule = fifty
            };

            // Remove LastPiece from pieces
            b.Pieces.Remove(b.LastPiece);

            // Return new board
            return b;
        }

        /// <summary>
        /// An Enum of possible chess modes
        /// </summary>
        public enum Gametype {
            /// <summary>
            /// A normal game
            /// </summary>
            Normal = 0,
            /// <summary>
            /// The Chess960 game mode
            /// </summary>
            Chess960 = 1,
            /// <summary>
            /// The Siege game mode
            /// </summary>
            Siege = 2,
            /// <summary>
            /// Don't create any pieces
            /// </summary>
            DontCreate = 142
        }
    }
}