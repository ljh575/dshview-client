// Turns the black DSH-Portable whale into an inverted white silhouette on a
// transparent background (the artwork's own cut-outs stay transparent), then
// writes a multi-size .ico with plain BMP entries and dark/light previews.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconWhite
{
    private static readonly Color Ink = Color.FromArgb(255, 255, 255);

    private static void Main(string[] args)
    {
        string srcIco = args[0];
        string dstIco = args[1];
        string previewDir = args[2];

        Bitmap master;
        byte[] artwork = LargestEntry(srcIco);
        using (MemoryStream ms = new MemoryStream(artwork))
        {
            master = new Bitmap(Image.FromStream(ms));
        }

        List<byte[]> images = new List<byte[]>();
        List<int> widths = new List<int>();
        foreach (int size in new int[] { 16, 24, 32, 48, 64, 128, 256 })
        {
            using (Bitmap scaled = Resize(master, size))
            using (Bitmap inverted = Invert(scaled))
            {
                images.Add(EncodeDib(inverted));
                widths.Add(size);
                if (size == 256)
                {
                    SaveOnBackground(inverted, Color.FromArgb(32, 32, 32), Path.Combine(previewDir, "white-dark.png"));
                    SaveOnBackground(inverted, Color.FromArgb(243, 243, 243), Path.Combine(previewDir, "white-light.png"));
                }
            }
        }

        WriteIco(dstIco, images, widths);
        master.Dispose();
        Console.WriteLine("wrote " + dstIco + " (" + images.Count + " sizes)");
    }

    // Keep the alpha channel (so anti-aliased edges stay smooth and the artwork's
    // belly / eye / fin cut-outs remain see-through) but repaint the ink white.
    private static Bitmap Invert(Bitmap source)
    {
        Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color c = source.GetPixel(x, y);
                if (c.A == 0) { result.SetPixel(x, y, Color.Transparent); continue; }
                result.SetPixel(x, y, Color.FromArgb(c.A, Ink.R, Ink.G, Ink.B));
            }
        }
        return result;
    }

    private static void SaveOnBackground(Bitmap icon, Color background, string path)
    {
        using (Bitmap canvas = new Bitmap(icon.Width + 24, icon.Height + 24, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(canvas))
        {
            g.Clear(background);
            g.DrawImage(icon, 12, 12, icon.Width, icon.Height);
            canvas.Save(path, ImageFormat.Png);
        }
    }

    private static Bitmap Resize(Bitmap source, int size)
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

    private static byte[] LargestEntry(string path)
    {
        byte[] all = File.ReadAllBytes(path);
        int count = BitConverter.ToUInt16(all, 4);
        int bestSize = -1, bestWidth = -1, bestOffset = -1;
        for (int i = 0; i < count; i++)
        {
            int off = 6 + i * 16;
            int w = all[off];
            if (w == 0) w = 256;
            int size = BitConverter.ToInt32(all, off + 8);
            if (w > bestWidth)
            {
                bestWidth = w;
                bestSize = size;
                bestOffset = BitConverter.ToInt32(all, off + 12);
            }
        }
        if (bestOffset < 0) throw new Exception("no icon entry found");
        byte[] img = new byte[bestSize];
        Array.Copy(all, bestOffset, img, 0, bestSize);
        return img;
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
