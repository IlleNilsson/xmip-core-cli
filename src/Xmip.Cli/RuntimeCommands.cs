using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Cli;

/// <summary>
/// The commands that reach a running Xmip — the first on this surface that do.
/// ADR-0027 said <c>abi</c>, <c>status</c> and <c>probe</c> all describe the
/// binding and none of them talks to a runtime; these two cross the operator
/// boundary in <c>xmip_operate.h</c>. The same two the desktop GUI runs, through
/// the same <see cref="Operator"/>, so what <c>xmip</c> says and what the
/// screen says cannot disagree.
/// </summary>
internal static class RuntimeCommands
{
    /// <summary>Every health record at and beneath a scope, worst first.</summary>
    public static int Health(string library, string scope)
    {
        using var runtime = Operator.Load(Path.GetFullPath(library), out var reason);

        if (runtime is null)
        {
            Console.Error.WriteLine(reason);
            return 1;
        }

        var records = runtime.Health(scope);

        if (records.Count == 0)
        {
            Console.Error.WriteLine($"Nothing at {scope}.");
            return 1;
        }

        foreach (var record in records)
        {
            Console.WriteLine($"{record.State,-9} {record.Severity,3}  {record.Scope}");

            if (record.Evidence.Length > 0)
            {
                Console.WriteLine($"               {record.Evidence}");
            }

            Console.WriteLine($"               observed {record.Observed:O}");
        }

        return 0;
    }

    /// <summary>
    /// The runtime's answer on a node configuration file, without starting it.
    /// The file's text crosses, not its path: the runtime validates a proposed
    /// document and publishes nothing (ADR-0027 clause 9).
    /// </summary>
    public static int Validate(string library, string configurationPath)
    {
        if (!File.Exists(configurationPath))
        {
            Console.Error.WriteLine($"No file at {configurationPath}.");
            return 2;
        }

        using var runtime = Operator.Load(Path.GetFullPath(library), out var reason);

        if (runtime is null)
        {
            Console.Error.WriteLine(reason);
            return 1;
        }

        var answer = runtime.Validate(File.ReadAllText(configurationPath));

        if (answer.IsValid)
        {
            Console.WriteLine($"{configurationPath} is valid");
            return 0;
        }

        Console.Error.WriteLine(
            $"{configurationPath}: {answer.Status} — {answer.Status.Explain()}");

        foreach (var problem in answer.Problems)
        {
            Console.Error.WriteLine($"  {problem}");
        }

        return 1;
    }
}
