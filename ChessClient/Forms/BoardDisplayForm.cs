using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ChessClient.Game;
using ChessClient.Net;
using ChessClient.Properties;

namespace ChessClient.Forms {
    /// <summary>
    /// Form to display the game
    /// </summary>
    public sealed class BoardDisplayForm : Form {

        #region DesignerCode
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private readonly IContainer components;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
                for (var i = 0; i < 12; i++)
                    _allImages[i].Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion

        /// <summary>
        /// All the images for the chess pieces
        /// </summary>
        private readonly Bitmap[] _allImages = new Bitmap[12];
        /// <summary>
        /// An array of rectangles and if they are Player 2's colour
        /// </summary>
        private Tuple<Rectangle, bool>[] _rectangles = new Tuple<Rectangle, bool>[64];
        /// <summary>
        /// If this client is player one. (White)
        /// </summary>
        private readonly bool _playerOne;
        /// <summary>
        /// If this client is playing or spectating
        /// </summary>
        private readonly bool _playing = true;
        /// <summary>
        /// A list of all squares that need to be highlighted a different colour
        /// </summary>
        private readonly List<Tuple<int, Color>> _hightlightSquares = new List<Tuple<int, Color>>();
        /// <summary>
        /// A display of all moves of the game
        /// </summary>
        private ListBox _moves;
        /// <summary>
        /// A display of all the chat messages
        /// </summary>
        public ListBox Chat;
        /// <summary>
        /// User input for chat messages
        /// </summary>
        private TextBox _chatMessage;
        /// <summary>
        /// Send button for chat messages
        /// </summary>
        private Button _sendMessage;
        /// <summary>
        /// The coordinate of the rectangle that has the mouse in
        /// </summary>
        private int _highlightCoord = 64;
        /// <summary>
        /// The name of this game
        /// </summary>
        public readonly string GameName;
        /// <summary>
        /// The board object that contains all piece and colour information
        /// </summary>
        private readonly Board _board;
        /// <summary>
        /// The client to send messages to the server
        /// </summary>
        public readonly Client Client;
        /// <summary>
        /// Button to propose a take back of a move
        /// </summary>
        private Button _proposeTakeback;
        /// <summary>
        /// Button to propose a draw
        /// </summary>
        private Button _requestDraw;
        /// <summary>
        /// Button to end the game with your lose
        /// </summary>
        private Button _resignGame;
        /// <summary>
        /// Button to leave the game
        /// </summary>
        private Button _leaveGame;

        /// <summary>
        /// Create a new BoardDisplayForm
        /// </summary>
        /// <param name="client">The client for the current player</param>
        /// <param name="board">The board object to display</param>
        /// <param name="player">Whether this player is player one, player two, or a spectator</param>
        /// <param name="name">The name of this game</param>
        public BoardDisplayForm(Client client, Board board, bool? player, string name) {
            components = new Container();
            AutoScaleMode = AutoScaleMode.Font;
            Icon = Resources.KnightIcon;

            _board = board;

            // Check if playing the game or watching
            if (player.HasValue)
                _playerOne = player.Value;
            else
                _playing = false;

            GameName = name;

            Client = client;
            Client.CurrentForm = this;

            var c = new Color[256];
            // Sets half of the colours to BlackColour and half to WhiteColour
            for (var i = 0; i < 128; i++)
                c[i] = _board.WhiteColour;
            for (var i = 128; i < 256; i++)
                c[i] = _board.BlackColour;
            c[255] = Color.Transparent;

            // Colour all the images for the pieces
            var colouredImage = Resources.ChessPieces.Colorize(c);
            for (var i = 0; i < 6; i++)
                for (var j = 0; j < 2; j++)
                    _allImages[i + (j * 6)] = colouredImage.Clone(
                        new Rectangle(10 + (300 * i) + (i == 5 ? -10 : 0), 60 + (360 * j), 300, 300),
                        Resources.ChessPieces.PixelFormat);
            colouredImage.Dispose();

            // Fill the array of rectangles with blank rectangles
            for (var i = 0; i < 64; i++)
                _rectangles[i] = Tuple.Create(new Rectangle(), false);

            // Size the form
            Width = 680;
            Height = 680;
            MinimumSize = new Size(440, 440);
            MaximumSize = new Size(Screen.PrimaryScreen.WorkingArea.Height, Screen.PrimaryScreen.WorkingArea.Height);
            MaximizeBox = false;
            DoubleBuffered = true;
            CenterToScreen();

            AssignEvents();
            SetupControls();

            ResizeBoard(null, null);
            _sendMessage.Refresh();
        }

