// Copies the RT_GROUP_ICON icon group out of a PE file verbatim, preserving the
// per-size artwork the original author shipped (no resampling).
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

internal static class IcoExtract
{
    private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    private static readonly IntPtr RT_ICON = new IntPtr(3);
    private static readonly IntPtr RT_GROUP_ICON = new IntPtr(14);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr data);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr module, IntPtr resource);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResNameProc callback, IntPtr param);

    private delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);

    private static string _outPath;
    private static bool _written;

    private static void Main(string[] args)
    {
        string exe = args[0];
        _outPath = args[1];
        IntPtr module = LoadLibraryEx(exe, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
        if (module == IntPtr.Zero) throw new Exception("LoadLibraryEx failed: " + Marshal.GetLastWin32Error());
        try
        {
            EnumResourceNames(module, RT_GROUP_ICON, new EnumResNameProc(OnGroup), IntPtr.Zero);
        }
        finally
        {
            FreeLibrary(module);
        }
        if (!_written) throw new Exception("no RT_GROUP_ICON resource found");
        Console.WriteLine("wrote " + _outPath);
    }

    private static bool OnGroup(IntPtr module, IntPtr type, IntPtr name, IntPtr param)
    {
        if (_written) return true; // first group wins

        IntPtr res = FindResource(module, name, RT_GROUP_ICON);
        if (res == IntPtr.Zero) return true;
        uint size = SizeofResource(module, res);
        IntPtr data = LockResource(LoadResource(module, res));
        if (data == IntPtr.Zero) return true;

        // GRPICONDIR: reserved(2) type(2) count(2) then GRPICONDIRENTRY[count]
        int count = Marshal.ReadInt16(data, 4);
        int headerSize = 6 + 16 * count;
        List<byte[]> images = new List<byte[]>();
        List<byte[]> entries = new List<byte[]>();

        for (int i = 0; i < count; i++)
        {
            int baseOff = 6 + i * 16;
            int id = (int)ReadUInt16(data, baseOff + 12);
            IntPtr icon = FindResource(module, new IntPtr(id), RT_ICON);
            if (icon == IntPtr.Zero) continue;
            uint iconSize = SizeofResource(module, icon);
            IntPtr iconData = LockResource(LoadResource(module, icon));
            if (iconData == IntPtr.Zero) continue;

            byte[] img = new byte[iconSize];
            Marshal.Copy(iconData, img, 0, (int)iconSize);
            images.Add(img);

            byte[] entry = new byte[16];
            entry[0] = Marshal.ReadByte(data, baseOff + 0);     // width
            entry[1] = Marshal.ReadByte(data, baseOff + 1);     // height
            entry[2] = Marshal.ReadByte(data, baseOff + 2);     // colours
            entry[3] = 0;
            WriteUInt16(entry, 4, (ushort)ReadUInt16(data, baseOff + 4)); // planes
            WriteUInt16(entry, 6, (ushort)ReadUInt16(data, baseOff + 6)); // bitcount
            WriteUInt32(entry, 8, iconSize);                    // bytes in resource
            entries.Add(entry);
        }

        if (images.Count == 0) return true;

        int offset = 6 + 16 * images.Count;
        for (int i = 0; i < images.Count; i++)
        {
            WriteUInt32(entries[i], 12, (uint)offset);
            offset += images[i].Length;
        }

        using (FileStream fs = new FileStream(_outPath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)images.Count);
            foreach (byte[] e in entries) w.Write(e);
            foreach (byte[] img in images) w.Write(img);
        }
        _written = true;
        Console.WriteLine("sizes: " + images.Count);
        return true;
    }

    private static ushort ReadUInt16(IntPtr p, int off)
    {
        return (ushort)Marshal.ReadInt16(p, off);
    }

    private static void WriteUInt16(byte[] b, int off, ushort v)
    {
        b[off] = (byte)(v & 0xFF);
        b[off + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static void WriteUInt32(byte[] b, int off, uint v)
    {
        b[off] = (byte)(v & 0xFF);
        b[off + 1] = (byte)((v >> 8) & 0xFF);
        b[off + 2] = (byte)((v >> 16) & 0xFF);
        b[off + 3] = (byte)((v >> 24) & 0xFF);
    }
}
