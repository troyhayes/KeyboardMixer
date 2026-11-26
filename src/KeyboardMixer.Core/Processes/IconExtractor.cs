using System.Runtime.InteropServices;

namespace KeyboardMixer.Core.Processes;

/// <summary>Premultiplied BGRA pixels, ready for a WinUI WriteableBitmap.</summary>
public sealed record IconPixels(int Width, int Height, byte[] Bgra);

/// <summary>
/// Extracts an executable's icon with plain Win32 calls. Unlike the shell thumbnail API this is
/// synchronous, deterministic and safe to call from any thread.
/// </summary>
public static unsafe partial class IconExtractor
{
    public static IconPixels? Extract(string exePath, int size)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;

        nint icon = 0;
        uint count = PrivateExtractIconsW(exePath, 0, size, size, &icon, null, 1, 0);
        if (count == 0 || count == uint.MaxValue || icon == 0) return null;

        try
        {
            return ToPixels(icon);
        }
        finally
        {
            DestroyIcon(icon);
        }
    }

    private static IconPixels? ToPixels(nint icon)
    {
        if (!GetIconInfo(icon, out var info)) return null;
        try
        {
            if (info.hbmColor == 0) return null; // monochrome icon; not worth supporting

            BITMAP bitmap;
            if (GetObjectW(info.hbmColor, sizeof(BITMAP), &bitmap) == 0) return null;
            int width = bitmap.bmWidth, height = bitmap.bmHeight;

            var pixels = ReadBits(info.hbmColor, width, height);
            if (pixels is null) return null;

            bool hasAlpha = false;
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0) { hasAlpha = true; break; }
            }

            if (!hasAlpha)
            {
                // Old-style icon: transparency lives in the AND mask (white = transparent).
                var mask = info.hbmMask != 0 ? ReadBits(info.hbmMask, width, height) : null;
                for (int i = 0; i < pixels.Length; i += 4)
                    pixels[i + 3] = mask is not null && mask[i] != 0 ? (byte)0 : (byte)255;
            }

            // WriteableBitmap expects premultiplied alpha.
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte a = pixels[i + 3];
                if (a == 255) continue;
                pixels[i] = (byte)(pixels[i] * a / 255);
                pixels[i + 1] = (byte)(pixels[i + 1] * a / 255);
                pixels[i + 2] = (byte)(pixels[i + 2] * a / 255);
            }

            return new IconPixels(width, height, pixels);
        }
        finally
        {
            if (info.hbmColor != 0) DeleteObject(info.hbmColor);
            if (info.hbmMask != 0) DeleteObject(info.hbmMask);
        }
    }

    private static byte[]? ReadBits(nint hBitmap, int width, int height)
    {
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER),
            biWidth = width,
            biHeight = -height, // top-down
            biPlanes = 1,
            biBitCount = 32,
        };
        var pixels = new byte[width * height * 4];
        nint dc = GetDC(0);
        try
        {
            fixed (byte* p = pixels)
            {
                return GetDIBits(dc, hBitmap, 0, (uint)height, p, &header, 0) == 0 ? null : pixels;
            }
        }
        finally
        {
            ReleaseDC(0, dc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public int fIcon;
        public int xHotspot;
        public int yHotspot;
        public nint hbmMask;
        public nint hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public nint bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
        // A 32bpp BI_RGB DIB has no color table, so bmiColors is not needed.
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint PrivateExtractIconsW(string file, int index, int cx, int cy, nint* icons, uint* ids, uint count, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetIconInfo(nint icon, out ICONINFO info);

    [LibraryImport("gdi32.dll")]
    private static partial int GetObjectW(nint obj, int size, void* buffer);

    [LibraryImport("gdi32.dll")]
    private static partial int GetDIBits(nint dc, nint bitmap, uint start, uint lines, void* bits, BITMAPINFOHEADER* info, uint usage);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint obj);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint dc);
}
