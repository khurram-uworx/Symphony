using Symphony.Data;
using Symphony.Dto;
using Symphony.Processing;
using Symphony.Services;
using System.Security.Claims;

namespace Symphony.Api;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/jobs").RequireRateLimiting(ApiRateLimitPolicy.PolicyName);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/telemetry", GetTelemetryAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/retry", RetryAsync);

        return endpoints;
    }

    public static IEndpointRouteBuilder MapJobApi(this IEndpointRouteBuilder endpoints) =>
        MapJobEndpoints(endpoints);

    public static IEndpointRouteBuilder MapJobs(this IEndpointRouteBuilder endpoints) =>
        MapJobEndpoints(endpoints);

    static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        JobService jobService,
        string? state = null,
        string? status = null,
        string? owner = null,
        string? ownerId = null,
        string? ownerUserId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? startDate = null,
        DateTimeOffset? to = null,
        DateTimeOffset? endDate = null,
        int? page = null,
        int? pageSize = null,
        int? size = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var isAdmin = IsAdmin(user);
        if (!isAdmin)
            ownerUserId = userId;

        var resolvedOwner = FirstNonEmpty(owner, ownerId, ownerUserId);
        var resolvedState = FirstNonEmpty(state, status);
        var resolvedFrom = from ?? startDate;
        var resolvedTo = to ?? endDate;
        var resolvedPageSize = pageSize ?? size ?? 50;
        if (resolvedPageSize <= 0)
            resolvedPageSize = 50;

        var jobs = await jobService.QueryJobsAsync(
            resolvedOwner,
            resolvedState,
            resolvedFrom,
            resolvedTo,
            page,
            resolvedPageSize,
            cancellationToken);
        var total = await jobService.CountJobsAsync(
            resolvedOwner,
            resolvedState,
            resolvedFrom,
            resolvedTo,
            cancellationToken);

        var dtos = jobs.Select(job => JobStatusDto.From(job)).ToList();
        return Results.Ok(dtos);
    }

    static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal user,
        JobService jobService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var isAdmin = IsAdmin(user);
        var job = isAdmin
            ? await jobService.GetJobAsync(id, cancellationToken)
            : await jobService.GetJobAsync(userId, id, false, cancellationToken);

        if (job is null || (!isAdmin && job.OwnerUserId != userId))
            return Results.NotFound(new { error = "Job not found." });

        return Results.Ok(JobStatusDto.From(job, job.Issues));
    }

    static async Task<IResult> GetTelemetryAsync(
        Guid id,
        ClaimsPrincipal user,
        JobService jobService,
        JobTelemetryCapture? jobTelemetryCapture,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var isAdmin = IsAdmin(user);
        var job = isAdmin
            ? await jobService.GetJobAsync(id, cancellationToken)
            : await jobService.GetJobAsync(userId, id, false, cancellationToken);

        if (job is null || (!isAdmin && job.OwnerUserId != userId))
            return Results.NotFound(new { error = "Job not found." });

        if (jobTelemetryCapture is null)
            return Results.Ok(new JobTelemetryBundle());

        var bundle = await jobTelemetryCapture.ReadAsync(id, cancellationToken);
        return Results.Ok(bundle);
    }

    static async Task<IResult> DeleteAsync(
        Guid id,
        ClaimsPrincipal user,
        JobService jobService,
        JobProcessor? jobProcessor,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var isAdmin = IsAdmin(user);
        var job = isAdmin
            ? await jobService.GetJobAsync(id, cancellationToken)
            : await jobService.GetJobAsync(userId, id, false, cancellationToken);

        if (job is null || (!isAdmin && job.OwnerUserId != userId))
            return Results.NotFound(new { error = "Job not found." });

        if (job.State is "Completed" or "CompletedWithFailures" or "Failed" or "Cancelled")
            return Results.Conflict(new { error = $"Job is already {job.State}." });

        jobProcessor?.Cancel(id);
        var cancelled = await jobService.CancelAsync(id, cancellationToken);
        if (!cancelled)
            return Results.Conflict(new { error = "Job could not be cancelled." });

        return Results.Ok(new { jobId = id, cancelled = true });
    }

    static async Task<IResult> RetryAsync(
        Guid id,
        ClaimsPrincipal user,
        JobService jobService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var isAdmin = IsAdmin(user);

        try
        {
            var retryJob = await jobService.CreateRetryAsync(id, userId, isAdmin, cancellationToken);
            return Results.Accepted($"/api/jobs/{retryJob.Id}", new { jobId = retryJob.Id, statusUrl = $"/api/jobs/{retryJob.Id}", retryOfJobId = id });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = "Job not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    static bool IsAdmin(ClaimsPrincipal user) =>
        user.IsInRole(AdminSeeder.AdminRole) ||
        user.IsInRole(Symphony.Services.UserManagementService.AdminSeeder.AdminRole);

    static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }

    static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? user.Identity?.Name;
}

