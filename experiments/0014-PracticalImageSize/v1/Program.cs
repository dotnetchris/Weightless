using System.IO.Compression;
using System.Security.Cryptography;
using Weightless;

return LabRun.Run(args, "practical-image-size", 1, (_, staging) =>
{
    const string claim = "Independently zlib-encoded practical images recover the v1 programs and preserve the tested inventory observations.";
    (string Parent, string Canonical)[] fixtures =
    [
        ("0010-AddInputGuards", "from-if.wlp"),
        ("0011-PropertyMapping", "mapping.wlp"),
        ("0012-BalanceHistory", "balance-history.wlp")
    ];
    var images = new List<ImageComparison>();
    foreach (var (parent, canonical) in fixtures)
    {
        var directory = $"experiments/{parent}/evidence-v1";
        Directory.CreateDirectory(Path.Combine(staging, parent));
        foreach (var path in Directory.GetFiles(directory, "*.wlp").Order(StringComparer.Ordinal))
        {
            var file = Path.GetFileName(path);
            var candidate = $"{parent}/{file}.zlib";
            var saved = Path.Combine(staging, candidate);
            var original = File.ReadAllBytes(path);
            using (var output = File.Create(saved))
            using (var encoder = new ZLibStream(output, CompressionLevel.SmallestSize))
                encoder.Write(original);

            var stored = File.ReadAllBytes(saved);
            images.Add(new($"{directory}/{file}", candidate, file == canonical,
                SizeChange.Compare(original.Length, stored.Length),
                Convert.ToHexString(SHA256.HashData(original)), Convert.ToHexString(SHA256.HashData(stored)),
                Decode(saved).AsSpan().SequenceEqual(original)));
        }
    }

    var byBaseline = images.ToDictionary(image => image.Baseline, StringComparer.Ordinal);
    var baselineReplay = InventoryWorkflow.Check(path => File.ReadAllBytes($"experiments/{path}"));
    var candidateReplay = InventoryWorkflow.Check(path => Decode(Path.Combine(staging, byBaseline[$"experiments/{path}"].Candidate)));
    var canonicalImages = images.Where(image => image.Canonical).ToArray();
    var total = SizeChange.Compare(canonicalImages.Sum(image => image.Size.BaselineBytes),
        canonicalImages.Sum(image => image.Size.CandidateBytes));
    var controlsPassed = baselineReplay.Controls.Concat(candidateReplay.Controls).All(row => row.Match);
    var agrees = images.All(image => image.RecoveredOriginalBytes)
        && baselineReplay.Cases.Concat(candidateReplay.Cases).All(row => row.Match);
    var verdict = !controlsPassed ? "failed" : agrees ? "tested" : "refuted";
    foreach (var image in canonicalImages)
        Console.WriteLine($"{image.Baseline}: {image.Size}");
    Console.WriteLine($"Three independent program files: {total}");
    return new LabFinding(claim, verdict, verdict == "tested", new
    {
        encoding = "One .NET ZLibStream at SmallestSize per complete v1 file",
        sharedMaterialBytes = 0,
        host = ".NET 10 with System.IO.Compression, existing v1 interpreters and CLR bindings",
        canonicalTotal = total, images, baselineReplay, candidateReplay
    });
});

static byte[] Decode(string path)
{
    using var file = File.OpenRead(path);
    using var decoder = new ZLibStream(file, CompressionMode.Decompress);
    using var recovered = new MemoryStream();
    decoder.CopyTo(recovered);
    return recovered.ToArray();
}

internal sealed record ImageComparison(string Baseline, string Candidate, bool Canonical, SizeChange Size,
    string BaselineSha256, string CandidateSha256, bool RecoveredOriginalBytes);

internal sealed record SizeChange(int BaselineBytes, int CandidateBytes, int BytesSaved, decimal PercentSaved, string Outcome)
{
    internal static SizeChange Compare(int baseline, int candidate)
    {
        int saved = baseline - candidate;
        string outcome = saved switch { > 0 => "shrank", < 0 => "grew", _ => "tied" };
        return new(baseline, candidate, saved, Math.Round(100m * saved / baseline, 2), outcome);
    }

    public override string ToString() => $"{BaselineBytes} -> {CandidateBytes} bytes ({Outcome}, {BytesSaved} bytes saved, {PercentSaved}% saved)";
}
