namespace Symphony.Agent;

class LiveSession
{
    public string? SessionId { get; set; }
    public string? ThreadId { get; set; }
    public string? TurnId { get; set; }
    public string? CodexAppServerPid { get; set; }
    public string? LastCodexEvent { get; set; }
    public DateTimeOffset? LastCodexTimestamp { get; set; }
    public string? LastCodexMessage { get; set; }
    public int CodexInputTokens { get; set; }
    public int CodexOutputTokens { get; set; }
    public int CodexTotalTokens { get; set; }
    public int LastReportedInputTokens { get; set; }
    public int LastReportedOutputTokens { get; set; }
    public int LastReportedTotalTokens { get; set; }
    public int TurnCount { get; set; }
}
