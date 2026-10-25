namespace Symphony.Data;

public sealed class JobTelemetry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobId { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string Signal { get; set; } = string.Empty;

    public string? TraceId { get; set; }

    public string? SpanId { get; set; }

    public string? ParentSpanId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Severity { get; set; }

    public long? DurationMs { get; set; }

    public string? PayloadJson { get; set; }

    public Job? Job { get; set; }
}