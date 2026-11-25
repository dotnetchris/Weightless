using Weightless;

internal sealed record ArithmeticControl(string Test, string Codec, int A, int B, int? ExpectedArm,
    int? ActualArm, int? Value, string? ExceptionType, bool Match);

internal static class PriorControls
{
    internal static List<ArithmeticControl> Run(Graph seed, Func<string, byte[]> load)
    {
        var results = new List<ArithmeticControl>();
        var entry = seed.Functions[0];
        var mixed = seed with { Functions = [entry with { Body = [entry.Body[0],
            new Addition(ValueKind.Int32, Overflow.Wrapping, 2, 1), new Return(3)] }] };
        var decoded = RunPackedCodec.Decode(RunPackedCodec.Encode(mixed));
        var sum = Interpreter.Observe(decoded, [int.MaxValue - 1, 1]);
        results.Add(new("checked-then-wrapping", "run-packed", int.MaxValue - 1, 1, null, null, (int?)sum.Value,
            sum.ExceptionType, decoded.Matches(mixed) && sum.Matches(new Observation(int.MinValue, null, "", [], []))));

        foreach (var (codec, suffix) in new[] { (EncodingKind.Baseline, "wlb"), (EncodingKind.Packed, "wlp") })
        {
            var program = SelectorCodec.Decode(load($"0008-Transfer/evidence-v3/selector.{suffix}"), codec);
            Check("saved", program, 0, int.MaxValue);
            Check("exchanged-targets", program with { Branch = new SelectorBranch(0, 1) }, 1, null);
            Check("zero-left-shift", program with { Left = new ShiftFitsInt32(0, 0) }, 1, null);
            Check("left-test-reads-b", program with { Left = new ShiftFitsInt32(1, 1) }, 1, null);

            void Check(string name, SelectorProgram candidate, int arm, int? value)
            {
                var recovered = SelectorCodec.Decode(SelectorCodec.Encode(candidate, codec).Bytes, codec);
                var actual = recovered.Execute(int.MaxValue, 0);
                var expected = new Observation(value, value.HasValue ? null : nameof(OverflowException), "", [], []);
                results.Add(new(name, codec.ToString(), int.MaxValue, 0, arm, actual.SelectedFunction,
                    (int?)actual.Outcome.Value, actual.Outcome.ExceptionType,
                    actual.SelectedFunction == arm && actual.Outcome.Matches(expected)));
            }
        }
        return results;
    }
}
