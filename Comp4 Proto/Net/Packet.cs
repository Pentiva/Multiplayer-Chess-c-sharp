using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO.Compression;
using System.Net;

namespace ChessClient.Net {
    /// <summary>
    /// A packet to communicate between the client and server
    /// </summary>
    [Serializable]
    public class Packet {
        /// <summary>
        /// The id of the packet
        /// </summary>
        private readonly byte _packetId;
        /// <summary>
        /// Port that the sender uses to receive messages
        /// </summary>
        private readonly ushort _senderPort;
        /// <summary>
        /// The address this packet originated from
        /// </summary>
        [NonSerialized]
        public IPEndPoint SenderAddress;
        /// <summary>
        /// What type of packet it is
        /// </summary>
        public readonly Packets Code;
        /// <summary>
        /// The message that gets sent
        /// </summary>
        public readonly List<string> Message = new List<string>(5);
        /// <summary>
        /// If this packet is a return message, the original message
        /// </summary>
        public List<string> ReturnMessage = new List<string>(5);

        /// <summary>
        /// Create a new packet
        /// </summary>
        /// <param name="globalPacketId">The unique id for this packet</param>
        /// <param name="port">The port that the sender is using to receive</param>
        /// <param name="code">The type of packet</param>
        /// <param name="message">The packets message</param>
        public Packet(byte globalPacketId, ushort port, Packets code, params string[] message) {
            _packetId = globalPacketId;
            _senderPort = port;
            Code = code;
            foreach (var s in message) {
                Message.Add(s);
            }
        }

        /// <summary>
        /// Set that this packet is a return message
        /// </summary>
        /// <param name="packet">The original message</param>
        public void SetReurnLink(Packet packet) {
            ReturnMessage = packet.Message;
        }

        /// <summary>
        /// Construct the sender's Address from a IP and port
        /// </summary>
        /// <param name="ip">The ip to use</param>
        public void SetSenderAddress(IPAddress ip) {
            SenderAddress = new IPEndPoint(ip, _senderPort);
        }

        /// <summary>
        /// Create a new Packet
        /// </summary>
        /// <param name="data">The compressed serialize packet</param>
        public Packet(IEnumerable<byte> data) {
            byte[] bytes;
            // Decompress the bytes
            using (var stream = new GZipStream(new MemoryStream(data.ToArray()), CompressionMode.Decompress)) {
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
                    bytes = memory.ToArray();
                }
            }

            Packet packet;
            // Deserialize the packet
            using (var stream = new MemoryStream(bytes))
                packet = (Packet)new BinaryFormatter().Deserialize(stream);
            // Assign the variables in packet to this
            _packetId = packet._packetId;
            _senderPort = packet._senderPort;
            Code = packet.Code;
            Message = packet.Message;
            ReturnMessage = packet.ReturnMessage;
        }

        /// <summary>
        /// Serialize and compress this packet
        /// </summary>
        /// <returns>BYTE[]: Serialize and compressed bytes</returns>
        public byte[] GetData() {
            byte[] data;
            // Serialize the packet
            using (var stream = new MemoryStream()) {
                var bin = new BinaryFormatter();
                bin.Serialize(stream, this);
                data = stream.ToArray();
            }
            // Compress the serialize packet
            using (var memory = new MemoryStream()) {
                using (var gzip = new GZipStream(memory, CompressionMode.Compress, true))
                    gzip.Write(data, 0, data.Length);
                return memory.ToArray();
            }
        }

        /// <summary>
        /// Check if two objects are equal
        /// </summary>
        /// <param name="obj">The object to compare this to</param>
        /// <returns>BOOL: If all the fields are equal</returns>
        public override bool Equals(object obj) {
            // Make sure it is a packet object
            if (!(obj is Packet)) return false;
            var p = (Packet)obj;
            // Check if all the fields are equal
            return (p._packetId == _packetId) && (p._senderPort == _senderPort) && (p.Code == Code) &&
                (p.Message.SequenceEqual(Message)) && (p.ReturnMessage.SequenceEqual(ReturnMessage)) &&
                (p.SenderAddress == null || (p.SenderAddress.Equals(SenderAddress)));
        }

        /// <summary>
        /// Need to override when overriding Equals
        /// </summary>
        /// <returns>Something that two equal packets will have in common, 
        /// but does not make them common</returns>
        public override int GetHashCode() {
            return _packetId * _senderPort;
        }

        /// <summary>
        /// Enum of packet codes
        /// </summary>
        public enum Packets {
            /// <summary>
            /// A error packet
            /// </summary>
            Error = 123,
            /// <summary>
            /// Simple no use packet
            /// </summary>
            Ping = 0,
            /// <summary>
            /// Client connect packet
            /// </summary>
            Connect = 1,
            /// <summary>
            /// Client disconnect packet
            /// </summary>
            Disconnect = 2,
            /// <summary>
            /// Packet contains info about games or players
            /// </summary>
            Info = 3,
            /// <summary>
            /// Packet containing a chat message
            /// </summary>
            Chat = 4,
            /// <summary>
            /// Packet about a game event
            /// </summary>
            Game = 5,
            /// <summary>
            /// Packet about a client leaving a game
            /// </summary>
            DisconnectGame = 6,
            /// <summary>
            /// The client asking for the server's ip address
            /// </summary>
            FindServer = 7,
            /// <summary>
            /// Packet for when a game ends or is going to end
            /// </summary>
            GameEnd = 8,
            /// <summary>
            /// Not really a packet, just a serialized board object
            /// </summary>
            FakeGame = 9
        }
    }
}