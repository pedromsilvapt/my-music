using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyMusic.Common.AudioIntegrity;
using MyMusic.Common.Seeding;
using MyMusic.Common.Services;
using MyMusic.Common.Services.AuditRules;
using MyMusic.Common.Services.PlaylistSongs;
using MyMusic.Common.Services.Sync;

namespace MyMusic.Common;

public static class HostBuilderExtensions
{
    public static T UseMyMusicCommon<T>(this T builder) where T : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<PurchasesQueue>();
        builder.Services.AddSingleton<MetadataFetchQueue>();
        builder.Services.AddHostedService<MetadataFetchCleanupService>();
        builder.Services.AddHostedService<BitrateBackfillService>();
        builder.Services.AddHostedService<WishlistBackgroundService>();
        builder.Services.AddSingleton<IFileSystem, FileSystem>();
        builder.Services.AddScoped<IMusicService, MusicService>();
        builder.Services.AddScoped<ISongMergeService, SongMergeService>();
        builder.Services.AddScoped<ISongUpdateService, SongUpdateService>();
        builder.Services.AddScoped<IPlaylistSongSkipService, PlaylistSongSkipService>();
        builder.Services.AddScoped<ISourcesService, SourcesService>();
        builder.Services.AddScoped<IWishlistService, WishlistService>();
        builder.Services.AddScoped<IPurchasesSearchService, PurchasesSearchService>();
        builder.Services.AddTransient<PurchasesQueue.PurchasesExecutor>();
        builder.Services.AddTransient<MetadataFetchQueue.MetadataFetchExecutor>();
        builder.Services.AddTransient<MusicImportJob>();

        builder.Services.AddScoped<ISyncCommitService, SyncCommitService>();
        builder.Services.AddHostedService<StagingDirectoryCleanupService>();
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<IAuditRule, MissingCoverAuditRule>();
        builder.Services.AddScoped<IAuditRule, MissingYearAuditRule>();
        builder.Services.AddScoped<IAuditRule, MissingGenresAuditRule>();
        builder.Services.AddScoped<IAuditRule, MissingLyricsAuditRule>();
        builder.Services.AddScoped<IAuditRule, MediumCoverAuditRule>();
        builder.Services.AddScoped<IAuditRule, SmallCoverAuditRule>();
        builder.Services.AddScoped<IAuditRule, NonJpegCoverAuditRule>();
        builder.Services.AddScoped<IAuditRule, NonSquareCoverAuditRule>();
        builder.Services.AddScoped<IAuditRule, SoundalikeAuditRule>();
        builder.Services.AddScoped<IAuditRule, MissingFileAuditRule>();
        builder.Services.AddScoped<IAuditRule, FileIntegrityAuditRule>();

        builder.Services.AddSingleton<IFpcalcService, FpcalcService>();
        builder.Services.AddScoped<AcousticFingerprintService>();

        builder.Services.AddScoped<IAuditRuleFieldMapper, AuditRuleFieldMapper>();

        builder.Services.AddScoped<ISeedService, SeedService>();
        builder.Services.AddScoped<ICountRecalculationService, CountRecalculationService>();

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
        builder.Services.Configure<AuditConfig>(builder.Configuration.GetSection("Audit"));
        builder.Services.Configure<AudioIntegrityConfig>(builder.Configuration.GetSection("AudioIntegrity"));

        builder.Services.AddSingleton<IAudioIntegrityService, AudioIntegrityService>();
        builder.Services.AddSingleton<IAudioIntegrityValidator, Mp3IntegrityValidator>();
        builder.Services.AddSingleton<IFFmpegRunner, FFmpegRunner>();

        return builder;
    }

    public static T BuildMyMusicCommon<T>(this T app) where T : IHost
    {
        using var serviceScope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<MusicDbContext>();
        context.Database.Migrate();

        var seedService = serviceScope.ServiceProvider.GetRequiredService<ISeedService>();
        seedService.SeedAsync().GetAwaiter().GetResult();

        return app;
    }
}