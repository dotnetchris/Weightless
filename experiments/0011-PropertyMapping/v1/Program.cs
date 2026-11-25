using System.Security.Cryptography;
using System.Text;
using Weightless;
using Weightless.Research.PropertyMapping;

return LabRun.Run(args, "property-mapping", 1, (_, staging) =>
{
    const string claim = "The decoded property assignments preserve binding, returned identity, write order and partial updates.";
    const string sourcePath = "experiments/0011-PropertyMapping/reference/StockMapping.cs";
    var text = File.ReadAllText(sourcePath).Replace("\r\n", "\n", StringComparison.Ordinal);
    var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    var images = new List<WireImage>();
    try
    {
        var bound = MappingBinding.Import(sourcePath, text);
        var decoded = SaveAndReopen("mapping.wlp", bound.Plan);
        var first = decoded.Assignments[0];
        var second = decoded.Assignments[1];
        var swapped = SaveAndReopen("swapped-source-members.wlp", decoded with
        {
            Assignments = [first with { Getter = second.Getter }, second with { Getter = first.Getter }]
        });
        var reversed = SaveAndReopen("reversed-assignments.wlp", decoded with
        {
            Assignments = [second, first]
        });

        var starting = new RowState("SKU-42", 8, 4);
        Update[] inputs = [new("ordinary", 17, 3), new("zero", 0, 0),
            new("idempotent", 8, 4), new("rejected-second-write", 17, -2)];
        var comparisons = inputs.Select(input =>
        {
            var expected = Observe(StockMapping.Apply, input, starting.Create());
            var actual = Observe(decoded.Execute, input, starting.Create());
            return new MappingCase(input, starting, expected, actual, expected == actual);
        }).ToArray();

        var ordinary = comparisons[0];
        var swappedActual = Observe(swapped.Execute, ordinary.Input, starting.Create());
        var swappedExpected = new MappingObservation(true, null, null, null, starting with { OnHand = 3, ReorderPoint = 17 });
        var swapControl = swappedActual == swappedExpected && swappedActual != ordinary.Expected;

        var rejected = comparisons[^1];
        var reversedActual = Observe(reversed.Execute, rejected.Input, starting.Create());
        var reversedExpected = new MappingObservation(null, nameof(ArgumentOutOfRangeException), "value", -2, starting);
        var reverseControl = reversedActual == reversedExpected && rejected.Expected == (reversedExpected with
        {
            Destination = starting with { OnHand = 17 }
        });

        RowState[] initialRows = [starting, new("SKU-7", 6, 2)];
        SessionUpdate[] updates =
        [
            new(1, new("other-row", 22, 5)),
            new(0, new("first-update", 17, 3)),
            new(0, new("late-rejection", 20, -2)),
            new(0, new("after-rejection", 21, 1))
        ];
        var session = Session(initialRows, updates, StockMapping.Apply)
            .Zip(Session(initialRows, updates, decoded.Execute))
            .Select(pair => new SessionComparison(pair.First, pair.Second, pair.First == pair.Second)).ToArray();
        var final = session[^1].Actual;
        var firstDisagreement = (object?)comparisons.FirstOrDefault(row => !row.Match) ??
            session.FirstOrDefault(row => !row.Match);
        var verdict = !swapControl || !reverseControl ? "failed" : firstDisagreement is not null ? "refuted" : "tested";
        return new LabFinding(claim, verdict, verdict == "tested", new
        {
            startRevision = "1ac9d1946048379a1d685e4cbf79c2cb03f3198e",
            predecessor = "effects/v3",
            source = new { path = sourcePath, bound.Method, bound.Compiler, sha256LfUtf8 = sourceHash, bound.Assignments },
            origin = "Assignments, order and return receiver come from Roslyn. A route-local table invokes the real declared CLR accessors.",
            seed = "The checked Add seed authenticates the runner's starting point.",
            observation = "Returned identity on success and caller alias state on every outcome, including SKU, both integer fields and argument fault details.",
            schema = new
            {
                receivers = StockSchema.Receivers,
                members = StockSchema.Members.Select((member, id) => new { id, member.Receiver, member.Name, member.Access })
            },
            images, comparisons, successiveUpdates = new
            {
                initialRows, steps = session, finalRows = new[] { final.FirstRow, final.SecondRow }
            },
            controls = new
            {
                swapSources = new { ordinary.Input, expected = swappedExpected, actual = swappedActual, passed = swapControl },
                reverseAssignments = new
                {
                    rejected.Input, reference = rejected.Expected, expected = reversedExpected, actual = reversedActual,
                    passed = reverseControl
                }
            },
            firstDisagreement
        });
    }
    catch (InvalidDataException exception)
    {
        return new LabFinding(claim, "failed", false, new { sourcePath, sha256LfUtf8 = sourceHash, images, error = exception.Message });
    }

    MappingPlan SaveAndReopen(string name, MappingPlan plan)
    {
        var path = Path.Combine(staging, name);
        File.WriteAllBytes(path, MappingCodec.Encode(plan).Bytes);
        var bytes = File.ReadAllBytes(path);
        var recovered = MappingCodec.Decode(bytes);
        GraphValidation.Require(recovered.Matches(plan), $"Structure changed in {name}.");
        var roundTrip = MappingCodec.Encode(recovered);
        GraphValidation.Require(bytes.AsSpan().SequenceEqual(roundTrip.Bytes), $"Bytes changed in {name}.");
        images.Add(new WireImage(name, bytes.Length, roundTrip.SchemaBytes, bytes.Length - roundTrip.SchemaBytes,
            Convert.ToHexString(SHA256.HashData(bytes))));
        return recovered;
    }
});

