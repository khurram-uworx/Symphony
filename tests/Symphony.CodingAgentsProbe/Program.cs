namespace Symphony.CodingAgentsProbe;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project tests/Symphony.CodingAgentsProbe -- copilot [question...]");
            Console.WriteLine("  dotnet run --project tests/Symphony.CodingAgentsProbe -- byom [model] [baseUrl] [wireApi] [question...]");
            Console.WriteLine("  dotnet run --project tests/Symphony.CodingAgentsProbe -- otel [output-file.jsonl] [question...]");
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "copilot" => await CopilotProbe.RunAsync(args.Skip(1).ToArray()),
            "byom" => await ByomProbe.RunAsync(args.Skip(1).ToArray()),
            "sdk-options" => await SdkOptionsProbe.RunAsync(args.Skip(1).ToArray()),
            "otel" => await CopilotTelemetryProbe.RunAsync(args.Skip(1).ToArray()),
            _ => await CopilotProbe.RunAsync(args)
        };
    }
}