namespace KeyboardMixer.Core.Audio;

public static class VolumeMath
{
    public const int MinStep = 1;
    public const int MaxStep = 25;

    /// <summary>
    /// Steps a 0..1 scalar volume by <paramref name="stepPercent"/> percentage points.
    /// Works on whole percentages so repeated steps never drift (e.g. 47% → 49% → 51%).
    /// </summary>
    public static float Step(float current, int stepPercent, bool up)
    {
        int percent = ToPercent(current);
        int next = Math.Clamp(percent + (up ? stepPercent : -stepPercent), 0, 100);
        return next / 100f;
    }

    public static int ToPercent(float scalar) => (int)Math.Round(Math.Clamp(scalar, 0f, 1f) * 100f, MidpointRounding.AwayFromZero);

    public static int ClampStep(int step) => Math.Clamp(step, MinStep, MaxStep);
}
