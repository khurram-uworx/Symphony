namespace Symphony.IssueTracker;

sealed class SubmissionContext
{
    public List<string> TicketIds { get; set; } = [];

    public List<object> WorkItems { get; set; } = [];
}
