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
// The console, the runtime and Ctrl+C are wired here and nowhere else.

Invocation? invocation = Invocation.Parse(args, out string problem);

if (invocation is null)
{
    Console.Error.WriteLine(problem);
    return 2;
}

// What this process says of itself while it runs (ADR-0053): the scope it was
// asked about, or the whole tree, and the purpose its document states.
using ProcessDeclaration? declared = ProcessDeclaration.Declare(
    "xmip-cli",
    invocation.Argument.StartsWith("xmip:", StringComparison.Ordinal)
        ? invocation.Argument
        : invocation.Remote ?? ScopeTree.Root,
    ProcessDeclaration.PurposeOf(TomlDocument.Read(
        Path.Combine(AppContext.BaseDirectory, SurfaceOpen.ConfigurationFile))));

return invocation.Command switch
{
    Command.Help => Usage.Print(Console.Out),
    Command.Abi => AbiCommand.Run(invocation.Json, Console.Out),
    Command.Status => StatusCommand.Run(
        invocation.Argument, invocation.Json, Console.Out, Console.Error),
    Command.Probe => ProbeCommand.Run(
        invocation.Argument, invocation.Json, Console.Out, Console.Error),
    Command.Health => await HealthAsync(invocation).ConfigureAwait(false),
    Command.Measure => await MeasureAsync(invocation).ConfigureAwait(false),
    Command.List => ScopeRead(invocation, list: true),
    Command.Show => ScopeRead(invocation, list: false),
    Command.Pause => Act(invocation, ScopeAction.Pause),
    Command.Resume => Act(invocation, ScopeAction.Resume),
    Command.Validate => Validate(invocation),
    _ => Usage.Print(Console.Out),
};

static async Task<int> HealthAsync(Invocation invocation)
{
    IOperatorSurface? surface = SurfaceOpen.Open(invocation, out string reason);
    using IDisposable? release = surface as IDisposable;

    if (surface is null)
    {
        Console.Error.WriteLine(reason);
        return 1;
    }

    // A scope that selects among scopes that exist may be a wildcard (ADR-0059
    // clause 7, read for every surface). A pattern that names nothing is
    // REFUSED here and nowhere else, so every command refuses it the same way.
    ScopeSelection? chosen = ScopeSelection.Of(surface, invocation.Argument, out string unmatched);

    if (chosen is null)
    {
        return Unmatched(invocation, unmatched);
    }

    if (!invocation.Follow)
    {
        return HealthCommand.Over(surface, chosen, invocation.Json, Console.Out, Console.Error);
    }

    using CancellationTokenSource stop = new();

    Console.CancelKeyPress += (_, interrupt) =>
    {
        // Ours to end: the loop stops, the runtime is released, the exit is
        // clean. Without this the process dies mid-line.
        interrupt.Cancel = true;
        stop.Cancel();
    };

    return await HealthCommand.FollowAsync(surface, chosen, Console.Out, stop.Token)
        .ConfigureAwait(false);
}

static async Task<int> MeasureAsync(Invocation invocation)
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
        return Unmatched(invocation, unmatched);
    }

    if (!invocation.Follow)
    {
        return MeasureCommand.Over(surface, chosen, invocation.Json, Console.Out, Console.Error);
    }

    using CancellationTokenSource stop = new();

    Console.CancelKeyPress += (_, interrupt) =>
    {
        interrupt.Cancel = true;
        stop.Cancel();
    };

    return await MeasureCommand.FollowAsync(surface, chosen, Console.Out, stop.Token)
        .ConfigureAwait(false);
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

static int ScopeRead(Invocation invocation, bool list)
{
    IOperatorSurface? surface = SurfaceOpen.Open(invocation, out string reason);
    using IDisposable? release = surface as IDisposable;

    if (surface is null)
    {
        Console.Error.WriteLine(reason);
        return 1;
    }

    // 'list' takes the cluster when no scope is named; either takes a pattern.
    ScopeSelection? chosen = ScopeSelection.Of(surface, invocation.Argument, out string unmatched);

    return chosen is null
        ? Unmatched(invocation, unmatched)
        : list
            ? ScopeCommand.ListOver(surface, chosen, invocation.Json, Console.Out, Console.Error)
            : ScopeCommand.ShowOver(surface, chosen, invocation.Json, Console.Out, Console.Error);
}

static int Act(Invocation invocation, ScopeAction action)
{
    IOperatorSurface? surface = SurfaceOpen.Open(invocation, out string reason);
    using IDisposable? release = surface as IDisposable;

    if (surface is null)
    {
        Console.Error.WriteLine(reason);
        return 1;
    }

    ScopeSelection? chosen = ScopeSelection.Of(surface, invocation.Argument, out string unmatched);

    return chosen is null
        ? Unmatched(invocation, unmatched)
        : ScopeCommand.ApplyOver(
            surface, chosen, action, Environment.UserName,
            invocation.Json, Console.Out, Console.Error);
}

// A pattern that named nothing, said once for every command: the words for a
// person, the same refusal as a document for --json, on stderr and never
// exit 0 (ADR-0059 clause 7).
static int Unmatched(Invocation invocation, string refusal)
{
    Console.Error.WriteLine(invocation.Json
        ? ScopeCommand.Unmatched(invocation.Argument, refusal)
        : refusal);

    return 1;
}
