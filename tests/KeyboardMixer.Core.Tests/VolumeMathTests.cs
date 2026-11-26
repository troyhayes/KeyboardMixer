using KeyboardMixer.Core.Audio;

namespace KeyboardMixer.Core.Tests;

public class VolumeMathTests
{
    [Theory]
    [InlineData(0.47f, 2, true, 0.49f)]
    [InlineData(0.47f, 2, false, 0.45f)]
    [InlineData(0.99f, 2, true, 1.00f)]
    [InlineData(0.01f, 5, false, 0.00f)]
    [InlineData(0.4699f, 2, true, 0.49f)] // snaps to whole percentages, no drift
    public void Step_ClampsAndSnaps(float current, int step, bool up, float expected)
    {
        Assert.Equal(expected, VolumeMath.Step(current, step, up), 3);
    }

    [Fact]
    public void RepeatedSteps_DoNotDrift()
    {
        float v = 0.5f;
        for (int i = 0; i < 10; i++) v = VolumeMath.Step(v, 2, up: true);
        for (int i = 0; i < 10; i++) v = VolumeMath.Step(v, 2, up: false);
        Assert.Equal(50, VolumeMath.ToPercent(v));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(100, 25)]
    [InlineData(5, 5)]
    public void ClampStep(int input, int expected) => Assert.Equal(expected, VolumeMath.ClampStep(input));
}
