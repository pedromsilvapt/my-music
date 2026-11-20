namespace MyMusic.Server.DTO.Config;

public record GetClientConfigResponse
{
    /// <summary>
    /// Feedback widget configuration, or <c>null</c> when the widget is disabled.
    /// </summary>
    public ClientFeedbackConfig? Feedback { get; set; }
}

public record ClientFeedbackConfig
{
    public required string Url { get; set; }
    public required string Project { get; set; }
}
