using GitHub.Copilot;
using System;
using System.Linq;
using System.Reflection;

namespace Symphony.CodingAgentsProbe;

internal static class SdkOptionsProbe
{
    public static Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("=== GitHub Copilot SDK CopilotClientOptions ===");
        foreach (var property in typeof(CopilotClientOptions).GetProperties().OrderBy(property => property.Name))
            Console.WriteLine($"{property.Name} : {property.PropertyType.FullName}");

        Console.WriteLine();
        Console.WriteLine("=== GitHub Copilot SDK SessionConfig ===");
        foreach (var property in typeof(SessionConfig).GetProperties().OrderBy(property => property.Name))
            Console.WriteLine($"{property.Name} : {property.PropertyType.FullName}");

        return Task.FromResult(0);
    }
}