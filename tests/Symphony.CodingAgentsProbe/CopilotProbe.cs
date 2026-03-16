using GitHub.Copilot;

namespace Symphony.CodingAgentsProbe;

internal static class CopilotProbe
{
    public static async Task<int> RunAsync(string[] args)
    {
        var question = args.Length > 0 ? string.Join(" ", args) : "Say hello in one sentence.";

        Console.WriteLine("=== Copilot SDK (no BYOM) ===");
        Console.WriteLine($"Question: {question}");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        await using var client = new CopilotClient();
        await client.StartAsync(cts.Token);

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            OnPermissionRequest = PermissionHandler.ApproveAll
        }, cts.Token);

        var reply = await session.SendAndWaitAsync(question, null, cts.Token);
        Console.WriteLine();
        Console.WriteLine("Assistant:");
        Console.WriteLine(reply?.Data?.Content);
        return 0;
    }
}