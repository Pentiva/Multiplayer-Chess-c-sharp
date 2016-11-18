using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using ChessClient.Forms;
using NATUPNPLib;

namespace ChessClient.Net {
    /// <summary>
    /// A client to communicate with the server
    /// </summary>
    public class Client {
        const int PacketSendAmount = 5;
        /// <summary>
        /// Used as an unique identifier for sent packets
        /// </summary>
        public byte GlobalPacketId { get; private set; }
        /// <summary>
        /// The port of the server
        /// </summary>
        const int ServerPort = 64425;
        /// <summary>
        /// The port the server will use to connect to this client
        /// </summary>
        readonly ushort _localPort = 64426;
        /// <summary>
        /// The name of the user
        /// </summary>
        public string ClientName { get; private set; }
        /// <summary>
        /// Socket to receive packets from the server
        /// </summary>
        private readonly Socket _listener;
        /// <summary>
        /// Disposes the socket to stop listening
        /// </summary>
        public void DisposeSocket() {
            _listener.Close();
        }
        /// <summary>
        /// To store the board object when returning the packet
        /// </summary>
        private byte[] _referenceGamePacket;
        /// <summary>
        /// To send packets to the server
        /// </summary>
        readonly UdpClient _sender = new UdpClient();
        /// <summary>
        /// The List of Packets and corresponding bytes that the server has returned from a message
        /// </summary>
        private readonly List<Tuple<Packet, byte[]>> _returns = new List<Tuple<Packet, byte[]>>(40);
        /// <summary>
        /// The list of Packets that the Clients what the server to respond to
        /// </summary>
        private readonly List<Packet> _wantedReturns = new List<Packet>(40);
        /// <summary>
        /// Is the client running?
        /// </summary>
        public bool IsRunning = true;

        /// <summary>
        /// The form the client is currently using
        /// </summary>
        public Form CurrentForm;

        /// <summary>
        /// History of received packets
        /// </summary>
        private readonly Queue<Packet> _history = new Queue<Packet>(20);

        /// <summary>
        /// Create a new Client
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="name"></param>
        /// <param name="startingPacketNo">The globalPacketId to start with</param>
        public Client(IPAddress ip, string name, byte startingPacketNo) {
            ClientName = name;
            _listener = new Socket(SocketType.Dgram, ProtocolType.Udp);
            // Connect to the server
            _sender.Connect(new IPEndPoint(ip, ServerPort));
            // Bind listener to a port to receive Packets from the server 
            var foundPort = false;
            while (!foundPort) {
                try {
                    _listener.Bind(new IPEndPoint(IPAddress.Any, _localPort));
                    foundPort = true;
                } catch {
                    // Port already in use, try next port.
                    _localPort += 1;
                }
            }

            // TODO Re-add
            //var mappings = new UPnPNATClass().StaticPortMappingCollection;
            //if (mappings != null) {
            //    try {
            //        mappings.Remove(_localPort, "UDP");
            //    } catch (Exception) {
            //        // Port already removed
            //    }
            //    var host = Dns.GetHostEntry(Dns.GetHostName());
            //    foreach (var ip2 in host.AddressList.Where(ip2 => ip2.AddressFamily == AddressFamily.InterNetwork)) {
            //        mappings.Add(_localPort, "UDP", _localPort, ip2.ToString(), true, "Local Web Server");
            //        Console.WriteLine("Forwarding port " + _localPort + " for ip " + ip2);
            //        break;
            //    }
            //} else Console.WriteLine("UPnP is not supported.");

            GlobalPacketId = startingPacketNo;

            // Start a new thread to listen for packets
            var clientThread = new Thread(ReceiveThread) {
                Name = "Receive Thread",
                Priority = ThreadPriority.AboveNormal
            };
            clientThread.Start();
        }

        /// <summary>
        /// While running constantly loops to listen for packets
        /// </summary>
        private void ReceiveThread() {
            try {
                while (IsRunning) {
                    var received = new byte[4096];
                    // Receive a message from the server
                    EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    _listener.ReceiveFrom(received, ref sender);
                    // Deal with the message
                    InterpretRequest(received);
                }
            } catch (Exception ex) {
                Console.WriteLine(ex.StackTrace);
            } finally {
                // Send a disconnect packet to the server
                var dissconectPacket = new Packet(GlobalPacketId++, _localPort, Packet.Packets.Disconnect, ClientName);
                _sender.Send(dissconectPacket.GetData(), dissconectPacket.GetData().Length);
                _sender.Close();

                var mappings = new UPnPNATClass().StaticPortMappingCollection;
                if (mappings != null) {
                    try {
                        mappings.Remove(_localPort, "UDP");
                    } catch (Exception) {
                        // Port already removed
                    }
                } else Console.WriteLine("UPnP is not supported.");
            }
        }

