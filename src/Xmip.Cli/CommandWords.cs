namespace Xmip.Cli;

/// <summary>The word each <see cref="Command"/> is typed as, and how many
/// arguments it takes: none, one, or one at most (ADR-0014 clause 8).</summary>
public static class CommandWords
{
    /// <summary>Each command's word, its command and its arguments.</summary>
    public static IReadOnlyDictionary<string, (Command Command, int Minimum, int Maximum)>
        Known { get; } = new Dictionary<string, (Command, int, int)>(StringComparer.Ordinal)
        {
            ["help"] = (Command.Help, 0, 0),
            ["abi"] = (Command.Abi, 0, 0),
            ["status"] = (Command.Status, 1, 1),
            ["probe"] = (Command.Probe, 1, 1),
            ["health"] = (Command.Health, 1, 1),
            ["measure"] = (Command.Measure, 0, 1),
            ["list"] = (Command.List, 0, 1),
            ["show"] = (Command.Show, 1, 1),
            ["pause"] = (Command.Pause, 1, 1),
            ["resume"] = (Command.Resume, 1, 1),
            ["validate"] = (Command.Validate, 1, 1),
            ["certificate"] = (Command.Certificate, 0, 1),
            ["database"] = (Command.Database, 1, 1),
            ["audit"] = (Command.Audit, 0, 1),
            ["event-subscriptions"] = (Command.EventSubscriptions, 0, 1),
            ["subscriptions"] = (Command.Subscriptions, 0, 1),
            ["dead-messages"] = (Command.DeadMessages, 0, 1),
            ["journey"] = (Command.Journey, 0, 1),
            ["secret"] = (Command.Secret, 0, 1),
            ["mcp"] = (Command.Mcp, 0, 0),
        };
}
