using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ChessClient.Net;
using ChessClient.Properties;

namespace ChessClient.Forms {
    /// <summary>
    /// Form to create a new game
    /// </summary>
    public sealed class GameCreatorForm : Form {

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

        /// <summary>
        /// The client to send/receive packets to/from the server
        /// </summary>
        public readonly Client Client;

        private readonly Label
            _colourLabel = new Label { Left = 6, Top = 10, Width = 80, Height = 20, Text = "Game Colour:" },
            _gameTypeLabel = new Label { Left = 6, Top = 70, Width = 80, Height = 20, Text = "Game Type:" },
            _nameLabel = new Label { Left = 6, Top = 130, Width = 80, Height = 20, Text = "Game Name:" };

        private readonly RadioButton
            _defaultColours = new RadioButton { Left = 10, Top = 30, Width = 70, Text = "Default", Checked = true, TabIndex = 0 },
            _randomColours = new RadioButton { Left = 80, Top = 30, Width = 70, Text = "Random", TabIndex = 1 },
            _customColours = new RadioButton { Left = 150, Top = 30, Width = 70, Text = "Custom", TabIndex = 2 };

        private readonly ComboBox
            _gameType = new ComboBox { Left = 10, Top = 90, Width = 100, Height = 20, DropDownStyle = ComboBoxStyle.DropDownList, TabIndex = 3 };

        private readonly Button
            _confirm = new Button { Left = 10, Top = 210, Width = 70, Height = 40, Text = "Confirm", TabIndex = 5 },
            _back = new Button { Left = 150, Top = 210, Width = 70, Height = 40, Text = "Back", TabIndex = 6 };

        private readonly TextBox
            _gameName = new TextBox { Left = 10, Top = 150, Width = 100, Height = 40, Text = "Name", TabIndex = 4 };

        /// <summary>
        /// Create a new GameCreatorForm
        /// </summary>
        /// <param name="client">The client used to connect to the server</param>
        public GameCreatorForm(Client client) {
            components = new Container();
            AutoScaleMode = AutoScaleMode.Font;

            Client = client;
            Client.CurrentForm = this;

            Icon = Resources.BoardIcon;
            Text = "Game Creator";

            Width = 250;
            Height = 300;

            CenterToScreen();

            // Add game options
            _gameType.Items.Add("Normal");
            _gameType.Items.Add("Chess960");
            _gameType.Items.Add("Siege");
            _gameType.SelectedIndex = 0;

            _confirm.Click += ConfirmClick;

            _gameName.KeyPress += (sender, e) => {
                if (e.KeyChar == Convert.ToChar(Keys.Enter))
                    _confirm.PerformClick();
            };

            _back.Click += (sender, e) => {
                Close();
            };

            Controls.Add(_colourLabel);
            Controls.Add(_gameTypeLabel);
            Controls.Add(_nameLabel);
            Controls.Add(_customColours);
            Controls.Add(_randomColours);
            Controls.Add(_defaultColours);
            Controls.Add(_gameType);
            Controls.Add(_confirm);
            Controls.Add(_back);
            Controls.Add(_gameName);
        }

        private void ConfirmClick(object sender, EventArgs e) {
            Hide();
            // Next Form
            if (_customColours.Checked) {
                // Open the colour picker form
                var a = new ColourPickerForm();
                a.ShowDialog();
                // Send the info to the server
                var ret = Client.SendMessage(Packet.Packets.Game, "ADD", _gameName.Text, _gameType.SelectedIndex.ToString(),
                    string.Format("{0:x6}", a.SelectedCol1.ToArgb()),
                    string.Format("{0:x6}", a.SelectedCol2.ToArgb()));
                if (ret.Item1.Code == Packet.Packets.Error) MessageBox.Show("Game with that name already exists");
            } else if (_randomColours.Checked) {
                // Randomly chose a colour
                var r = new Random();
                var col1 = Color.FromArgb(r.Next(0, 256), r.Next(0, 256), r.Next(0, 256));
                var col2 = Color.FromArgb(255 - col1.R, 255 - col1.G, 255 - col1.B);
                // Send the info to the server
                var ret = Client.SendMessage(Packet.Packets.Game, "ADD", _gameName.Text, _gameType.SelectedIndex.ToString(), col1.Name, col2.Name);
                if (ret.Item1.Code == Packet.Packets.Error) MessageBox.Show("Game with that name already exists");
            } else if (_defaultColours.Checked) {
                // Send the info to the server
                var ret = Client.SendMessage(Packet.Packets.Game, "ADD", _gameName.Text, _gameType.SelectedIndex.ToString(), "ffffffff", "ff000000");
                if (ret.Item1.Code == Packet.Packets.Error) MessageBox.Show("Game with that name already exists");
            }
            Close();
        }
    }
}