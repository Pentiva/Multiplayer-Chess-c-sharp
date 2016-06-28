using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using ChessClient;
using ChessClient.Game;
using System.Text.RegularExpressions;
using ChessClient.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ChessClient.Forms;
using NATUPNPLib;

namespace Server {
    /// <summary>
    /// The server to send info to all the clients
    /// </summary>
    public sealed class ChessServer : IDisposable {
        const int PacketSendAmount = 5;
        /// <summary>
        /// Used as an unique identifier for sent packets
        /// </summary>
        private static byte _globalPacketId;
        /// <summary>
        /// The socket used to listen to the clients
        /// </summary>
        readonly Socket _listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        /// <summary>
        /// The UdpClient used to send messages to the clients
        /// </summary>
        static UdpClient _client = new UdpClient();
        /// <summary>
        /// The port the server listens on
        /// </summary>
        private const int LocalPort = 64425;
        /// <summary>
        /// A list of addresses and names for all connect players and their state
        /// </summary>
        readonly List<Tuple<IPEndPoint, string, string>> _connected = new List<Tuple<IPEndPoint, string, string>>(20);
        /// <summary>
        /// List of games currently being played
        /// </summary>
        List<Games> _currentGames = new List<Games>(20);
        /// <summary>
        /// List of swearwords that get filtered (formatted for regex)
        /// </summary>
        private readonly string _filter = "";
        /// <summary>
        /// Is the server running?
        /// </summary>
        private bool _isRunning = true;
        /// <summary>
        /// True when in the saving state when closing
        /// </summary>
        private bool _savingState;
        /// <summary>
        /// List of information about current threads waiting for players to connect
        /// </summary> 
        private List<Tuple<string, string, string>> _waitingList = new List<Tuple<string, string, string>>(20);
        /// <summary>
        /// History of received packets
        /// </summary>
        private readonly Queue<Packet> _history = new Queue<Packet>(40);
        /// <summary>
        /// List of all previously completed games, the players, and how they ended
        /// </summary>
        private List<string> _gameHistory = new List<string>();

        /// <summary>
        /// Format the ending of a game into the correct string, tell everyone about it then delete it
        /// </summary>
        /// <param name="gameName">The name of the game</param>
        /// <param name="winner">The winner of the game</param>
        /// <param name="reason">The reason the game has ended</param>
        private void EndAGame(string gameName, string winner, string reason) {
            foreach (var game in _currentGames.Where(game => game.Name.Equals(gameName))) {
                string formattedReason;
                // Assign the correct message to the sentence
                switch (reason) {
                    case "RESIGN":
                        winner = winner.Equals(game.Player1Name) ? game.Player2Name : game.Player1Name;
                        formattedReason = winner + " has resigned";
                        break;
                    case "DRAW":
                        formattedReason = "both players agreed to a draw";
                        break;
                    case "FORCED":
                        formattedReason = "the game was forcibly closed";
                        break;
                    case "CHECKMATE":
                        formattedReason = winner + " has won";
                        break;
                    case "STALE":
                        formattedReason = "a stalemate has been reached";
                        break;
                    case "FIFTY":
                        formattedReason = "fifty moves without pawn moving or piece taken.";
                        break;
                    default:
                        formattedReason = "ERROR";
                        break;
                }
                // Tell every player the game has ended
                SendPacketToPlayers(Packet.Packets.Chat, game.GetReceivers(),
                    "TEXT", "[Server] - " + formattedReason);
                // Add to the game history
                _gameHistory.Add(gameName + " has ended at " + TimeZone.CurrentTimeZone.ToLocalTime(DateTime.Now) +
                    " because " + formattedReason + ". Players were " + game.Player1Name + " and " + game.Player2Name + ".");
                // Print to console
                Console.WriteLine(gameName + " has ended at " + TimeZone.CurrentTimeZone.ToLocalTime(DateTime.Now) +
                    " because " + formattedReason + ". Players were " + game.Player1Name + " and " + game.Player2Name + ".");
                break;
            }
        }

        /// <summary>
        /// Create a new Server object
        /// </summary>
        private ChessServer() {
            // Display the local ip address for the server
            var portforwardIp = IPAddress.Loopback;
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)) {
                Console.Title = "IP: " + ip;
                portforwardIp = ip;
                break;
            }
            var mappings = new UPnPNATClass().StaticPortMappingCollection;
            if (mappings != null) {
                mappings.Add(LocalPort, "UDP", LocalPort, portforwardIp.ToString(), true, "Local Web Server");
            } else Console.WriteLine("UPnP is not supported.");

            _listener.EnableBroadcast = true;
            // Populate the swearword filter
            if (File.Exists("swearWords.csv")) {
                var temp = File.ReadAllText("swearWords.csv").Split(',');
                foreach (var word in temp)
                    _filter += word + @"|";
                _filter = @"(" + _filter.Substring(0, _filter.Length - 1) + @")";
            } else {
                var stream = File.Create("swearWords.csv");
                stream.Dispose();
            }

            // Load old saves
            try {
                if (File.Exists("save/games.bin"))
                    LoadEndedGames();
                else {
                    Console.WriteLine("Cannot open save/games.bin. Creating the file.");
                    var stream = File.Create("games.bin");
                    stream.Dispose();
                }
                if (File.Exists("save/waiting.bin"))
                    LoadWaiting();
                else {
                    Console.WriteLine("Cannot open save/waiting.bin. Creating the file.");
                    var stream = File.Create("waiting.bin");
                    stream.Dispose();
                }
                if (File.Exists("save/currentGames.bin"))
                    LoadGames("currentGames");
                else {
                    Console.WriteLine("Cannot open save/currentGames.bin. Creating the file.");
                    var stream = File.Create("currentGames.bin");
                    stream.Dispose();
                }
            } catch (Exception) {
                Console.WriteLine("One or more files are corrupt.");
            }

            _listener.Bind(new IPEndPoint(IPAddress.Any, LocalPort));

            // Receive packets from clients
            new Thread(() => {
                while (_isRunning) {
                    var receiveBytes = new byte[4096];
                    EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    try {
                        _listener.ReceiveFrom(receiveBytes, ref sender);
                    } catch (Exception exception) {
                        Console.WriteLine(_isRunning ? exception.StackTrace : "Server Closing");
                    }

                    if (!receiveBytes.All(a => a.Equals(0)))
                        InterpretData(sender as IPEndPoint, receiveBytes);
                }
            }).Start();

