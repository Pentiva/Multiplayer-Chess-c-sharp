using System.ComponentModel;
using System.Windows.Forms;
using ChessClient.Game;

namespace ChessClient.Forms {
    /// <summary>
    /// Menu for choosing outcome of pawn promotion
    /// </summary>
    public class PromotionMenuForm : Form {

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
        /// Stores the Piece chosen
        /// </summary>
        public Pieces Chosen { get; private set; }

        /// <summary>
        /// Creates a new PromotionMenuForm
        /// </summary>
        public PromotionMenuForm() {
            components = new Container();
            AutoScaleMode = AutoScaleMode.Font;

            Chosen = Pieces.Queen;

            FormBorderStyle = FormBorderStyle.None;
            Width = 60;
            Height = 120;
            CenterToScreen();

            // Add radio buttons
            var queen = new RadioButton { Left = 10, Top = 10, Width = 70, Text = "Queen", Checked = true };
            queen.CheckedChanged += (sender, e) => {
                if (queen.Checked)
                    Chosen = Pieces.Queen;
            };
            Controls.Add(queen);
            var rook = new RadioButton { Left = 10, Top = 30, Width = 70, Text = "Rook" };
            rook.CheckedChanged += (sender, e) => {
                if (rook.Checked)
                    Chosen = Pieces.Rook;
            };
            Controls.Add(rook);
            var bishop = new RadioButton { Left = 10, Top = 50, Width = 70, Text = "Bishop" };
            bishop.CheckedChanged += (sender, e) => {
                if (bishop.Checked)
                    Chosen = Pieces.Bishop;
            };
            Controls.Add(bishop);
            var knight = new RadioButton { Left = 10, Top = 70, Width = 70, Text = "Knight" };
            knight.CheckedChanged += (sender, e) => {
                if (knight.Checked)
                    Chosen = Pieces.Knight;
            };
            Controls.Add(knight);

            var confirm = new Button { Text = "C\nO\nN\nF\nI\nR\nM", Left = 80, Top = 5, Width = 20, Height = 110 };
            confirm.Click += (sender, e) => {
                Close();
            };
            Controls.Add(confirm);
        }
    }
}