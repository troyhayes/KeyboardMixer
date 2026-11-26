using KeyboardMixer.Core.Processes;

namespace KeyboardMixer.Core.Tests;

public class ExeNameTests
{
    [Theory]
    [InlineData("Spotify.exe", "spotify.exe")]
    [InlineData("spotify", "spotify.exe")]
    [InlineData("  \"C:\\Program Files\\Mozilla Firefox\\firefox.exe\" ", "firefox.exe")]
    [InlineData("C:/apps/Discord.EXE", "discord.exe")]
    public void Normalize(string input, string expected) => Assert.Equal(expected, ExeName.Normalize(input));

    [Fact]
    public void Matches_IgnoresCaseAndExtension()
    {
        Assert.True(ExeName.Matches("Discord.exe", "discord"));
        Assert.False(ExeName.Matches("discord.exe", "discordptb.exe"));
        Assert.False(ExeName.Matches(null, "discord.exe"));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(".exe", false)]
    [InlineData("a|b.exe", false)]
    [InlineData("chrome", true)]
    public void IsValid(string input, bool expected) => Assert.Equal(expected, ExeName.IsValid(input));

    [Fact]
    public void FriendlyName() => Assert.Equal("Spotify", ExeName.ToFriendlyName("spotify.exe"));
}
