using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ChessClient.Game;
using ChessClient.Net;
using ChessClient.Properties;

namespace ChessClient.Forms {
    /// <summary>
    /// Form to display the games list and other server info
    /// </summary>
    public sealed class ServerDisplayForm : Form {

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
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }
        #endregion

        private Client _client;
        /// <summary>
        /// List of game from the server
        /// </summary>
        public readonly BindingList<string> OpenGamesList = new BindingList<string>();
        /// <summary>
        /// List of the colours for the game list
        /// </summary>
        public readonly List<Tuple<string, Color, Color>> Colours = new List<Tuple<string, Color, Color>>();

        private readonly Button
            _startAGame = new Button { Left = 454, Top = 39, Width = 120, Height = 89, Text = "Start A \n New Game", FlatStyle = FlatStyle.Flat },
            _watchGame = new Button { Left = 454, Top = 127, Width = 120, Height = 89, Text = "Watch Game", FlatStyle = FlatStyle.Flat },
            _joinGame = new Button { Left = 454, Top = 216, Width = 120, Height = 89, Text = "Join Game", FlatStyle = FlatStyle.Flat };

        /// <summary>
        /// Display the currently connect players
        /// </summary>
        public readonly ListBox
            ConnectedPlayers = new ListBox { Left = 10, Top = 40, Width = 110, Height = 270, SelectionMode = SelectionMode.None, TabStop = false };
        private readonly ListBox
            _openGames = new ListBox { Left = 120, Top = 40, Width = 330, Height = 270, DrawMode = DrawMode.OwnerDrawFixed };

        private readonly Label
            _username = new Label { Left = 10, Top = 10, Width = 110, Height = 30, TabStop = false },
            _searchLabel = new Label { Left = 120, Top = 10, Width = 45, Height = 30, Text = "Search", TabStop = false };

        private readonly TextBox
            _search = new TextBox { Left = 165, Top = 10, Width = 285, Height = 20, Text = "" };

        private readonly ComboBox
            _sort = new ComboBox { Left = 454, Top = 10, Width = 120, Height = 20, DropDownStyle = ComboBoxStyle.DropDownList };

        /// <summary>
        /// Create a new ServerDisplayForm
        /// </summary>
        /// <param name="client">The player's client</param>
        public ServerDisplayForm(Client client) {
            components = new Container();
            AutoScaleMode = AutoScaleMode.Font;

            Text = "Server Info";
            Icon = Resources.WifiIcon;
            CheckForIllegalCrossThreadCalls = false;

            _client = client;
            _client.CurrentForm = this;

            _username.Text = client.ClientName;

            _openGames.DataSource = OpenGamesList;

            // Searching updates the list of games but keeps the original
            _search.TextChanged += (sender, e) => {
                if (_search.TextLength == 0)
                    // Set the list box to a new list of the sorted items
                    _openGames.DataSource = new BindingList<string>(SortGames(OpenGamesList.ToList()));
                else {
                    var searchGames = new List<string>();
                    // Copy from master list all items that contain the search term
                    foreach (var games in OpenGamesList)
                        try {
                            if (games.Contains(_search.Text))
                                searchGames.Add(games);
                        } catch (Exception ex) {
                            Console.WriteLine(ex.StackTrace);
                        }
                    // If no games match the search term
                    if (searchGames.Count == 0) searchGames.Add("No Games with that name");
                    // Sort the list
                    _openGames.DataSource = SortGames(searchGames);
                }
            };

            _search.KeyDown += (sender, e) => {
                if (!e.Control || e.KeyCode != Keys.Back) return;
                e.Handled = true;
                e.SuppressKeyPress = true;
                _search.Text = "";
            };

            // Custom drawing of the game list (for gradient)
            _openGames.DrawItem += (sender, e) => {
                // Draw the default list box item
                e.DrawBackground();
                var g = e.Graphics;

                if (_openGames.Items.Count == 0) {
                    _openGames.SelectedIndex = -1;
                    return;
                }

                var a = Tuple.Create("No Games with that name ", Color.Black, Color.Black);

                try {
                    foreach (var item in Colours.Where(item => item.Item1.Equals(_openGames.Items[e.Index].ToString())))
                        a = item;
                } catch (Exception ex) {
                    Console.WriteLine(ex.StackTrace);
                }

                // Draw the gradient background
                g.FillRectangle(new LinearGradientBrush(e.Bounds, a.Item2, a.Item3, LinearGradientMode.Horizontal), e.Bounds);
                // Draw the game display string
                g.DrawString(a.Item1, e.Font, new SolidBrush(e.ForeColor), new PointF(e.Bounds.X, e.Bounds.Y));
            };

            // Double Click to select a game
            _openGames.DoubleClick += (sender, e) => {
                var gameName = _openGames.SelectedItem.ToString();
                var a = gameName.Split('-');
                gameName = a[0].Substring(0, a[0].Length - 1);

                if (gameName.Equals("No Games with that name"))
                    MessageBox.Show("No Game Selected");
                // Try and join a game
                else if (!JoinGame())
                    // Else spectate
                    if (!WatchGame())
                        MessageBox.Show("Can't Watch or join game, Try again");
            };

            Controls.Add(_startAGame);
            // Open up Game Creator, to create a game
            _startAGame.Click += (sender, e) => {
                Hide();
                var creator = new GameCreatorForm(client);
                creator.ShowDialog(this);
                _client = creator.Client;

                _client.CurrentForm = this;

                RefreshLists();
                Show();
            };
            _watchGame.Click += (sender, e) => { WatchGame(); };
            _joinGame.Click += (sender, e) => { JoinGame(); };

            // Add sort options
            _sort.Items.AddRange(new object[] { 
                "Date added (Old->New)", "Date added (New->Old)",
                "Name (A-Z)", "Name (Z-A)"
            });
            _sort.SelectedIndex = 0;
            _sort.SelectedIndexChanged += (sender, e) => { ResetListView(); };

            Controls.Add(_watchGame);
            Controls.Add(_joinGame);
            Controls.Add(_openGames);
            Controls.Add(ConnectedPlayers);
            Controls.Add(_username);
            Controls.Add(_searchLabel);
            Controls.Add(_search);
            Controls.Add(_sort);

            // Safely close everything when form closed
            FormClosing += (sender, e) => {
                _client.IsRunning = false;
                _client.DisposeSocket();
            };

            var s = new Size(600, 350);
            Size = s;
            MinimumSize = s;
            MaximumSize = s;

            CenterToScreen();

            // Get player list and game list
            RefreshLists();
        }

