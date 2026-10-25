using Microsoft.EntityFrameworkCore;
using Symphony.Data;
using Symphony.Dto;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Symphony.Services;

public sealed class JobTelemetryCapture
{
    readonly IDbContextFactory<SymphonyDbContext> dbFactory;
    readonly string rootPath;
    readonly AsyncLocal<Guid?> currentJob = new();
    readonly ConcurrentDictionary<Guid, string> jobFiles = new();
    readonly ConcurrentDictionary<Guid, bool> jobContent = new();

    public JobTelemetryCapture(IDbContextFactory<SymphonyDbContext> dbFactory)
    {
        this.dbFactory = dbFactory;
        rootPath = Path.Combine(Path.GetTempPath(), "symphony-telemetry");
        Directory.CreateDirectory(rootPath);
    }

    public string? CurrentFilePath
    {
        get
        {
            if (currentJob.Value is not Guid jobId)
                return null;

            return jobFiles.TryGetValue(jobId, out var path) ? path : null;
        }
    }

    public bool CurrentContentEnabled
    {
        get
        {
            if (currentJob.Value is not Guid jobId)
                return false;

            return jobContent.TryGetValue(jobId, out var enabled) && enabled;
        }
    }

    public void BeginJob(Guid jobId, bool captureContent)
    {
        currentJob.Value = jobId;
        var path = Path.Combine(rootPath, jobId + ".jsonl");
        jobFiles[jobId] = path;
        jobContent[jobId] = captureContent;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    public void EndJobScope() => currentJob.Value = null;

    public async Task<JobTelemetryBundle> ReadAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (jobFiles.TryGetValue(jobId, out var path) && File.Exists(path))
        {
            var parsed = ParseFile(path);
            parsed.Entries = parsed.Entries
                .OrderByDescending(entry => entry.Timestamp)
                .Take(100)
                .ToList();
            return parsed;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.JobTelemetry
            .AsNoTracking()
            .Where(item => item.JobId == jobId)
            .OrderByDescending(item => item.Timestamp)
            .Take(100)
            .ToListAsync(cancellationToken);

        return new JobTelemetryBundle
        {
            Entries = rows
                .Select(row => new JobTelemetryEntry
                {
                    Signal = row.Signal,
                    Timestamp = row.Timestamp,
                    TraceId = row.TraceId,
                    SpanId = row.SpanId,
                    ParentSpanId = row.ParentSpanId,
                    Name = row.Name,
                    Severity = row.Severity,
                    DurationMs = row.DurationMs,
                    PayloadJson = row.PayloadJson
                }).ToList()
        };
    }

    public async Task FinalizeAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            if (!jobFiles.TryGetValue(jobId, out var path))
                return;

            var bundle = File.Exists(path) ? ParseFile(path) : new JobTelemetryBundle();
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            await db.JobTelemetry
                .Where(item => item.JobId == jobId)
                .ExecuteDeleteAsync(cancellationToken);

            if (bundle.Entries.Count > 0)
            {
                var rows = bundle.Entries.Select(entry => new JobTelemetry
                {
                    JobId = jobId,
                    Timestamp = entry.Timestamp == default ? DateTimeOffset.UtcNow : entry.Timestamp,
                    Signal = string.IsNullOrWhiteSpace(entry.Signal) ? "span" : entry.Signal,
                    TraceId = entry.TraceId,
                    SpanId = entry.SpanId,
                    ParentSpanId = entry.ParentSpanId,
                    Name = string.IsNullOrWhiteSpace(entry.Name) ? entry.Signal : entry.Name,
                    Severity = entry.Severity,
                    DurationMs = entry.DurationMs,
                    PayloadJson = entry.PayloadJson
                }).ToList();

                db.JobTelemetry.AddRange(rows);
                await db.SaveChangesAsync(cancellationToken);
            }

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }

