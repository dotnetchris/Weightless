using System.Security.Cryptography;
using Weightless;

return LabRun.Run(args, "product-settlement", 1, (seed, _) =>
{
    const string claim = "The three saved practical plans, called in sequence, preserve the inventory operation's outcome and caller-owned state on the specified cases.";
    var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal);
    byte[] Load(string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine("experiments", path));
        inputs[path] = Convert.ToHexString(SHA256.HashData(bytes));
        return bytes;
    }

    try
    {
        var history = PriorControls.Run(seed, Load);
        var workflow = InventoryWorkflow.Check(Load);
        var controlsPassed = history.All(row => row.Match) && workflow.Controls.All(row => row.Match);
        var agrees = workflow.Cases.All(row => row.Match);
        string verdict = !controlsPassed ? "failed" : agrees ? "tested" : "refuted";
        return new LabFinding(claim, verdict, controlsPassed && agrees, new
        {
            parentRevision = "40ed20a682d73c9e269e6cb0243b5e91b5057529",
            parents = new[] { "capacity/v3", "transfer/v3", "add-input-guards/v1", "property-mapping/v1", "balance-history/v1" },
            inputSha256 = inputs, historicalControls = history, inventory = workflow
        });
    }
    catch (Exception error) when (error is IOException or InvalidDataException)
    {
        return new LabFinding(claim, "failed", false, new { error = error.Message, inputSha256 = inputs });
    }
});
