using System;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Windows.Forms;

namespace ChessClient {
    /// <summary>
    /// IPv4 input control
    /// </summary>
    public sealed partial class IpField : UserControl {
        /// <summary>
        /// The 4 input text boxes
        /// </summary>
        private readonly TextBox[] _textBoxes = new TextBox[4];
        /// <summary>
        /// The 3 labels to separate the text boxes
        /// </summary>
        private readonly Label[] _labels = new Label[3];
        /// <summary>
        /// The IP from the 4 text boxes as a string
        /// </summary>
        public string Ip { private set; get; }
        /// <summary>
        /// The IP from the 4 text boxes as an IPAdress
        /// </summary>
        public IPAddress IpAddress { private set; get; }
        /// <summary>
        /// What happens when pressing enter on the last textbox
        /// </summary>
        public Action OnComplete;

        /// <summary>
        /// Create a new IPField
        /// </summary>
        public IpField() {
            InitializeComponent();
            for (var i = 0; i < 4; i++) {
                #region "TextBox Setup"
                // Setup the text boxes
                _textBoxes[i] = new TextBox { TabIndex = i };
                // When a key is pressed make sure it is a number or enter
                _textBoxes[i].KeyPress += (sender, e) => {
                    // Ignore if it is not a digit
                    if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                        e.Handled = true;
                    var tb = sender as TextBox;
                    // If enter is pressed and is the last text box, perform the supplied event
                    if (tb != null && ((int)tb.Tag == 3 && e.KeyChar == Convert.ToChar(Keys.Enter)))
                        OnComplete.Invoke();
                };
                // Make sure that the number is 255 max
                _textBoxes[i].TextChanged += (sender, e) => {
                    var tb = sender as TextBox;
                    if (tb != null && tb.Text.Length != 0) {
                        // Focus the next box when a textbox is complete
                        if ((int)tb.Tag < 3)
                            if (tb.Text.Length > 2)
                                _textBoxes[(int)tb.Tag + 1].Focus();
                        if (int.Parse(tb.Text, new NumberFormatInfo()) > 255)
                            tb.Text = "255";
                        if (tb.Text.Length > 3) {
                            tb.Text = tb.Text.Remove(3);
                            tb.SelectionStart = 3;
                        }
                    }
                    // Update the string
                    UpdateIp();
                };
                _textBoxes[i].Tag = i;
                _textBoxes[i].TextAlign = HorizontalAlignment.Center;
                Controls.Add(_textBoxes[i]);
                #endregion
                #region "Label Setup"
                if (i == 3) continue;
                // Add all labels
                _labels[i] = new Label {
                    Text = ".",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(Font.FontFamily, 8, FontStyle.Bold)
                };
                Controls.Add(_labels[i]);

                #endregion
            }
            // Set a default ip
            DefaultIp();

            SizeChanged += IPField_SizeChanged;
        }

        /// <summary>
        /// Set the default IP
        /// </summary>
        private void DefaultIp() {
            _textBoxes[0].Text = "127";
            _textBoxes[1].Text = "0";
            _textBoxes[2].Text = "0";
            _textBoxes[3].Text = "1";
        }

        /// <summary>
        /// Set the ip in the control
        /// </summary>
        /// <param name="bytes">The ip address to set</param>
        public void SetIp(byte[] bytes) {
            for (var i = 0; i < 4; i++)
                _textBoxes[i].Text = bytes[i].ToString();
        }

        /// <summary>
        /// Convert the text boxes to an actual IP
        /// </summary>
        private void UpdateIp() {
            var newIp = "";
            var address = new byte[4];
            for (var i = 0; i < 4; i++) {
                newIp += _textBoxes[i].Text + ".";
                if (_textBoxes[i].Text.Length != 0)
                    address[i] = Convert.ToByte(_textBoxes[i].Text, new NumberFormatInfo());
            }
            Ip = newIp.TrimEnd('.');
            IpAddress = new IPAddress(address);
        }

        /// <summary>
        /// Change the textbox and label sizes when resized
        /// </summary>
        /// <param name="sender">The control that fired the event</param>
        /// <param name="e">Additional info from the event</param>
        private void IPField_SizeChanged(object sender, EventArgs e) {
            using (var tb = new TextBox { Width = 20, Height = 124124 })
                if (Height > tb.Height) Height = tb.Height;
            for (var i = 0; i < 4; i++) {
                _textBoxes[i].Width = (Width / 4) - (Width / 20);
                _textBoxes[i].Height = Height;
                _textBoxes[i].Left = (i * (_textBoxes[i].Width + (Width / 20)));
                _textBoxes[i].Top = 0;
                if (i == 3) continue;
                _labels[i].Width = Width / 20;
                _labels[i].Height = (int)(Height / 1.5);
                _labels[i].Left = _textBoxes[i].Left + _textBoxes[i].Width + (_labels[i].Width / 4) - 2;
                _labels[i].Top = 0;
            }
        }
    }
}