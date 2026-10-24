using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Symphony.Data;
using Symphony.Dto;
using Symphony.Services;

namespace Symphony.Api;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workflows");

        group.MapGet("/", ListAsync);
        group.MapGet("/{name}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{name}", UpdateAsync);
        group.MapDelete("/{name}", DeleteAsync);

        return endpoints;
    }

    public static IEndpointRouteBuilder MapWorkflowApi(this IEndpointRouteBuilder endpoints) =>
        MapWorkflowEndpoints(endpoints);

    public static IEndpointRouteBuilder MapWorkflows(this IEndpointRouteBuilder endpoints) =>
        MapWorkflowEndpoints(endpoints);

    public static IEndpointRouteBuilder Map(this IEndpointRouteBuilder endpoints) =>
        MapWorkflowEndpoints(endpoints);

    public static IEndpointRouteBuilder MapApi(this IEndpointRouteBuilder endpoints) =>
        MapWorkflowEndpoints(endpoints);

    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints) =>
        MapWorkflowEndpoints(endpoints);

    public static IEndpointRouteBuilder RegisterEndpoints(this IEndpointRouteBuilder endpoints) =>
        MapWorkflowEndpoints(endpoints);

    static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        WorkflowService workflowService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var workflows = await workflowService.ListAsync(userId, cancellationToken);
        return Results.Ok(workflows.Select(WorkflowDto.From).ToList());
    }

    static async Task<IResult> GetAsync(
        string name,
        ClaimsPrincipal user,
        WorkflowService workflowService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var workflow = await workflowService.GetAsync(userId, name, cancellationToken);
        if (workflow is null)
            return Results.NotFound(new { error = "Workflow not found." });

        return Results.Ok(WorkflowDto.From(workflow));
    }

    static async Task<IResult> CreateAsync(
        ClaimsPrincipal user,
        WorkflowDto? request,
        WorkflowService workflowService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        if (!WorkflowDto.IsValid(request, out var validationError))
            return Results.BadRequest(new { error = validationError });

        try
        {
            var created = await workflowService.CreateAsync(
                userId,
                request!.Name,
                request.Content,
                request.IsDefault,
                cancellationToken);

            return Results.Ok(WorkflowDto.From(created));
        }
        catch (WorkflowConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = exception.Code });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    static async Task<IResult> UpdateAsync(
        string name,
        ClaimsPrincipal user,
        WorkflowDto? request,
        WorkflowService workflowService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        if (request is null)
            return Results.BadRequest(new { error = "Request body is required." });

        if (string.IsNullOrWhiteSpace(request.Content))
            return Results.BadRequest(new { error = "Workflow content cannot be empty." });

        try
        {
            var updated = await workflowService.UpdateAsync(
                userId,
                name,
                new Symphony.Data.Workflow
                {
                    Name = string.IsNullOrWhiteSpace(request.Name) ? name : request.Name,
                    Content = request.Content,
                    IsDefault = request.IsDefault
                },
                cancellationToken);

            if (updated is null)
                return Results.NotFound(new { error = "Workflow not found." });

            return Results.Ok(WorkflowDto.From(updated));
        }
        catch (WorkflowConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = exception.Code });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    static async Task<IResult> DeleteAsync(
        string name,
        ClaimsPrincipal user,
        WorkflowService workflowService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        try
        {
            var deleted = await workflowService.DeleteAsync(userId, name, cancellationToken);
            if (!deleted)
                return Results.NotFound(new { error = "Workflow not found." });

            return Results.Ok(new { deleted = true });
        }
        catch (WorkflowConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message, code = exception.Code });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    static string? GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? user.Identity?.Name;
}