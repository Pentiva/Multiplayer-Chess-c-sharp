using System;
using System.Collections.Generic;
using ChessClient.Game;
using System.Drawing;
using System.Net;

namespace Server {
    /// <summary>
    /// Info about the game and the players
    /// </summary>
    [Serializable]
    public class Games {
        /// <summary>
        /// The name of the game
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// The board object to store the game information
        /// </summary>
        public readonly Board Board;
        /// <summary>
        /// Name of player one
        /// </summary>
        public string Player1Name { get; private set; }
        /// <summary>
        /// IP and port of player one
        /// </summary>
        [NonSerialized]
        private IPEndPoint _player1Address;
        /// <summary>
        /// Name of player two
        /// </summary>
        public string Player2Name { get; private set; }
        /// <summary>
        /// IP and port of player two
        /// </summary>
        [NonSerialized]
        private IPEndPoint _player2Address;
        /// <summary>
        /// All players currently watching the game
        /// </summary>
        [NonSerialized]
        public List<IPEndPoint> Audience;
        /// <summary>
        /// List of all moves in the game
        /// </summary>
        public readonly List<string> MoveHistory;
        /// <summary>
        /// List of all chat messages sent
        /// </summary>
        public readonly List<string> Chat;

        /// <summary>
        /// Create a new Games structure
        /// </summary>
        /// <param name="name">The name of the game</param>
        /// <param name="mode">The mode of the game</param>
        /// <param name="colours">The two colours of the game</param>
        public Games(string name, Board.Gametype mode, Tuple<Color, Color> colours) {
            Name = name;
            // Create a new board object
            Board = new Board(colours.Item1, colours.Item2, mode);
            Player1Name = "Open";
            _player1Address = null;
            Player2Name = "Open";
            _player2Address = null;
            Audience = new List<IPEndPoint>();
            MoveHistory = new List<string>();
            Chat = new List<string>();
        }

        /// <summary>
        /// Get all players connected to this game
        /// </summary>
        /// <returns>A list of IPs and ports that include player one, two and all audience</returns>
        public IEnumerable<IPEndPoint> GetReceivers() {
            if (Audience == null) Audience = new List<IPEndPoint>();
            var a = new IPEndPoint[Audience.Count + (_player1Address != null ? 1 : 0) + (_player2Address != null ? 1 : 0)];
            var pointer = 0;
            if (_player1Address != null)
                a[pointer++] = _player1Address;
            if (_player2Address != null)
                a[pointer++] = _player2Address;
            Audience.CopyTo(a, pointer);
            return a;
        }

        /// <summary>
        /// Add Player One to this game
        /// </summary>
        /// <param name="name">The name of the player</param>
        /// <param name="address">The ip and port of the player</param>
        public void AddPlayerOne(string name, IPEndPoint address) {
            Player1Name = name;
            _player1Address = address;
        }

        /// <summary>
        /// Remove player one from this game
        /// </summary>
        public void RemovePlayerOne() {
            Player1Name = "Open";
            _player1Address = null;
        }

        /// <summary>
        /// Remove player two from this game
        /// </summary>
        public void RemovePlayerTwo() {
            Player2Name = "Open";
            _player2Address = null;
        }

        /// <summary>
        /// Remove a player from the game
        /// </summary>
        /// <param name="name">The name of the player to remove</param>
        public void RemovePlayer(string name) {
            if (name.Equals(Player1Name))
                _player1Address = null;
            else if (name.Equals(Player2Name))
                _player2Address = null;
        }

        /// <summary>
        /// Add player two to this game
        /// </summary>
        /// <param name="name">The name of the player</param>
        /// <param name="address">The ip and port of the player</param>
        public void AddPlayerTwo(string name, IPEndPoint address) {
            Player2Name = name;
            _player2Address = address;
        }

        /// <summary>
        /// Convert the game to a readable format
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return Name + "," + Player1Name + "," + Player2Name + "," +
                   string.Format("{0:x6}", Board.WhiteColour.ToArgb()) + "," +
                   string.Format("{0:x6}", Board.BlackColour.ToArgb());
        }
    }
}