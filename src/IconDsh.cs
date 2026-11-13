// Converts the official DSH app icon (a 1024px PNG with the rounded-square
// backdrop) into a multi-size .ico with plain BMP entries, so Explorer, the
// title bar and the taskbar all get real bitmaps instead of a 16px fallback.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconDsh
{
    private static void Main(string[] args)
    {
        string sourcePng = args[0];
        string dstIco = args[1];

        List<byte[]> images = new List<byte[]>();
        List<int> widths = new List<int>();
        using (Image source = Image.FromFile(sourcePng))
        {
            foreach (int size in new int[] { 16, 24, 32, 48, 64, 128, 256 })
            {
                using (Bitmap scaled = Resize(source, size))
                {
                    images.Add(EncodeDib(scaled));
                    widths.Add(size);
                }
            }
        }

        WriteIco(dstIco, images, widths);
        Console.WriteLine("wrote " + dstIco + " (" + images.Count + " sizes)");
    }

    private static Bitmap Resize(Image source, int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, size, size));
        }
        return bmp;
    }

    private static void WriteIco(string path, List<byte[]> images, List<int> widths)
    {
        int offset = 6 + 16 * images.Count;
        using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)images.Count);
            for (int i = 0; i < images.Count; i++)
            {
                int d = widths[i];
                w.Write((byte)(d >= 256 ? 0 : d));
                w.Write((byte)(d >= 256 ? 0 : d));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write(images[i].Length);
                w.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] img in images) w.Write(img);
        }
    }

    private static byte[] EncodeDib(Bitmap bmp)
    {
        int width = bmp.Width;
        int height = bmp.Height;
        int maskStride = ((width + 31) / 32) * 4;
        int imageSize = width * height * 4;
        byte[] outBytes = new byte[40 + imageSize + maskStride * height];

        BitConverter.GetBytes(40).CopyTo(outBytes, 0);
        BitConverter.GetBytes(width).CopyTo(outBytes, 4);
        BitConverter.GetBytes(height * 2).CopyTo(outBytes, 8);
        BitConverter.GetBytes((short)1).CopyTo(outBytes, 12);
        BitConverter.GetBytes((short)32).CopyTo(outBytes, 14);
        BitConverter.GetBytes(imageSize + maskStride * height).CopyTo(outBytes, 20);

        int pos = 40;
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = bmp.GetPixel(x, y);
                outBytes[pos++] = c.B;
                outBytes[pos++] = c.G;
                outBytes[pos++] = c.R;
                outBytes[pos++] = c.A;
            }
        }
        return outBytes;
    }
}
