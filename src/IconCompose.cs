// Composes a practical .ico:
//   * keeps the hand-tuned 16x16 artwork from the source group verbatim
//   * keeps the 256x256 entry verbatim (usually PNG-compressed)
//   * adds BMP entries for 24/32/48/64/128 so GDI+ (title bar, alt-tab) has
//     real bitmaps to pick from instead of falling back to 16x16.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconCompose
{
    private static void Main(string[] args)
    {
        string srcIco = args[0];
        string dstIco = args[1];

        List<byte[]> srcImages = new List<byte[]>();
        List<int> srcWidths = new List<int>();
        ReadIco(srcIco, srcImages, srcWidths);

        byte[] small = null;   // 16x16 raw entry
        byte[] large = null;   // 256x256 raw entry
        int largeIndex = -1;
        for (int i = 0; i < srcImages.Count; i++)
        {
            if (srcWidths[i] <= 16 && small == null) small = srcImages[i];
            if (srcWidths[i] >= 256) { large = srcImages[i]; largeIndex = i; }
        }
        if (large == null)
        {
            // fall back to the biggest entry available
            int best = 0;
            for (int i = 0; i < srcImages.Count; i++) if (srcWidths[i] > srcWidths[best]) best = i;
            large = srcImages[best];
            largeIndex = best;
        }

        Bitmap art;
        using (MemoryStream ms = new MemoryStream(large))
        {
            art = new Bitmap(Image.FromStream(ms));
        }

        List<byte[]> images = new List<byte[]>();
        List<int> widths = new List<int>();

        if (small != null) { images.Add(small); widths.Add(16); }
        else { images.Add(EncodeDib(Resize(art, 16))); widths.Add(16); }

        foreach (int size in new int[] { 24, 32, 48, 64, 128 })
        {
            images.Add(EncodeDib(Resize(art, size)));
            widths.Add(size);
        }
        images.Add(large);
        widths.Add(256);

        WriteIco(dstIco, images, widths);
        art.Dispose();
        Console.WriteLine("wrote " + dstIco + " with " + images.Count + " entries");
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

    private static void ReadIco(string path, List<byte[]> images, List<int> widths)
    {
        byte[] all = File.ReadAllBytes(path);
        int count = BitConverter.ToUInt16(all, 4);
        for (int i = 0; i < count; i++)
        {
            int off = 6 + i * 16;
            int w = all[off];
            if (w == 0) w = 256;
            int size = BitConverter.ToInt32(all, off + 8);
            int dataOff = BitConverter.ToInt32(all, off + 12);
            byte[] img = new byte[size];
            Array.Copy(all, dataOff, img, 0, size);
            images.Add(img);
            widths.Add(w);
        }
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
        int maskSize = maskStride * height;
        byte[] outBytes = new byte[40 + imageSize + maskSize];

        BitConverter.GetBytes(40).CopyTo(outBytes, 0);
        BitConverter.GetBytes(width).CopyTo(outBytes, 4);
        BitConverter.GetBytes(height * 2).CopyTo(outBytes, 8);
        BitConverter.GetBytes((short)1).CopyTo(outBytes, 12);
        BitConverter.GetBytes((short)32).CopyTo(outBytes, 14);
        BitConverter.GetBytes(imageSize + maskSize).CopyTo(outBytes, 20);

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
