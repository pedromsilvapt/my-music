using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyMusic.Server.DTO.Config;

namespace MyMusic.Server.Controllers;

[ApiController]
[Route("config")]
public class ConfigController(IOptionsSnapshot<FeedbackConfig> feedbackConfig) : ControllerBase
{
    /// <summary>
    /// Runtime configuration the web client needs on startup.
    /// </summary>
    [HttpGet("client", Name = "GetClientConfig")]
    public GetClientConfigResponse GetClientConfig()
    {
        var feedback = feedbackConfig.Value;

        return new GetClientConfigResponse
        {
            Feedback = string.IsNullOrWhiteSpace(feedback.Url) || string.IsNullOrWhiteSpace(feedback.Project)
                ? null
                : new ClientFeedbackConfig { Url = feedback.Url.TrimEnd('/'), Project = feedback.Project },
        };
    }
}
