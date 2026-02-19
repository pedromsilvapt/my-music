namespace MyMusic.Common;

public class Config
{
    public required string MusicRepositoryPath { get; set; }

    public string DefaultNamingTemplate { get; set; } =
        "{{ album.artist.name ?? artists[0].name ?? \"Unknown\" }}/{{ album.name ?? \"No Album\" }}/{{ simple_label }}.mp3";
}