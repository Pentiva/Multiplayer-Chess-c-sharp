using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using ChessClient.Game;
using ChessClient.Net;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace ChessAI {
    public class ChessGame {

        [DllImport("kernel32")]
        private static extern bool AllocConsole();

        public Board Board;
        private Tuple<int, int> _lastPlayerOneMove;
        private Tuple<int, int> _lastPlayerTwoMove;

        const int MaxGenerations = 1000;
        const int ChildRate = 10;
        const int MutationRate = 25;

        public ChessGame(string gameName, bool displayBoard) {
            AllocConsole();

            Console.ReadLine();

            Thread.Sleep(2000);
            var cA = new Client(IPAddress.Loopback, "AI1", (byte)new Random().Next(256));
            cA.SendMessage(Packet.Packets.Connect, cA.ClientName);

            //var r = new Random();
            //var col1 = Color.FromArgb(r.Next(0, 256), r.Next(0, 256), r.Next(0, 256));
            //var col2 = Color.FromArgb(255 - col1.R, 255 - col1.G, 255 - col1.B);
            var cB = new Client(IPAddress.Loopback, "AI2", (byte)new Random().Next(256));
            cB.SendMessage(Packet.Packets.Connect, cB.ClientName);

            cA.SendMessage(Packet.Packets.Game, "ADD", gameName, "0", "ff000000", "ffffffff");

            var acceptedA = cA.SendMessage(Packet.Packets.Game, "JOIN", gameName);
            if (acceptedA.Item1.Code == Packet.Packets.Error)
                return;

            var acceptedB = cB.SendMessage(Packet.Packets.Game, "JOIN", gameName);
            if (acceptedB.Item1.Code == Packet.Packets.Error)
                return;

            var retirn = cA.SendMessage(Packet.Packets.Game, "GET", gameName, "NoUpdate");
            Board = Board.UnSerialize(retirn.Item2);
            DrawBoard();

            var a = new NeuralNetwork(new Random(), true, this);
            var b = new NeuralNetwork(new Random(), false, this);

            //Thread.Sleep(4000);
            var gameInfo = GameLoop(gameName, displayBoard, a, b, cA, cB);

            //Console.WriteLine("End Game");
            //Console.WriteLine("White: " + gameInfo.WhiteCheckMate);
            //Console.WriteLine("Black: " + gameInfo.BlackCheckMate);
            //Console.WriteLine("Check W: " + gameInfo.AmountCheckW);
            //Console.WriteLine("Check B: " + gameInfo.AmountCheckB);
            //Console.WriteLine("Move Count W: " + gameInfo.MoveCountW);
            //Console.WriteLine("Move Count B: " + gameInfo.MoveCountB);
            //Console.WriteLine("Total Time W: " + gameInfo.TotalTimeW);
            //Console.WriteLine("Total Time B: " + gameInfo.TotalTimeB);
            //Console.WriteLine("Draw: " + gameInfo.Draw);

            cA.SendMessage(Packet.Packets.DisconnectGame, gameName, cA.ClientName, "PLAYING");
            cB.SendMessage(Packet.Packets.DisconnectGame, gameName, cB.ClientName, "PLAYING");

            var whiteOld = a;
            var blackOld = b;
            for (var i = 0; i < MaxGenerations; i++) {
                Console.WriteLine("Generation " + i);

                cA.SendMessage(Packet.Packets.Game, "ADD", gameName, "0", "ff000000", "ffffffff");

                acceptedA = cA.SendMessage(Packet.Packets.Game, "JOIN", gameName);
                if (acceptedA.Item1.Code == Packet.Packets.Error)
                    return;

                acceptedB = cB.SendMessage(Packet.Packets.Game, "JOIN", gameName);
                if (acceptedB.Item1.Code == Packet.Packets.Error)
                    return;

                retirn = cA.SendMessage(Packet.Packets.Game, "GET", gameName, "NoUpdate");
                Board = Board.UnSerialize(retirn.Item2);

                var children = RunGeneration(gameName, whiteOld, blackOld, cA, cB);
                whiteOld = children.Item1;
                blackOld = children.Item2;

                cA.SendMessage(Packet.Packets.DisconnectGame, gameName, cA.ClientName, "PLAYING");
                cB.SendMessage(Packet.Packets.DisconnectGame, gameName, cB.ClientName, "PLAYING");
            }


            cA.IsRunning = false;
            cA.DisposeSocket();
            cB.IsRunning = false;
            cB.DisposeSocket();

            Console.Read();
        }

        private Tuple<NeuralNetwork, NeuralNetwork> RunGeneration(string gameName, ICloneable a, ICloneable b, Client cA, Client cB) {
            var generationFromA = new List<NeuralNetwork>(ChildRate);
            var generationFromB = new List<NeuralNetwork>(ChildRate);
            for (var j = 0; j < ChildRate; j++) {
                generationFromA.Add(Mutate((NeuralNetwork)a.Clone()));
                generationFromB.Add(Mutate((NeuralNetwork)b.Clone()));
            }

            var whiteFitnesses = new double[ChildRate];
            var blackFitnesses = new double[ChildRate];
            var blackWinCount = new int[ChildRate];
            for (var j = 0; j < ChildRate; j++) {
                var winCount = 0;
                var totalFitness = 0d;
                for (var k = 0; k < ChildRate; k++) {
                    Console.WriteLine(j + " " + k);

                    var g = GameLoop(gameName, false, generationFromA[j], generationFromB[k], cA, cB);
                    totalFitness += g.CalculateFitness(true);
                    blackFitnesses[k] += g.CalculateFitness(false);

                    if (g.WhiteCheckMate) winCount++; else if (g.BlackCheckMate) blackWinCount[k]++;
                }

                totalFitness /= winCount;
                whiteFitnesses[j] = totalFitness;
            }

            for (var j = 0; j < ChildRate; j++)
                blackFitnesses[j] /= blackWinCount[j];

            var best = Tuple.Create(0, 0d);
            var secondBest = Tuple.Create(0, 0d);
            for (var j = 0; j < ChildRate; j++) {
                if (whiteFitnesses[j] >= best.Item2) {
                    secondBest = best;
                    best = Tuple.Create(j, whiteFitnesses[j]);
                } else if (whiteFitnesses[j] < best.Item2 && whiteFitnesses[j] >= secondBest.Item2)
                    secondBest = Tuple.Create(j, whiteFitnesses[j]);
            }
            var breedWhite = Breed(generationFromA[best.Item1], generationFromA[secondBest.Item1]);

            best = Tuple.Create(0, 0d);
            secondBest = Tuple.Create(0, 0d);
            for (var j = 0; j < ChildRate; j++) {
                if (blackFitnesses[j] >= best.Item2) {
                    secondBest = best;
                    best = Tuple.Create(j, blackFitnesses[j]);
                } else if (blackFitnesses[j] < best.Item2 && blackFitnesses[j] >= secondBest.Item2)
                    secondBest = Tuple.Create(j, blackFitnesses[j]);
            }
            var breedBlack = Breed(generationFromB[best.Item1], generationFromB[secondBest.Item1]);

            return Tuple.Create(breedWhite, breedBlack);
        }

        private static NeuralNetwork Breed(NeuralNetwork a, NeuralNetwork b) {
            return a.Breed((NeuralNetwork)b.Clone(), a.WeightingCount > b.WeightingCount ? a.WeightingCount : b.WeightingCount);
        }

        private static NeuralNetwork Mutate(NeuralNetwork n) {
            var r = new Random();
            var i = r.Next(ChildRate + 1);
            var direction = "";
            if (i < ChildRate / 3) direction = "UP";
            else if (i > ChildRate / 3 && i < 2 * ChildRate / 3) direction = "DOWN";
            else if (i > 2 * ChildRate / 3) direction = "RANDOM";
            n.EditWeightings(direction);
            return n;
        }

        private struct GameReturn {
            public bool WhiteCheckMate;
            public bool BlackCheckMate;
            public int AmountCheckW;
            public int AmountCheckB;
            public int MoveCountW;
            public int MoveCountB;
            public long TotalTimeW;
            public long TotalTimeB;
            public bool Draw;

            public double CalculateFitness(bool white) {
                var checkMate = white ? WhiteCheckMate : BlackCheckMate;
                var amountCheck = white ? AmountCheckW : AmountCheckB;
                var moveCount = white ? MoveCountW : MoveCountB;
                var totalTime = white ? TotalTimeW : TotalTimeB;
                var totalTimeOther = white ? TotalTimeB : TotalTimeW;

                var fitness = 0d;

                fitness += checkMate ? 10d : Draw ? 5d : 0d;
                fitness += amountCheck / 5d;
                fitness += checkMate ? moveCount / 10 : Draw ? moveCount / 100 : moveCount / 500;
                fitness += totalTimeOther / (double)totalTime;

                return fitness;
            }
        }

        public void GameLoop(string gameName, NeuralNetwork a, Client cA) {
            var totalTimeA = new Stopwatch();

            var gameEnded = false;

            while (!gameEnded) {
                do { RefreshBoard(cA, gameName); Thread.Sleep(5000); } while (!Board.PlayerOneTurn);
                totalTimeA.Start();
                Console.Title = Board.FiftyMoveRule.ToString();
                if (Board.FiftyMoveRule >= 50) break;
                var moveA = a.Output;
                var messageA = cA.SendMessage(Packet.Packets.Game, "MOVE", gameName, moveA.Item1.ToString(),
                        moveA.Item2.ToString(), true.ToString());

                if (messageA.Item1.Code == Packet.Packets.Error) {
                    gameEnded = !RefreshBoard(cA, gameName);
                    continue;
                }

                totalTimeA.Stop();
                gameEnded = !RefreshBoard(cA, gameName);

                //if (Board.CheckCheck(Board.WhiteColour)) 
            }
            totalTimeA.Stop();
        }

        private GameReturn GameLoop(string gameName, bool displayBoard, NeuralNetwork a, NeuralNetwork b, Client cA, Client cB) {
            var checkCountA = 0;
            var moveCountA = 0;
            var totalTimeA = new Stopwatch();

            var checkCountB = 0;
            var moveCountB = 0;
            var totalTimeB = new Stopwatch();

            var gameEnded = false;

            var skipA = false;
            while (!gameEnded) {
                if (!skipA) {
                    do { } while (!Board.PlayerOneTurn);
                    totalTimeA.Start();
                    Console.Title = Board.FiftyMoveRule.ToString();
                    if (Board.FiftyMoveRule >= 50) break;
                    var moveA = a.Output;
                    var messageA = cA.SendMessage(Packet.Packets.Game, "MOVE", gameName, moveA.Item1.ToString(),
                            moveA.Item2.ToString(), true.ToString());

                    if (messageA.Item1.Code == Packet.Packets.Error) {
                        Console.WriteLine("[X:" + ((moveA.Item1 % 8) + 1) + " Y:" + (8 - (moveA.Item1 / 8)) + "](" +
                                          moveA.Item1 + ") -> " +
                                          "[X:" + ((moveA.Item2 % 8) + 1) + " Y:" + (8 - (moveA.Item2 / 8)) + "](" +
                                          moveA.Item2 + ") AI1");
                        gameEnded = !RefreshBoard(cA, gameName);
                        if (displayBoard) DrawBoard();
                        continue;
                    }
                    _lastPlayerOneMove = moveA;
                    totalTimeA.Stop();
                    //Thread.Sleep(1000);
                    gameEnded = !RefreshBoard(cA, gameName);

                    if (Board.CheckCheck(Board.WhiteColour)) checkCountA++;
                    moveCountA++;
                    if (displayBoard) DrawBoard();
                }
                skipA = false;

                if (Board.CheckCheckMate(Board.WhiteColour) || Board.CheckCheckMate(Board.BlackColour)) break;
                if (gameEnded) break;

                do { } while (Board.PlayerOneTurn);

                totalTimeB.Start();
                Console.Title = Board.FiftyMoveRule.ToString();
                if (Board.FiftyMoveRule >= 50) break;
                var moveB = b.Output;
                var messageB = cB.SendMessage(Packet.Packets.Game, "MOVE", gameName, moveB.Item1.ToString(), moveB.Item2.ToString(), false.ToString());

                if (messageB.Item1.Code == Packet.Packets.Error) {
                    Console.WriteLine("[X:" + ((moveB.Item1 % 8) + 1) + " Y:" + (8 - (moveB.Item1 / 8)) + "](" + moveB.Item1 + ") -> " +
                                        "[X:" + ((moveB.Item2 % 8) + 1) + " Y:" + (8 - (moveB.Item2 / 8)) + "](" + moveB.Item2 + ") AI2");
                    gameEnded = !RefreshBoard(cA, gameName);
                    skipA = true;
                    if (displayBoard) DrawBoard();
                    continue;
                }
                _lastPlayerTwoMove = moveB;
                totalTimeB.Stop();

                //Thread.Sleep(1000);
                gameEnded = !RefreshBoard(cB, gameName);

                if (Board.CheckCheck(Board.BlackColour)) checkCountB++;
                moveCountB++;
                if (displayBoard) DrawBoard();
            }

            totalTimeA.Stop();
            totalTimeB.Stop();

            if (Board.PlayerOneTurn)
                MovePiece(_lastPlayerOneMove.Item1, _lastPlayerOneMove.Item2);
            else
                MovePiece(_lastPlayerTwoMove.Item1, _lastPlayerTwoMove.Item2);

            var returnInfo = new GameReturn {
                WhiteCheckMate = Board.CheckCheckMate(Board.BlackColour),
                BlackCheckMate = Board.CheckCheckMate(Board.WhiteColour),
                AmountCheckW = checkCountA,
                AmountCheckB = checkCountB,
                MoveCountW = moveCountA,
                MoveCountB = moveCountB,
                TotalTimeW = totalTimeA.ElapsedMilliseconds,
                TotalTimeB = totalTimeB.ElapsedMilliseconds,
                Draw = (Board.FiftyMoveRule >= 50)
            };
            return returnInfo;
        }

        private void DrawBoard() {
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.Clear();
            for (var i = 1; i < 9; i++)
                for (var j = 1; j < 9; j++) {
                    Console.SetCursorPosition(i * 2, j);
                    var piece = Board.GetPiece(i, j);
                    Console.ForegroundColor = piece.Owner.Equals(Board.WhiteColour)
                        ? ConsoleColor.White
                        : ConsoleColor.Black;
                    switch (piece.PieceType) {
                        case Pieces.Knight:
                            Console.WriteLine("N");
                            break;
                        case Pieces.Null:
                            Console.WriteLine(" ");
                            break;
                        default:
                            Console.WriteLine(piece.PieceType.ToString()[0]);
                            break;
                    }
                }
            Console.SetCursorPosition(0, 10);
        }

        private bool RefreshBoard(Client c, string gameName) {
            var returned = c.SendMessage(Packet.Packets.Game, "GET", gameName, "NoUpdate");
            if (returned.Item1.Code == Packet.Packets.Error) return false;
            Board = Board.UnSerialize(returned.Item2);
            return true;
        }

        /// <summary>
        /// Move a piece on a local board
        /// </summary>
        /// <param name="startIndex">The index the piece moves from</param>
        /// <param name="endIndex">The index the piece moves to</param>
        private void MovePiece(int startIndex, int endIndex) {
            Board.LastMove = Tuple.Create(startIndex, endIndex);
            var x = (startIndex % 8) + 1;
            var y = (startIndex / 8) + 1;
            var piece = Board.GetPiece(x, y);
            // Add move to list box
            x = (endIndex % 8) + 1;
            y = (endIndex / 8) + 1;
            // Move the piece
            piece.Move(Board, x, y);
            // Add move to list box
            // Refresh all available moves for every piece
            lock (Board.Pieces) {
                foreach (var pieces in Board.Pieces)
                    pieces.GetMoves(Board, true);
            }
        }

        /// <summary>
        /// Move a piece and swap player turn
        /// </summary>
        /// <param name="startIndex">The index the piece moves from</param>
        /// <param name="endIndex">The index the piece moves to</param>
        /// <param name="turn">If it is player ones turn</param>
        public void MovePiece(int startIndex, int endIndex, bool turn) {
            MovePiece(startIndex, endIndex);
            Board.PlayerOneTurn = !turn;
        }

        /// <summary>
        /// Remove a piece from the board without it being taken
        /// </summary>
        /// <param name="pieceIndex">What square the piece is in</param>
        public void SpecialRemove(int pieceIndex) {
            var x = (pieceIndex % 8) + 1;
            var y = (pieceIndex / 8) + 1;
            Board.TakenPieces.Add(Board.GetPiece(x, y));
            Board.Pieces.Remove(Board.GetPiece(x, y));

            foreach (var pieces in Board.Pieces)
                pieces.GetMoves(Board, true);
        }

        /// <summary>
        /// Add a piece to the board
        /// </summary>
        /// <param name="pieceIndex">The square to add the piece to</param>
        /// <param name="pieceName">The name of the piece type</param>
        /// <param name="owner">What colour it should be, null for promotion</param>
        public void SpecialAdd(int pieceIndex, string pieceName, string owner) {
            Piece piece = new Pawn(0, 0, Color.White);
            var x = (pieceIndex % 8) + 1;
            var y = (pieceIndex / 8) + 1;
            var ownerColour = Color.Black;
            if (owner.Equals("NULL")) ownerColour = (y == 1 ? Board.WhiteColour : Board.BlackColour);
            else if (owner.Equals("WHITE")) ownerColour = Board.BlackColour;
            else if (owner.Equals("BLACK")) ownerColour = Board.WhiteColour;
            switch (pieceName) {
                case "Pawn":
                    piece = new Pawn(x, y, ownerColour);
                    break;
                case "Rook":
                    piece = new Rook(x, y, ownerColour);
                    break;
                case "Bishop":
                    piece = new Bishop(x, y, ownerColour);
                    break;
                case "Knight":
                    piece = new Knight(x, y, ownerColour);
                    break;
                case "Queen":
                    piece = new Queen(x, y, ownerColour);
                    break;
                // Shouldn't be needed
                case "King":
                    piece = new King(x, y, ownerColour);
                    break;
            }
            // Remove any old piece
            Board.Pieces.Remove(Board.GetPiece(x, y));
            // Add the new piece
            Board.Pieces.Add(piece);

            foreach (var pieces in Board.Pieces)
                pieces.GetMoves(Board, true);
        }
    }
}
