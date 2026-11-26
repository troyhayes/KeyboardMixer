using KeyboardMixer.Core.Flyout;
using KeyboardMixer.Core.Settings;

namespace KeyboardMixer.Core.Tests;

public class FlyoutPlacementTests
{
    // Second monitor to the right of a 1920px primary; taskbar takes the bottom 40px.
    private static readonly PixelRect Work = new(1920, 0, 2560, 1400);

    [Theory]
    [InlineData(FlyoutPosition.BottomCenter, 1920 + (2560 - 400) / 2, 1400 - 80 - 10)]
    [InlineData(FlyoutPosition.TopCenter, 1920 + (2560 - 400) / 2, 10)]
    [InlineData(FlyoutPosition.BottomRight, 1920 + 2560 - 400 - 10, 1400 - 80 - 10)]
    [InlineData(FlyoutPosition.TopLeft, 1920 + 10, 10)]
    public void Positions(FlyoutPosition pos, int x, int y) =>
        Assert.Equal((x, y), FlyoutPlacement.Compute(Work, 400, 80, pos, 10));

    [Fact]
    public void StaysInsideTinyWorkArea() =>
        Assert.Equal((0, 0), FlyoutPlacement.Compute(new PixelRect(0, 0, 300, 50), 400, 80, FlyoutPosition.BottomRight, 10));
}
