using System.Text.Json.Serialization;

namespace MyMusic.CLI.Services.Sync;

/// <summary>
/// Source-generated JSON metadata for the free-form <c>Data</c> payloads of sync records (required for Native AOT).
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RenameData))]
[JsonSerializable(typeof(Phases.SyncCheckCreateUpdateData))]
[JsonSerializable(typeof(ConflictCheckData))]
[JsonSerializable(typeof(UpdateLocalCheckData))]
internal partial class SyncDataJsonContext : JsonSerializerContext;