            await ApplyRetentionAsync(jobId, db, cancellationToken);
        }
        finally
        {
            jobFiles.TryRemove(jobId, out _);
            jobContent.TryRemove(jobId, out _);
            if (currentJob.Value == jobId)
                currentJob.Value = null;
        }
    }

    static JobTelemetryBundle ParseFile(string path)
    {
        var bundle = new JobTelemetryBundle();
        try
        {
            using var reader = new StreamReader(path);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var signal = GetString(root, "type") ?? "event";
                    var entry = new JobTelemetryEntry
                    {
                        Signal = signal,
                        Timestamp = ParseTimestamp(root),
                        TraceId = GetString(root, "traceId"),
                        SpanId = GetString(root, "spanId"),
                        ParentSpanId = GetString(root, "parentSpanId"),
                        Name = GetString(root, "name") ?? signal,
                        Severity = GetString(root, "severity") ?? GetString(root, "level"),
                        DurationMs = ParseDuration(root),
                        PayloadJson = line
                    };

                    bundle.Entries.Add(entry);
                }
                catch
                {
                    bundle.Truncated = true;
                }
            }
        }
        catch
        {
        }

        return bundle;
    }

    async Task ApplyRetentionAsync(Guid jobId, SymphonyDbContext db, CancellationToken cancellationToken)
    {
        var ownerUserId = await db.Jobs
            .AsNoTracking()
            .Where(job => job.Id == jobId)
            .Select(job => job.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(ownerUserId))
            return;

        var retainedJobIds = await db.JobTelemetry
            .AsNoTracking()
            .Join(db.Jobs, telemetry => telemetry.JobId, job => job.Id, (telemetry, job) => new { telemetry.JobId, job.OwnerUserId, job.QueuedAt })
            .Where(item => item.OwnerUserId == ownerUserId)
            .GroupBy(item => item.JobId)
            .Select(group => new { JobId = group.Key, QueuedAt = group.Max(item => item.QueuedAt) })
            .OrderByDescending(item => item.QueuedAt)
            .Select(item => item.JobId)
            .Take(20)
            .ToListAsync(cancellationToken);

        var retained = retainedJobIds.ToHashSet();
        var staleJobIds = await db.JobTelemetry
            .AsNoTracking()
            .Join(db.Jobs, telemetry => telemetry.JobId, job => job.Id, (telemetry, job) => new { telemetry.JobId, job.OwnerUserId })
            .Where(item => item.OwnerUserId == ownerUserId && !retained.Contains(item.JobId))
            .Select(item => item.JobId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (staleJobIds.Count == 0)
            return;

        await db.JobTelemetry
            .Where(item => staleJobIds.Contains(item.JobId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    static DateTimeOffset ParseTimestamp(JsonElement element)
    {
        if (element.TryGetProperty("startTime", out var startTime))
        {
            if (startTime.ValueKind == JsonValueKind.Array && startTime.GetArrayLength() >= 2 &&
                startTime[0].TryGetInt64(out var seconds) && startTime[1].TryGetInt64(out var nanos))
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nanos / 100);
            }

            if (startTime.ValueKind == JsonValueKind.String && long.TryParse(startTime.GetString(), out var epoch))
                return FromUnixTimeSecondsSafe(epoch);
        }

        if (element.TryGetProperty("timestamp", out var timestamp) && timestamp.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(timestamp.GetString(), out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow;
    }

    static long? ParseDuration(JsonElement element)
    {
        DateTimeOffset? start = null;
        DateTimeOffset? end = null;

        if (element.TryGetProperty("startTime", out var startTime) && startTime.ValueKind == JsonValueKind.Array && startTime.GetArrayLength() >= 2 &&
            startTime[0].TryGetInt64(out var startSeconds) && startTime[1].TryGetInt64(out var startNanos))
        {
            start = DateTimeOffset.FromUnixTimeSeconds(startSeconds).AddTicks(startNanos / 100);
        }

        if (element.TryGetProperty("endTime", out var endTime) && endTime.ValueKind == JsonValueKind.Array && endTime.GetArrayLength() >= 2 &&
            endTime[0].TryGetInt64(out var endSeconds) && endTime[1].TryGetInt64(out var endNanos))
        {
            end = DateTimeOffset.FromUnixTimeSeconds(endSeconds).AddTicks(endNanos / 100);
        }

        if (start is null || end is null)
            return null;

        return (long)Math.Round((end.Value - start.Value).TotalMilliseconds);
    }

    static DateTimeOffset FromUnixTimeSecondsSafe(long epoch)
    {
        try
        {
            return epoch > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                : DateTimeOffset.FromUnixTimeSeconds(epoch);
        }
        catch
        {
            return DateTimeOffset.UtcNow;
        }
    }
}
