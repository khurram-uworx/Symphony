using Microsoft.EntityFrameworkCore;
using Symphony.Data;
using Symphony.Dto;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Symphony.Services;

public sealed class JobTelemetryCapture
{
    readonly IDbContextFactory<SymphonyDbContext> dbFactory;
    readonly string receiverBaseUrl;
    readonly AsyncLocal<Guid?> currentJob = new();
    readonly ConcurrentDictionary<Guid, ConcurrentQueue<JobTelemetryEntry>> jobBuffers = new();
    readonly ConcurrentDictionary<Guid, bool> jobContent = new();

    public JobTelemetryCapture(IDbContextFactory<SymphonyDbContext> dbFactory, string receiverBaseUrl)
    {
        this.dbFactory = dbFactory;
        this.receiverBaseUrl = string.IsNullOrWhiteSpace(receiverBaseUrl) ? "http://127.0.0.1:5080" : receiverBaseUrl.TrimEnd('/');
    }

    public Guid? CurrentJobId => currentJob.Value;

    public string ReceiverBaseUrl => receiverBaseUrl;

    public string? CurrentFilePath => null;

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
        jobBuffers[jobId] = new ConcurrentQueue<JobTelemetryEntry>();
        jobContent[jobId] = captureContent;
    }

    public void EndJobScope() => currentJob.Value = null;

    public bool IsActive(Guid jobId) => jobBuffers.ContainsKey(jobId);

    public void AppendEntry(Guid jobId, JobTelemetryEntry entry)
    {
        if (jobBuffers.TryGetValue(jobId, out var queue))
            queue.Enqueue(entry);
    }

    public async Task<JobTelemetryBundle> ReadAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (jobBuffers.TryGetValue(jobId, out var queue))
        {
            var parsed = new JobTelemetryBundle
            {
                Entries = queue.ToArray()
                    .OrderByDescending(entry => entry.Timestamp)
                    .Take(100)
                    .ToList()
            };
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
            var bundle = new JobTelemetryBundle();
            if (jobBuffers.TryRemove(jobId, out var queue))
                bundle.Entries = queue.ToArray().ToList();

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

            await ApplyRetentionAsync(jobId, db, cancellationToken);
        }
        finally
        {
            jobContent.TryRemove(jobId, out _);
            if (currentJob.Value == jobId)
                currentJob.Value = null;
        }
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

    public static Guid? TryGetJobId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = TryGetJobId(item);
                if (found is not null)
                    return found;
            }
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, "resourceSpans", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "resourceMetrics", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "resourceLogs", StringComparison.OrdinalIgnoreCase))
            {
                var found = TryGetJobId(property.Value);
                if (found is not null)
                    return found;
            }

            if (string.Equals(property.Name, "resource", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.TryGetProperty("attributes", out var attributes))
                {
                    foreach (var attribute in attributes.EnumerateArray())
                    {
                        if (!attribute.TryGetProperty("key", out var key) || key.GetString() != "job.id")
                            continue;

                        if (attribute.TryGetProperty("value", out var value) &&
                            value.TryGetProperty("stringValue", out var stringValue))
                        {
                            var text = stringValue.GetString();
                            if (Guid.TryParse(text, out var jobId))
                                return jobId;
                        }
                    }
                }
            }

            var nested = TryGetJobId(property.Value);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    public static Guid? TryGetJobId(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return TryGetJobId(document.RootElement);
        }
        catch
        {
            return null;
        }
    }

    public static List<JobTelemetryEntry> ParseOtlpTraces(string json)
    {
        var entries = new List<JobTelemetryEntry>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("resourceSpans", out var resourceSpans) ||
                resourceSpans.ValueKind != JsonValueKind.Array)
            {
                return entries;
            }

            foreach (var resourceSpan in resourceSpans.EnumerateArray())
            {
                if (!resourceSpan.TryGetProperty("scopeSpans", out var scopeSpans) ||
                    scopeSpans.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var scopeSpan in scopeSpans.EnumerateArray())
                {
                    if (!scopeSpan.TryGetProperty("spans", out var spans) ||
                        spans.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var span in spans.EnumerateArray())
                    {
                        var start = FromUnixNanos(GetString(span, "startTimeUnixNano"));
                        var end = FromUnixNanos(GetString(span, "endTimeUnixNano"));
                        long? duration = null;
                        if (GetString(span, "startTimeUnixNano") is string startText &&
                            GetString(span, "endTimeUnixNano") is string endText &&
                            long.TryParse(startText, out var startNanos) &&
                            long.TryParse(endText, out var endNanos))
                        {
                            duration = (long)Math.Round((endNanos - startNanos) / 1_000_000.0);
                        }

                        var severity = span.TryGetProperty("status", out var status) && status.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number
                            ? code.GetInt32() switch { 1 => "unset", 2 => "error", _ => "ok" }
                            : null;

                        entries.Add(new JobTelemetryEntry
                        {
                            Signal = "span",
                            Timestamp = start,
                            TraceId = GetString(span, "traceId"),
                            SpanId = GetString(span, "spanId"),
                            ParentSpanId = GetString(span, "parentSpanId"),
                            Name = GetString(span, "name") ?? "span",
                            Severity = severity,
                            DurationMs = duration,
                            PayloadJson = span.GetRawText()
                        });
                    }
                }
            }
        }
        catch
        {
        }

        return entries;
    }

    public static List<JobTelemetryEntry> ParseOtlpMetrics(string json)
    {
        var entries = new List<JobTelemetryEntry>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("resourceMetrics", out var resourceMetrics) ||
                resourceMetrics.ValueKind != JsonValueKind.Array)
            {
                return entries;
            }

            foreach (var resourceMetric in resourceMetrics.EnumerateArray())
            {
                if (!resourceMetric.TryGetProperty("scopeMetrics", out var scopeMetrics) ||
                    scopeMetrics.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var scopeMetric in scopeMetrics.EnumerateArray())
                {
                    if (!scopeMetric.TryGetProperty("metrics", out var metrics) ||
                        metrics.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var metric in metrics.EnumerateArray())
                    {
                        var name = GetString(metric, "name") ?? "metric";
                        if (metric.TryGetProperty("sum", out var sum) && sum.TryGetProperty("dataPoints", out var points))
                            AddMetricPoints(entries, name, points);
                        else if (metric.TryGetProperty("gauge", out var gauge) && gauge.TryGetProperty("dataPoints", out var gaugePoints))
                            AddMetricPoints(entries, name, gaugePoints);
                        else if (metric.TryGetProperty("histogram", out var histogram) && histogram.TryGetProperty("dataPoints", out var histogramPoints))
                            AddMetricPoints(entries, name, histogramPoints);
                    }
                }
            }
        }
        catch
        {
        }

        return entries;
    }

    public static List<JobTelemetryEntry> ParseOtlpLogs(string json)
    {
        var entries = new List<JobTelemetryEntry>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("resourceLogs", out var resourceLogs) ||
                resourceLogs.ValueKind != JsonValueKind.Array)
            {
                return entries;
            }

            foreach (var resourceLog in resourceLogs.EnumerateArray())
            {
                if (!resourceLog.TryGetProperty("scopeLogs", out var scopeLogs) ||
                    scopeLogs.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var scopeLog in scopeLogs.EnumerateArray())
                {
                    if (!scopeLog.TryGetProperty("logRecords", out var logRecords) ||
                        logRecords.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var record in logRecords.EnumerateArray())
                    {
                        var timestamp = FromUnixNanos(GetString(record, "timeUnixNano"));
                        var severity = GetString(record, "severityText");
                        string? name = null;
                        if (record.TryGetProperty("body", out var body) && body.TryGetProperty("stringValue", out var bodyValue))
                            name = bodyValue.GetString();
                        entries.Add(new JobTelemetryEntry
                        {
                            Signal = "log",
                            Timestamp = timestamp,
                            TraceId = GetString(record, "traceId"),
                            SpanId = GetString(record, "spanId"),
                            Name = string.IsNullOrWhiteSpace(name) ? "log" : name!,
                            Severity = severity,
                            PayloadJson = record.GetRawText()
                        });
                    }
                }
            }
        }
        catch
        {
        }

        return entries;
    }

    static void AddMetricPoints(List<JobTelemetryEntry> entries, string name, JsonElement points)
    {
        if (points.ValueKind != JsonValueKind.Array)
            return;

        foreach (var point in points.EnumerateArray())
        {
            DateTimeOffset timestamp = DateTimeOffset.UtcNow;
            if (GetString(point, "timeUnixNano") is string timeText && long.TryParse(timeText, out var nanos))
                timestamp = DateTimeOffset.FromUnixTimeMilliseconds(nanos / 1_000_000).AddTicks((nanos % 1_000_000) / 100);

            entries.Add(new JobTelemetryEntry
            {
                Signal = "metric",
                Timestamp = timestamp,
                Name = name,
                PayloadJson = point.GetRawText()
            });
        }
    }

    static DateTimeOffset FromUnixNanos(string? value)
    {
        if (value is not null && long.TryParse(value, out var nanos))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(nanos / 1_000_000).AddTicks((nanos % 1_000_000) / 100);
        }

        return DateTimeOffset.UtcNow;
    }

    static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }
}