        /// <summary>
        /// Find a server on a local network
        /// </summary>
        /// <returns>The IP of the server, or the local ip if it fails</returns>
        public static IPAddress GetServer() {
            // Random port to assign to the client
            ushort tempLocalPort = 64419;
            // Create a new udp client to send a packet
            var udpBroadcast = new UdpClient();
            var foundPort = false;
            while (!foundPort) {
                try {
                    udpBroadcast = new UdpClient(tempLocalPort) { EnableBroadcast = true };
                    foundPort = true;
                } catch {
                    // Port already in use, try next port.
                    tempLocalPort += 1;
                }
            }
            // Connect to the broadcast ip and port
            try {
                udpBroadcast.Connect(IPAddress.Broadcast, ServerPort);
            } catch (Exception) {
                MessageBox.Show("Sorry, your network does not support broadcast, can't find the server automatically.");
                return IPAddress.Loopback;
            }
            // Create a new packet
            var packet = new Packet((byte)new Random().Next(0, 255), tempLocalPort, Packet.Packets.FindServer, "Hello?");
            // Send the packet
            for (var i = 0; i < PacketSendAmount; i++)
                udpBroadcast.Send(packet.GetData(), packet.GetData().Length);
            // Close the udp client
            udpBroadcast.Close();
            var end = new IPEndPoint(IPAddress.Any, 0);
            // Create a new unbound udp client to listen for a reply
            var udpResponse = new UdpClient(tempLocalPort);
            new Thread(() => {
                // If no response after 5 seconds, stop
                Thread.Sleep(5000);
                if (udpResponse != null)
                    udpResponse.Close();
            }).Start();
            try {
                // Wait for reply
                udpResponse.Receive(ref end);
            } catch (Exception) {
                MessageBox.Show("No server on local network");
                return IPAddress.Loopback;
            }
            udpResponse.Close();
            // Return the ip of the server
            return end.Address;
        }

