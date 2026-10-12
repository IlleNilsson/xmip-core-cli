using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>Opens the surface an invocation reads: the surface, or null with
/// <paramref name="reason"/> saying why. <see cref="SurfaceOpen.Open"/> is the
/// executable's; a test hands one of its own.</summary>
public delegate IOperatorSurface? SurfaceOpener(Invocation invocation, out string reason);

/// <summary>
/// Every command, answered once: the line parsed into an
/// <see cref="Invocation"/>, the surface opened, the answer written to the
/// output and error writers the caller hands in, and the exit code returned.
/// The executable runs one per process over the console; <c>mcp</c> runs one
/// per tool call over writers of its own (ADR-0071), so a tool and its
/// command cannot answer differently.
/// </summary>
public static class CommandRun
{
    /// <summary>Answer <paramref name="invocation"/>. <c>help</c> and
    /// anything unanswered here print the usage; <c>mcp</c> is
    /// <see cref="McpServer"/>'s, never answered here.</summary>
    public static async Task<int> AnswerAsync(
        Invocation invocation,
        ProgramAudit audit,
        SurfaceOpener open,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        bool json = invocation.Json;
        string who = ScopeOperation.Who(invocation.Who);

        return invocation.Command switch
        {
            Command.Abi => AbiCommand.Run(json, output),
            Command.Status => StatusCommand.Run(invocation.Argument, json, output, error),
            Command.Probe => ProbeCommand.Run(invocation.Argument, json, output, error),
            Command.Health => await Over(invocation, open, error, (surface, chosen) =>
                invocation.Follow
                    ? FollowAsync(surface, chosen, HealthCommand.Answer, output)
                    : Task.FromResult(HealthCommand.Over(surface, chosen, json, output, error)))
                .ConfigureAwait(false),
            Command.Measure => await Over(invocation, open, error, (surface, chosen) =>
                invocation.Follow
                    ? FollowAsync(surface, chosen, MeasureCommand.Answer, output)
                    : Task.FromResult(MeasureCommand.Over(surface, chosen, json, output, error)))
                .ConfigureAwait(false),
            Command.List => await Over(invocation, open, error, (surface, chosen) =>
                Task.FromResult(ScopeCommand.ListOver(surface, chosen, json, output, error)))
                .ConfigureAwait(false),
            Command.Show => await Over(invocation, open, error, (surface, chosen) =>
                Task.FromResult(ScopeCommand.ShowOver(surface, chosen, json, output, error)))
                .ConfigureAwait(false),
            Command.Pause => await Over(invocation, open, error, (surface, chosen) =>
                Task.FromResult(ScopeCommand.ApplyOver(
                    surface, chosen, ScopeAction.Pause, who, json, output, error)))
                .ConfigureAwait(false),
            Command.Resume => await Over(invocation, open, error, (surface, chosen) =>
                Task.FromResult(ScopeCommand.ApplyOver(
                    surface, chosen, ScopeAction.Resume, ScopeOperation.Who(null),
                    json, output, error)))
                .ConfigureAwait(false),
            Command.Validate => ValidateCommand.Over(
                SurfaceOpen.Runtime(invocation), invocation.Argument, json, output, error),

            // The audit read back from where this executable's own records go.
            Command.Audit => AuditCommand.Run(
                audit, invocation.Audit ?? new AuditQuery(), json, output, error),

            // The Event subscriptions, the Subscriptions, the Dead Message
            // Queues and a Journey that failed, over the surface the line and
            // the document choose; the pattern is each query's, not a scope.
            Command.EventSubscriptions => On(invocation, open, error, surface =>
                EventSubscriptionCommand.Over(
                    surface,
                    invocation.EventSubscriptions ?? new EventSubscriptionQuery(),
                    invocation.EventAct,
                    who,
                    json,
                    output,
                    error)),
            Command.Subscriptions => On(invocation, open, error, surface =>
                SubscriptionCommand.Over(
                    surface,
                    invocation.Subscriptions ?? new SubscriptionQuery(),
                    invocation.Act,
                    who,
                    json,
                    output,
                    error)),
            Command.DeadMessages => On(invocation, open, error, surface =>
                DeadMessageCommand.Over(
                    surface,
                    invocation.DeadMessages ?? new DeadMessageQuery(),
                    invocation.Replay,
                    who,
                    json,
                    output,
                    error)),
            Command.Journey when invocation.JourneyAct is { } act => On(
                invocation, open, error, surface => JourneyCommand.Over(
                    surface, invocation.Location, invocation.Argument, act, who,
                    json, output, error)),
            Command.Journey => On(
                invocation, open, error, surface => JourneyCommand.List(
                    surface, invocation.Location, (invocation.From, invocation.Most),
                    json, output, error)),
            _ => Usage.Print(output),
        };
    }

    // Every command that answers over scopes, once: open the surface the line
    // and the document choose, select what the argument names — a wildcard
    // among the scopes that exist (ADR-0059 clause 7, read for every surface)
    // — and refuse a pattern that names nothing here and nowhere else: the
    // words for a person, the same refusal as a document for --json, on the
    // error writer and never exit 0.
    private static async Task<int> Over(
        Invocation invocation,
        SurfaceOpener open,
        TextWriter error,
        Func<IOperatorSurface, ScopeSelection, Task<int>> answer)
    {
        IOperatorSurface? surface = open(invocation, out string reason);
        using IDisposable? release = surface as IDisposable;

        if (surface is null)
        {
            error.WriteLine(reason);
            return 1;
        }

        ScopeSelection? chosen =
            ScopeSelection.Of(surface, invocation.Argument, out string unmatched);

        if (chosen is null)
        {
            error.WriteLine(invocation.Json
                ? ScopeCommand.Unmatched(invocation.Argument, unmatched)
                : unmatched);

            return 1;
        }

        return await answer(surface, chosen).ConfigureAwait(false);
    }

    // A command that answers over the surface itself, not over scopes: open
    // the surface the line and the document choose, answer, release it.
    private static int On(
        Invocation invocation,
        SurfaceOpener open,
        TextWriter error,
        Func<IOperatorSurface, int> answer)
    {
        IOperatorSurface? surface = open(invocation, out string reason);
        using IDisposable? release = surface as IDisposable;

        if (surface is null)
        {
            error.WriteLine(reason);
            return 1;
        }

        return answer(surface);
    }

    // --follow (Follow) until Ctrl+C, which is ours to end: the loop stops,
    // the runtime is released, the exit is clean. Without this the process
    // dies mid-line.
    private static async Task<int> FollowAsync(
        IOperatorSurface surface,
        ScopeSelection chosen,
        Func<IOperatorSurface, ScopeSelection, string> document,
        TextWriter output)
    {
        using CancellationTokenSource stop = new();

        void Interrupted(object? sender, ConsoleCancelEventArgs interrupt)
        {
            interrupt.Cancel = true;
            stop.Cancel();
        }

        Console.CancelKeyPress += Interrupted;

        try
        {
            return await Follow.RunAsync(surface, chosen, document, output, stop.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= Interrupted;
        }
    }
}