        /// <summary>
        /// Refresh the list of games
        /// </summary>
        private void ResetListView() {
            var temp = _search.Text;
            _search.Text = " ";
            _search.Text = temp;
        }

        /// <summary>
        /// Delegate object to reset the list view
        /// </summary>
        private delegate void ObjectResetDelegate();
        /// <summary>
        /// Try and reset the list view
        /// </summary>
        public void ResetDelegate() {
            // If it is not on the main thread
            if (InvokeRequired) {
                // we then create the delegate again
                // if you've made it global then you won't need to do this
                ObjectResetDelegate method = ResetDelegate;
                // we then simply invoke it and return
                BeginInvoke(method);
                return;
            }
            // Reset list view
            ResetListView();
        }

        /// <summary>
        /// Sort the list of games
        /// </summary>
        /// <param name="list">The list of game display names to sort</param>
        /// <returns>LIST[STRING]: A list of sorted strings</returns>
        private List<string> SortGames(List<string> list) {
            var array = list.ToArray();
            switch (_sort.SelectedIndex) {
                // Sort based on Date added, which is how it is added
                case 0:
                    break;
                // Sort based on the opposite to default
                case 1:
                    list.Reverse();
                    break;
                // Sort based on name
                case 2:
                    Quicksort(array, 0, array.Length - 1);
                    return new List<string>(array);
                // Sort based on the opposite to name
                case 3:
                    Quicksort(array, 0, array.Length - 1);
                    return new List<string>(array.Reverse());
            }
            return list;
        }

        /// <summary>
        /// Quicksort
        /// </summary>
        /// <param name="elements">List of strings</param>
        /// <param name="left">Where to start the left pivot</param>
        /// <param name="right">Where to start the right pivot</param>
        private static void Quicksort(IList<string> elements, int left, int right) {
            while (true) {
                if (elements.Count == 0) return;
                var i = left;
                var j = right;
                var pivot = elements[(left + right) / 2];

                while (i <= j) {
                    while (String.Compare(elements[i], pivot, StringComparison.Ordinal) < 0)
                        i++;
                    while (String.Compare(elements[j], pivot, StringComparison.Ordinal) > 0)
                        j--;

                    if (i > j) continue;
                    var tmp = elements[i];
                    elements[i] = elements[j];
                    elements[j] = tmp;
                    i++;
                    j--;
                }
                if (left < j)
                    Quicksort(elements, left, j);
                // No recursive tail to try and minimize chances of stack overflow (unlikely)
                if (i < right) {
                    left = i;
                    continue;
                }
                break;
            }
        }