            new Thread(HttpServer).Start();
            // Command inputs
            CommandInterpreter();
        }

        /// <summary>
        /// Interprets commands and loops until server is stopped
        /// </summary>
        private void CommandInterpreter() {
            while (_isRunning) {
                // Get the input
                var wholeCommand = Console.ReadLine();
                if (wholeCommand == null) continue;
                var command = Regex.Matches(wholeCommand, @"[\""].+?[\""]|[^ ]+")
                                        .Cast<Match>().Select(m => m.Value).ToArray();
                command[0] = command[0].ToLower();
                for (var i = 1; i < command.Length; i++) command[i] = command[i].Replace("\"", "");
                if (command[0].Equals("save")) {
                    if (!CommandSyntax(command, 2, "<filename>")) continue;
                    SaveGames(command[1]);
                } else if (command[0].Equals("load")) {
                    // Load all saved games
                    if (!CommandSyntax(command, 2, "<filename>")) continue;
                    LoadGames(command[1]);
                } else if (command[0].Equals("rem")) {
                    // Remove a player from a game
                    if (!CommandSyntax(command, 3, "<Game Name> <Player Name>")) continue;
                    foreach (var game in _currentGames.Where(game => game.Name.Equals(command[1]))) {
                        if (game.Player1Name.Equals(command[2]))
                            game.RemovePlayerOne();
                        else if (game.Player2Name.Equals(command[2]))
                            game.RemovePlayerTwo();
                        else
                            Console.WriteLine("No player by that name in that game");
                        SendPacketToAllPlayers(null, Packet.Packets.Game, "UPDATEGAME");
                        break;
                    }
                } else if (command[0].Equals("end")) {
                    // End a game
                    if (!CommandSyntax(command, 2, "<Game Name>")) continue;
                    EndAGame(command[1], "Server", "FORCED");
                    SendPacketToAllPlayers(null, Packet.Packets.Game, "UPDATEGAME");
                } else if (command[0].Equals("list")) {
                    // List all previous games
                    foreach (var game in _gameHistory)
                        Console.WriteLine(game);
                } else if (command[0].Equals("players")) {
                    Console.WriteLine(_connected.Aggregate("",
                       (current, game) => current + game.Item2 + " - " + game.Item3 + "\n"));
                } else if (command[0].Equals("games")) {
                    Console.WriteLine(_currentGames.Aggregate("",
                        (current, game) => current + game.Name + " - [" + game.Player1Name + "/" + game.Player2Name + "]\n"));
                } else if (command[0].Equals("stop")) {
                    // Tell every player that the server is closed
                    SendPacketToAllPlayers(null, Packet.Packets.Disconnect, "Server", "Server is closing");
                    // Stop the server
                    _isRunning = false;
                } else if (command[0].Equals("help") || command[0].Equals("?")) {
                    Console.WriteLine("save, load, rem, end, list, players, games, stop, help, ?");
                } else {
                    CommandSyntax(command, int.MaxValue, "<command> <parameters>");
                }
            }
        }

        #region Interpret
        /// <summary>
        /// Interpret any received data and act accordingly
        /// </summary>
        /// <param name="address">The address the data came from</param>
        /// <param name="data">The data received</param>
        private void InterpretData(IPEndPoint address, IEnumerable<byte> data) {
            // Create a packet from the data
            var packet = new Packet(data.ToArray());
            packet.SetSenderAddress(address.Address);

            // Make sure the packet hasn't been received already
            if (_history.Any(item => item.Equals(packet))) return;
            if (_history.Count > 20) _history.Dequeue();
            _history.Enqueue(packet);

            // Get the name for the player if already connected
            var nameForUser = "Unknown";
            var list = (_connected.Where(item => item.Item1.Equals(packet.SenderAddress)).Select(name => name.Item2)).ToList();
            if (list.Count > 0)
                nameForUser = list[0];

            switch (packet.Code) {
                case Packet.Packets.Ping:
                    break;
                case Packet.Packets.Connect:
                    InterpretConnect(packet, out nameForUser);
                    break;
                case Packet.Packets.Disconnect:
                    InterpretDisconnect(packet, out nameForUser);
                    break;
                case Packet.Packets.Info:
                    // Send the list of players to the client
                    if (packet.Message[0].Equals("Players"))
                        SendPacket(packet.SenderAddress, Packet.Packets.Info, packet, _connected.Select(name => name.Item2).ToArray());
                    // Send the list of games to the client
                    if (packet.Message[0].Equals("Games"))
                        if (_currentGames.Count > 0)
                            SendPacket(packet.SenderAddress, Packet.Packets.Info, packet, _currentGames.Select(name => name.ToString()).ToArray());
                        else
                            SendPacket(packet.SenderAddress, Packet.Packets.Info, packet, "empty");
                    break;
                case Packet.Packets.Game:
                    InterpretGame(packet, nameForUser);
                    break;
                case Packet.Packets.Chat:
                    InterpretChat(packet);
                    break;
                case Packet.Packets.DisconnectGame:
                    InterpretDisconnectGame(packet, nameForUser);
                    break;
                case Packet.Packets.GameEnd:
                    InterpretGameEnd(packet);
                    break;
                default:
                    SendPacket(packet.SenderAddress, 0, "NOMESSAGE", "Oh, hi there!");
                    break;
            }
            // Don't display message if can't get address
            if (packet.SenderAddress == null) return;
            // Display the message
            if (packet.Code == Packet.Packets.Connect)
                Console.WriteLine("[" + MapToIPv4(packet.SenderAddress).Address + ":" + packet.SenderAddress.Port + "/" + nameForUser + "] - (" + (packet.Code) + ") - " + string.Join(",", packet.Message) + " @ " + TimeZone.CurrentTimeZone.ToLocalTime(DateTime.Now));
            else
                Console.WriteLine("[" + (nameForUser.Equals("Unknown") ? MapToIPv4(packet.SenderAddress).Address + ":" + packet.SenderAddress.Port : nameForUser) + "] - (" + (packet.Code) + ") - " + string.Join(",", packet.Message) + " @ " + TimeZone.CurrentTimeZone.ToLocalTime(DateTime.Now));
        }

        /// <summary>
        /// Interpret a Connect packet
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        /// <param name="nameForUser">The name associated with the player</param>
        private void InterpretConnect(Packet packet, out string nameForUser) {
            if (_connected.Select(info => info.Item2).Contains(packet.Message[0])) {
                // Name already taken
                SendPacket(packet.SenderAddress, Packet.Packets.Error, packet, "Name already taken.");
                nameForUser = "Not " + packet.Message[0];
            } else if (packet.Message[0].Equals("Check") || packet.Message[0].Equals("Players") || packet.Message[0].Equals("Server")) {
                // Unavailable names
                SendPacket(packet.SenderAddress, Packet.Packets.Error, packet, "Can't have that name.");
                nameForUser = "Not " + packet.Message[0];
            } else {
                // Name acceptable
                lock (_connected)
                    _connected.Add(Tuple.Create(packet.SenderAddress, packet.Message[0], "ServerView"));
                SendPacket(packet.SenderAddress, Packet.Packets.Connect, packet, "That's Ok");
                // Tell every player that they have joined
                SendPacketToAllPlayers(packet.SenderAddress, Packet.Packets.Connect, "NOMESSAGE", packet.Message[0]);
                nameForUser = packet.Message[0];
            }
        }

        /// <summary>
        /// Interpret a disconnect packet
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        /// <param name="nameForUser">The name associated with the player</param>
        private void InterpretDisconnect(Packet packet, out string nameForUser) {
            if (_connected.Select(info => info.Item2).Contains(packet.Message[0])) {
                // Remove from connected players
                for (var i = 0; i < _connected.Count; i++)
                    if (_connected[i].Item2 == packet.Message[0]) {
                        lock (_connected)
                            _connected.RemoveAt(i);
                        break;
                    }
                // Tell every player that they have disconnected
                SendPacketToAllPlayers(packet.SenderAddress, Packet.Packets.Disconnect, "NOMESSAGE", packet.Message[0]);
                nameForUser = packet.Message[0];
            } else {
                // Fairly pointless, they probably wont receive it
                SendPacket(packet.SenderAddress, Packet.Packets.Error, packet, "Can't Disconnect");
                nameForUser = "Not " + packet.Message[0];
            }
        }

        /// <summary>
        /// Interpret a chat packet
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        private void InterpretChat(Packet packet) {
            var newMessage = packet.Message[1];
            // Filter for swear words
            var itemRegex = new Regex(_filter, RegexOptions.IgnoreCase);
            newMessage = itemRegex.Matches(packet.Message[1]).Cast<Match>().Where(itemMatch => !string.IsNullOrEmpty(itemMatch.Value)).Aggregate(newMessage, (current, itemMatch) => current.Replace(itemMatch.Value, GetFilter(itemMatch.Value)));
            // Tell the client it is accepted
            SendPacket(packet.SenderAddress, Packet.Packets.Chat, packet, "ignore");
            foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[0]))) {
                // Tell every player the message
                SendPacketToPlayers(Packet.Packets.Chat, game.GetReceivers(), "TEXT", newMessage);
                game.Chat.Add(newMessage);
            }
        }

        /// <summary>
        /// Interpret a disconnect game packet
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        /// <param name="nameForUser">The name associated with the player</param>
        private void InterpretDisconnectGame(Packet packet, string nameForUser) {
            SendPacket(packet.SenderAddress, Packet.Packets.Game, packet, "OK");
            foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[0]))) {
                // Find correct game
                if (packet.Message[2].Equals("WATCHING"))
                    // If they were watching, remove from audience
                    game.Audience.Remove(packet.SenderAddress);
                else
                    // Else remove from playing
                    game.RemovePlayer(packet.Message[1]);
                // Change their status to viewing the server
                var oldPlayer = _connected.Where(item => item.Item2.Equals(nameForUser)).ToList()[0];
                var newPlayer = Tuple.Create(oldPlayer.Item1, oldPlayer.Item2, "ServerView");
                lock (_connected) {
                    _connected.Remove(oldPlayer);
                    _connected.Add(newPlayer);
                }
                break;
            }
        }

        /// <summary>
        /// Interpret a Game end packet
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        private void InterpretGameEnd(Packet packet) {
            if (packet.Message[0].Equals("DRAW")) {
                // If a player requests a draw
                foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[1]))) {
                    if (!packet.Message[2].Equals(game.Player1Name) && !packet.Message[2].Equals(game.Player2Name))
                        SendPacket(packet.SenderAddress, Packet.Packets.Error, packet, "You shouldn't be here!");
                    else {
                        // If they were playing
                        SendPacket(packet.SenderAddress, Packet.Packets.GameEnd, packet, "That should be fine");
                        var gameName = game.Name;
                        var player = packet.Message[2].Equals(game.Player1Name) ? game.Player2Name : game.Player1Name;
                        if (_waitingList.Any(wait => wait.Item1.Equals(player) && wait.Item2.Equals(gameName))) return;
                        new Thread(() => {
                            WaitForPlayer(player, gameName, "DRAW");
                        }).Start();
                        var add = Tuple.Create(gameName, player, "DRAW");
                        _waitingList.Add(add);
                    }
                }
            } else if (packet.Message[0].Equals("RESIGN")) {
                // If a player forfeits the game
                SendPacket(packet.SenderAddress, Packet.Packets.GameEnd, packet, "Ok");
                EndAGame(packet.Message[1], packet.Message[2], packet.Message[0]);
            } else if (packet.Message[0].Equals("DRAWR")) {
                // If the player replies to the draw request
                SendPacket(packet.SenderAddress, Packet.Packets.GameEnd, packet, "Ok");
                if (!packet.Message[2].Equals("YES")) return;
                // If it was a yes
                EndAGame(packet.Message[1], packet.Message[2], "DRAW");
            } else if (packet.Message[0].Equals("LEAVE")) {
                // If a player leaves a game
                SendPacket(packet.SenderAddress, Packet.Packets.GameEnd, packet, "Ok");
                foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[1]))) {
                    // Remove the player
                    if (game.Player1Name.Equals(packet.Message[2]))
                        game.RemovePlayerOne();
                    else if (game.Player2Name.Equals(packet.Message[2]))
                        game.RemovePlayerTwo();
                    // Tell every player
                    SendPacketToPlayers(Packet.Packets.Chat, game.GetReceivers(),
                        "TEXT", "[Server] - " + packet.Message[2] + " has left the game.");
                    SendPacketToAllPlayers(null, Packet.Packets.Game, "UPDATEGAME");
                    break;
                }
            } else
                SendPacket(packet.SenderAddress, Packet.Packets.Error, packet, "Don't know what you want");
        }

        #region InterpretGame
        /// <summary>
        /// Interpret a packet with a "Game" code
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        /// <param name="nameForUser">The name associated with the player</param>
        private void InterpretGame(Packet packet, string nameForUser) {
            if (packet.Message[0].Equals("ADD"))
                InterpretAddGame(packet);
            else if (packet.Message[0].Equals("GET"))
                InterpretGetGame(packet);
            else if (packet.Message[0].Equals("WATCH"))
                InterpretWatchGame(packet, nameForUser);
            else if (packet.Message[0].Equals("JOIN"))
                InterpretJoinGame(packet, nameForUser);
            else if (packet.Message[0].Equals("MOVE"))
                InterpretMovePiece(packet);
            else if (packet.Message[0].Equals("TAKE") || packet.Message[0].Equals("TAKER"))
                InterpretTakeBack(packet);
        }

        /// <summary>
        /// Interpret a Game packet that is adding a game
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        private void InterpretAddGame(Packet packet) {
            // Add a game to the list of games
            if (_currentGames.Any(item => item.Name.Equals(packet.Message[1])))
                // Can't have that name
                SendPacket(packet.SenderAddress, Packet.Packets.Error, packet, "A game with that name is already created");
            else {
                // Create the game locally and tell every player
                var game = new Games(packet.Message[1], (Board.Gametype)byte.Parse(packet.Message[2]),
                    Tuple.Create(ColorTranslator.FromHtml("#" + packet.Message[3]), ColorTranslator.FromHtml("#" + packet.Message[4])));
                SendPacket(packet.SenderAddress, Packet.Packets.Game, packet, "Game Created");
                SendPacketToAllPlayers(packet.SenderAddress, Packet.Packets.Game, "ADDGAME", game.ToString());
                _currentGames.Add(game);
            }
        }

        /// <summary>
        /// Interpret a Game packet that is requesting a game
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        private void InterpretGetGame(Packet packet) {
            // Send a game back to a client
            foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[1]))) {
                SendGame(packet.SenderAddress, game);
                // Allow the game to load on client

                var chat = game.Chat;
                var moveHistory = game.MoveHistory;
                var name = game.Name;
                var p1 = game.Player1Name;
                var p2 = game.Player2Name;
                if (packet.Message.Count <= 2) {
                    new Thread(() => {
                        Thread.Sleep(3000);
                        // Send all chat messages
                        foreach (var message in chat)
                            SendPacket(packet.SenderAddress, Packet.Packets.Chat, "TEXT", message);
                        // Send all moves
                        foreach (var move in moveHistory)
                            SendPacket(packet.SenderAddress, Packet.Packets.Game, "MOVEF", move);

                        SendPacket(packet.SenderAddress, Packet.Packets.Info, "INFO", name, p1, p2);
                    }).Start();
                }
            }
        }

        /// <summary>
        /// Interpret a Game packet that is requesting to watch a game
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        /// /// <param name="nameForUser">The name associated with the player</param>
        private void InterpretWatchGame(Packet packet, string nameForUser) {
            // Add the client to the list of spectators
            foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[1]))) {
                if (game.Audience == null) game.Audience = new List<IPEndPoint>();
                game.Audience.Add(packet.SenderAddress);

                SendPacket(packet.SenderAddress, Packet.Packets.Game, packet, "Have fun watching");

                // Update client status
                var oldPlayer = _connected.Where(item => item.Item2.Equals(nameForUser)).ToList()[0];
                var newPlayer = Tuple.Create(oldPlayer.Item1, oldPlayer.Item2, "Watching");
                lock (_connected) {
                    _connected.Remove(oldPlayer);
                    _connected.Add(newPlayer);
                }
                break;
            }
        }

        /// <summary>
        /// Interpret a Game packet that is requesting to join a game
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        /// <param name="nameForUser">The name of the player that the packet came from</param>
        private void InterpretJoinGame(Packet packet, string nameForUser) {
            // Try and add the client to player one or two, else error
            foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[1]))) {
                if (game.Player1Name.Equals("Open") || game.Player1Name.Equals(nameForUser)) {
                    // Check player one is available or they are player one
                    game.AddPlayerOne(nameForUser, packet.SenderAddress);
                    SendPacket(packet.SenderAddress, Packet.Packets.Game, packet, "PlayerOne");
                    SendPacketToAllPlayers(packet.SenderAddress, Packet.Packets.Game, "UPDATEGAME");
                    SendPacketToPlayers(Packet.Packets.Info, game.GetReceivers(), "INFO", game.Name, game.Player1Name, game.Player2Name);
                    // Update client status
                    var oldPlayer = _connected.Where(item => item.Item2.Equals(nameForUser)).ToList()[0];
                    var newPlayer = Tuple.Create(oldPlayer.Item1, oldPlayer.Item2, game.Name);
                    lock (_connected) {
                        _connected.Remove(oldPlayer);
                        _connected.Add(newPlayer);
                    }
                } else if (game.Player2Name.Equals("Open") || game.Player2Name.Equals(nameForUser)) {
                    // Check player two is available or they are player two
                    game.AddPlayerTwo(nameForUser, packet.SenderAddress);
                    SendPacket(packet.SenderAddress, Packet.Packets.Game, packet, "PlayerTwo");
                    SendPacketToAllPlayers(packet.SenderAddress, Packet.Packets.Game, "UPDATEGAME");
                    SendPacketToPlayers(Packet.Packets.Info, game.GetReceivers(), "INFO", game.Name, game.Player1Name, game.Player2Name);
                    // Update client status
                    var oldPlayer = _connected.Where(item => item.Item2.Equals(nameForUser)).ToList()[0];
                    var newPlayer = Tuple.Create(oldPlayer.Item1, oldPlayer.Item2, game.Name);
                    lock (_connected) {
                        _connected.Remove(oldPlayer);
                        _connected.Add(newPlayer);
                    }
                } else
                    // Tell them they can't join that game
                    SendPacket(packet.SenderAddress, Packet.Packets.Error, packet, "Not open");
                break;
            }
        }

        /// <summary>
        /// Interpret a Game packet that is moving a piece
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        private void InterpretMovePiece(Packet packet) {
            // Move a piece
            foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[1]))) {
                var i = int.Parse(packet.Message[2]);
                var x = (i % 8) + 1;
                var y = (i / 8) + 1;
                var piece = game.Board.GetPiece(x, y);
                if (piece.Moves == null) continue;

                // Validate the move
                var replacementGame = ValidateMove(game, piece, packet, x, y, i);

                // Check for check
                if (replacementGame.Board.CheckCheck(packet.Message[4].Equals("False") ? replacementGame.Board.BlackColour : replacementGame.Board.WhiteColour)) {
                    SendPacketToPlayers(Packet.Packets.Chat, game.GetReceivers(), "TEXT", "Check - " + (packet.Message[4].Equals("True") ? "Black" : "White"));
                    game.Chat.Add("Check - " + (packet.Message[4].Equals("True") ? "Black" : "White"));
                    game.MoveHistory.Add("Check - " + (packet.Message[4].Equals("True") ? "Black" : "White"));
                }
                // Check for checkmate
                if (replacementGame.Board.CheckCheckMate(packet.Message[4].Equals("False") ? replacementGame.Board.WhiteColour : replacementGame.Board.BlackColour)) {
                    game.Chat.Add(packet.Message[4].Equals("True") ? "White wins" : "Black wins");
                    EndAGame(replacementGame.Name, packet.Message[4].Equals("True") ?
                        replacementGame.Player1Name : replacementGame.Player2Name, "CHECKMATE");
                }

                // Check for draws
                if (replacementGame.Board.FiftyMoveRule >= 50) {
                    game.Chat.Add("Draw. Fifty moves without pawn moving or piece taken.");
                    EndAGame(replacementGame.Name, "Neither", "FIFTY");
                }
                // Update the game in the list
                try {
                    var index = _currentGames.IndexOf(game);
                    _currentGames.RemoveAt(index);
                    _currentGames.Insert(index, replacementGame);
                } catch (ArgumentOutOfRangeException) {
                    // Ignore
                    // Game has ended no need to re add
                }
                break;
            }
        }

        private static Games ValidateMove(Games replacementGame, Piece piece, Packet packet, int x, int y, int i) {
            foreach (var move in piece.Moves) {
                // Find correct move
                var j = int.Parse(packet.Message[3]);
                var x1 = (j % 8) + 1;
                var y1 = (j / 8) + 1;
                if (move.Item1 != x1 || move.Item2 != y1) continue;
                // Special cases (En passant, castling)
                replacementGame = InterpretSpecialCase(piece, replacementGame, x, y, y1);
                // Tell the client who sent the move that it is valid
                SendPacket(packet.SenderAddress, Packet.Packets.Game, packet, "OK");
                // Tell every player watching and playing about the move
                SendPacketToPlayers(Packet.Packets.Game, replacementGame.GetReceivers(), "MOVE", packet.Message[2], packet.Message[3], packet.Message[4]);
                // Move the piece locally
                replacementGame.Board.GetPiece(x, y).Move(replacementGame.Board, x1, y1);
                // Change whose turn it is
                replacementGame.Board.PlayerOneTurn = !packet.Message[4].Equals("True");
                // Add the move to the move history
                replacementGame.MoveHistory.Add(BoardDisplayForm.ToDisplay(int.Parse(packet.Message[2])) + " → " + BoardDisplayForm.ToDisplay(int.Parse(packet.Message[3])));
                replacementGame.Board.LastMove = Tuple.Create(i, j);
                // Check for promotion
                if (piece.PieceType == Pieces.Pawn && piece.Y == (piece.Owner.Equals(replacementGame.Board.WhiteColour) ? 1 : 8)) {
                    Piece newPiece;
                    var x3 = (j % 8) + 1;
                    var y3 = (j / 8) + 1;
                    // Promote to queen if it isn't specified
                    switch (packet.Message.Count > 5 ? packet.Message[5] : "Queen") {
                        case "Rook":
                            newPiece = new Rook(x3, y3, piece.Owner);
                            break;
                        case "Bishop":
                            newPiece = new Bishop(x3, y3, piece.Owner);
                            break;
                        case "Knight":
                            newPiece = new Knight(x3, y3, piece.Owner);
                            break;
                        case "Queen":
                            newPiece = new Queen(x3, y3, piece.Owner);
                            break;
                        default:
                            newPiece = new Pawn(x3, y3, piece.Owner);
                            break;
                    }
                    // Tell the clients about the pawn being promoted
                    replacementGame.Board.Pieces.Remove(replacementGame.Board.GetPiece(x3, y3));
                    replacementGame.Board.Pieces.Add(newPiece);
                    SendPacketToPlayers(Packet.Packets.Game, replacementGame.GetReceivers(), "SPEC", j.ToString(), packet.Message.Count > 5 ? packet.Message[5] : "Queen");
                }
                // Update every piece's moves
                foreach (var pieces in replacementGame.Board.Pieces)
                    pieces.GetMoves(replacementGame.Board, true);
            }
            return replacementGame;
        }

        /// <summary>
        /// Interpret a piece's movement to determine a special case
        /// </summary>
        /// <param name="piece">The piece that is moving</param>
        /// <param name="replacementGame">The game object that contains the board</param>
        /// <param name="x">The original x of the piece</param>
        /// <param name="y">The original y of the piece</param>
        /// <param name="y1">The destination y of the piece</param>
        /// <returns>The modified game object</returns>
        private static Games InterpretSpecialCase(Piece piece, Games replacementGame, int x, int y, int y1) {
            switch (piece.PieceType) {
                case Pieces.Pawn:
                    var lastStart = replacementGame.Board.LastMove.Item1;
                    var lastEnd = replacementGame.Board.LastMove.Item2;

                    var takePiece = replacementGame.Board.GetPiece((lastEnd % 8) + 1, (lastEnd / 8) + 1);

                    if (takePiece.PieceType == Pieces.Pawn)
                        if (!takePiece.Owner.Equals(piece.Owner))
                            if ((lastStart / 8) - 2 == (lastEnd / 8) || (lastStart / 8) + 2 == (lastEnd / 8))
                                if (((lastEnd / 8) + 1) == y)
                                    if ((lastEnd % 8) == x || (lastEnd % 8) + 2 == x) {
                                        SendPacketToPlayers(Packet.Packets.Game, replacementGame.GetReceivers(), "SPEC", (lastEnd).ToString());
                                        replacementGame.Board.Pieces.Remove(takePiece);
                                    }
                    break;
                case Pieces.King:
                    var colour = piece.Owner == replacementGame.Board.BlackColour ? 1 : 8;
                    if (piece.MoveCount == 0 && y == y1) {
                        var otherPiece = replacementGame.Board.GetPiece(8, colour);
                        for (var i = 1; i < 9; i++)
                            Console.WriteLine(i + " " + replacementGame.Board.GetPiece(i, colour).PieceType);
                        if (otherPiece.PieceType == Pieces.Rook && otherPiece.MoveCount == 0)
                            if (replacementGame.Board.IsEmptyAndValid(6, colour) &&
                                replacementGame.Board.IsEmptyAndValid(7, colour)) {
                                SendPacketToPlayers(Packet.Packets.Game, replacementGame.GetReceivers(), "MOVE", ((8 - 1) + ((colour - 1) * 8)).ToString(), ((6 - 1) + ((colour - 1) * 8)).ToString());
                                replacementGame.Board.GetPiece(8, colour).Move(replacementGame.Board, 6, colour);
                            }
                        otherPiece = replacementGame.Board.GetPiece(1, colour);
                        if (otherPiece.PieceType == Pieces.Rook && otherPiece.MoveCount == 0)
                            if (replacementGame.Board.IsEmptyAndValid(2, colour) &&
                                replacementGame.Board.IsEmptyAndValid(3, colour) &&
                                replacementGame.Board.IsEmptyAndValid(4, colour)) {
                                SendPacketToPlayers(Packet.Packets.Game, replacementGame.GetReceivers(), "MOVE", ((1 - 1) + ((colour - 1) * 8)).ToString(), ((4 - 1) + ((colour - 1) * 8)).ToString());
                                replacementGame.Board.GetPiece(1, colour).Move(replacementGame.Board, 4, colour);
                            }
                    }
                    break;
            }
            return replacementGame;
        }

        /// <summary>
        /// Interpret a Game packet that is requesting to redo a move
        /// </summary>
        /// <param name="packet">The packet to interpret</param>
        private void InterpretTakeBack(Packet packet) {
            if (packet.Message[0].Equals("TAKE")) {
                // The packet is from the requestor
                foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[1]))) {
                    if (!packet.Message[2].Equals(game.Player1Name) && !packet.Message[2].Equals(game.Player2Name))
                        SendPacket(packet.SenderAddress, Packet.Packets.Error, packet, "You shouldn't be here!");
                    else {
                        SendPacket(packet.SenderAddress, Packet.Packets.Game, packet, "That should be fine");
                        var gameName = game.Name;
                        var player = packet.Message[2].Equals(game.Player1Name) ? game.Player2Name : game.Player1Name;
                        new Thread(() => {
                            WaitForPlayer(player, gameName, "TAKE");
                        }).Start();
                        var add = Tuple.Create(gameName, player, "TAKE");
                        _waitingList.Add(add);
                    }
                }
            } else if (packet.Message[0].Equals("TAKER")) {
                // The packet is from the other player
                if (!packet.Message[2].Equals("YES")) return;
                foreach (var game in _currentGames.Where(game => game.Name.Equals(packet.Message[1]))) {
                    SendPacket(packet.SenderAddress, Packet.Packets.Game, packet, "OK");

                    var x = (game.Board.LastMove.Item2 % 8) + 1;
                    var y = (game.Board.LastMove.Item2 / 8) + 1;

                    var x1 = (game.Board.LastMove.Item1 % 8) + 1;
                    var y1 = (game.Board.LastMove.Item1 / 8) + 1;

                    var lastPieceClone = Piece.Clone(game.Board.LastPiece);

                    SendPacketToPlayers(Packet.Packets.Game, game.GetReceivers(), "MOVE", game.Board.LastMove.Item2.ToString(),
                        game.Board.LastMove.Item1.ToString(), game.Board.PlayerOneTurn.ToString());
                    game.Board.GetPiece(x, y).Move(game.Board, x1, y1);

                    var removeMessage = game.MoveHistory[game.MoveHistory.Count - 1];
                    game.MoveHistory.Remove(removeMessage);
                    SendPacketToPlayers(Packet.Packets.Game, game.GetReceivers(), "RMOVEF");
                    SendPacketToPlayers(Packet.Packets.Game, game.GetReceivers(), "RMOVEF");

                    if (!lastPieceClone.Equals(Board.Null)) {
                        game.Board.Pieces.Add(lastPieceClone);
                        SendPacketToPlayers(Packet.Packets.Game, game.GetReceivers(), "SPEC",
                            game.Board.LastMove.Item2.ToString(),
                            lastPieceClone.PieceType.ToString(),
                            lastPieceClone.Owner.Equals(game.Board.BlackColour) ? "BLACK" : "WHITE");
                        game.Board.TakenPieces.RemoveAt(game.Board.TakenPieces.Count - 1);
                    }

                    foreach (var piece in game.Board.Pieces) {
                        piece.GetMoves(game.Board, true);
                    }
                }
            }
        }
        #endregion
        #endregion

        #region SendPacket
        /// <summary>
        /// Send a game to a client
        /// </summary>
        /// <param name="address">The client to send to</param>
        /// <param name="game">The game to send</param>
        private static void SendGame(IPEndPoint address, Games game) {
            try {
                _client = new UdpClient();
                // Connect to the client
                _client.Connect(MapToIPv4(address));
                var data = game.Board.Serialize();
                // Send the packet (multiple times for udp)
                for (var i = 0; i < PacketSendAmount; i++)
                    _client.Send(data, data.Length);
                _client.Close();
            } catch (Exception ex) {
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Send a packet to a client in return
        /// </summary>
        /// <param name="address">The client to send to</param>
        /// <param name="packet">The packet to send</param>
        private static void SendPacket(IPEndPoint address, Packet packet) {
            _client = new UdpClient();
            // Connect to the client
            try {
                _client.Connect(MapToIPv4(address));
                for (var i = 0; i < PacketSendAmount; i++)
                    _client.Send(packet.GetData(), packet.GetData().Length);//, MapToIPv4(address));
            } catch (SocketException ex) {
                // Send the packet (multiple times for udp)
                Console.WriteLine(ex.ErrorCode);
                Console.WriteLine(ex.Message);
                Console.WriteLine(address.ToString());
            }
            _client.Close();
        }

        /// <summary>
        /// Convert an IPv6 address to its IPv4 equivalent. (Fix for .NET implementation of MapToIPv4)
        /// </summary>
        /// <param name="address">The address to convert</param>
        /// <returns>IPENDPOINT: An IPv4 address and port</returns>
        private static IPEndPoint MapToIPv4(IPEndPoint address) {
            if (address.AddressFamily != AddressFamily.InterNetworkV6)
                return address;
            try {
                // Use reflection to get the array of ushorts
                const BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public
                                               | BindingFlags.NonPublic | BindingFlags.Static;
                var field = typeof(IPAddress).GetField("m_Numbers", bindFlags);
                var mNumbers = field != null ? (ushort[])field.GetValue(address.Address) : null;
                // Return if address is not valid or not an ipv4
                Console.WriteLine(address.Address);
                if (mNumbers == null || !IsIPv4MappedToIPv6(address.Address))
                    return new IPEndPoint(IPAddress.Loopback, address.Port);
                // Convert 8 shorts to a long
                var v4Address = (((mNumbers[6] & 0x0000FF00) >> 8) |
                                 ((mNumbers[6] & 0x000000FF) << 8)) | ((uint)(((mNumbers[7] & 0x0000FF00) >> 8) |
                                                                               ((mNumbers[7] & 0x000000FF) << 8)) << 16);
                // Return the IPEndPoint
                return new IPEndPoint(v4Address, address.Port);
            } catch (Exception) {
                Console.WriteLine("TASDASD");
                return new IPEndPoint(IPAddress.Loopback, address.Port);
            }
        }

        private static bool IsIPv4MappedToIPv6(IPAddress ip) {
            try {
                if (ip.AddressFamily != AddressFamily.InterNetworkV6) {
                    return false;
                }
                const BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public
                                               | BindingFlags.NonPublic | BindingFlags.Static;
                var field = typeof(IPAddress).GetField("m_Numbers", bindFlags);
                var mNumbers = field != null ? (ushort[])field.GetValue(ip) : null;
                for (var i = 0; i < 5; i++) {
                    if (mNumbers != null && mNumbers[i] != 0) {
                        return false;
                    }
                }
                return mNumbers != null && (mNumbers[5] == 0xFFFF);
            } catch (Exception) {
                Console.WriteLine("AFJGJRS");
                return true;
            }
        }

        /// <summary>
        /// Send a packet to a client in return
        /// </summary>
        /// <param name="address">The client to send to</param>
        /// <param name="code">The packet code</param>
        /// <param name="recieve">The packet that was received from the client</param>
        /// <param name="sendMessages">The message to send</param>
        private static void SendPacket(IPEndPoint address, Packet.Packets code, Packet recieve, params string[] sendMessages) {
            var sendPacket = new Packet(_globalPacketId++, LocalPort, code, sendMessages);
            sendPacket.SetReurnLink(recieve);
            SendPacket(address, sendPacket);
        }

        /// <summary>
        /// Send a packet to a client with a fake return message
        /// </summary>
        /// <param name="address">The client to send to</param>
        /// <param name="code">The packet code</param>
        /// <param name="fakeMessage">The fake message</param>
        /// <param name="sendMessages">The message to send</param>
        private static void SendPacket(IPEndPoint address, Packet.Packets code, string fakeMessage, params string[] sendMessages) {
            var sendPacket = new Packet(_globalPacketId++, LocalPort, code, sendMessages);
            sendPacket.SetReurnLink(new Packet(0, LocalPort, 0, fakeMessage));
            SendPacket(address, sendPacket);
        }

        /// <summary>
        /// Send a packet to all connect players
        /// </summary>
        /// <param name="address">An optional parameter to exclude a client from the message</param>
        /// <param name="code">The packet code</param>
        /// <param name="fakeMessage">The fake message</param>
        /// <param name="sendMessages">The message to send</param>
        private void SendPacketToAllPlayers(IPEndPoint address, Packet.Packets code, string fakeMessage, params string[] sendMessages) {
            // Remove the address from the list of connected players
            var copyList = _connected.Select(p => p.Item1).ToList();
            if (address != null)
                copyList.Remove(address);
            foreach (var player in copyList)
                SendPacket(player, code, fakeMessage, sendMessages);
        }

        /// <summary>
        /// Send a packet to a list of players
        /// </summary>
        /// <param name="code">The packet code</param>
        /// <param name="players">The list of client to send to</param>
        /// <param name="fakeMessage">The fake message</param>
        /// <param name="sendMessages">The message to send</param>
        private static void SendPacketToPlayers(Packet.Packets code, IEnumerable<IPEndPoint> players, string fakeMessage, params string[] sendMessages) {
            foreach (var player in players)
                SendPacket(player, code, fakeMessage, sendMessages);
        }
        #endregion

        /// <summary>
        /// Get a filtered version of a word
        /// </summary>
        /// <param name="orignal">The original word</param>
        /// <returns>STRING: A filtered word</returns>
        private static string GetFilter(string orignal) {
            var filtered = orignal[0].ToString();
            // Replace every character except the first with *
            filtered = orignal.Aggregate(filtered, (current, letter) => current + "*");
            return new string(filtered.Take(orignal.Length).ToArray());
        }

        /// <summary>
        /// Wait for a player to come online and join a game
        /// </summary>
        /// <param name="playerName">The player to wait for</param>
        /// <param name="gameName">The name of the game they have to join</param>
        /// <param name="message">The message to send to them when they connect</param>
        private void WaitForPlayer(string playerName, string gameName, string message) {
            var found = false;
            do {
                // Stop thread if server has stopped
                if (_savingState) return;
                try {
                    // Because in different thread, this can cause errors with addition and subtraction to the list
                    found = _connected.Where(item => item.Item2.Equals(playerName) && item.Item3.Equals(gameName)).ToList().Count != 0;
                } catch (Exception) {
                    // ignored
                }
            } while (!found);
            var joinedPlayer = _connected.Where(item => item.Item2 == playerName && item.Item3.Equals(gameName)).ToList();
            Thread.Sleep(3000);
            SendPacket(joinedPlayer[0].Item1, Packet.Packets.Game, message);
            foreach (var wait in _waitingList.Where(wait => wait.Item1.Equals(playerName) && wait.Item2.Equals(gameName))) {
                _waitingList.Remove(wait);
                break;
            }
        }

        private void SaveWaiting() {
            var fs = new FileStream("save/waiting.bin", FileMode.Create);
            var compressor = new GZipStream(fs, CompressionMode.Compress);

            var bf = new BinaryFormatter();
            bf.Serialize(compressor, _waitingList);
            compressor.Close();
            fs.Close();
        }

        private void LoadWaiting() {
            // Decompress and deserialize
            try {
                var fs = new FileStream("save/waiting.bin", FileMode.Open);
                var decompressor = new GZipStream(fs, CompressionMode.Decompress);
                var bf = new BinaryFormatter();
                _waitingList = (List<Tuple<string, string, string>>)bf.Deserialize(decompressor);
                decompressor.Close();
                fs.Close();
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
            // Restart the threads to wait for them
            foreach (var waiting in _waitingList) {
                var waiting1 = waiting;
                new Thread(() => {
                    WaitForPlayer(waiting1.Item1, waiting1.Item2, waiting1.Item3);
                }).Start();
            }
        }

        private void SaveEndedGames() {
            var fs = new FileStream("save/games.bin", FileMode.Create);
            var compressor = new GZipStream(fs, CompressionMode.Compress);

            var bf = new BinaryFormatter();
            bf.Serialize(compressor, _gameHistory);
            compressor.Close();
            fs.Close();
        }

        private void LoadEndedGames() {
            try {
                var fs = new FileStream("save/games.bin", FileMode.Open);
                var decompressor = new GZipStream(fs, CompressionMode.Decompress);
                var bf = new BinaryFormatter();
                _gameHistory = (List<string>)bf.Deserialize(decompressor);
                decompressor.Close();
                fs.Close();
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }

        private void SaveGames(string fileName) {
            // Save all currently running games
            byte[] raw;
            using (var stream = new MemoryStream()) {
                var bin = new BinaryFormatter();
                bin.Serialize(stream, _currentGames);
                raw = stream.ToArray();
            }

            byte[] b;
            using (var memory = new MemoryStream()) {
                using (var gzip = new GZipStream(memory, CompressionMode.Compress, true))
                    gzip.Write(raw, 0, raw.Length);
                b = memory.ToArray();
            }
            File.WriteAllBytes("save/" + fileName + ".bin", b);
        }

        private void LoadGames(string fileName) {
            var compressed = File.ReadAllBytes("save/" + fileName + ".bin");

            byte[] raw;
            using (var stream = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress)) {
                const int size = 4096;
                var buffer = new byte[size];
                using (var memory = new MemoryStream()) {
                    int count;
                    do {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0) {
                            memory.Write(buffer, 0, count);
                        }
                    } while (count > 0);
                    raw = memory.ToArray();
                }
            }

            using (var stream = new MemoryStream(raw)) {
                var bin = new BinaryFormatter();
                _currentGames = (List<Games>)bin.Deserialize(stream);
            }
        }

        /// <summary>
        /// Check syntax and display error message
        /// </summary>
        /// <param name="command">The command inputted</param>
        /// <param name="syntaxNumber">The amount of variables there should be</param>
        /// <param name="properSyntax">The syntax that should be used</param>
        /// <returns>BOOL: If the command is the proper syntax</returns>
        private static bool CommandSyntax(IReadOnlyCollection<string> command, int syntaxNumber, string properSyntax) {
            if (command.Count == syntaxNumber) return true;
            // Change the colour of the console, and display the correct syntax
            var temp = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("Invalid Syntax, Use: " + properSyntax);
            Console.ForegroundColor = temp;
            return false;
        }

        bool _disposed;
        /// <summary>
        /// Dispose of the server object
        /// </summary>
        public void Dispose() {
            Dispose(true);
        }

        /// <summary>
        /// Dispose of the server object
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing) {
            if (_disposed)
                return;
            if (disposing) {
                _client.Close();
                _listener.Close();
                _httpListener.Close();
            }
            _disposed = true;
        }

        #region WebServer
        private readonly HttpListener _httpListener = new HttpListener();
        const string ResponseStringEnd = "\n  </body>\n</html>";
        private void HttpServer() {
            if (!HttpListener.IsSupported) {
                Console.WriteLine("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
                return;
            }

            // netsh http show urlacl
            // http://*:8989/
            // http://*:5357/
            _httpListener.Prefixes.Add(@"http://localhost:8425/");
            try {
                _httpListener.Start();
            } catch (Exception e) {
                Console.WriteLine("Web server not supported");
                Console.WriteLine(e);
                return;
            }
            Console.WriteLine("Started Webserver");
            // Note: The GetContext method blocks while waiting for a request. 
            while (_isRunning) {
                HttpListenerContext context;
                try {
                    context = _httpListener.GetContext();
                } catch (Exception) {
                    // Http listener has closed
                    continue;
                }
                new Thread(() => {
                    InterpretRequest(context);
                }).Start();
            }
        }

        private void InterpretRequest(HttpListenerContext context) {
            var responseStringStart =
                "<html>\n" +
                "  <head>\n" +
                "    <title>Chess Server</title>\n" +
                "    <meta http-equiv=\"refresh\" content=\"10\">\n" +
                "  <head>\n" + "  <body>\n" +
                "    <h1>" + DateTime.Now + "</h1>\n";

            var request = context.Request;

            // Obtain a response object.
            var response = context.Response;

            // Convert from %20 and others to correct characters from the URI
            var fullRequest = System.Web.HttpUtility.UrlDecode(new string(request.RawUrl.Skip(1).ToArray())) ??
                              new string(request.RawUrl.Skip(1).ToArray());
            // Split up into sections based on slashes
            var allRequests = fullRequest.Split('/');
            // Ignore the case of the first path
            allRequests[0] = allRequests[0].ToLower();
            var responseStringMiddle = "";
            byte[] buffer;

            switch (allRequests[0]) {
                case "players":
                    buffer = GetPlayersPage(responseStringStart);
                    break;
                case "games":
                    buffer = GetGamesPage(responseStringStart);
                    break;
                case "watch":
                    buffer = GetWatchPage(responseStringStart, allRequests);
                    break;
                case "chat":
                    // Return a webpage with each chat message as a new paragraph
                    foreach (var game in _currentGames.Where(item => item.Name.Equals(allRequests[1])))
                        responseStringMiddle = game.Chat.Aggregate("", (current, message) => current + ("<p>" + message + "</p>\n"));
                    buffer = Encoding.UTF8.GetBytes(responseStringStart + responseStringMiddle + ResponseStringEnd);
                    break;
                case "pics":
                    buffer = GetPicture(responseStringStart, allRequests, fullRequest, ref response);
                    break;
                case "favicon.ico":
                    // Browsers request this by default, return an image to use a icon
                    var fInfo2 = new FileInfo("pics/favicon.ico");
                    var numBytes2 = fInfo2.Length;
                    var fStream2 = new FileStream("pics/favicon.ico", FileMode.Open, FileAccess.Read);
                    var br2 = new BinaryReader(fStream2);
                    buffer = br2.ReadBytes((int)numBytes2);
                    br2.Close();
                    fStream2.Close();
                    response.ContentType = "image/ico";
                    break;
                default:
                    // Default response of telling them the path is not valid and a list of other options
                    responseStringMiddle = "<h2>" + fullRequest + " is not a valid path!</h2>\n" +
                                           "<h3>Try one of these instead: </h3>" +
                                           "<ul><li>Players/</li>\n" +
                                           "<li>Games/</li>\n" +
                                           "<li>Watch/\"Game Name\"/</li>\n" +
                                           "<li>Chat/\"Game Name\"/</li>\n" +
                                           "</ul>";
                    buffer = Encoding.UTF8.GetBytes(responseStringStart + responseStringMiddle + ResponseStringEnd);
                    break;
            }

            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            try {
                output.Write(buffer, 0, buffer.Length);
                // You must close the output stream.
                output.Close();
                if (!allRequests[0].Equals("pics"))
                    Console.WriteLine("[Browser] : " + context.Request.RemoteEndPoint + " - " + fullRequest);
            } catch (Exception) {
                // It is probably closing
            }
        }

        private byte[] GetPlayersPage(string responseStringStart) {
            // Combine the list of every connected players name and what they are doing
            var responseStringMiddle = "<p>Players connected: " + _connected.Count + "</p>\n" +
                    _connected.Aggregate("<p>", (current, player) =>
                        current + (player.Item2 + " is " +
                        (player.Item3.Equals("ServerView") ? "in the Server List" :
                        (player.Item3.Equals("Watching") ? "watching a game" :
                        " playing a game called " + player.Item3)) +
                        "<br>")).TrimEnd(',') + "</p>\n";
            return Encoding.UTF8.GetBytes(responseStringStart + responseStringMiddle + ResponseStringEnd);
        }

        private byte[] GetGamesPage(string responseStringStart) {
            var responseStringMiddle = "";
            // Display a list of all games with inline css to show a gradient of the game's colours
            foreach (var game in _currentGames) {
                var gameStuff = game.ToString().Split(',');
                responseStringMiddle += "<p style=\"background: linear-gradient(to right, " +
                          ColorTranslator.ToHtml(game.Board.BlackColour) + "," +
                          ColorTranslator.ToHtml(game.Board.WhiteColour) +
                          ");color:" + ColorTranslator.ToHtml(game.Board.WhiteColour) + "\">" +
                          gameStuff[0] + " - [" + gameStuff[1] + "/" + gameStuff[2] + "]" +
                          "</p>\n";
            }
            return Encoding.UTF8.GetBytes(responseStringStart + responseStringMiddle + ResponseStringEnd);
        }

        private byte[] GetWatchPage(string responseStringStart, IReadOnlyList<string> allRequests) {
            // Add all the headings to the table
            var responseStringMiddle = "<table>\n" +
                                         "  <tr>\n" +
                                         "    <th>x</th><th>1</th><th>2</th><th>3</th><th>4</th><th>5</th><th>6</th><th>7</th><th>8</th>\n" +
                                         "  </tr>\n";
            // Find the game with the same name
            foreach (var game in _currentGames.Where(game => game.Name.Equals(allRequests[1]))) {
                // Get the colours of the game
                var colour1 = game.Board.BlackColour.ToArgb();
                var colour2 = game.Board.WhiteColour.ToArgb();
                var urlPath = "../pics/pieces/" + colour1 + "/" + colour2 + "/pieces.png";
                // Add all the images from a sprite sheet
                responseStringStart = responseStringStart.Replace(
                    "    <meta http-equiv=\"refresh\" content=\"10\">\n",
                    "    <meta http-equiv=\"refresh\" content=\"30\">\n" +
                    "    <style>\n" +
                    "    .RookB { width: 300px; height: 300px; background: url(" + urlPath + ") -10px -60px }\n" +
                    "    .BishopB { width: 300px; height: 300px; background: url(" + urlPath + ") -310px -60px }\n" +
                    "    .QueenB { width: 300px; height: 300px; background: url(" + urlPath + ") -610px -60px }\n" +
                    "    .KingB { width: 300px; height: 300px; background: url(" + urlPath + ") -910px -60px }\n" +
                    "    .knightB { width: 300px; height: 300px; background: url(" + urlPath + ") -1210px -60px }\n" +
                    "    .PawnB { width: 300px; height: 300px; background: url(" + urlPath + ") -1510px -60px }\n" +
                    "    .RookW { width: 300px; height: 300px; background: url(" + urlPath + ") -10px -420px }\n" +
                    "    .BishopW { width: 300px; height: 300px; background: url(" + urlPath + ") -310px -420px }\n" +
                    "    .QueenW { width: 300px; height: 300px; background: url(" + urlPath + ") -610px -420px }\n" +
                    "    .KingW { width: 300px; height: 300px; background: url(" + urlPath + ") -910px -420px }\n" +
                    "    .KnightW { width: 300px; height: 300px; background: url(" + urlPath + ") -1210px -420px }\n" +
                    "    .PawnW { width: 300px; height: 300px; background: url(" + urlPath + ") -1510px -420px }\n" +
                    "    .blank { width: 300px; height: 300px; background-color: #ffffff; }\n" +
                    "    tr { -moz-transform: scale(0.33, 0.33); zoom: 0.33; zoom: 33%; }\n" +
                    "    </style>\n" +
                    "    <script>" +
                    "    " +
                    "    </script>");
                // Go through every square and draw the piece
                for (var i = 1; i < 9; i++) {
                    responseStringMiddle += "<tr><th>" + i + "</th>";
                    for (var j = 1; j < 9; j++) {
                        var tempPiece = game.Board.GetPiece(j, i);
                        if (tempPiece.Equals(Board.Null)) {
                            responseStringMiddle += "<td><img class=\"blank\" alt=\"\" border=3 height=80 width=80></img></td>\n";
                            continue;
                        }
                        var id = tempPiece.PieceType + (tempPiece.Owner.ToArgb() == game.Board.WhiteColour.ToArgb() ? "W" : "B");
                        responseStringMiddle += "<td><img class=\"" + id + "\" alt=\"\" border=3 height=80 width=80></img></td>\n";
                    }
                    responseStringMiddle += "</tr>\n";
                }
            }
            return Encoding.UTF8.GetBytes(responseStringStart + responseStringMiddle + ResponseStringEnd);
        }

        private static byte[] GetPicture(string responseStringStart, IReadOnlyList<string> allRequests, string fullRequest, ref HttpListenerResponse response) {
            byte[] tempBuffer;
            if (allRequests[1].Equals("pieces")) {
                var c = new Color[256];
                // Sets half of the colours to BlackColour and half to WhiteColour
                var col1 = Color.FromArgb(int.Parse(allRequests[2]));
                if (col1.A != 255)
                    col1 = Color.FromArgb(255, col1);
                var col2 = Color.FromArgb(int.Parse(allRequests[3]));
                if (col2.A != 255)
                    col2 = Color.FromArgb(255, col2);
                for (var k = 0; k < 128; k++)
                    c[k] = col1;
                for (var k = 128; k < 256; k++)
                    c[k] = col2;
                c[255] = Color.Transparent;

                // Get and colour the sprite sheet
                var fullImage = new Bitmap("pics/pieces/full.png").Colorize(c);

                var guid = Guid.NewGuid();
                // Save the image temporarily to a random file
                fullImage.Save("pics/pieces/" + guid + ".png");
                // Dispose of the image
                fullImage.Dispose();

                // Load in the image into a stream
                var fInfo = new FileInfo("pics/pieces/" + guid + ".png");
                var numBytes = fInfo.Length;
                var fStream = new FileStream("pics/pieces/" + guid + ".png", FileMode.Open, FileAccess.Read);
                var br = new BinaryReader(fStream);
                tempBuffer = br.ReadBytes((int)numBytes);
                br.Close();
                fStream.Close();
                // Set the response to a png
                response.ContentType = "image/png";
                File.Delete("pics/pieces/" + guid + ".png");
                // Tell the browser to cache the image
                response.AddHeader("Cache-Control", "public, max-age=99936000");
            } else
                try {
                    var fInfo = new FileInfo("pics/" + allRequests[1]);
                    var numBytes = fInfo.Length;
                    var fStream = new FileStream("pics/" + allRequests[1], FileMode.Open, FileAccess.Read);
                    var br = new BinaryReader(fStream);
                    tempBuffer = br.ReadBytes((int)numBytes);
                    br.Close();
                    fStream.Close();
                    response.ContentType = "image/png";
                } catch (Exception) {
                    // Not a valid file, show error
                    var responseStringMiddle = "<h2>Not a valid image " + fullRequest + "</h2>";
                    tempBuffer = Encoding.UTF8.GetBytes(responseStringStart + responseStringMiddle + ResponseStringEnd);
                }
            return tempBuffer;
        }
        #endregion

        /// <summary>
        /// Main method
        /// </summary>
        static void Main() {
            if (IsLinux) {
                Maximize();
                var icon = Icon.ExtractAssociatedIcon("pics/favicon.ico");

                var process = Process.GetCurrentProcess();

                Thread.Sleep(50);
                if (icon != null) {
                    SendMessage(process.MainWindowHandle, 0x80, 1, icon.Handle);
                    SendMessage(process.MainWindowHandle, 0x80, 0, icon.Handle);
                }
            }
            // Start a server
            var s = new ChessServer();
            File.Delete("save/waiting.bin");
            s.SaveWaiting();
            s._savingState = true;
            File.Delete("save/games.bin");
            s.SaveEndedGames();
            File.Delete("save/currentGames");
            s.SaveGames("currentGames");

            s.Dispose();
        }

        private static bool IsLinux {
            get {
                var p = (int)Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hwnd, int message, int wParam, IntPtr lParam);

        private static void Maximize() {
            var p = Process.GetCurrentProcess();
            ShowWindow(p.MainWindowHandle, 3); //SW_MAXIMIZE = 3
        }
    }
}