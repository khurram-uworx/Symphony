using GitHub.Copilot;

namespace Symphony.CodingAgentsProbe;

internal static class CopilotTelemetryProbe
{
    public static async Task<int> RunAsync(string[] args)
    {
        string outputPath;
        string question;

        if (args.Length > 0 && (args[0].EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
            || args[0].Contains(Path.DirectorySeparatorChar)
            || args[0].Contains('/')))
        {
            outputPath = Path.GetFullPath(args[0]);
            question = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "Say hello in one sentence.";
        }
        else
        {
            outputPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "copilot-otel.jsonl"));
            question = args.Length > 0 ? string.Join(" ", args) : "Say hello in one sentence.";
        }

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        Console.WriteLine("=== Copilot SDK OpenTelemetry (file exporter) ===");
        Console.WriteLine($"Question: {question}");
        Console.WriteLine($"Trace file: {outputPath}");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        await using var client = new CopilotClient(new CopilotClientOptions
        {
            Telemetry = new TelemetryConfig
            {
                FilePath = outputPath,
                ExporterType = "file",
                SourceName = "symphony-probe",
                CaptureContent = true,
            },
        });
        await client.StartAsync(cts.Token);

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            OnPermissionRequest = PermissionHandler.ApproveAll,
        }, cts.Token);

        var reply = await session.SendAndWaitAsync(question, null, cts.Token);

        Console.WriteLine();
        Console.WriteLine("Assistant:");
        Console.WriteLine(reply?.Data?.Content);

        await client.StopAsync();
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

        Console.WriteLine();
        if (File.Exists(outputPath))
        {
            var lines = File.ReadAllLines(outputPath);
            Console.WriteLine($"Telemetry: {lines.Length} JSONL record(s), {new FileInfo(outputPath).Length} bytes.");
            foreach (var line in lines.Take(3))
                Console.WriteLine("  " + (line.Length > 200 ? line[..200] + "..." : line));
        }
        else
        {
            Console.WriteLine("Telemetry: no trace file was produced.");
        }

        return 0;
    }
}