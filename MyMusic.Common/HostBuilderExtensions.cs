using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyMusic.Common.Services;

namespace MyMusic.Common;

public static class HostBuilderExtensions
{
    public static T UseMyMusicCommon<T>(this T builder) where T : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<PurchasesQueue>();
        builder.Services.AddScoped<IFileSystem, FileSystem>();
        builder.Services.AddScoped<IMusicService, MusicService>();
        builder.Services.AddScoped<ISourcesService, SourcesService>();
        builder.Services.AddTransient<PurchasesQueue.PurchasesExecutor>();
        builder.Services.AddTransient<MusicImportJob>();

        // Add services to the container.
        builder.Services.AddDbContext<MusicDbContext>((sp, options) =>
        {
            var connectionString = builder.Configuration.GetConnectionString("Postgres");

            // TODO Add configuration
            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .UseProjectables();
        });

        builder.Services.Configure<Config>(builder.Configuration.GetSection("MyMusic"));

        return builder;
    }

    public static T BuildMyMusicCommon<T>(this T app) where T : IHost
    {
        using var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<MusicDbContext>();
        context.Database.Migrate();

        return app;
    }
}