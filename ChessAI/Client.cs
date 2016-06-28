using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ChessClient.Net;

namespace ChessAI {
    /// <summary>
    /// A client to communicate with the server
    /// </summary>
    public class Client {
        const int PacketSendAmount = 5;
        /// <summary>
        /// Used as an unique identifier for sent packets
        /// </summary>
        private byte GlobalPacketId { get; set; }
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
        /// History of received packets
        /// </summary>
        private readonly Queue<Packet> _history = new Queue<Packet>(20);

        //public ChessGame CurrentGame;

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
            while (IsRunning) {
                var received = new byte[4096];
                // Receive a message from the server
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                try {
                    _listener.ReceiveFrom(received, ref sender);
                } catch (Exception) {
                    // ignored
                }
                // Deal with the message
                InterpretRequest(received);
            }

            // Send a disconnect packet to the server
            var dissconectPacket = new Packet(GlobalPacketId++, _localPort, Packet.Packets.Disconnect, ClientName);
            _sender.Send(dissconectPacket.GetData(), dissconectPacket.GetData().Length);
            _sender.Close();
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
                try {
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
                } catch (Exception) {
                    // ignored
                }
                return;
            }

            // If it is a return packet, add it to the list and exit
            lock (_wantedReturns)
                if (_wantedReturns.Any(item => item.Message.SequenceEqual(packet.ReturnMessage))) {
                    _returns.Add(Tuple.Create(packet, data.ToArray()));
                    return;
                }

            switch (packet.Code) {
                case Packet.Packets.Ping:
                    SendMessage(Packet.Packets.Ping, ClientName, "I AM HERE!");
                    break;
                case Packet.Packets.Connect:
                    // Don't Care
                    break;
                case Packet.Packets.Disconnect:
                    if (packet.ReturnMessage[0].Equals("Server")) {
                        Environment.Exit(0);
                    }
                    break;
                case Packet.Packets.Info:
                    // Don't Care
                    break;
                case Packet.Packets.Game:
                    if (packet.ReturnMessage[0].Equals("ADDGAME")) {
                        // Don't Care
                    } else if (packet.ReturnMessage[0].Equals("UPDATEGAME")) {
                        // Don't Care
                    } else if (packet.ReturnMessage[0].Equals("MOVE")) {
                        //if (packet.Message.Count > 2)
                        //    CurrentGame.MovePiece(int.Parse(packet.Message[0]), int.Parse(packet.Message[1]), packet.Message[2].Equals("True"));
                        //else if (!packet.Message[0].Equals("OK"))
                        //    CurrentGame.MovePiece(int.Parse(packet.Message[0]), int.Parse(packet.Message[1]));
                    } else if (packet.ReturnMessage[0].Equals("SPEC")) {
                        //switch (packet.Message.Count) {
                        //    case 1:
                        //        // Remove a piece for pawn special moves
                        //        CurrentGame.SpecialRemove(int.Parse(packet.Message[0]));
                        //        break;
                        //    case 2:
                        //        // Add a piece for promotion
                        //        CurrentGame.SpecialAdd(int.Parse(packet.Message[0]), packet.Message[1], "NULL");
                        //        break;
                        //    case 3:
                        //        // Add a piece from redoing a move
                        //        CurrentGame.SpecialAdd(int.Parse(packet.Message[0]), packet.Message[1], packet.Message[2]);
                        //        break;
                        //}
                    } else if (packet.ReturnMessage[0].Equals("MOVEF")) {
                        //CurrentGame.FakeMove(packet.Message[0]);
                    } else if (packet.ReturnMessage[0].Equals("TAKE")) {
                        // Don't Care
                    } else if (packet.ReturnMessage[0].Equals("RMOVEF")) {
                        // Don't Care
                    } else if (packet.ReturnMessage[0].Equals("DRAW")) {
                        // Don't Care
                    }
                    break;
                case Packet.Packets.Chat:
                    // Don't Care
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
                    _sender.Send(sendPacket.GetData(), sendPacket.GetData().Length);
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
                // Check though every returned packet
                foreach (var returned in _returns.Where(returned => returned.Item1.ReturnMessage.SequenceEqual(sendPacket.Message))) {
                    // Remove it from the lists
                    _wantedReturns.Remove(sendPacket);
                    _returns.Remove(returned);
                    // return it
                    return returned.Item1.Code == Packet.Packets.FakeGame ? Tuple.Create(returned.Item1, _referenceGamePacket) : returned;
                }
                Thread.Sleep(40);
            }
            return null;
        }
    }
}