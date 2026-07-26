using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyMusic.Common.AudioIntegrity;
using MyMusic.Common.Seeding;
using MyMusic.Common.Services;
using MyMusic.Common.Services.AuditRules;
using MyMusic.Common.Services.Devices;
using MyMusic.Common.Services.PlaylistSongs;
using MyMusic.Common.Services.Sync;

namespace MyMusic.Common;

public static class HostBuilderExtensions
{
    public static T UseMyMusicCommon<T>(this T builder) where T : IHostApplicationBuilder
    {
        // Infrastructure
        builder.Services.AddSingleton<IFileSystem, FileSystem>();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddHttpClient();

        // Background services
        builder.Services.AddHostedService<MetadataFetchCleanupService>();
        builder.Services.AddHostedService<BitrateBackfillService>();
        builder.Services.AddHostedService<WishlistBackgroundService>();
        builder.Services.AddHostedService<StagingDirectoryCleanupService>();

        // Queues and executors
        builder.Services.AddSingleton<PurchasesQueue>();
        builder.Services.AddSingleton<MetadataFetchQueue>();
        builder.Services.AddTransient<PurchasesQueue.PurchasesExecutor>();
        builder.Services.AddTransient<MetadataFetchQueue.MetadataFetchExecutor>();
        builder.Services.AddTransient<MusicImportJob>();

        // Music services
        builder.Services.AddScoped<IMusicService, MusicService>();
        builder.Services.AddScoped<ISongMergeService, SongMergeService>();
        builder.Services.AddScoped<ISongUpdateService, SongUpdateService>();
        builder.Services.AddScoped<ISourcesService, SourcesService>();
        builder.Services.AddScoped<IWishlistService, WishlistService>();
        builder.Services.AddScoped<IPurchasesSearchService, PurchasesSearchService>();
        builder.Services.AddScoped<IPlaylistSongSkipService, PlaylistSongSkipService>();

        // Delete services
        builder.Services.AddScoped<IUserDeleteService, UserDeleteService>();
        builder.Services.AddScoped<ISongDeleteService, SongDeleteService>();
        builder.Services.AddScoped<IAlbumDeleteService, AlbumDeleteService>();
        builder.Services.AddScoped<IArtistDeleteService, ArtistDeleteService>();
        builder.Services.AddScoped<IGenreDeleteService, GenreDeleteService>();
        builder.Services.AddScoped<IArtworkDeleteService, ArtworkDeleteService>();

        // Device services
        builder.Services.AddScoped<IDeviceLookupService, DeviceLookupService>();
        builder.Services.AddScoped<IDeviceListService, DeviceListService>();
        builder.Services.AddScoped<IDeviceGetService, DeviceGetService>();
        builder.Services.AddScoped<IDeviceCreateService, DeviceCreateService>();
        builder.Services.AddScoped<IDeviceUpdateService, DeviceUpdateService>();
        builder.Services.AddScoped<IDeviceDeleteService, DeviceDeleteService>();
        builder.Services.AddScoped<IDeviceFilterValuesService, DeviceFilterValuesService>();

        // Sync session services
        builder.Services.AddScoped<ISyncSessionLookupService, SyncSessionLookupService>();
        builder.Services.AddScoped<ISyncSessionListService, SyncSessionListService>();
        builder.Services.AddScoped<ISyncSessionRecordsQueryService, SyncSessionRecordsQueryService>();
        builder.Services.AddScoped<ISyncSessionFilterValuesService, SyncSessionFilterValuesService>();
        builder.Services.AddScoped<ISyncSessionDeleteService, SyncSessionDeleteService>();
        builder.Services.AddScoped<ISyncSessionPruneService, SyncSessionPruneService>();

        // Sync workflow services
        builder.Services.AddScoped<ISyncActionsServerFactory, SyncActionsServerFactory>();
        builder.Services.AddSingleton<ISyncPathResolver, SyncPathResolver>();
        builder.Services.AddSingleton<ISyncComparisonHelper, SyncComparisonHelper>();
        builder.Services.AddScoped<ISyncCommitService, SyncCommitService>();
        builder.Services.AddScoped<ISyncUploadService, SyncUploadService>();
        builder.Services.AddScoped<ISyncStartService, SyncStartService>();
        builder.Services.AddScoped<ISyncCompleteService, SyncCompleteService>();
        builder.Services.AddScoped<ISyncCancelService, SyncCancelService>();
        builder.Services.AddScoped<ISyncPendingActionsService, SyncPendingActionsService>();
        builder.Services.AddScoped<ISyncDeviceSongsService, SyncDeviceSongsService>();
        builder.Services.AddScoped<ISyncCheckService, SyncCheckService>();
        builder.Services.AddScoped<ISyncResolveConflictsService, SyncResolveConflictsService>();
        builder.Services.AddScoped<ISyncReportErrorService, SyncReportErrorService>();
        builder.Services.AddScoped<ISyncAcknowledgeService, SyncAcknowledgeService>();

        // Image and metadata services
        builder.Services.AddScoped<IImageCacheService, ImageCacheService>();
        builder.Services.AddScoped<IThumbnailProxyService, ThumbnailProxyService>();
        builder.Services.AddScoped<IImageComparisonService, ImageComparisonService>();
        builder.Services.AddScoped<ISoundalikeMergeService, SoundalikeMergeService>();
        builder.Services.AddScoped<ISoundalikeResolutionService, SoundalikeResolutionService>();
        builder.Services.AddScoped<MetadataDiffBuilder>();

        // Audit services
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
        builder.Services.AddScoped<IAuditRuleFieldMapper, AuditRuleFieldMapper>();

        // Fingerprint and audio integrity
        builder.Services.AddSingleton<IFpcalcService, FpcalcService>();
        builder.Services.AddScoped<AcousticFingerprintService>();
        builder.Services.AddSingleton<IAudioIntegrityService, AudioIntegrityService>();
        builder.Services.AddSingleton<IAudioIntegrityValidator, Mp3IntegrityValidator>();
        builder.Services.AddSingleton<IFFmpegRunner, FFmpegRunner>();

        // Seed and count
        builder.Services.AddScoped<ISeedService, SeedService>();
        builder.Services.AddScoped<ICountRecalculationService, CountRecalculationService>();

        // DbContext and configuration
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
        builder.Services.Configure<ThumbnailCacheConfig>(builder.Configuration.GetSection("ThumbnailCache"));

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