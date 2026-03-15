namespace MyMusic.Server;

public class ServerConfig
{
    public required string ClientUrl { get; set; }

    public string ApiBasePath { get; set; } = "/api";
}