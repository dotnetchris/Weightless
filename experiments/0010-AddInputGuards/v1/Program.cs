using System.Security.Cryptography;
using System.Text;
using Weightless;
using Weightless.Research.InputGuards;

return LabRun.Run(args, "add-input-guards", 1, (seed, staging) =>
{
    const string claim = "Both source forms lower to one ordered typed plan and preserve checked Add and public argument diagnostics.";
    const string sourcePath = "experiments/0010-AddInputGuards/reference/GuardedAdd.cs";
    var text = File.ReadAllText(sourcePath).Replace("\r\n", "\n", StringComparison.Ordinal);
    var sourceHash = Hash(text);
    var images = new List<WireImage>();
    try
    {
        var imported = GuardBinding.Import(sourcePath, text, seed);
        var explicitPlan = SaveAndReopen("from-if.wlp", imported.Explicit.Plan);
        var helperPlan = SaveAndReopen("from-helper.wlp", imported.Helper.Plan);
        var plansMatch = explicitPlan.Matches(helperPlan);
        var wiresMatch = GuardCodec.Encode(explicitPlan).AsSpan().SequenceEqual(GuardCodec.Encode(helperPlan));

        var raised = explicitPlan with
        {
            Guards = explicitPlan.Guards.SetItem(0, explicitPlan.Guards[0] with { LowerBound = 0 })
        };
        var explicitEdit = GuardBinding.RaiseFirstBound(text, nameof(GuardedAdd.WithIf));
        var helperEdit = GuardBinding.RaiseFirstBound(text, nameof(GuardedAdd.WithHelper));
        var reimportedExplicit = GuardBinding.Import(sourcePath, explicitEdit, seed);
        var reimportedHelper = GuardBinding.Import(sourcePath, helperEdit, seed);
        var explicitSourceControl = reimportedExplicit.Explicit.Plan.Matches(raised) &&
            reimportedExplicit.Helper.Plan.Matches(helperPlan);
        var helperSourceControl = reimportedHelper.Helper.Plan.Matches(raised) &&
            reimportedHelper.Explicit.Plan.Matches(explicitPlan);
        var raisedBound = SaveAndReopen("raised-first-bound.wlp", raised);
        var reversed = SaveAndReopen("reversed-guards.wlp", explicitPlan with
        {
            Guards = [explicitPlan.Guards[1], explicitPlan.Guards[0]]
        });

        int[] values = [int.MinValue, -2, -1, 0, 1, int.MaxValue];
        var rows = (from a in values from b in values select Compare(a, b)).ToArray();
        var raisedActual = Observe(() => raisedBound.Execute(-1, 0));
        var raisedExpected = new GuardObservation(null, nameof(ArgumentOutOfRangeException), "a", -1);
        var raisedOriginal = Observe(() => explicitPlan.Execute(-1, 0));
        var raisedControl = raisedActual == raisedExpected && raisedOriginal == new GuardObservation(-1);
        var reversedActual = Observe(() => reversed.Execute(-2, -3));
        var reversedExpected = new GuardObservation(null, nameof(ArgumentOutOfRangeException), "b", -3);
        var reversedOriginal = Observe(() => explicitPlan.Execute(-2, -3));
        var orderControl = reversedActual == reversedExpected &&
            reversedOriginal == new GuardObservation(null, nameof(ArgumentOutOfRangeException), "a", -2);

        var firstDisagreement = rows.FirstOrDefault(row => !row.Match);
        var firstOracleDisagreement = rows.FirstOrDefault(row => row.IfReference != row.HelperReference);
        var controlsPassed = explicitSourceControl && helperSourceControl && raisedControl && orderControl;
        var verdict = !controlsPassed || firstOracleDisagreement is not null ? "failed"
            : !plansMatch || !wiresMatch || firstDisagreement is not null ? "refuted" : "tested";
        return new LabFinding(claim, verdict, verdict == "tested", new
        {
            startRevision = "1ac9d1946048379a1d685e4cbf79c2cb03f3198e",
            predecessor = "0001-Addition and guarded arithmetic experiment 0005",
            source = new
            {
                path = sourcePath, sha256LfUtf8 = sourceHash, imported.Compiler,
                explicitForm = new { imported.Explicit.Method, imported.Explicit.Origins },
                helperForm = new { imported.Helper.Method, imported.Helper.Origins }
            },
            origin = "Roslyn independently binds the two source blocks, including the effective diagnostic arguments. The checked Add body is the frozen seed.",
            observation = "Return value or exception type, plus ParamName and ActualValue for argument failures.",
            guards = explicitPlan.Guards, plansMatch, wiresMatch, images, values, cases = rows,
            comparisons = rows.Length * 2, firstOracleDisagreement, firstDisagreement,
            controls = new
            {
                sourceBoundEdits = new
                {
                    explicitForm = new { sha256LfUtf8 = Hash(explicitEdit), importedGuards = reimportedExplicit.Explicit.Plan.Guards,
                        passed = explicitSourceControl },
                    helperForm = new { sha256LfUtf8 = Hash(helperEdit), importedGuards = reimportedHelper.Helper.Plan.Guards,
                        passed = helperSourceControl }
                },
                raisedBound = new { a = -1, b = 0, original = raisedOriginal, expected = raisedExpected, actual = raisedActual,
                    passed = raisedControl },
                reversedOrder = new { a = -2, b = -3, original = reversedOriginal, expected = reversedExpected,
                    actual = reversedActual, passed = orderControl }
            }
        });

        GuardCase Compare(int a, int b)
        {
            var ifExpected = Observe(() => new GuardObservation(GuardedAdd.WithIf(a, b)));
            var helperExpected = Observe(() => new GuardObservation(GuardedAdd.WithHelper(a, b)));
            var ifActual = Observe(() => explicitPlan.Execute(a, b));
            var helperActual = Observe(() => helperPlan.Execute(a, b));
            return new GuardCase(a, b, ifExpected, helperExpected, ifActual, helperActual,
                ifExpected == helperExpected && ifExpected == ifActual && helperExpected == helperActual);
        }
    }
    catch (InvalidDataException exception)
    {
        return new LabFinding(claim, "failed", false, new { sourcePath, sha256LfUtf8 = sourceHash, images, error = exception.Message });
    }

    GuardPlan SaveAndReopen(string name, GuardPlan plan)
    {
        var path = Path.Combine(staging, name);
        File.WriteAllBytes(path, GuardCodec.Encode(plan));
        var bytes = File.ReadAllBytes(path);
        var recovered = GuardCodec.Decode(bytes);
        GraphValidation.Require(recovered.Matches(plan), $"Structure changed in {name}.");
        GraphValidation.Require(bytes.AsSpan().SequenceEqual(GuardCodec.Encode(recovered)), $"Bytes changed in {name}.");
        var sumBytes = GraphCodec.Encode(recovered.Sum, EncodingKind.Packed).Length;
        images.Add(new WireImage(name, bytes.Length, bytes.Length - sumBytes, sumBytes, Convert.ToHexString(SHA256.HashData(bytes))));
        return recovered;
    }
});

static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

static GuardObservation Observe(Func<GuardObservation> run)
{
    try
    {
        return run();
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return new GuardObservation(null, exception.GetType().Name, exception.ParamName, (int?)exception.ActualValue);
    }
    catch (OverflowException exception)
    {
        return new GuardObservation(null, exception.GetType().Name);
    }
}

internal sealed record GuardCase(int A, int B, GuardObservation IfReference, GuardObservation HelperReference,
    GuardObservation IfImage, GuardObservation HelperImage, bool Match);
internal sealed record WireImage(string File, int TotalBytes, int GuardAndHeaderBytes, int CheckedSumBytes, string Sha256);
