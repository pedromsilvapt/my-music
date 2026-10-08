using Microsoft.Playwright;
using Shouldly;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Opens a song's details and asserts the format of its file on the server, going by the extension of its path.
/// </summary>
public class ShouldSongFileHaveExtensionFlow(string songTitle, string extension) : IFlow
{
    public async Task ExecuteAsync(IPage page)
    {
        var songDetails = await new OpenSongDetailsFlow(songTitle).ExecuteAsync(page);
        var repositoryPath = await songDetails.GetRepositoryPathAsync();

        repositoryPath.ShouldNotBeNull();
        Path.GetExtension(repositoryPath).ShouldBe(extension, $"Song '{songTitle}' should be a {extension} file");
    }
}
