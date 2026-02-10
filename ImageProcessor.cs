using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide image paths.");
            return;
        }

        foreach (var path in args)
        {
            ProcessImage(path);
        }
    }

    static void ProcessImage(string path)
    {
        try
        {
            Console.WriteLine("Processing: " + path);
            if (!File.Exists(path))
            {
                Console.WriteLine("File not found: " + path);
                return;
            }

            using (Bitmap original = new Bitmap(path))
            {
                // Create a new bitmap with the same dimensions and ARGB pixel format
                using (Bitmap newBitmap = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb))
                {
                    // Lock bits for faster processing
                    BitmapData sourceData = original.LockBits(new Rectangle(0, 0, original.Width, original.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    BitmapData targetData = newBitmap.LockBits(new Rectangle(0, 0, newBitmap.Width, newBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                    int bytes = Math.Abs(sourceData.Stride) * original.Height;
                    byte[] rgbValues = new byte[bytes];
                    byte[] resultValues = new byte[bytes];

                    System.Runtime.InteropServices.Marshal.Copy(sourceData.Scan0, rgbValues, 0, bytes);

                    for (int i = 0; i < bytes; i += 4)
                    {
                        byte b = rgbValues[i];
                        byte g = rgbValues[i + 1];
                        byte r = rgbValues[i + 2];
                        // rgbValues[i+3] is alpha, ignore source alpha (usually 255 for jpg)

                        // Calculate brightness
                        // Using standard luminance formula: 0.299R + 0.587G + 0.114B
                        // Or simple average for white detection
                        double brightness = (r + g + b) / 3.0;

                        byte alpha = 255;
                        double thresholdHigh = 250.0;
                        double thresholdLow = 200.0;

                        if (brightness >= thresholdHigh)
                        {
                            alpha = 0;
                        }
                        else if (brightness > thresholdLow)
                        {
                            // Fade out
                            double factor = (thresholdHigh - brightness) / (thresholdHigh - thresholdLow);
                            alpha = (byte)(255 * factor);
                        }
                        
                        // Special handling for "almost white" but colored pixels (preserve color)
                        // If saturation is low and brightness is high -> transparent
                        // If saturation is high, keep it opaque even if bright (e.g. bright yellow)
                        
                        double max = Math.Max(r, Math.Max(g, b));
                        double min = Math.Min(r, Math.Min(g, b));
                        double saturation = (max == 0) ? 0 : (1.0 - (min / max));

                        if (saturation > 0.1) // If it has color
                        {
                            // If it's very bright but colorful (like yellow flower), keep it more opaque
                            // But for ink wash, usually white is background.
                            // Let's stick to simple brightness fade for now, but maybe tweak thresholds.
                            // Yellow is (255, 255, 0) -> Avg = 170. Safe.
                            // Light yellow (255, 255, 200) -> Avg = 236. Might fade.
                            // Let's adjust thresholds.
                            if (brightness > 240) alpha = (byte)(alpha * 0.5); // Reduce alpha for very bright colors to blend
                        }

                        resultValues[i] = b;
                        resultValues[i + 1] = g;
                        resultValues[i + 2] = r;
                        resultValues[i + 3] = alpha;
                    }

                    System.Runtime.InteropServices.Marshal.Copy(resultValues, 0, targetData.Scan0, bytes);

                    original.UnlockBits(sourceData);
                    newBitmap.UnlockBits(targetData);

                    string dir = Path.GetDirectoryName(path);
                    string name = Path.GetFileNameWithoutExtension(path);
                    string newPath = Path.Combine(dir, name + ".png");

                    newBitmap.Save(newPath, ImageFormat.Png);
                    Console.WriteLine("Saved to: " + newPath);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
