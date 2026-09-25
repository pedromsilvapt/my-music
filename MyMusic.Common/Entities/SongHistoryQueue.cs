using System.ComponentModel.DataAnnotations;
using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Entities;

public class SongHistoryQueue
{
    public long Id { get; set; }

    public long SongId { get; set; }

    public int SongRevision { get; set; }

    /// <summary>
    /// PostgreSQL transaction ID (<c>txid_current()</c>) at the time the trigger
    /// enqueued this row. All queue entries produced within the same DB
    /// transaction share this value, enabling the worker to compact them into a
    /// single <see cref="SongHistory"/> row. Null for rows created before the
    /// migration that introduced this column.
    /// </summary>
    public long? TransactionId { get; set; }

    public required SongSnapshot Data { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int ErrorCount { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }
}