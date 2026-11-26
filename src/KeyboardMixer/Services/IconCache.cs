using System.Runtime.InteropServices.WindowsRuntime;
using KeyboardMixer.Core;
using KeyboardMixer.Core.Processes;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KeyboardMixer.Services;

/// <summary>Loads and caches application icons from executable paths. Call from the UI thread.</summary>
internal sealed class IconCache
{
    // 64px covers the 32px flyout icon at 200% scaling.
    private const int IconSize = 64;

    private readonly Dictionary<string, Task<ImageSource?>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<ImageSource?> GetAsync(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath)) return Task.FromResult<ImageSource?>(null);

        if (!_cache.TryGetValue(exePath, out var task))
        {
            task = LoadAsync(exePath);
            _cache[exePath] = task;
        }
        return task;
    }

    private static async Task<ImageSource?> LoadAsync(string exePath)
    {
        try
        {
            // Pixel extraction is plain Win32 work; keep it off the UI thread.
            var pixels = await Task.Run(() => IconExtractor.Extract(exePath, IconSize));
            if (pixels is null) return null;

            var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                await stream.WriteAsync(pixels.Bgra);
            }
            bitmap.Invalidate();
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Error($"Icon load failed for {exePath}", ex);
            return null;
        }
    }
}
