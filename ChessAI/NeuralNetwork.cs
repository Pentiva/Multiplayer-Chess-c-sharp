using ChessClient.Game;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessAI {
    public class NeuralNetwork : ICloneable {

        private readonly Random _random;

        private Piece[] _input = new Piece[64];
        //private readonly Characteristics _playerType;

        List<Tuple<int, double, int>> _weighting = new List<Tuple<int, double, int>>(64);
        public int WeightingCount {
            get {
                return _weighting.Count;
            }
        }

        private ChessGame _game;

        public Tuple<int, int> Output {
            get {
                return CalculateOutput();
            }
        }

        private readonly bool _playerOne;

        public NeuralNetwork(Random rand, bool playerOne, ChessGame g) {
            _random = new Random(rand.Next());
            var neuronAmount = _random.Next(64);
            _playerOne = playerOne;
            for (var i = 0; i < neuronAmount; i++)
                _weighting.Add(Tuple.Create(_random.Next(64), _random.NextDouble(), _random.Next(64)));
            //_playerType = (Characteristics)_random.Next(4096);
            _game = g;
        }

        private Tuple<int, int> CalculateOutput() {
            for (var i = 0; i < 8; i++)
                for (var j = 0; j < 8; j++)
                    _input[i + (j * 8)] = _game.Board.GetPiece(i + 1, j + 1);

            var viable = new List<Tuple<int, double, int>>();
            while (viable.Count == 0) {
                viable.AddRange(from link in _weighting where _input[link.Item1].Owner == (_playerOne ? _game.Board.BlackColour : _game.Board.WhiteColour) where _input[link.Item3].Owner == (_playerOne ? _game.Board.WhiteColour : _game.Board.BlackColour) where _input[link.Item3].Moves.Count != 0 select link);
                if (viable.Count == 0)
                    FormNewNeuron(0.005d);
            }

            var potentialMoves = new List<Tuple<int, int>>();
            var bestMove = 0d;
            foreach (var link in viable.Select(item => item.Item3)) {
                foreach (var move in _input[link].Moves) {
                    var moveRating = RatePiece(_input[(move.Item1) - 1 + ((move.Item2 - 1) * 8)].PieceType);
                    if (moveRating > bestMove) {
                        bestMove = moveRating;
                        potentialMoves.Clear();
                        potentialMoves.Add(Tuple.Create(link, (move.Item1) - 1 + ((move.Item2 - 1) * 8)));
                    } else if (Math.Abs(moveRating - bestMove) < 0.0001) {
                        potentialMoves.Add(Tuple.Create(link, (move.Item1) - 1 + ((move.Item2 - 1) * 8)));
                    }
                }
            }

            var output = potentialMoves[_random.Next(potentialMoves.Count)];
            return output;
        }

        static double RatePiece(Pieces p) {
            switch (p) {
                case Pieces.Rook:
                    return 5;
                case Pieces.Bishop:
                    return 3;
                case Pieces.Queen:
                    return 9;
                case Pieces.Knight:
                    return 3;
                case Pieces.King:
                    return float.MaxValue;
                case Pieces.Pawn:
                    return 1;
            }
            return 0;
        }

        public NeuralNetwork Breed(NeuralNetwork n, int mergeAmount) {
            var nWeightings = n._weighting;
            n._weighting = new List<Tuple<int, double, int>>();
            for (var i = 0; i < mergeAmount; i++) {
                Tuple<int, double, int> newWeighting;
                do {
                    newWeighting = _weighting[_random.Next(0, _weighting.Count)];
                } while (n._weighting.Any(weight => weight.Equals(newWeighting)));
                n._weighting.Add(newWeighting);
            }
            for (var i = 0; i < mergeAmount; i++) {
                Tuple<int, double, int> newWeighting;
                do {
                    newWeighting = nWeightings[_random.Next(0, nWeightings.Count)];
                } while (n._weighting.Any(weight => weight.Equals(newWeighting)));
                n._weighting.Add(newWeighting);
            }
            return n;
        }

        public void EditWeightings(string direction) {
            var amountToChange = _random.Next(_weighting.Count);
            var newWeighting = new List<Tuple<int, double, int>>(_weighting.Count);
            for (var i = amountToChange; i > 0; i--) {
                var changing = _weighting[_random.Next(_weighting.Count)];
                _weighting.Remove(changing);
                var newWeightMax = 1d - changing.Item2;
                var changeAmount = _random.NextDouble();
                while (changeAmount > newWeightMax)
                    changeAmount = _random.NextDouble();

                switch (direction) {
                    case "UP":
                        newWeighting.Add(Tuple.Create(changing.Item1, changing.Item2 + changeAmount, changing.Item3));
                        break;
                    case "DOWN":
                        newWeighting.Add(Tuple.Create(changing.Item1, changing.Item2 + changeAmount, changing.Item3));
                        break;
                    case "RANDOM":
                        newWeighting.Add(_random.Next(2) == 0
                            ? Tuple.Create(changing.Item1, changing.Item2 + changeAmount, changing.Item3)
                            : Tuple.Create(changing.Item1, changing.Item2 - changeAmount, changing.Item3));
                        break;
                }
            }
            newWeighting.AddRange(_weighting);
            _weighting = newWeighting;
        }

        public void FormNewNeuron(int endPoint) {
            var neuron = Tuple.Create(_random.Next(64), _random.NextDouble(), _random.Next(64));
            while (neuron.Item3 != endPoint)
                neuron = Tuple.Create(_random.Next(64), _random.NextDouble(), _random.Next(64));
            _weighting.Add(neuron);
        }

        private void FormNewNeuron() {
            _weighting.Add(Tuple.Create(_random.Next(64), _random.NextDouble(), _random.Next(64)));
        }

        private void FormNewNeuron(double weighting) {
            _weighting.Add(Tuple.Create(_random.Next(64), weighting, _random.Next(64)));
        }

        public object Clone() {
            var newNetwork = (NeuralNetwork)MemberwiseClone();
            newNetwork._input = _input;
            newNetwork._game = _game;

            return newNetwork;
        }

        //private enum Characteristics {
        //    Aggressive = 1, Defensive = 2, Forward = 4, Safe = 8, Risky = 16, Unpredictable = 32,
        //    PawnPref = 64, RookPref = 128, BishopPref = 256, KnightPref = 1024, QueenPref = 2048
        //}
    }

}
