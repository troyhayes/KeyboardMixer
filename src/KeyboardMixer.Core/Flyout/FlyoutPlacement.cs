using KeyboardMixer.Core.Settings;

namespace KeyboardMixer.Core.Flyout;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public static class FlyoutPlacement
{
    /// <summary>
    /// Top-left corner for a flyout of the given size inside a monitor's work area. All values are
    /// physical pixels; <paramref name="margin"/> is the gap from the work-area edges.
    /// </summary>
    public static (int X, int Y) Compute(PixelRect workArea, int width, int height, FlyoutPosition position, int margin)
    {
        int left = workArea.X + margin;
        int right = workArea.Right - width - margin;
        int centerX = workArea.X + (workArea.Width - width) / 2;
        int top = workArea.Y + margin;
        int bottom = workArea.Bottom - height - margin;

        var (x, y) = position switch
        {
            FlyoutPosition.TopCenter => (centerX, top),
            FlyoutPosition.BottomRight => (right, bottom),
            FlyoutPosition.BottomLeft => (left, bottom),
            FlyoutPosition.TopRight => (right, top),
            FlyoutPosition.TopLeft => (left, top),
            _ => (centerX, bottom),
        };

        // Keep it on screen even if the work area is smaller than the flyout.
        x = Math.Max(workArea.X, Math.Min(x, workArea.Right - width));
        y = Math.Max(workArea.Y, Math.Min(y, workArea.Bottom - height));
        return (x, y);
    }
}
