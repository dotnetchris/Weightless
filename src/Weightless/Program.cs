using System.Text.Json;

namespace Weightless;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static int Main(string[] args)
    {
        try
        {
            switch (args)
            {
                case ["emit-add", var directory]:
                    Directory.CreateDirectory(directory);
                    foreach (var overflow in Enum.GetValues<Overflow>())
                    {
                        var name = overflow.ToString().ToLowerInvariant();
                        File.WriteAllBytes(Path.Combine(directory, $"add-{name}.wlb"),
                            GraphCodec.Encode(Fixtures.Add(overflow), EncodingKind.Baseline));
                    }
                    return 0;
                case ["verify-add", var directory, var original]:
                    return AdditionReplay.Run(directory, original);
                case ["suite", var directory]:
                    return ExperimentSuite.Run(Path.GetFullPath(directory));
                case ["verify-corpus", var directory]:
                    return ExperimentSuite.Verify(directory);
                default:
                    Console.Error.WriteLine(
                        "Usage: suite DIRECTORY | verify-corpus DIRECTORY | emit-add DIRECTORY | verify-add DIRECTORY ORIGINAL_JSON");
                    return 2;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
