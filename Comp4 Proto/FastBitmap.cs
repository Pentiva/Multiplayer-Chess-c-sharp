using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace ChessClient {
    /// <summary>
    /// A improvement on the Bitmap class to perform pixel editing faster
    /// </summary>
    public class FastBitmap {
        /// <summary>
        /// Create a new FastBitmap
        /// </summary>
        /// <param name="bitmap">The bitmap.</param>
        public FastBitmap(Bitmap bitmap) {
            InternalBitmap = bitmap;
            Height = InternalBitmap.Height;
            Width = InternalBitmap.Width;
        }

        /// <summary>
        /// The height of the bitmap
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// The original bitmap that was superseded
        /// </summary>
        public Bitmap InternalBitmap { get; private set; }

        /// <summary>
        /// The width of the bitmap
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// The attributes of the bitmap
        /// </summary>
        private BitmapData Data { get; set; }

        /// <summary>
        /// A pointer to the memory containing the bitmap
        /// </summary>
        private unsafe byte* DataPointer { get; set; }

        /// <summary>
        /// The size in bytes of each pixel
        /// </summary>
        private int PixelSize { get; set; }

        /// <summary>
        /// Lock the bitmap to edit it through memory
        /// </summary>
        public unsafe void Lock() {
            if (Data != null)
                return;
            Data = InternalBitmap.LockBits(new Rectangle(0, 0, InternalBitmap.Width, InternalBitmap.Height),
                ImageLockMode.ReadWrite,
                InternalBitmap.PixelFormat);
            PixelSize = GetPixelSize();
            DataPointer = (byte*)Data.Scan0;
        }

        /// <summary>
        /// Get the size of the pixels in bytes
        /// </summary>
        /// <returns></returns>
        private int GetPixelSize() {
            switch (Data.PixelFormat) {
                case PixelFormat.Format24bppRgb:
                    return 3;
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppPArgb:
                case PixelFormat.Format32bppRgb:
                    return 4;
            }
            return 0;
        }

        /// <summary>
        /// Unlocks the bitmap to return the original bitmap
        /// </summary>
        /// <returns>The bitmap after finishing the edit</returns>
        public unsafe FastBitmap Unlock() {
            if (Data == null)
                return this;
            InternalBitmap.UnlockBits(Data);
            Data = null;
            DataPointer = null;
            return this;
        }

        /// <summary>
        /// Get the colour of the pixel at a position
        /// </summary>
        /// <param name="x">The x coord of the pixel to get</param>
        /// <param name="y">The y coord of the pixel to get</param>
        /// <returns>The colour of the pixel</returns>
        public unsafe Color GetPixel(int x, int y) {
            var tempPointer = DataPointer + (y * Data.Stride) + (x * PixelSize);
            return (PixelSize == 3) ?
                Color.FromArgb(tempPointer[2], tempPointer[1], tempPointer[0]) :
                Color.FromArgb(tempPointer[3], tempPointer[2], tempPointer[1], tempPointer[0]);
        }

        /// <summary>
        /// Set the colour of the pixel at a position
        /// </summary>
        /// <param name="x">The x coord of the pixel to set</param>
        /// <param name="y">The y coord of the pixel to set</param>
        /// <param name="pixelColor">The new colour of the pixel</param>
        public unsafe void SetPixel(int x, int y, Color pixelColor) {
            var tempPointer = DataPointer + (y * Data.Stride) + (x * PixelSize);
            if (PixelSize == 3) {
                tempPointer[2] = pixelColor.R;
                tempPointer[1] = pixelColor.G;
                tempPointer[0] = pixelColor.B;
                return;
            }
            tempPointer[3] = pixelColor.A;
            tempPointer[2] = pixelColor.R;
            tempPointer[1] = pixelColor.G;
            tempPointer[0] = pixelColor.B;
        }
    }

    /// <summary>
    /// Class to house the Colourize extension method
    /// </summary>
    public static class BitmapExtension {
        /// <summary>
        /// Converts a monochrome image into the colours in the parsed array
        /// </summary>
        /// <param name="originalImage">The image to convert</param>
        /// <param name="colors">The colours to convert to</param>
        /// <returns>BITMAP: The parsed image in the new colours</returns>
        public static Bitmap Colorize(this Bitmap originalImage, Color[] colors) {
            if (colors.Length < 256)
                return new Bitmap(1, 1);
            var newImage = new FastBitmap(originalImage);
            newImage.Lock();
            Parallel.For(0, newImage.Width, x => {
                for (var y = 0; y < newImage.Height; ++y) {
                    int colorUsing = newImage.GetPixel(x, y).R;
                    newImage.SetPixel(x, y, colors[colorUsing]);
                }
            });
            return newImage.Unlock().InternalBitmap;
        }
    }
}