        /// <summary>
        /// Cause client updates based on received data
        /// </summary>
        /// <param name="data">The data that was received</param>
        private void InterpretRequest(IReadOnlyList<byte> data) {
            // Create a fake packet as placeholder
            Packet packet;
            try {
                // Convert the byte array to a packet object
                packet = new Packet(data.ToArray());
                // Make sure the packet hasn't been received already
                if (_history.Any(item => item.Equals(packet))) return;
                if (_history.Count > 20) _history.Dequeue();
                _history.Enqueue(packet);
            } catch (System.IO.InvalidDataException) {
                // if it fails, it is probably a Board object and should be handled differently
                foreach (var pack in _wantedReturns)
                    if (pack.Message[0].Equals("GET")) {
                        var fakePacket = new Packet(0, _localPort, Packet.Packets.FakeGame, "REF", data[0].ToString());
                        fakePacket.SetReurnLink(pack);
                        if (_referenceGamePacket == null) {
                            _referenceGamePacket = data.ToArray();
                            _returns.Add(Tuple.Create(fakePacket, data.ToArray()));
                        } else if (!_referenceGamePacket.SequenceEqual(data.ToArray())) {
                            _referenceGamePacket = data.ToArray();
                            _returns.Add(Tuple.Create(fakePacket, data.ToArray()));
                        }
                    }
                return;
            }

            // If it is a return packet, add it to the list and exit
            try {
                lock (_wantedReturns)
                    if (_wantedReturns.Any(item => item.Message.SequenceEqual(packet.ReturnMessage))) {
                        _returns.Add(Tuple.Create(packet, data.ToArray()));
                        return;
                    }
            } catch (Exception e) {
                Console.WriteLine(e.StackTrace);
            }

            switch (packet.Code) {
                case Packet.Packets.Ping:
                    SendMessage(Packet.Packets.Ping, ClientName, "I AM HERE!");
                    break;
                case Packet.Packets.Connect:
                    // Add a new player to the list
                    if (CurrentForm is ServerDisplayForm) {
                        // Wait until items aren't null 
                        do { } while (((ServerDisplayForm)CurrentForm).ConnectedPlayers == null);
                        ((ServerDisplayForm)CurrentForm).ConnectedPlayers.Items.Add(packet.Message[0]);
                        ((ServerDisplayForm)CurrentForm).ConnectedPlayers.Refresh();
                    }
                    break;
                case Packet.Packets.Disconnect:
                    // Check that the server isn't closing
                    if (packet.ReturnMessage[0].Equals("Server")) {
                        var accept = MessageBox.Show(new Form { TopMost = true, TopLevel = true }, "The server is closing, do you want to close this program",
                            "Server Closing", MessageBoxButtons.YesNo);
                        if (accept != DialogResult.Yes) return;
                        Properties.Settings.Default.Username = ClientName;
                        Properties.Settings.Default.PacketNo = GlobalPacketId;
                        Properties.Settings.Default.Save();
                        Application.Exit();
                        return;
                    }
                    // Remove a disconnected player from the list
                    if (CurrentForm is ServerDisplayForm) {
                        // Wait until items aren't null 
                        do { } while (((ServerDisplayForm)CurrentForm).ConnectedPlayers == null);
                        ((ServerDisplayForm)CurrentForm).ConnectedPlayers.Items.Remove(packet.Message[0]);
                        ((ServerDisplayForm)CurrentForm).ConnectedPlayers.Refresh();
                    }
                    break;
                case Packet.Packets.Info:
                    if (CurrentForm is BoardDisplayForm)
                        ((BoardDisplayForm)CurrentForm).UpdateTitleDelegate(packet.Message[0], packet.Message[1], packet.Message[2]);
                    break;
                case Packet.Packets.Game:
                    if (packet.ReturnMessage[0].Equals("ADDGAME")) {
                        // Add a new game to the list
                        if (CurrentForm is ServerDisplayForm) {
                            var gameMessage = packet.Message[0].Split(',');
                            var gameDisplay = gameMessage[0] + " - " + "[" + gameMessage[1] + "/" + gameMessage[2] + "]";
                            ((ServerDisplayForm)CurrentForm).OpenGamesList.Add(gameDisplay);
                            ((ServerDisplayForm)CurrentForm).Colours.Add(Tuple.Create(gameDisplay, ColorTranslator.FromHtml("#" + gameMessage[3]), ColorTranslator.FromHtml("#" + gameMessage[4])));
                            // Refresh the list to update it
                            ((ServerDisplayForm)CurrentForm).ResetDelegate();
                        }
                    } else if (packet.ReturnMessage[0].Equals("UPDATEGAME")) {
                        // Update the display name of a game if a player joins or leaves a game
                        if (CurrentForm is ServerDisplayForm)
                            ((ServerDisplayForm)CurrentForm).RefreshDelegate();
                    } else if (packet.ReturnMessage[0].Equals("MOVE")) {
                        // Move a piece from one square to another
                        if (CurrentForm is BoardDisplayForm)
                            if (packet.Message.Count > 2)
                                ((BoardDisplayForm)CurrentForm).MovePiece(int.Parse(packet.Message[0]), int.Parse(packet.Message[1]), packet.Message[2].Equals("True"));
                            else if (!packet.Message[0].Equals("OK"))
                                ((BoardDisplayForm)CurrentForm).MovePieceDelegate(int.Parse(packet.Message[0]), int.Parse(packet.Message[1]));
                    } else if (packet.ReturnMessage[0].Equals("SPEC")) {
                        // Removes a piece from the board
                        if (CurrentForm is BoardDisplayForm) {
                            switch (packet.Message.Count) {
                                case 1:
                                    // Remove a piece for pawn special moves
                                    ((BoardDisplayForm)CurrentForm).SpecialRemove(int.Parse(packet.Message[0]));
                                    break;
                                case 2:
                                    // Add a piece for promotion
                                    ((BoardDisplayForm)CurrentForm).SpecialAdd(int.Parse(packet.Message[0]), packet.Message[1], "NULL");
                                    break;
                                case 3:
                                    // Add a piece from redoing a move
                                    ((BoardDisplayForm)CurrentForm).SpecialAdd(int.Parse(packet.Message[0]), packet.Message[1], packet.Message[2]);
                                    break;
                            }
                        }
                    } else if (packet.ReturnMessage[0].Equals("MOVEF")) {
                        // Add a message to the moves list without moving a piece
                        if (CurrentForm is BoardDisplayForm) {
                            try {
                                ((BoardDisplayForm)CurrentForm).FakeMove(packet.Message[0]);
                                ((BoardDisplayForm)CurrentForm).RefreshDelegate();
                            } catch (Exception e) {
                                Console.WriteLine(e.StackTrace);
                            }
                        }
                    } else if (packet.ReturnMessage[0].Equals("TAKE")) {
                        // Ask the player if they agree
                        var answer = MessageBox.Show(new Form { TopMost = true }, "Opponent has requested a redo of their last move. You you accept?", "Take back", MessageBoxButtons.YesNo);
                        if (CurrentForm is BoardDisplayForm)
                            SendMessage(Packet.Packets.Game, "TAKER", ((BoardDisplayForm)CurrentForm).GameName, answer == DialogResult.Yes ? "YES" : "NO");
                    } else if (packet.ReturnMessage[0].Equals("RMOVEF")) {
                        // Remove a message to the moves list without moving a piece
                        if (CurrentForm is BoardDisplayForm) {
                            try {
                                ((BoardDisplayForm)CurrentForm).FakeRemove();
                                ((BoardDisplayForm)CurrentForm).RefreshDelegate();
                            } catch (Exception e) {
                                Console.WriteLine(e.StackTrace);
                            }
                        }
                    } else if (packet.ReturnMessage[0].Equals("DRAW")) {
                        // Ask the player if they agree
                        var answer = MessageBox.Show(new Form { TopMost = true }, "Opponent has requested a draw. You you accept?", "Draw", MessageBoxButtons.YesNo);
                        if (CurrentForm is BoardDisplayForm)
                            SendMessage(Packet.Packets.GameEnd, "DRAWR", ((BoardDisplayForm)CurrentForm).GameName, answer == DialogResult.Yes ? "YES" : "NO");
                    }
                    break;
                case Packet.Packets.Chat:
                    if (CurrentForm is BoardDisplayForm) {
                        // Add the message to the chat box
                        // Wait until items aren't null 
                        do { } while (((BoardDisplayForm)CurrentForm).Chat == null);
                        ((BoardDisplayForm)CurrentForm).Chat.Items.Add(packet.Message[0]);
                        ((BoardDisplayForm)CurrentForm).Chat.Refresh();
                        var visibleItems = ((BoardDisplayForm)CurrentForm).Chat.ClientSize.Height / ((BoardDisplayForm)CurrentForm).Chat.ItemHeight;
                        ((BoardDisplayForm)CurrentForm).Chat.TopIndex = Math.Max(((BoardDisplayForm)CurrentForm).Chat.Items.Count - visibleItems + 1, 0);
                        try {
                            ((BoardDisplayForm)CurrentForm).RefreshDelegate();
                        } catch (Exception e) {
                            Console.WriteLine(e.StackTrace);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Send a message to the server
        /// </summary>
        /// <param name="code">The type of packet</param>
        /// <param name="messages">The message to send</param>
        /// <returns>TUPLE[PACKET, BYTE[]]: The returned packet and it's bytes</returns>
        public Tuple<Packet, byte[]> SendMessage(Packet.Packets code, params string[] messages) {
            // Construct a packet from the info given
            var sendPacket = new Packet(GlobalPacketId++, _localPort, code, messages);
            // Add it to a list containing packet that need a response
            _wantedReturns.Add(sendPacket);

            // Send the packet (multiple times for udp) 
            // (In a new thread so that the return message can be dealt with if possible before all redundant messages are sent)
            new Thread(() => {
                for (var i = 0; i < PacketSendAmount; i++)
                    try {
                        _sender.Send(sendPacket.GetData(), sendPacket.GetData().Length);
                    } catch (Exception) {
                        MessageBox.Show("A Communication Error has occurred, the program will close now, please restart.");
                        Application.Exit();
                    }
            }).Start();

            var reply = WaitForReply(sendPacket);
            if (reply != null) return reply;

            // If nothing is returned, return random junk
            var returnPacket = new Packet(0, 0, Packet.Packets.Error, "null");
            returnPacket.SetReurnLink(sendPacket);
            return Tuple.Create(returnPacket, new byte[] { 1, 2, 3, 4, 5, 6 });
        }

        private Tuple<Packet, byte[]> WaitForReply(Packet sendPacket) {
            for (var i = 0; i < 200; i++) {
                try {
                    // Check though every returned packet
                    foreach (var returned in _returns.Where(returned => returned.Item1.ReturnMessage.SequenceEqual(sendPacket.Message))) {
                        // Remove it from the lists
                        _wantedReturns.Remove(sendPacket);
                        _returns.Remove(returned);
                        // return it
                        return returned.Item1.Code == Packet.Packets.FakeGame ? Tuple.Create(returned.Item1, _referenceGamePacket) : returned;
                    }
                } catch (Exception ex) {
                    Console.WriteLine(ex.StackTrace);
                }
                Thread.Sleep(40);
            }
            return null;
        }
    }
}