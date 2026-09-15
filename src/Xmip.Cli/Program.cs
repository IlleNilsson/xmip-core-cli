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

    if (!invocation.Follow)
    {
        return HealthCommand.Run(
            surface, invocation.Argument, invocation.Json, Console.Out, Console.Error);
    }

    using CancellationTokenSource stop = new();

    Console.CancelKeyPress += (_, interrupt) =>
    {
        // Ours to end: the loop stops, the runtime is released, the exit is
        // clean. Without this the process dies mid-line.
        interrupt.Cancel = true;
        stop.Cancel();
    };

    return await HealthCommand.FollowAsync(
        surface, invocation.Argument, Console.Out, HealthCommand.Interval, stop.Token)
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

    string scope = ScopeOrCluster(invocation);

    if (!invocation.Follow)
    {
        return MeasureCommand.Run(surface, scope, invocation.Json, Console.Out, Console.Error);
    }

    using CancellationTokenSource stop = new();

    Console.CancelKeyPress += (_, interrupt) =>
    {
        interrupt.Cancel = true;
        stop.Cancel();
    };

    return await MeasureCommand.FollowAsync(surface, scope, Console.Out, stop.Token)
        .ConfigureAwait(false);
}

// 'measure' and 'list' take the cluster when no scope is named.
static string ScopeOrCluster(Invocation invocation)
{
    return string.IsNullOrEmpty(invocation.Argument) ? ScopeTree.Root : invocation.Argument;
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
    using NativeOperator surface = new(RuntimeChoice.Find(invocation.Runtime));

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

    string scope = ScopeOrCluster(invocation);

    return list
        ? ScopeCommand.List(surface, scope, invocation.Json, Console.Out, Console.Error)
        : ScopeCommand.Show(surface, scope, invocation.Json, Console.Out, Console.Error);
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

    return ScopeCommand.Apply(
        surface, invocation.Argument, action, Environment.UserName,
        invocation.Json, Console.Out, Console.Error);
}
