using GitHub.Copilot;

namespace Symphony.CodingAgentsProbe;

internal static class ByomProbe
{
    // Verified against https://api.x.ai/v1 with GitHub.Copilot.SDK 1.0.26.
    // Wire API must be completions. ApiKey is read from the environment; do not hard-code it.
    const string DefaultModel = "grok-4.20-non-reasoning";
    const string DefaultBaseUrl = "https://api.x.ai/v1";
    const string DefaultWireApi = "completions";

    public static async Task<int> RunAsync(string[] args)
    {
        // args: [model] [baseUrl] [wireApi] [question...]
        var model = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("COPILOT_MODEL") ?? DefaultModel;
        var baseUrl = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("XAI_BASE_URL") ?? DefaultBaseUrl;
        var wireApi = args.Length > 2 ? args[2] : Environment.GetEnvironmentVariable("COPILOT_PROVIDER_WIRE_API") ?? DefaultWireApi;
        var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
        var question = args.Length > 3 ? string.Join(" ", args.Skip(3)) : "Say hello in one sentence.";

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("XAI_API_KEY is required.");

        Console.WriteLine("=== Copilot SDK BYOM ===");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine($"Base URL: {baseUrl}");
        Console.WriteLine($"Provider: openai");
        Console.WriteLine($"Wire API: {wireApi}");
        Console.WriteLine("Authentication: ApiKey (XAI_API_KEY)");
        Console.WriteLine($"Question: {question}");
        Console.WriteLine();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var provider = new ProviderConfig
        {
            Type = "openai",
            BaseUrl = baseUrl.TrimEnd('/') + "/",
            WireApi = wireApi,
            ModelId = model,
            WireModel = model,
            ApiKey = apiKey,
            MaxPromptTokens = 8000,
            MaxOutputTokens = 512,
        };

        await using var client = new CopilotClient(new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForStdio("copilot"),
            WorkingDirectory = AppContext.BaseDirectory
        });
        await client.StartAsync(cts.Token);

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            ClientName = "coding-agents-probe-byom",
            Model = model,
            Provider = provider,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = "Answer every user question in English. Be concise and factual."
            },
            AvailableTools = [],
            Tools = [],
            EnableConfigDiscovery = false,
            EnableSkills = false,
            EnableHostGitOperations = false
        }, cts.Token);

        string? answer = null;
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var events = session.On<SessionEvent>(evt =>
        {
            if (evt is AssistantMessageEvent message && !string.IsNullOrWhiteSpace(message.Data?.Content))
                answer = message.Data.Content;
            if (evt is SessionIdleEvent && !string.IsNullOrWhiteSpace(answer))
                idle.TrySetResult();
        });

        // SendAndWaitAsync does not return after a BYOM turn, even though the assistant message and session.idle events arrive.
        var send = session.SendAndWaitAsync(question, TimeSpan.FromMinutes(2), cts.Token);
        var completed = await Task.WhenAny(send, idle.Task);
        if (completed == send)
            answer ??= (await send)?.Data?.Content;

        if (string.IsNullOrWhiteSpace(answer))
            throw new InvalidOperationException("The model returned an empty response.");

        Console.WriteLine("Assistant:");
        Console.WriteLine(answer);
        return 0;
    }
}
