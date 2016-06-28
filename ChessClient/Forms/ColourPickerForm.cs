using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace ChessClient.Forms {
    /// <summary>
    /// Form to pick a colour scheme for a game
    /// </summary>
    public sealed class ColourPickerForm : Form {

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

        readonly Color[] _cols = ColorStructToList().ToArray();
        /// <summary>
        /// The colour selected for Black
        /// </summary>
        public Color SelectedCol1 { get; private set; }
        /// <summary>
        /// The colour selected for White
        /// </summary>
        public Color SelectedCol2 { get; private set; }

        private readonly TrackBar
            _r = new TrackBar { Left = 10, Top = 20, Size = new Size(100, 10), TickStyle = TickStyle.None },
            _g = new TrackBar { Left = 10, Top = 80, Size = new Size(100, 10), TickStyle = TickStyle.None },
            _b = new TrackBar { Left = 10, Top = 140, Size = new Size(100, 10), TickStyle = TickStyle.None };

        private readonly TextBox
            _rVal = new TextBox { Left = 120, Top = 20, Size = new Size(30, 10), Text = "255" },
            _gVal = new TextBox { Left = 120, Top = 80, Size = new Size(30, 10), Text = "255" },
            _bVal = new TextBox { Left = 120, Top = 140, Size = new Size(30, 10), Text = "255" };

        private readonly RadioButton
            _col1 = new RadioButton { Left = 160, Top = 120, Text = "White Colour", Checked = true },
            _col2 = new RadioButton { Left = 160, Top = 140, Text = "Black Colour" };

        private readonly Button
            _confirm = new Button { Width = 50, Left = (300 / 2) - 70, Top = 220, Height = 25, Text = "Confirm" },
            _reset = new Button { Width = 50, Left = (300 / 2) + 20, Top = 220, Height = 25, Text = "Reset" };

        private readonly Label
            _colourName = new Label { Left = 10, Top = 200, Text = "White/Black" };

        private bool
            _manualChange;

        /// <summary>
        /// Create a new ColourPickerForm
        /// </summary>
        public ColourPickerForm() {
            components = new Container();
            AutoScaleMode = AutoScaleMode.Font;
            Paint += PaintColours;
            FormClosing += (sender, e) => { Dispose(); };
            DoubleBuffered = true;
            MaximumSize = new Size(300, 300);
            MinimumSize = new Size(300, 300);
            CenterToScreen();

            SelectedCol1 = Color.White;
            SelectedCol2 = Color.Black;

            _r.SetRange(0, 255);
            _g.SetRange(0, 255);
            _b.SetRange(0, 255);

            _r.Value = 255;
            _g.Value = 255;
            _b.Value = 255;

            FormBorderStyle = FormBorderStyle.None;

            // Add all controls to the form
            Controls.Add(_confirm);
            Controls.Add(_reset);
            Controls.Add(_col1);
            Controls.Add(_col2);
            Controls.Add(_r);
            Controls.Add(_rVal);
            Controls.Add(_g);
            Controls.Add(_gVal);
            Controls.Add(_b);
            Controls.Add(_bVal);
            Controls.Add(_colourName);

            // Assign Events

            _confirm.Click += (sender, e) => {
                Hide();
                Close();
            };

            _reset.Click += (sender, e) => {
                _col1.Checked = true;
                _r.Value = 255;
                _g.Value = 255;
                _b.Value = 255;
                SelectedCol2 = Color.White;
            };

            _col1.Click += (sender, e) => {
                _manualChange = true;
                _r.Value = SelectedCol1.R;
                _g.Value = SelectedCol1.G;
                _b.Value = SelectedCol1.B;
                _manualChange = false;
            };
            _col2.Click += (sender, e) => {
                _manualChange = true;
                _r.Value = SelectedCol2.R;
                _g.Value = SelectedCol2.G;
                _b.Value = SelectedCol2.B;
                _manualChange = false;
            };

            _r.ValueChanged += SliderChanged;
            _rVal.KeyPress += KeyCheck;
            _rVal.TextChanged += TextCheck;

            _g.ValueChanged += SliderChanged;
            _gVal.KeyPress += KeyCheck;
            _gVal.TextChanged += TextCheck;

            _b.ValueChanged += SliderChanged;
            _bVal.KeyPress += KeyCheck;
            _bVal.TextChanged += TextCheck;

            Refresh();
            new Thread(() => {
                while (!IsDisposed)
                    Invalidate();
            }).Start();
        }

        /// <summary>
        /// Checks if you pressed and number key
        /// </summary>
        /// <param name="sender">The control that fired the event</param>
        /// <param name="e">Additional info from the event</param>
        private static void KeyCheck(object sender, KeyPressEventArgs e) {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        /// <summary>
        /// Force 255 limit and update the slider
        /// </summary>
        /// <param name="sender">The control that fired the event</param>
        /// <param name="e">Additional info from the event</param>
        private void TextCheck(object sender, EventArgs e) {
            var tb = sender as TextBox;
            if (tb != null && tb.Text.Length != 0) {
                if (int.Parse(tb.Text, new NumberFormatInfo()) > 255)
                    tb.Text = "255";
                if (tb.Text.Length > 3) {
                    tb.Text = tb.Text.Remove(3);
                    tb.SelectionStart = 3;
                }
            }
            _r.Value = _rVal.Text.Length > 0 ? int.Parse(_rVal.Text, new NumberFormatInfo()) : 0;
            _g.Value = _gVal.Text.Length > 0 ? int.Parse(_gVal.Text, new NumberFormatInfo()) : 0;
            _b.Value = _bVal.Text.Length > 0 ? int.Parse(_bVal.Text, new NumberFormatInfo()) : 0;
        }

        /// <summary>
        /// Update the textbox when the slider moves
        /// </summary>
        /// <param name="sender">The control that fired the event</param>
        /// <param name="e">Additional info from the event</param>
        private void SliderChanged(object sender, EventArgs e) {
            if (!_manualChange) {
                if (_col1.Checked) SelectedCol1 = Color.FromArgb(_r.Value, _g.Value, _b.Value);
                else SelectedCol2 = Color.FromArgb(_r.Value, _g.Value, _b.Value);
            }
            _rVal.Text = _r.Value.ToString();
            _gVal.Text = _g.Value.ToString();
            _bVal.Text = _b.Value.ToString();
            _rVal.Refresh();
            _gVal.Refresh();
            _bVal.Refresh();
        }

        /// <summary>
        /// Uses reflection to get every Colour in System.Drawing.Color
        /// </summary>
        /// <returns>LIST[COLOR]: Every predefined colour</returns>
        private static List<Color> ColorStructToList() {
            return typeof(Color).GetProperties(BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public)
                                .Select(c => (Color)c.GetValue(null, null)).ToList();
        }

        private readonly Random _random = new Random();
        /// <summary>
        /// Draw the selected colours as a gradient in a box
        /// </summary>
        /// <param name="sender">The control that fired the event</param>
        /// <param name="e">Additional info from the event</param>
        private void PaintColours(object sender, PaintEventArgs e) {
            var g = e.Graphics;
            var brush = new LinearGradientBrush(new Rectangle(160, 20, 80, 80), SelectedCol1, SelectedCol2, 45f);
            g.FillRectangle(brush, 160, 20, 80, 80);

            var col1Name = SelectedCol1.Name;
            var col2Name = SelectedCol2.Name;
            foreach (var item in _cols) {
                if (item.R == SelectedCol1.R && item.G == SelectedCol1.G && item.B == SelectedCol1.B)
                    col1Name = item.Name;
                if (item.R == SelectedCol2.R && item.G == SelectedCol2.G && item.B == SelectedCol2.B)
                    col2Name = item.Name;
            }
            _colourName.Text = col1Name + "/" + col2Name;

            // Fixes controls not showing, while also not flickering distractingly
            if (_random.Next(0, 100) == 7)
                Refresh();
        }
    }
}