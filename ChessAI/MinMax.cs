//using System;
//using System.Collections.Generic;
//using System.Linq;
//using ChessClient.Game;
//using System.Drawing;

//namespace ChessAI {
//    class MinMax {

//        public MinMax() {
//            Console.BackgroundColor = ConsoleColor.Gray;
//            Console.CursorVisible = false;
//            Console.Clear();
//            var b = new Board(Color.White, Color.Black, Board.Gametype.DontCreate) {
//                Pieces = new List<Piece>
//                {
//                    new Pawn(3, 3, Color.White),
//                    new Rook(8, 8, Color.Black),
//                    new King(8, 6, Color.White),
//                    new King(1, 1, Color.Black)
//                }
//            };


//            foreach (var piece in b.Pieces) {
//                piece.GetMoves(b, true);
//            }
//            DrawChess(b);

//            var a = Piece(b);
//            Console.WriteLine(a);
//        }

//        static Tuple<Tuple<int, int>, Piece> Piece(Board board) {
//            var best = Tuple.Create<Tuple<int, int>, float, Piece>(Tuple.Create(0, 0), 0f, null);
//            foreach (var piece in board.Pieces) {
//                var newBoard = Board.Clone(board);
//                var a = Piece(piece, newBoard);
//                if (a.Item2 > best.Item2) best = Tuple.Create(a.Item1, a.Item2, piece);
//            }
//            return Tuple.Create(best.Item1, best.Item3);
//        }

//        static Tuple<Tuple<int, int>, float> Piece(Piece p, Board board) {
//            var best = Tuple.Create(Tuple.Create(0, 0), 0f);
//            var listOfGood = new List<Tuple<int, int>>();
//            foreach (var move in p.Moves) {
//                var newPiece = ChessClient.Game.Piece.Clone(p);
//                var newBoard = Board.Clone(board);
//                var a = OptimumRoute(newPiece, move, newBoard, 0);
//                if (a > best.Item2) {
//                    best = Tuple.Create(move, a);
//                    if (listOfGood.Count != 0)
//                        listOfGood.RemoveRange(0, listOfGood.Count - 1);
//                }
//                if (Math.Abs(a - best.Item2) < 0.00000001f) listOfGood.Add(move);
//            }
//            if (listOfGood.Count > 0)
//                best = Tuple.Create(listOfGood[new Random().Next(0, listOfGood.Count)], best.Item2);
//            return best;
//        }

//        static float OptimumRoute(Piece p, Tuple<int, int> move, Board board, int depth) {
//            if (depth == 5) return 0f;

//            var total = 0f;
//            total += RatePiece(board.GetPiece(p.X + move.Item1, p.Y + move.Item2).PieceType);
//            var smallTotal = 0f;
//            p.Move(board, move.Item1, move.Item2);

//            foreach (var piece in board.Pieces.Where(piece2 => !piece2.Owner.Equals(p.Owner))) {
//                piece.GetMoves(board, true);
//                smallTotal += piece.Moves.Sum(move2 => RatePiece(board.GetPiece(piece.X + move2.Item1, piece.Y + move2.Item2).PieceType));
//                smallTotal /= RatePiece(piece.PieceType);
//            }

//            total -= smallTotal;
//            smallTotal = 0f;

//            foreach (var piece in board.Pieces.Where(piece2 => piece2.Owner.Equals(p.Owner))) {
//                piece.GetMoves(board, true);
//                smallTotal += piece.Moves.Sum(move2 => RatePiece(board.GetPiece(piece.X + move2.Item1, piece.Y + move2.Item2).PieceType));
//                smallTotal /= RatePiece(piece.PieceType);
//            }

//            total += smallTotal;
//            var best = 0f;
//            var listOfGood = new List<Tuple<int, int>>();

//            foreach (var move3 in p.Moves) {
//                var clonePiece3 = ChessClient.Game.Piece.Clone(p);
//                var cloneBoard3 = Board.Clone(board);
//                var next = OptimumRoute(clonePiece3, move3, cloneBoard3, depth + 1);
//                if (next > best) {
//                    best = next;
//                    listOfGood.RemoveRange(0, listOfGood.Count - 1);
//                }
//                if (Math.Abs(next - best) < 0.00000001f) listOfGood.Add(move3);
//            }
//            // var best = (from move3 in p.Moves let clonePiece3 = ChessClient.Game.Piece.Clone(p) let cloneBoard3 = Board.Clone(board) select OptimumRoute(clonePiece3, move3, cloneBoard3, depth + 1)).Concat(new[] { 0f }).Max();

//            //if (listOfGood.Count > 0)
//            //{
//            //    new Random().Next(0, listOfGood.Count);
//            //}

//            return total + best + (0.001f * listOfGood.Count);
//        }

//        static void DrawChess(Board board) {
//            for (var i = 1; i < 9; i++)
//                for (var j = 1; j < 9; j++) {
//                    var piece = board.GetPiece(i, j);
//                    if (piece.Equals(Board.Null)) continue;
//                    Console.SetCursorPosition(i * 2, j);
//                    Console.ForegroundColor = piece.Owner.Equals(Color.FromArgb(Color.Black.ToArgb())) ? ConsoleColor.Black : ConsoleColor.White;
//                    Console.Write(PieceIcon(piece.PieceType));
//                }
//        }

//        static Char PieceIcon(Pieces p) {
//            switch (p) {
//                case Pieces.Rook:
//                    return 'R';
//                case Pieces.Bishop:
//                    return 'B';
//                case Pieces.Queen:
//                    return 'Q';
//                case Pieces.King:
//                    return 'K';
//                case Pieces.Knight:
//                    return 'N';
//                case Pieces.Pawn:
//                    return 'P';
//            }
//            return 'h';
//        }

//        static float RatePiece(Pieces p) {
//            switch (p) {
//                case Pieces.Rook:
//                    return 5;
//                case Pieces.Bishop:
//                    return 3;
//                case Pieces.Queen:
//                    return 9;
//                case Pieces.Knight:
//                    return 3;
//                case Pieces.King:
//                    return float.MaxValue;
//                case Pieces.Pawn:
//                    return 1;
//            }
//            return 0;
//        }
//    }
//}
