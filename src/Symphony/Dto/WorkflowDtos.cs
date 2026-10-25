namespace Symphony.Dto;

public class WorkflowDto
{
    public string Id { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static WorkflowDto From(Symphony.Data.Workflow source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new WorkflowDto
        {
            Id = source.Id,
            OwnerUserId = source.OwnerUserId,
            Name = source.Name,
            Content = source.Content,
            IsDefault = source.IsDefault,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }

    public Symphony.Data.Workflow ToWorkflow(string? ownerUserId = null)
    {
        return new Symphony.Data.Workflow
        {
            Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString() : Id,
            OwnerUserId = ownerUserId ?? OwnerUserId,
            Name = Name,
            Content = Content,
            IsDefault = IsDefault,
            CreatedAt = CreatedAt == default ? DateTimeOffset.UtcNow : CreatedAt,
            UpdatedAt = UpdatedAt == default ? DateTimeOffset.UtcNow : UpdatedAt
        };
    }

    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Workflow name cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            error = "Workflow content cannot be empty.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool IsValid(WorkflowDto? dto, out string? error)
    {
        if (dto is null)
        {
            error = "Request body is required.";
            return false;
        }

        return dto.IsValid(out error);
    }
}

public sealed class WorkflowResponse : WorkflowDto { }

public sealed class WorkflowModel : WorkflowDto { }

public sealed class WorkflowRequest : WorkflowDto { }

public sealed class WorkflowView : WorkflowDto { }

public sealed class WorkflowItem : WorkflowDto { }

public sealed class CreateWorkflowRequest : WorkflowDto { }

public sealed class UpdateWorkflowRequest : WorkflowDto { }

public sealed class SaveWorkflowRequest : WorkflowDto { }

public sealed class WorkflowCreateRequest : WorkflowDto { }

public sealed class WorkflowUpdateRequest : WorkflowDto { }