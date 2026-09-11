using System.Globalization;

namespace Xmip.Cli;

/// <summary>
/// One command line, parsed: the command, its one argument, and the three
/// options. Parsing knows nothing about the runtime or the console, which is
/// what lets it be tested with a string array and nothing else.
/// </summary>
/// <param name="Command">The command named first on the line.</param>
/// <param name="Argument">The command's argument — a status code, a library
/// path, a scope, a configuration path — or empty for the ones without.</param>
/// <param name="Json">Emit one JSON document instead of text for a person.
/// ADR-0014 clause 10.</param>
/// <param name="Follow">Emit JSON Lines of health as it changes, until
/// interrupted. Implies <see cref="Json"/>.</param>
/// <param name="Runtime">An explicit runtime library, overriding discovery.
/// Null when the discovery rule decides.</param>
public sealed record Invocation(
    Command Command, string Argument, bool Json, bool Follow, string? Runtime)
{
    private static readonly Dictionary<string, (Command Command, bool TakesArgument)> Known =
        new(StringComparer.Ordinal)
        {
            ["help"] = (Command.Help, false),
            ["abi"] = (Command.Abi, false),
            ["status"] = (Command.Status, true),
            ["probe"] = (Command.Probe, true),
            ["health"] = (Command.Health, true),
            ["validate"] = (Command.Validate, true),
        };

    /// <summary>
    /// Parse a command line. Options may sit anywhere; the first bare word is
    /// the command and the second its argument. Null with a problem in
    /// <paramref name="problem"/> when the line cannot be obeyed — the caller
    /// prints it and exits 2.
    /// </summary>
    public static Invocation? Parse(IReadOnlyList<string> args, out string problem)
    {
        List<string> words = [];
        bool json = false;
        bool follow = false;
        string? runtime = null;

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];

            switch (arg)
            {
                case "--json":
                    json = true;
                    break;
                case "--follow":
                    follow = true;
                    break;
                case "--runtime":
                    if (i + 1 >= args.Count)
                    {
                        problem = "--runtime needs a path.";
                        return null;
                    }

                    runtime = args[++i];
                    break;
                case "--help" or "-h":
                    words.Insert(0, "help");
                    break;
                default:
                    // A status code is zero or negative, so '-22' is a word,
                    // not an option.
                    if (arg.StartsWith('-') && !int.TryParse(
                        arg, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                    {
                        problem = $"'{arg}' is not an xmip option. Try 'xmip help'.";
                        return null;
                    }

                    words.Add(arg);
                    break;
            }
        }

        if (words.Count == 0)
        {
            problem = string.Empty;
            return new Invocation(Command.Help, string.Empty, json, follow, runtime);
        }

        if (!Known.TryGetValue(words[0], out (Command Command, bool TakesArgument) known))
        {
            problem = $"'{words[0]}' is not an xmip command. Try 'xmip help'.";
            return null;
        }

        int expected = known.TakesArgument ? 2 : 1;

        if (words.Count != expected)
        {
            problem = known.TakesArgument
                ? $"'{words[0]}' takes exactly one argument. Try 'xmip help'."
                : $"'{words[0]}' takes no argument. Try 'xmip help'.";
            return null;
        }

        if (follow && known.Command != Command.Health)
        {
            problem = "--follow only applies to 'health'.";
            return null;
        }

        problem = string.Empty;

        return new Invocation(
            known.Command,
            known.TakesArgument ? words[1] : string.Empty,
            json || follow,
            follow,
            runtime);
    }
}
