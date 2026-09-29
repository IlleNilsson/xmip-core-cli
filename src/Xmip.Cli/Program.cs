using Xmip.Cli;
using Xmip.Surface;

// The Xmip command line. ADR-0014: every user-interfacing module is .NET 11,
// and xmip-core-abi is the exception. This executable is argument parsing and
// rendering over Xmip.Surface, the model every .NET surface shares (ADR-0052),
// and Xmip.Abi, the one binding over the C ABI. It links no Rust.
//
// Text for a person on stdout; --json one document, --follow JSON Lines
// (ADR-0014 clause 10). Every complaint goes to stderr with a non-zero exit:
// 2 when the line could not be obeyed, 1 when the thing asked about is wrong.
// The console, the runtime, Ctrl+C and the audit are wired here and nowhere
// else.
//
// Every invocation is audited through the capability (ADR-0062): the command
// as it begins, its end, a non-zero exit as a failure with what it said on
// stderr, a line that could not be obeyed, and anything unhandled — which
// then ends the process exactly as it would have. CommandAudit says what.

EchoedWriter complaints = new(Console.Error);
Console.SetError(TextWriter.Synchronized(complaints));

Invocation? invocation = Invocation.Parse(args, out string problem);
ProgramAudit audit = CommandAudit.Open(invocation);
audit.WatchUnhandled();

if (invocation is null)
{
    Console.Error.WriteLine(problem);
    CommandAudit.Refused(audit, problem);
    return 2;
}

CommandAudit.Begun(audit, invocation);
int exit = await RunAsync(invocation, audit).ConfigureAwait(false);
CommandAudit.Ended(audit, invocation, exit, complaints.Said);

return exit;

static async Task<int> RunAsync(Invocation invocation, ProgramAudit audit)
{
    // What this process says of itself while it runs (ADR-0053): the scope it was
    // asked about, or the whole tree, and the purpose its document states.
    using ProcessDeclaration? declared = ProcessDeclaration.Declare(
        "xmip-cli",
        invocation.Argument.StartsWith("xmip:", StringComparison.Ordinal)
            ? invocation.Argument
            : invocation.Remote ?? ScopeTree.Root,
        ProcessDeclaration.PurposeOf(TomlDocument.Read(
            Path.Combine(AppContext.BaseDirectory, SurfaceOpen.ConfigurationFile))),
        SurfaceOpen.Runtime(invocation));

    bool json = invocation.Json;
    TextWriter output = Console.Out;
    TextWriter error = Console.Error;

    return invocation.Command switch
    {
        Command.Help => Usage.Print(output),
        Command.Abi => AbiCommand.Run(json, output),
        Command.Status => StatusCommand.Run(invocation.Argument, json, output, error),
        Command.Probe => ProbeCommand.Run(invocation.Argument, json, output, error),
        Command.Health => await OverAsync(invocation, (surface, chosen) => invocation.Follow
            ? FollowAsync(surface, chosen, HealthCommand.Answer)
            : Task.FromResult(HealthCommand.Over(surface, chosen, json, output, error)))
            .ConfigureAwait(false),
        Command.Measure => await OverAsync(invocation, (surface, chosen) => invocation.Follow
            ? FollowAsync(surface, chosen, MeasureCommand.Answer)
            : Task.FromResult(MeasureCommand.Over(surface, chosen, json, output, error)))
            .ConfigureAwait(false),
        Command.List => await OverAsync(invocation, (surface, chosen) => Task.FromResult(
            ScopeCommand.ListOver(surface, chosen, json, output, error))).ConfigureAwait(false),
        Command.Show => await OverAsync(invocation, (surface, chosen) => Task.FromResult(
            ScopeCommand.ShowOver(surface, chosen, json, output, error))).ConfigureAwait(false),
        Command.Pause => await OverAsync(invocation, (surface, chosen) => Task.FromResult(
            ScopeCommand.ApplyOver(
                surface, chosen, ScopeAction.Pause, ScopeOperation.Who(invocation.Who),
                json, output, error))).ConfigureAwait(false),
        Command.Resume => await OverAsync(invocation, (surface, chosen) => Task.FromResult(
            ScopeCommand.ApplyOver(
                surface, chosen, ScopeAction.Resume, ScopeOperation.Who(null),
                json, output, error))).ConfigureAwait(false),
        Command.Validate => Validate(invocation),

        // The audit read back from where this executable's own records go.
        Command.Audit => AuditCommand.Run(
            audit, invocation.Audit ?? new AuditQuery(), json, output, error),

        // The Event subscriptions, over the surface the line and the
        // document choose; the pattern is SubscriptionQuery's, not a scope.
        Command.Subscriptions => Subscriptions(invocation, output, error),
        _ => Usage.Print(output),
    };
}

// Every command that answers over scopes, once: open the surface the line and
// the document choose, select what the argument names — a wildcard among the
// scopes that exist (ADR-0059 clause 7, read for every surface) — and refuse a
// pattern that names nothing here and nowhere else: the words for a person,
// the same refusal as a document for --json, on stderr and never exit 0.
static async Task<int> OverAsync(
    Invocation invocation, Func<IOperatorSurface, ScopeSelection, Task<int>> answer)
{
    IOperatorSurface? surface = SurfaceOpen.Open(invocation, out string reason);
    using IDisposable? release = surface as IDisposable;

    if (surface is null)
    {
        Console.Error.WriteLine(reason);
        return 1;
    }

    ScopeSelection? chosen = ScopeSelection.Of(surface, invocation.Argument, out string unmatched);

    if (chosen is null)
    {
        Console.Error.WriteLine(invocation.Json
            ? ScopeCommand.Unmatched(invocation.Argument, unmatched)
            : unmatched);

        return 1;
    }

    return await answer(surface, chosen).ConfigureAwait(false);
}

// The subscriptions a line asks for, listed or acted on.
static int Subscriptions(Invocation invocation, TextWriter output, TextWriter error)
{
    IOperatorSurface? surface = SurfaceOpen.Open(invocation, out string reason);
    using IDisposable? release = surface as IDisposable;

    if (surface is null)
    {
        error.WriteLine(reason);
        return 1;
    }

    return SubscriptionCommand.Over(
        surface,
        invocation.Subscriptions ?? new SubscriptionQuery(),
        invocation.Act,
        ScopeOperation.Who(invocation.Who),
        invocation.Json,
        output,
        error);
}

// --follow (Follow) until Ctrl+C, which is ours to end: the loop stops, the
// runtime is released, the exit is clean. Without this the process dies
// mid-line.
static async Task<int> FollowAsync(
    IOperatorSurface surface,
    ScopeSelection chosen,
    Func<IOperatorSurface, ScopeSelection, string> document)
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
        return await Follow.RunAsync(surface, chosen, document, Console.Out, stop.Token)
            .ConfigureAwait(false);
    }
    finally
    {
        Console.CancelKeyPress -= Interrupted;
    }
}

static int Validate(Invocation invocation)
{
    string configurationPath = invocation.Argument;

    if (!File.Exists(configurationPath))
    {
        Console.Error.WriteLine($"No file at {configurationPath}.");
        return 2;
    }

    // The shared surface answers with the record and the sentence together
    // (ADR-0052 clause 4), so the command renders the same verdict the
    // desktop's Configure page decides from.
    using NativeOperator surface = new(SurfaceOpen.Runtime(invocation));

    if (!surface.IsLoaded)
    {
        Console.Error.WriteLine(surface.Reason);
        return 1;
    }

    return ValidateCommand.Run(
        surface.Validate(configurationPath), invocation.Json, Console.Out, Console.Error);
}
