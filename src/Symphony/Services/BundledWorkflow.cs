using System.Reflection;
using System.Text;

namespace Symphony.Services;

public static class BundledWorkflow
{
    public const string DefaultName = "Workflow";

    static string? cachedContent;

    public static string Content
    {
        get
        {
            if (cachedContent is not null)
                return cachedContent;

            var assembly = typeof(BundledWorkflow).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("WORKFLOW.md", StringComparison.Ordinal))
                ?? throw new FileNotFoundException("Bundled WORKFLOW.md resource was not found.");

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException("Bundled WORKFLOW.md resource could not be opened.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            cachedContent = reader.ReadToEnd();
            return cachedContent;
        }
    }
}