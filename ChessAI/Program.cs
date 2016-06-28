using System;

namespace ChessAI {
    static class Program {
        static void Main() {
            var a = new ChessGame("AIGame", true);
            Console.WriteLine(a);
            Console.Read();
        }
    }
}