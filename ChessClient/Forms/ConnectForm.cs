using System;
using System.ComponentModel;
using System.Windows.Forms;
using ChessClient.Net;
using System.Drawing;

namespace ChessClient.Forms {
    /// <summary>
    /// Form to enter details to connect to the server
    /// </summary>
    public sealed class ConnectForm : Form {

        private bool _isMouseDown;
        private Point _mouseLocation;

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
        /// Create a new ConnectForm
        /// </summary>
        public ConnectForm() {
            System.Threading.Thread.Sleep(1000);

            components = new Container();
            AutoScaleMode = AutoScaleMode.Font;

            Text = "Connect";

            // Set up form
            FormBorderStyle = FormBorderStyle.None;
            Width = 320;
            Height = 180;
            CenterToScreen();

            MouseDown += (sender, args) => { _isMouseDown = true; _mouseLocation = args.Location; };
            MouseUp += (sender, args) => { _isMouseDown = false; };

            MouseMove += (sender, args) => {
                if (!_isMouseDown) return;
                var p = PointToScreen(args.Location);
                Location = new Point(p.X - _mouseLocation.X, p.Y - _mouseLocation.Y);
            };

            // Add fields
            var username = new TextBox { Width = 160, Height = 222, Text = Properties.Settings.Default.Username, Top = 40, TabIndex = 0 };
            var ipField = new IpField { Width = 160, Height = 222, Top = 80, TabIndex = 1 };
            var connectButton = new Button {
                Width = 80, Height = username.Height, Text = "Connect", Top = 120, TabIndex = 2,
                TabStop = false, FlatStyle = FlatStyle.Flat
            };

            if (username.Text.Equals("Name") || username.Text.Equals(""))
                username.Text = Environment.UserName;

            // Press the Connect Button when the enter key is pressed in the username input
            username.KeyPress += (sender, e) => {
                if (e.KeyChar == Convert.ToChar(Keys.Enter))
                    connectButton.PerformClick();
            };
            username.KeyDown += (sender, args) => {
                if (args.KeyCode == Keys.Escape) Application.Exit();
                if (args.Control && args.KeyCode == Keys.Back) {
                    args.SuppressKeyPress = true;
                    ((TextBox)sender).Clear();
                }
                if (!args.Control || args.KeyCode != Keys.I) return;
                Hide();
                new GameModifierForm().ShowDialog();
            };
            username.Left = (Width / 2) - (username.Width / 2);

            ipField.Left = (Width / 2) - (ipField.Width / 2);
            // Press the Connect Button when the enter key is pressed in the IP input
            ipField.OnComplete = () => connectButton.PerformClick();
            // Try and find a server on the local network
            ipField.SetIp(Client.GetServer().GetAddressBytes());

            connectButton.Left = (Width / 2) - (connectButton.Width / 2);
            // Connect to the server when the Connect button is pressed, and if successful open server display form
            connectButton.Click += (sender, e) => {
                var ip = ipField.IpAddress;

                var c = new Client(ip, username.Text, Properties.Settings.Default.PacketNo);
                var accepted = c.SendMessage(Packet.Packets.Connect, c.ClientName);
                if (accepted.Item1.Code == Packet.Packets.Error) {
                    if (accepted.Item2[0] == 1 && accepted.Item2[1] == 2 && accepted.Item2[2] == 3 &&
                        accepted.Item2[3] == 4 && accepted.Item2[4] == 5 && accepted.Item2[5] == 6)
                        MessageBox.Show("Can't connect to the server");
                    else
                        MessageBox.Show("Name already taken");
                } else {
                    Hide();
                    new ServerDisplayForm(c) { Text = "IP: " + ipField.Ip }.ShowDialog(this);
                    Properties.Settings.Default.Username = username.Text;
                    Properties.Settings.Default.PacketNo = c.GlobalPacketId;
                    Properties.Settings.Default.Save();
                    Close();
                }
            };
            connectButton.FlatAppearance.BorderSize = 0;

            Controls.Add(ipField);
            Controls.Add(username);
            Controls.Add(connectButton);
        }
    }
}