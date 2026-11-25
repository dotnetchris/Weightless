using System.Collections.Immutable;
using Weightless;

internal sealed record FaultObservation(string? FaultingOperation, Observation Outcome)
{
    internal bool Matches(FaultObservation other) =>
        FaultingOperation == other.FaultingOperation && Outcome.Matches(other.Outcome);
}

// Probe the fault site with actual interpreter runs on growing prefixes of the decoded straight-line schedule.
internal sealed class FaultSiteProbe
{
    private readonly Graph schedule;
    private readonly (string Operation, Graph Graph)[] prefixes;

    internal FaultSiteProbe(Graph schedule, Graph seed)
    {
        this.schedule = schedule;
        var entry = schedule.Functions[0];
        GraphValidation.Require(schedule.Contract == Postcondition.None && schedule.Functions.Length == 2 &&
            new Graph(Postcondition.None, [schedule.Functions[1]]).Matches(seed) &&
            entry.Body.Length == 4 && entry.Body[0] is Call { Function: 1 } && entry.Body[^1] is Return &&
            entry.Body.Count(node => node is Store) == 1 && entry.Body.Count(node => node is Load) == 1,
            "Fault-site probes require the v2 checked Add, Store and Load schedule.");

        prefixes = entry.Body.Take(3).Select((instruction, index) =>
        {
            var operation = instruction switch
            {
                Call => "checked-add",
                Store => "store",
                Load => "load",
                _ => throw new InvalidDataException("Unexpected operation in the v2 schedule.")
            };
            // Parameter 2 is already available. Its value is immaterial when a prefix faults.
            var body = entry.Body.Take(index + 1).Append(new Return(2)).ToImmutableArray();
            var prefix = schedule with { Functions = schedule.Functions.SetItem(0, entry with { Body = body }) };
            GraphValidation.Validate(prefix);
            return (operation, prefix);
        }).ToArray();
    }

    internal FaultObservation Observe(InputCase input)
    {
        var full = Interpreter.Observe(schedule, input.FreshArguments());
        if (full.ExceptionType is null)
            return new FaultObservation(null, full);

        foreach (var (operation, prefix) in prefixes)
        {
            var partial = Interpreter.Observe(prefix, input.FreshArguments());
            if (partial.ExceptionType is null)
                continue;

            GraphValidation.Require(partial.Matches(full), "First faulting prefix disagreed with the full schedule.");
            return new FaultObservation(operation, full);
        }
        throw new InvalidDataException("Full schedule faulted without a faulting prefix.");
    }
}