        /// <summary>
        /// Assigns events
        /// </summary>
        private void AssignEvents() {
            // Assign events
            Paint += PaintBoard;
            Resize += ResizeBoard;
            MouseClick += BoardMouseClick;
            FormClosing += (sender, e) => { Dispose(); };
            ResizeEnd += (sender, e) => { _moves.Refresh(); };

            // Scroll to the bottom and refresh when form gains focus
            Activated += (sender, e) => {
                var visibleItems = _moves.ClientSize.Height / _moves.ItemHeight;
                _moves.TopIndex = Math.Max(_moves.Items.Count - visibleItems + 1, 0);
                _moves.Refresh();
                visibleItems = Chat.ClientSize.Height / Chat.ItemHeight;
                Chat.TopIndex = Math.Max(Chat.Items.Count - visibleItems + 1, 0);
                Chat.Refresh();
                // Redraw the board
                Invalidate();
            };

            // Colour the rectangle currently highlighted
            MouseMove += (sender, e) => {
                _highlightCoord = 65;
                for (var i = 0; i < 64; i++)
                    if (new Rectangle(e.X, e.Y, 1, 1).IntersectsWith(_rectangles[i].Item1))
                        _highlightCoord = i;
                // Redraw the board
                Invalidate();
            };
        }

        /// <summary>
        /// Setup the controls on the form
        /// </summary>
        private void SetupControls() {
            // Setup the controls with values that are permanent
            _moves = new ListBox {
                Top = 10, ScrollAlwaysVisible = true, SelectionMode = SelectionMode.None, IntegralHeight = false, TabStop = false
            };
            Controls.Add(_moves);

            Chat = new ListBox {
                ScrollAlwaysVisible = true, SelectionMode = SelectionMode.None, IntegralHeight = false, TabStop = false
            };
            Controls.Add(Chat);

            _chatMessage = new TextBox { Left = 10, AutoSize = false };
            _chatMessage.KeyPress += (sender, e) => {
                if (e.KeyChar == Convert.ToChar(Keys.Enter))
                    // Perform a click when enter is pressed
                    _sendMessage.PerformClick();
            };
            Controls.Add(_chatMessage);

            _sendMessage = new Button { Text = "Submit" };
            _sendMessage.Click += SendMessage;
            Controls.Add(_sendMessage);

            _proposeTakeback = new Button { BackgroundImage = Resources.TakebackMove, BackgroundImageLayout = ImageLayout.Stretch };
            _proposeTakeback.Click += (sender, e) => {
                if (_playerOne == _board.PlayerOneTurn) {
                    MessageBox.Show("It is currently your move");
                    return;
                }
                var returned = Client.SendMessage(Packet.Packets.Game, "TAKE", GameName, Client.ClientName);
                if (returned.Item1.Code != Packet.Packets.Error) return;
                MessageBox.Show("Server has not allowed the take back");
            };
            if (_playing) {
                Controls.Add(_proposeTakeback);
                var takebackToolTip = new ToolTip();
                takebackToolTip.SetToolTip(_proposeTakeback, "Redo that move");
            }

            _requestDraw = new Button { BackgroundImage = Resources.DrawGame, BackgroundImageLayout = ImageLayout.Stretch };
            _requestDraw.Click += (sender, e) => {
                var returned = Client.SendMessage(Packet.Packets.GameEnd, "DRAW", GameName, Client.ClientName);
                if (returned.Item1.Code != Packet.Packets.Error) return;
                MessageBox.Show("Server has not allowed the draw");
            };
            if (_playing) {
                Controls.Add(_requestDraw);
                var drawToolTip = new ToolTip();
                drawToolTip.SetToolTip(_requestDraw, "Request a draw");
            }

            _resignGame = new Button { BackgroundImage = Resources.ResignGame, BackgroundImageLayout = ImageLayout.Stretch };
            _resignGame.Click += (sender, e) => {
                var returned = Client.SendMessage(Packet.Packets.GameEnd, "RESIGN", GameName, Client.ClientName);
                if (returned.Item1.Code != Packet.Packets.Error) return;
                MessageBox.Show("Server has not allowed the resignation");
            };
            if (_playing) {
                Controls.Add(_resignGame);
                var resignToolTip = new ToolTip();
                resignToolTip.SetToolTip(_resignGame, "Loose the game");
            }

            _leaveGame = new Button { BackgroundImage = Resources.LeaveGame, BackgroundImageLayout = ImageLayout.Stretch };
            _leaveGame.Click += (sender, e) => {
                var returned = Client.SendMessage(Packet.Packets.GameEnd, "LEAVE", GameName, Client.ClientName);
                if (returned.Item1.Code != Packet.Packets.Error) { Close(); return; }
                MessageBox.Show("Server has not allowed you to leave");
            };
            if (!_playing) return;
            Controls.Add(_leaveGame);
            var leaveToolTip = new ToolTip();
            leaveToolTip.SetToolTip(_leaveGame, "Leave the game");
        }

