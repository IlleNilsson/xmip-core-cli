using System.Text;
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
// The console and the audit are wired here and nowhere else; what each
// command answers is CommandRun's, the one mcp's tools answer by too.
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

    // An MCP server over standard input and output (ADR-0071): stdout carries
    // the protocol and nothing else, so it is opened raw, without the
    // console's encoding preamble, each message ending in one line feed;
    // everything else said goes to stderr.
    if (invocation.Command == Command.Mcp)
    {
        using StreamReader input = new(Console.OpenStandardInput(), new UTF8Encoding(false));
        using StreamWriter protocol = new(Console.OpenStandardOutput(), new UTF8Encoding(false))
        {
            AutoFlush = true,
            NewLine = "\n",
        };

        return await new McpServer(invocation, audit, SurfaceOpen.Open, Console.Error)
            .RunAsync(input, protocol, CancellationToken.None)
            .ConfigureAwait(false);
    }

    return await CommandRun.AnswerAsync(
        invocation, audit, SurfaceOpen.Open, Console.Out, Console.Error).ConfigureAwait(false);
}
