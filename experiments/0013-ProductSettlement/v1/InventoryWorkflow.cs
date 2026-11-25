using Weightless.Research.BalanceHistory;
using Weightless.Research.InputGuards;
using Weightless.Research.PropertyMapping;

internal sealed record InventoryInput(string Name, int Units, int Adjustment, int AlertBelow, int[] Deltas);
internal sealed record InventoryState(int? Value, string? Fault, string? Parameter, int? ActualValue,
    bool? ReturnedOriginalRow, string Sku, int OnHand, int ReorderPoint, int[] History, int[] Deltas)
{
    internal bool Matches(InventoryState other) =>
        (Value, Fault, Parameter, ActualValue, ReturnedOriginalRow, Sku, OnHand, ReorderPoint) ==
        (other.Value, other.Fault, other.Parameter, other.ActualValue, other.ReturnedOriginalRow, other.Sku, other.OnHand, other.ReorderPoint)
        && History.SequenceEqual(other.History) && Deltas.SequenceEqual(other.Deltas);
}

internal sealed record WorkflowCase(InventoryInput Input, InventoryState Reference, InventoryState Decoded, bool Match);
internal sealed record WorkflowControl(string Change, string Input, InventoryState Expected, InventoryState Actual, bool Match);
internal sealed record WorkflowResults(List<WorkflowCase> Cases, List<WorkflowControl> Controls);
internal sealed record InventoryPlan(GuardPlan Guard, MappingPlan Mapping, HistoryPlan History);

internal static class InventoryWorkflow
{
    internal static WorkflowResults Check(Func<string, byte[]> load)
    {
        GuardPlan Guard(string file) => GuardCodec.Decode(load($"0010-AddInputGuards/evidence-v1/{file}.wlp"));
        MappingPlan Map(string file) => MappingCodec.Decode(load($"0011-PropertyMapping/evidence-v1/{file}.wlp"));
        HistoryPlan History(string file) => HistoryCodec.Decode(load($"0012-BalanceHistory/evidence-v1/{file}.wlp"));
        var original = new InventoryPlan(Guard("from-if"), Map("mapping"), History("balance-history"));
        var ordinary = new InventoryInput("receive-and-project", 8, 2, 3, [3, 0, -2]);
        var floor = new InventoryInput("inclusive-floor", -1, 0, 3, []);
        var rejected = new InventoryInput("two-invalid-operands", -2, -3, 3, [1]);
        var setter = new InventoryInput("second-setter-fails", 15, 2, -2, [1]);
        var late = new InventoryInput("second-append-overflows", int.MaxValue - 1, 0, 3, [1, 1]);
        InventoryInput[] inputs = [ordinary, floor, rejected, setter, late,
            new("opening-overflow", int.MaxValue, 1, 3, [1]), new("negative-total", -1, -1, 3, [])];
        var cases = new List<WorkflowCase>();
        foreach (var input in inputs)
        {
            var expected = Observe(input, null);
            var actual = Observe(input, original);
            cases.Add(new(input, expected, actual, actual.Matches(expected)));
        }

        var controls = new List<WorkflowControl>();
        Control("equivalent-helper-image", ordinary, original with { Guard = Guard("from-helper") },
            new(11, null, null, null, true, "SKU-42", 10, 3, [90, 13, 13, 11], [3, 0, -2]), equivalent: true);
        Control("raised-first-bound", floor, original with { Guard = Guard("raised-first-bound") },
            new(null, nameof(ArgumentOutOfRangeException), "a", -1, null, "SKU-42", 8, 4, [90], []));
        Control("reversed-guards", rejected, original with { Guard = Guard("reversed-guards") },
            new(null, nameof(ArgumentOutOfRangeException), "b", -3, null, "SKU-42", 8, 4, [90], [1]));
        Control("swapped-getters", ordinary, original with { Mapping = Map("swapped-source-members") },
            new(4, null, null, null, true, "SKU-42", 3, 10, [90, 6, 6, 4], [3, 0, -2]));
        Control("reversed-assignments", setter, original with { Mapping = Map("reversed-assignments") },
            new(null, nameof(ArgumentOutOfRangeException), "value", -2, null, "SKU-42", 8, 4, [90], [1]));
        Control("append-delta", ordinary, original with { History = History("append-input-delta") },
            new(11, null, null, null, true, "SKU-42", 10, 3, [90, 3, 0, -2], [3, 0, -2]));
        Control("commit-after-traversal", late, original with { History = History("commit-after-traversal") },
            new(null, nameof(OverflowException), null, null, true, "SKU-42", int.MaxValue - 1, 3, [90], [1, 1]));
        return new(cases, controls);

        void Control(string change, InventoryInput input, InventoryPlan variant, InventoryState expected, bool equivalent = false)
        {
            var actual = Observe(input, variant);
            var baseline = cases.Single(row => row.Input.Name == input.Name).Decoded;
            controls.Add(new(change, input.Name, expected, actual,
                actual.Matches(expected) && actual.Matches(baseline) == equivalent));
        }
    }

    private static InventoryState Observe(InventoryInput input, InventoryPlan? plan)
    {
        var row = new StockRow("SKU-42") { OnHand = 8, ReorderPoint = 4 };
        var deltas = (int[])input.Deltas.Clone();
        List<int> history = [90];
        bool? returnedOriginal = null;
        try
        {
            if (plan is null)
            {
                var quantity = GuardedAdd.WithIf(input.Units, input.Adjustment);
                var mapped = StockMapping.Apply(new StockUpdate(quantity, input.AlertBelow), row);
                returnedOriginal = ReferenceEquals(mapped, row);
                return Capture(BalanceHistory.Append(mapped.OnHand, deltas, history));
            }

            var guarded = plan.Guard.Execute(input.Units, input.Adjustment);
            if (guarded.ExceptionType is not null)
                return Capture(null, guarded.ExceptionType, guarded.Parameter, guarded.ActualValue);
            var destination = plan.Mapping.Execute(new StockUpdate(guarded.Value!.Value, input.AlertBelow), row);
            returnedOriginal = ReferenceEquals(destination, row);
            var projection = plan.History.Execute(destination.OnHand, deltas, history);
            return Capture(projection.Value, projection.ExceptionType);
        }
        catch (ArgumentOutOfRangeException error)
        {
            return Capture(null, error.GetType().Name, error.ParamName, (int?)error.ActualValue);
        }
        catch (OverflowException error)
        {
            return Capture(null, error.GetType().Name);
        }

        InventoryState Capture(int? value, string? fault = null, string? parameter = null, int? actualValue = null) =>
            new(value, fault, parameter, actualValue, returnedOriginal, row.Sku, row.OnHand, row.ReorderPoint,
                history.ToArray(), deltas);
    }
}
