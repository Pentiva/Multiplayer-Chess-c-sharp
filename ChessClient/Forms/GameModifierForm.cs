using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ChessClient.Forms {
    /// <summary>
    /// Form to create a new game rule or starting position
    /// </summary>
    public sealed class GameModifierForm : Form {

        private readonly Tuple<Rectangle, bool>[] _rectangles = new Tuple<Rectangle, bool>[64];
        private readonly List<Tuple<int, Color>> _hightlightSquares = new List<Tuple<int, Color>>();
        private int _highlightCoord = 64;

        #region DesignerCode
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private readonly System.ComponentModel.IContainer components;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion

        /// <summary>
        /// Create a new GameModifierForm
        /// </summary>
        public GameModifierForm() {
            components = new System.ComponentModel.Container();
            Text = "GameModifierForm";
            Size = new Size(800, 800);

            for (var i = 0; i < 64; i++)
                _rectangles[i] = Tuple.Create(new Rectangle(), false);

            Paint += OnPaint;
            Resize += OnResize;
            MouseClick += OnMouseClick;
            DoubleBuffered = true;
            MouseMove += (sender, e) => {
                _highlightCoord = 65;
                for (var i = 0; i < 64; i++)
                    if (new Rectangle(e.X, e.Y, 1, 1).IntersectsWith(_rectangles[i].Item1))
                        _highlightCoord = i;
                // Redraw the board
                Invalidate();
            };
            OnResize(null, null);
        }

        private void OnMouseClick(object sender, MouseEventArgs e) {
            for (var i = 0; i < 64; i++)
                // Check what square is clicked
                if (new Rectangle(e.X, e.Y, 1, 1).IntersectsWith(_rectangles[i].Item1)) {
                    // Ignore not left clicks
                    if (e.Button != MouseButtons.Left) continue;
                    _hightlightSquares.Add(Tuple.Create(i, Color.Purple));
                }
        }

        private void OnResize(object sender, EventArgs e) {
            var widthScale = Width / 10;
            var heightScale = Height / 10;
            // Resizes the squares based on the form size
            for (var i = 0; i < 8; i++)
                for (var j = 0; j < 8; j++)
                    _rectangles[i + (j * 8)] = Tuple.Create(new Rectangle(i * widthScale + 10, j * heightScale + 10, widthScale - 2, heightScale - 2), (i % 2 == 0 ^ j % 2 == 1));
        }

        private void OnPaint(object sender, PaintEventArgs e) {
            var graphics = e.Graphics;
            graphics.Clear(Color.White);
            // Fill in Black squares
            graphics.FillRectangles(new SolidBrush(Color.White), _rectangles.Where(rect => rect.Item2).Select(rect => rect.Item1).ToArray());
            graphics.FillRectangles(new SolidBrush(Color.Black), _rectangles.Where(rect => !rect.Item2).Select(rect => rect.Item1).ToArray());

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
                        ? new SolidBrush(Color.Gray)
                        : new SolidBrush(Color.DarkGray), _rectangles[_highlightCoord].Item1);

            // Outline every square
            graphics.DrawRectangles(new Pen(Color.White), _rectangles.Select(rect => rect.Item1).ToArray());
        }
    }
}
