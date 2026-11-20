namespace MyMusic.Server;

/// <summary>
/// Configuration of the Feedbox feedback widget embedded in the web client.
/// The widget is disabled unless both <see cref="Url"/> and <see cref="Project"/> are set.
/// </summary>
public class FeedbackConfig
{
    /// <summary>
    /// Base URL of the Feedbox server (e.g. <c>https://feedbox.home</c>).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Name of the Feedbox project that receives the feedback.
    /// </summary>
    public string? Project { get; set; }
}
