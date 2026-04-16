// This file is deprecated. Use ModularSyncService from MyMusic.CLI.Services.Sync namespace instead.
// The implementation has been refactored into a modular architecture.
// This file is kept for reference and will be removed in a future version.

#if false
using System.IO.Abstractions;
using System.IO.Hashing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;
using MyMusic.Common.Services.Sync.Types;
using Refit;
using Path = System.IO.Path;
using SyncDirection = MyMusic.Common.Services.Sync.Types.SyncDirection;
using SyncProgress = MyMusic.Common.Services.Sync.Types.SyncProgress;
using SyncResult = MyMusic.Common.Services.Sync.Types.SyncResult;

namespace MyMusic.CLI.Services;

[Obsolete("Use ModularSyncService from MyMusic.CLI.Services.Sync namespace instead. This class will be removed in a future version.")]
public class SyncService(
    IMyMusicClient client,
    IFileScanner fileScanner,
    IFileSystem fileSystem,
    IOptions<MyMusicOptions> options,
    ILogger<SyncService> logger) : ISyncService
{
    // Original implementation has been moved to modular architecture.
    // See MyMusic.CLI/Services/Sync/ for the new implementation.
}
#endif
