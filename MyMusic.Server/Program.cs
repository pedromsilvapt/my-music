using MyMusic.Common;
using MyMusic.Server;

var builder = WebApplication.CreateBuilder(args)
    .UseMyMusicConfiguration()
    .UseMyMusicCommon()
    .UseMyMusicServer();

var app = builder.Build()
    .BuildMyMusicCommon()
    .BuildMyMusicServer();

app.Run();