        /// <summary>
        /// Add the clients name to the message and sends it to the server
        /// </summary>
        /// <param name="sender">The control that fired the event</param>
        /// <param name="e">Additional info from the event</param>
        private void SendMessage(object sender, EventArgs e) {
            if (_chatMessage.Text.Length <= 0) return;
            var returnMessage = Client.SendMessage(Packet.Packets.Chat, GameName, "[" + Client.ClientName + "] " + _chatMessage.Text);
            if (returnMessage.Item1.Code != Packet.Packets.Error) _chatMessage.Clear();
            _chatMessage.Refresh();
        }

        #region "Windows Override Resize"
        /// <param name="m">The Windows <see cref="T:System.Windows.Forms.Message"/> to process. </param>
        protected override void WndProc(ref Message m) {
            if (m.Msg == 0x216 || m.Msg == 0x214) { // WM_MOVING || WM_SIZING
                // Keep the window square
                var rc = (Rect)Marshal.PtrToStructure(m.LParam, typeof(Rect));
                var w = rc.Right - rc.Left;
                var h = rc.Bottom - rc.Top;
                var z = w > h ? w : h;
                rc.Bottom = rc.Top + z;
                rc.Right = rc.Left + z;
                Marshal.StructureToPtr(rc, m.LParam, false);
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public readonly int Left; public readonly int Top; public int Right; public int Bottom; }
        #endregion

        /// <summary>
        /// Minus one colour from another
        /// </summary>
        /// <param name="colour1">The main colour</param>
        /// <param name="colour2">The colour to minus from colour1</param>
        /// <returns>COLOR: colour1-colour2</returns>
        private static Color ColourMinus(Color colour1, Color colour2) {
            var r = colour1.R - colour2.R;
            while (r <= 0) r += 255;
            var g = colour1.G - colour2.G;
            while (g <= 0) g += 255;
            var b = colour1.B - colour2.B;
            while (b <= 0) b += 255;
            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// Adds one colour to another
        /// </summary>
        /// <param name="colour1">The main colour</param>
        /// <param name="colour2">The colour to add from colour1</param>
        /// <returns>COLOR: colour1+colour2</returns>
        private static Color ColourAdd(Color colour1, Color colour2) {
            var r = colour1.R + colour2.R;
            while (r >= 255) r -= 255;
            var g = colour1.G + colour2.G;
            while (g >= 255) g -= 255;
            var b = colour1.B + colour2.B;
            while (b >= 255) b -= 255;
            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// Draws the Squares, the grid references and the pieces (taken and in play)
        /// </summary>
        /// <param name="sender">The control that fired the event</param>
        /// <param name="e">Additional info from the event</param>
        private void PaintBoard(object sender, PaintEventArgs e) {
            var graphics = e.Graphics;
            graphics.Clear(Color.White);
            // Fill in Black squares
            graphics.FillRectangles(new SolidBrush(_board.WhiteColour), _rectangles.Where(rect => rect.Item2).Select(rect => rect.Item1).ToArray());
            // Fill in White squares
            graphics.FillRectangles(new SolidBrush(_board.BlackColour), _rectangles.Where(rect => !rect.Item2).Select(rect => rect.Item1).ToArray());
            // Fill in Special squares
            foreach (var item in _hightlightSquares)
                using (var brush = new SolidBrush(item.Item2))
                    graphics.FillRectangle(brush, _rectangles[item.Item1].Item1);
            // Fill in highlight squares
            if (_highlightCoord < 64)
                graphics.FillRectangle(
                    _rectangles.Where(rect => !rect.Item2)
                        .Select(rect => rect.Item1)
                        .ToArray()
                        .Contains(_rectangles[_highlightCoord].Item1)
                        ? new SolidBrush(ColourAdd(_board.WhiteColour, Color.Gray))
                        : new SolidBrush(ColourMinus(_board.BlackColour, Color.DarkGray)), _rectangles[_highlightCoord].Item1);

            // Outline every square
            graphics.DrawRectangles(new Pen(_board.WhiteColour), _rectangles.Select(rect => rect.Item1).ToArray());

            // Draw the pieces to the board
            try {
                foreach (var piece in _board.Pieces)
                    graphics.DrawImage(_allImages[(int)piece.PieceType + (piece.Owner.ToArgb() == _board.WhiteColour.ToArgb() ? 0 : 6)],
                        _rectangles[((piece.X - 1) + ((piece.Y - 1) * 8))].Item1);
            } catch (Exception ex) {
                Console.WriteLine(ex.StackTrace);
            }

            // Draw Taken pieces
            for (var i = 0; i < _board.TakenPieces.Count; i++) {
                var takenPiece = _board.TakenPieces[i];
                graphics.DrawImage(_allImages[(int)takenPiece.PieceType + (takenPiece.Owner.ToArgb() == _board.WhiteColour.ToArgb() ? 0 : 6)],
                    _moves.Right + (Width / 40) + (i >= 16 ? (Width / 24) : 0),
                    _moves.Top + (i % 16 * (Height / 28)),
                    Width / 28,
                    Height / 28);
            }

            // Draw numbers at Right of board
            for (var i = 1; i < 9; i++) {
                var stringSize = graphics.MeasureString(i.ToString(), new Font(FontFamily.GenericMonospace, (Width / 46f), FontStyle.Bold));
                graphics.DrawString(i.ToString(), new Font(FontFamily.GenericMonospace, (Width / 46f), FontStyle.Bold), Brushes.Black,
                    new Point((int)(_rectangles[63].Item1.Left + (_rectangles[63].Item1.Width * 1.5) - (stringSize.Width / 2) + (_playerOne || !_playing ? 0 : (_rectangles[63].Item1.Width * 7.25))),
                        (int)(_rectangles[63 - ((i - 1) * 8)].Item1.Top + (_rectangles[63].Item1.Height * 0.5) - (stringSize.Height / 2))));
            }
            // Draw letters at bottom of board
            for (var i = 1; i < 9; i++) {
                var stringSize = graphics.MeasureString(Encoding.ASCII.GetString(new[] { (byte)(i + 64) }),
                    new Font(FontFamily.GenericMonospace, (Width / 46f), FontStyle.Bold));
                graphics.DrawString(Encoding.ASCII.GetString(new[] { (byte)(i + 64) }), new Font(FontFamily.GenericMonospace, (Width / 46f), FontStyle.Bold),
                    Brushes.Black, new Point((int)(_rectangles[55 + i].Item1.Left + (_rectangles[63].Item1.Width / 2) - (stringSize.Width / 2)),
                        (int)(_rectangles[63].Item1.Top + (_rectangles[63].Item1.Height * 1.5) - (stringSize.Height / 2) + (_playerOne || !_playing ? 0 : (_rectangles[63].Item1.Height * 7.25)))));
            }
        }

        /// <summary>
        /// Changes the layout of the controls when the form is resized
        /// </summary>
        /// <param name="sender">The control that fired the event</param>
        /// <param name="e">Additional info from the event</param>
        private void ResizeBoard(object sender, EventArgs e) {
            var widthScale = Width / 14;
            var heightScale = Height / 14;
            // Resizes the squares based on the form size
            for (var i = 0; i < 8; i++)
                for (var j = 0; j < 8; j++)
                    _rectangles[i + (j * 8)] = Tuple.Create(new Rectangle(i * widthScale + 10, j * heightScale + 10, widthScale - 2, heightScale - 2), (i % 2 == 0 ^ j % 2 == 1));
            // Resizes the moves list box
            _moves.Width = (Width / 5) + 14;
            _moves.Height = _rectangles[63].Item1.Bottom - _moves.Top;
            _moves.Left = _rectangles[7].Item1.Right + Width / 64 + (Width / 22);
            _moves.Font = new Font(FontFamily.GenericMonospace, (Width / 38f), FontStyle.Bold);
            // Scrolls to the bottom of the list box
            var visibleItems = _moves.ClientSize.Height / _moves.ItemHeight;
            _moves.TopIndex = Math.Max(_moves.Items.Count - visibleItems + 1, 0);
            _moves.Refresh();
            // Resizes the chat list box
            Chat.Width = Width - 30;
            Chat.Height = (int)(Math.Floor((Height / 4.01f) + (Height / 100f))) - 30;
            Chat.Top = (_rectangles[63].Item1.Top + (_rectangles[63].Item1.Height * 2));
            Chat.Font = new Font(FontFamily.GenericMonospace, (Width / 46f), FontStyle.Bold);
            // Scrolls to the bottom of the list box
            visibleItems = Chat.ClientSize.Height / Chat.ItemHeight;
            Chat.TopIndex = Math.Max(Chat.Items.Count - visibleItems + 1, 0);
            Chat.Refresh();
            // Resizes the chat textbox
            _chatMessage.Width = (int)(Width / 1.2333f) + Width / 34;
            _chatMessage.Top = Chat.Bottom + 5;
            _chatMessage.Height = Height / 22;
            _chatMessage.Font = new Font(FontFamily.GenericMonospace, (Width / 44f), FontStyle.Regular);
            _chatMessage.Refresh();
            // Resizes the submit button
            _sendMessage.Font = new Font(FontFamily.GenericMonospace, (Width / 66f), FontStyle.Regular);
            _sendMessage.Top = _chatMessage.Top;
            _sendMessage.Left = _chatMessage.Right + 2;
            _sendMessage.Height = Height / 22;
            _sendMessage.Width = Width / 10;
            _sendMessage.Refresh();

            // Redraw the board
            Invalidate();

            // Don't need to resizes if not playing
            if (!_playing) return;

            // Resizes the Propose take back button
            _proposeTakeback.Left = _moves.Left;
            _proposeTakeback.Top = _moves.Bottom + 2;
            _proposeTakeback.Width = Width / 14;
            _proposeTakeback.Height = Width / 17;

            // Resizes the request draw button
            _requestDraw.Left = _proposeTakeback.Right + (Width / 340);
            _requestDraw.Top = _moves.Bottom + 2;
            _requestDraw.Width = Width / 14;
            _requestDraw.Height = Width / 17;

            // Resizes the resign button
            _resignGame.Left = _requestDraw.Right + (Width / 340);
            _resignGame.Top = _moves.Bottom + 2;
            _resignGame.Width = Width / 14;
            _resignGame.Height = Width / 17;

            // Resizes the Leave button
            _leaveGame.Left = _resignGame.Right + (Width / 340);
            _leaveGame.Top = _moves.Bottom + 2;
            _leaveGame.Width = Width / 14;
            _leaveGame.Height = Width / 17;

            // If not player one, flip the board
            if (!_playerOne) _rectangles = _rectangles.Reverse().ToArray();
        }

        /// <summary>
        /// Handles mouse clicks on the board
        /// </summary>
        /// <param name="sender">The control that fired the event</param>
        /// <param name="e">Additional info from the event</param>
        private void BoardMouseClick(object sender, MouseEventArgs e) {
            // Redraw the board
            Invalidate();
            // If they are not playing, they don't need to click
            //if (!_playing) { _hightlightSquares.Clear(); return; }
            // If it is not their turn, they don't need to click
            if (_board.PlayerOneTurn != _playerOne && _playing) { _hightlightSquares.Clear(); return; }
            for (var i = 0; i < 64; i++)
                // Check what square is clicked
                if (new Rectangle(e.X, e.Y, 1, 1).IntersectsWith(_rectangles[i].Item1)) {
                    // Ignore not left clicks
                    if (e.Button != MouseButtons.Left) continue;
                    // If there are no highlighted squares, some need to be highlighted
                    if (_hightlightSquares.Count == 0) {
                        var x = (i % 8) + 1;
                        var y = (i / 8) + 1;
                        var piece = _board.GetPiece(x, y);
                        piece.GetMoves(_board, true);
                        // Piece has to be the same colour as the player
                        if (piece.Owner == (!_playerOne ? _board.WhiteColour : _board.BlackColour) && _playing) break;
                        if (piece.PieceType == Pieces.Null) break;
                        // Add the piece to the highlighted squares
                        _hightlightSquares.Add(Tuple.Create(i, Color.Purple));
                        Console.WriteLine(piece.Owner);
                        if (piece.Moves == null) continue;
                        foreach (var move in piece.Moves) {
                            var x1 = move.Item1 - 1;
                            var y1 = move.Item2 - 1;
                            // Add all possible moves to highlighted squares
                            _hightlightSquares.Add(Tuple.Create(x1 + (y1 * 8), Color.Green));
                        }
                    } else if (_hightlightSquares.Count > 0 && _playing) {
                        if (_hightlightSquares.Contains(Tuple.Create(i, Color.Green))) {
                            var y1 = (i / 8) + 1;
                            Tuple<Packet, byte[]> valid;
                            if (_board.GetPiece((_hightlightSquares[0].Item1 % 8) + 1, (_hightlightSquares[0].Item1 / 8) + 1).PieceType == Pieces.Pawn && (y1 == 1 || y1 == 8)) {
                                // If it is a pawn at the other end of the board, then ask player what piece to promote to
                                var chosen = new PromotionMenuForm();
                                chosen.ShowDialog();
                                // Tell the server, the move and what promotion
                                valid = Client.SendMessage(Packet.Packets.Game, "MOVE", GameName, _hightlightSquares[0].Item1.ToString(), i.ToString(), _playerOne.ToString(), chosen.Chosen.ToString());
                            } else
                                // Tell the server the the move
                                valid = Client.SendMessage(Packet.Packets.Game, "MOVE", GameName, _hightlightSquares[0].Item1.ToString(), i.ToString(), _playerOne.ToString());
                            if (valid.Item1.Code == Packet.Packets.Error) MessageBox.Show("Not a valid move according to the server");
                        }
                        _hightlightSquares.Clear();
                    } else {
                        _hightlightSquares.Clear();
                        var x = (i % 8) + 1;
                        var y = (i / 8) + 1;
                        var piece = _board.GetPiece(x, y);
                        // Piece has to be the same colour as the player
                        if (piece.PieceType == Pieces.Null) break;
                        // Add the piece to the highlighted squares
                        _hightlightSquares.Add(Tuple.Create(i, Color.Purple));
                        if (piece.Moves == null) continue;
                        foreach (var move in piece.Moves) {
                            var x1 = move.Item1 - 1;
                            var y1 = move.Item2 - 1;
                            // Add all possible moves to highlighted squares
                            _hightlightSquares.Add(Tuple.Create(x1 + (y1 * 8), Color.Green));
                        }
                    }
                }
        }

        /// <summary>
        /// Delegate object to refresh the screen
        /// </summary>
        private delegate void ObjectMovePieceDelegate(int startIndex, int endIndex);
        /// <summary>
        /// Try and refresh the screen
        /// </summary>
        public void MovePieceDelegate(int startIndex, int endIndex) {
            // If it is not on the main thread
            if (InvokeRequired) {
                // we then create the delegate again
                // if you've made it global then you won't need to do this
                ObjectMovePieceDelegate method = MovePieceDelegate;
                // we then simply invoke it and return
                BeginInvoke(method, startIndex, endIndex);
                return;
            }
            // Move piece
            MovePiece(startIndex, endIndex);
            // Redraw the board
            Invalidate();
        }

        /// <summary>
        /// Move a piece on a local board
        /// </summary>
        /// <param name="startIndex">The index the piece moves from</param>
        /// <param name="endIndex">The index the piece moves to</param>
        private void MovePiece(int startIndex, int endIndex) {
            _board.LastMove = Tuple.Create(startIndex, endIndex);
            var x = (startIndex % 8) + 1;
            var y = (startIndex / 8) + 1;
            var piece = _board.GetPiece(x, y);
            // Add move to list box
            if (_moves.Items.Count == 0) _moves.Items.Add(ToDisplay(startIndex));
            else
                if (((string)_moves.Items[_moves.Items.Count - 1]).Length < 5)
                    _moves.Items[_moves.Items.Count - 1] += " → " + ToDisplay(startIndex);
                else
                    _moves.Items.Add(ToDisplay(startIndex));
            x = (endIndex % 8) + 1;
            y = (endIndex / 8) + 1;
            // Move the piece
            piece.Move(_board, x, y);
            // Add move to list box
            if (_moves.Items.Count == 0) _moves.Items.Add(ToDisplay(endIndex));
            else
                if (((string)_moves.Items[_moves.Items.Count - 1]).Length < 5)
                    _moves.Items[_moves.Items.Count - 1] += " → " + ToDisplay(endIndex);
                else
                    _moves.Items.Add(ToDisplay(endIndex));
            var visibleItems = _moves.ClientSize.Height / _moves.ItemHeight;
            _moves.TopIndex = Math.Max(_moves.Items.Count - visibleItems + 1, 0);
            _moves.Refresh();
            // Refresh all available moves for every piece
            foreach (var pieces in _board.Pieces)
                pieces.GetMoves(_board, true);
            // Redraw the board
            Invalidate();
        }

        /// <summary>
        /// Add a string to the moves list box
        /// </summary>
        /// <param name="s">The string to add</param>
        public void FakeMove(string s) {
            try {
                if (s == "") return;
                s = s.Replace('?', '→');
                do { } while (_moves == null);
                _moves.Items.Add(s);
                var visibleItems = _moves.ClientSize.Height / _moves.ItemHeight;
                _moves.TopIndex = Math.Max(_moves.Items.Count - visibleItems + 1, 0);
                _moves.Refresh();
            } catch (Exception ex) {
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Remove a the last move in the move list
        /// </summary>
        public void FakeRemove() {
            try {
                _moves.Items.RemoveAt(_moves.Items.Count - 1);
                var visibleItems = _moves.ClientSize.Height / _moves.ItemHeight;
                _moves.TopIndex = Math.Max(_moves.Items.Count - visibleItems + 1, 0);
                _moves.Refresh();
            } catch (Exception ex) {
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Delegate object to refresh the screen
        /// </summary>
        private delegate void ObjectRefreshDelegate();
        /// <summary>
        /// Try and refresh the screen
        /// </summary>
        public void RefreshDelegate() {
            // If it is not on the main thread
            if (InvokeRequired) {
                // we then create the delegate again
                // if you've made it global then you won't need to do this
                ObjectRefreshDelegate method = RefreshDelegate;
                // we then simply invoke it and return
                BeginInvoke(method);
                return;
            }
            // Refresh lists
            _moves.Refresh();
            Chat.Refresh();
            // Redraw the board
            Invalidate();
        }

        /// <summary>
        /// Delegate object to update the form title
        /// </summary>
        private delegate void ObjectUpdateTitleDelegate(string gameName, string player1, string player2);
        /// <summary>
        /// Try and update the form title
        /// </summary>
        public void UpdateTitleDelegate(string gameName, string player1, string player2) {
            // If it is not on the main thread
            if (InvokeRequired) {
                // we then create the delegate again
                // if you've made it global then you won't need to do this
                ObjectUpdateTitleDelegate method = UpdateTitleDelegate;
                // we then simply invoke it and return
                BeginInvoke(method, gameName, player1, player2);
                return;
            }
            Text = "";
            Text += _playing ? "Playing " : "Watching ";
            Text += gameName + " played by ";
            Text += _playerOne ? "you and " + player2 :
                _playing ? player1 + " and you" : player1 + " and " + player2;
        }

        /// <summary>
        /// Move a piece and swap player turn
        /// </summary>
        /// <param name="startIndex">The index the piece moves from</param>
        /// <param name="endIndex">The index the piece moves to</param>
        /// <param name="turn">If it is player ones turn</param>
        public void MovePiece(int startIndex, int endIndex, bool turn) {
            MovePiece(startIndex, endIndex);
            _board.PlayerOneTurn = !turn;
        }

        /// <summary>
        /// Remove a piece from the board without it being taken
        /// </summary>
        /// <param name="pieceIndex">What square the piece is in</param>
        public void SpecialRemove(int pieceIndex) {
            var x = (pieceIndex % 8) + 1;
            var y = (pieceIndex / 8) + 1;
            _board.TakenPieces.Add(_board.GetPiece(x, y));
            _board.Pieces.Remove(_board.GetPiece(x, y));

            foreach (var pieces in _board.Pieces)
                pieces.GetMoves(_board, true);

            // Redraw the board
            Invalidate();
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
            if (owner.Equals("NULL")) ownerColour = (y == 1 ? _board.WhiteColour : _board.BlackColour);
            else if (owner.Equals("WHITE")) ownerColour = _board.WhiteColour;
            else if (owner.Equals("BLACK")) ownerColour = _board.BlackColour;
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
            _board.Pieces.Remove(_board.GetPiece(x, y));
            // Add the new piece
            _board.Pieces.Add(piece);

            foreach (var pieces in _board.Pieces)
                pieces.GetMoves(_board, true);

            // Redraw the board
            Invalidate();
        }

        /// <summary>
        /// Convert the usable 1D value into a readable format
        /// </summary>
        /// <param name="index">Value to convert</param>
        /// <returns>STRING: Letter Number pair</returns>
        public static string ToDisplay(int index) {
            return Encoding.ASCII.GetString(
                new[] { Convert.ToByte(((index % 8) + 1) + 64, new NumberFormatInfo()) }
                ) + (8 - (index / 8));
        }
    }
}