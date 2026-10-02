using System;

namespace MiniRealisticAirways;

// Pure pixel logic shared by the runtime and the focused fuel-icon regression.
internal static class FuelIconRasterizer
{
    internal static byte[] CreateFrame(byte[] alpha, int width, int height, int percent)
    {
        if (width <= 0 || height <= 0 || alpha == null || alpha.Length != width * height)
            throw new ArgumentException("Invalid fuel icon alpha mask.");
        if (percent < 0 || percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));

        int bottom = height;
        int top = -1;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (alpha[y * width + x] == 0) continue;
            bottom = Math.Min(bottom, y);
            top = Math.Max(top, y);
        }
        if (top < bottom) throw new ArgumentException("Fuel icon has no visible pixels.");

        // Transparent margins in the supplied PNG must not consume the first
        // or last part of the fuel range. Unity texture rows run bottom to top.
        int filledRows = percent * (top - bottom + 1) / 100;
        byte[] rgba = new byte[alpha.Length * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int pixel = y * width + x;
            byte shade = y < bottom + filledRows ? (byte)255 : (byte)128;
            rgba[pixel * 4] = rgba[pixel * 4 + 1] = rgba[pixel * 4 + 2] = shade;
            rgba[pixel * 4 + 3] = alpha[pixel];
        }
        return rgba;
    }
}
