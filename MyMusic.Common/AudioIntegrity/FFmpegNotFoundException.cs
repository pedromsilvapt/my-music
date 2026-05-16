namespace MyMusic.Common.AudioIntegrity;

/// <summary>
/// Thrown when ffmpeg cannot be located or started.
/// </summary>
public class FFmpegNotFoundException : InvalidOperationException
{
    public FFmpegNotFoundException(string message)
        : base(message)
    {
    }

    public FFmpegNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
