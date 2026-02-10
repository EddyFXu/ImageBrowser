using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

class Program {
    static void Main() {
        try {
            using (Bitmap bmp = new Bitmap(256, 256)) {
                using (Graphics g = Graphics.FromImage(bmp)) {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    
                    // Draw Flower
                    Brush[] petals = { Brushes.HotPink, Brushes.DeepPink, Brushes.MediumVioletRed, Brushes.PaleVioletRed };
                    for(int i=0; i<8; i++) {
                        g.FillEllipse(petals[i%4], 128 + (float)(60 * Math.Cos(i * Math.PI / 4)) - 40, 
                                                   128 + (float)(60 * Math.Sin(i * Math.PI / 4)) - 40, 80, 80);
                    }
                    g.FillEllipse(Brushes.Gold, 128-30, 128-30, 60, 60);
                }
                
                using (FileStream fs = new FileStream("app.ico", FileMode.Create)) {
                     // ICO Header
                     fs.WriteByte(0); fs.WriteByte(0);
                     fs.WriteByte(1); fs.WriteByte(0); // Type 1 = ICO
                     fs.WriteByte(1); fs.WriteByte(0); // Count = 1
                     
                     // Directory Entry
                     fs.WriteByte(0); // Width 256
                     fs.WriteByte(0); // Height 256
                     fs.WriteByte(0); // Colors
                     fs.WriteByte(0); // Reserved
                     fs.WriteByte(1); fs.WriteByte(0); // Planes
                     fs.WriteByte(32); fs.WriteByte(0); // BPP
                     
                     using (MemoryStream ms = new MemoryStream()) {
                         bmp.Save(ms, ImageFormat.Png);
                         byte[] pngData = ms.ToArray();
                         
                         fs.Write(BitConverter.GetBytes(pngData.Length), 0, 4); // Size
                         fs.Write(BitConverter.GetBytes(6 + 16), 0, 4); // Offset (Header 6 + DirEntry 16)
                         
                         fs.Write(pngData, 0, pngData.Length);
                     }
                }
            }
            Console.WriteLine("app.ico generated successfully.");
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
