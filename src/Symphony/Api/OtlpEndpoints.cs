using Microsoft.AspNetCore.SignalR;
using Symphony.Dto;
using Symphony.Hubs;
using Symphony.Services;
using System.Text.Json;

namespace Symphony.Api;

public static class OtlpEndpoints
{
    public static IEndpointRouteBuilder MapOtlpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/v1/traces", (HttpRequest request, JobTelemetryCapture capture, IHubContext<JobUpdateHub> hub, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
            HandleAsync(request, capture, hub, loggerFactory.CreateLogger("OtlpEndpoints"), OtlpKind.Traces, cancellationToken));

        endpoints.MapPost("/v1/metrics", (HttpRequest request, JobTelemetryCapture capture, IHubContext<JobUpdateHub> hub, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
            HandleAsync(request, capture, hub, loggerFactory.CreateLogger("OtlpEndpoints"), OtlpKind.Metrics, cancellationToken));

        endpoints.MapPost("/v1/logs", (HttpRequest request, JobTelemetryCapture capture, IHubContext<JobUpdateHub> hub, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
            HandleAsync(request, capture, hub, loggerFactory.CreateLogger("OtlpEndpoints"), OtlpKind.Logs, cancellationToken));

        // Tolerate a common prefixed base URL (e.g. http://host/otlp).
        endpoints.MapPost("/otlp/v1/traces", (HttpRequest request, JobTelemetryCapture capture, IHubContext<JobUpdateHub> hub, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
            HandleAsync(request, capture, hub, loggerFactory.CreateLogger("OtlpEndpoints"), OtlpKind.Traces, cancellationToken));

        endpoints.MapPost("/otlp/v1/metrics", (HttpRequest request, JobTelemetryCapture capture, IHubContext<JobUpdateHub> hub, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
            HandleAsync(request, capture, hub, loggerFactory.CreateLogger("OtlpEndpoints"), OtlpKind.Metrics, cancellationToken));

        endpoints.MapPost("/otlp/v1/logs", (HttpRequest request, JobTelemetryCapture capture, IHubContext<JobUpdateHub> hub, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
            HandleAsync(request, capture, hub, loggerFactory.CreateLogger("OtlpEndpoints"), OtlpKind.Logs, cancellationToken));

        return endpoints;
    }

    static async Task<IResult> HandleAsync(
        HttpRequest request,
        JobTelemetryCapture capture,
        IHubContext<JobUpdateHub> hub,
        ILogger logger,
        OtlpKind kind,
        CancellationToken cancellationToken)
    {
        string body;
        try
        {
            using var reader = new StreamReader(request.Body);
            body = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read OTLP request body.");
            return Results.Ok();
        }

        if (string.IsNullOrWhiteSpace(body))
            return Results.Ok();

        Guid? jobId;
        try
        {
            using var document = JsonDocument.Parse(body);
            jobId = JobTelemetryCapture.TryGetJobId(document.RootElement);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid JSON in OTLP payload.");
            return Results.Ok();
        }

        if (jobId is null || !capture.IsActive(jobId.Value))
        {
            // Acknowledge to avoid SDK retry storms.
            return Results.Ok();
        }

        List<JobTelemetryEntry> entries;
        try
        {
            entries = kind switch
            {
                OtlpKind.Traces => JobTelemetryCapture.ParseOtlpTraces(body),
                OtlpKind.Metrics => JobTelemetryCapture.ParseOtlpMetrics(body),
                OtlpKind.Logs => JobTelemetryCapture.ParseOtlpLogs(body),
                _ => []
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse OTLP payload.");
            return Results.Ok();
        }

        foreach (var entry in entries)
        {
            capture.AppendEntry(jobId.Value, entry);
            _ = hub.Clients
                .Group($"telemetry:{jobId.Value}")
                .SendAsync("TelemetryReceived", jobId.Value, entry, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.Exception is not null)
                        logger.LogWarning(task.Exception, "Failed to push telemetry over SignalR.");
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        return Results.Ok();
    }

    enum OtlpKind
    {
        Traces,
        Metrics,
        Logs
    }
}