        /// <summary>
        /// Try and watch a game
        /// </summary>
        /// <returns>BOOL: Whether the server accepted the request</returns>
        private bool WatchGame() {
            // Get the name of the game
            var gameName = _openGames.SelectedItem.ToString();
            var a = gameName.Split('-');
            gameName = a[0].Substring(0, a[0].Length - 1);

            if (gameName.Equals("No Games with that name")) {
                MessageBox.Show("No Game Selected");
                return false;
            }
            // Send a request packet to the server
            var ret = _client.SendMessage(Packet.Packets.Game, "WATCH", gameName);
            if (ret.Item1.Code == Packet.Packets.Error) {
                MessageBox.Show("Unable to watch game, Try watching instead");
                return false;
            }
            // Send a request packet to get the board object from the server
            var b = _client.SendMessage(Packet.Packets.Game, "GET", gameName);
            if (b.Item1.Code == Packet.Packets.Error) {
                MessageBox.Show("No Game Found");
                return false;
            }
            Hide();
            try {
                // Open a game with the returned board object and spectator mode
                using (var board = new BoardDisplayForm(_client, Board.UnSerialize(b.Item2), null, gameName)) {
                    board.ShowDialog();
                    _client = board.Client;
                }

                _client.CurrentForm = this;

                _client.SendMessage(Packet.Packets.DisconnectGame, gameName, _client.ClientName, "WATCHING");
            } catch (Exception ex) {
                MessageBox.Show(ex.StackTrace);
            }
            RefreshLists();
            Show();
            return true;
        }

        /// <summary>
        /// Try and join a game
        /// </summary>
        /// <returns>BOOL: Whether the server accepted the request</returns>
        private bool JoinGame() {
            // Get the name of the game
            var gameName = _openGames.SelectedItem.ToString();
            var temp = gameName.Split('-');
            gameName = temp[0].Substring(0, temp[0].Length - 1);

            if (gameName.Equals("No Games with that name")) {
                MessageBox.Show("No Game Selected");
                return false;
            }
            // Send a request packet to the server
            var accepted = _client.SendMessage(Packet.Packets.Game, "JOIN", gameName);
            if (accepted.Item1.Code == Packet.Packets.Error) {
                MessageBox.Show("Unable to join game, Try watching instead");
                return false;
            }
            // Send a request packet to get the board object from the server
            var b = _client.SendMessage(Packet.Packets.Game, "GET", gameName);
            if (b.Item1.Code == Packet.Packets.Error) {
                MessageBox.Show("No Game Found");
                return false;
            }
            Hide();
            try {
                // Open a game with the returned board object and the player number
                using (var board = new BoardDisplayForm(_client, Board.UnSerialize(b.Item2), accepted.Item1.Message[0].Equals("PlayerOne"), gameName)) {
                    board.ShowDialog();
                    _client = board.Client;
                }

                _client.CurrentForm = this;

                _client.SendMessage(Packet.Packets.DisconnectGame, gameName, _client.ClientName, "PLAYING");
            } catch (Exception ex) {
                MessageBox.Show(ex.StackTrace);
                return false;
            }
            RefreshLists();
            Show();
            return true;
        }

        /// <summary>
        /// Update the players and games list boxes
        /// </summary>
        private void RefreshLists() {
            // Clear the lists
            ConnectedPlayers.Items.Clear();
            OpenGamesList.Clear();
            Colours.Clear();

            // Get the list of players from the server
            var recieveMessage = _client.SendMessage(Packet.Packets.Info, "Players");
            if (recieveMessage.Item1.Message.Count != 0 && recieveMessage.Item1.Message[0].Equals("null")) {
                // Didn't get any players, tell player to refresh
                ConnectedPlayers.Items.Add("Player list didn't load");
                ConnectedPlayers.Items.Add("Please refresh.");
            } else
                // Add all Players to the list
                foreach (var name in recieveMessage.Item1.Message)
                    ConnectedPlayers.Items.Add(name);
            // Get the list of games from the server
            recieveMessage = _client.SendMessage(Packet.Packets.Info, "Games");
            if (recieveMessage.Item1.Message.Count != 0 && recieveMessage.Item1.Message[0].Equals("null")) {
                // Didn't get any games, tell player to refresh
                OpenGamesList.Add("The List of games wasn't received properly, please refresh.");
                Colours.Add(Tuple.Create("The List of games wasn't received properly, please refresh.", Color.Orange, Color.Orange));
            } else
                // Add all games to the list, and corresponding colours
                foreach (var gameInfo in recieveMessage.Item1.Message.Select(game => game.Split(','))) {
                    if (gameInfo.Length >= 3)
                        OpenGamesList.Add(gameInfo[0] + " - " + "[" + gameInfo[1] + "/" + gameInfo[2] + "]");
                    if (gameInfo.Length == 5)
                        Colours.Add(Tuple.Create(gameInfo[0] + " - " + "[" + gameInfo[1] + "/" + gameInfo[2] + "]", ColorTranslator.FromHtml("#" + gameInfo[3]), ColorTranslator.FromHtml("#" + gameInfo[4])));
                }

            // Update the screen
            ConnectedPlayers.Refresh();
            _openGames.Refresh();
            ResetListView();
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
            RefreshLists();
        }
    }
}