static MappingObservation Observe(Func<StockUpdate, StockRow, StockRow> apply, Update input, StockRow owned)
{
    var alias = owned;
    try
    {
        var returned = apply(new StockUpdate(input.Units, input.AlertBelow), owned);
        return new MappingObservation(ReferenceEquals(returned, alias), null, null, null, RowState.Capture(alias));
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return new MappingObservation(null, exception.GetType().Name, exception.ParamName,
            (int?)exception.ActualValue, RowState.Capture(alias));
    }
}

static StepTrace[] Session(RowState[] initial, SessionUpdate[] updates, Func<StockUpdate, StockRow, StockRow> apply)
{
    var rows = initial.Select(state => state.Create()).ToArray();
    GraphValidation.Require(!ReferenceEquals(rows[0], rows[1]), "Session requires two distinct rows.");
    return updates.Select(update =>
    {
        var observation = Observe(apply, update.Input, rows[update.Receiver]);
        return new StepTrace(update, observation, RowState.Capture(rows[0]), RowState.Capture(rows[1]));
    }).ToArray();
}

internal sealed record RowState(string Sku, int OnHand, int ReorderPoint)
{
    internal StockRow Create() => new(Sku) { OnHand = OnHand, ReorderPoint = ReorderPoint };
    internal static RowState Capture(StockRow row) => new(row.Sku, row.OnHand, row.ReorderPoint);
}
internal sealed record Update(string Name, int Units, int AlertBelow);
internal sealed record MappingObservation(bool? ReturnedOriginal, string? ExceptionType, string? Parameter, int? ActualValue,
    RowState Destination);
internal sealed record MappingCase(Update Input, RowState Initial, MappingObservation Expected, MappingObservation Actual, bool Match);
internal sealed record SessionUpdate(int Receiver, Update Input);
internal sealed record StepTrace(SessionUpdate Update, MappingObservation Observation, RowState FirstRow, RowState SecondRow);
internal sealed record SessionComparison(StepTrace Expected, StepTrace Actual, bool Match);
internal sealed record WireImage(string File, int TotalBytes, int SchemaBytes, int HeaderAndProgramBytes, string Sha256);
