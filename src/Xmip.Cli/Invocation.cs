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
/// <param name="Follow">Emit JSON Lines of health or activity as it changes, until
/// interrupted. Implies <see cref="Json"/>.</param>
/// <param name="Runtime">An explicit runtime library, overriding discovery.
/// Null when the discovery rule decides.</param>
public sealed record Invocation(
    Command Command, string Argument, bool Json, bool Follow, string? Runtime)
{
    private static readonly Dictionary<string, (Command Command, int Minimum, int Maximum)> Known =
        new(StringComparer.Ordinal)
        {
            ["help"] = (Command.Help, 0, 0),
            ["abi"] = (Command.Abi, 0, 0),
            ["status"] = (Command.Status, 1, 1),
            ["probe"] = (Command.Probe, 1, 1),
            ["health"] = (Command.Health, 1, 1),
            ["activity"] = (Command.Activity, 0, 1),
            ["list"] = (Command.List, 0, 1),
            ["show"] = (Command.Show, 1, 1),
            ["pause"] = (Command.Pause, 1, 1),
            ["resume"] = (Command.Resume, 1, 1),
            ["start"] = (Command.Start, 1, 1),
            ["stop"] = (Command.Stop, 1, 1),
            ["restart"] = (Command.Restart, 1, 1),
            ["validate"] = (Command.Validate, 1, 1),
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

        if (!Known.TryGetValue(words[0], out (Command Command, int Minimum, int Maximum) known))
        {
            problem = $"'{words[0]}' is not an xmip command. Try 'xmip help'.";
            return null;
        }

        int arguments = words.Count - 1;

        if (arguments < known.Minimum || arguments > known.Maximum)
        {
            problem = known.Minimum == 0 && known.Maximum == 0
                ? $"'{words[0]}' takes no argument. Try 'xmip help'."
                : known.Minimum == 0
                    ? $"'{words[0]}' takes at most one argument. Try 'xmip help'."
                    : $"'{words[0]}' takes exactly one argument. Try 'xmip help'.";
            return null;
        }

        if (follow && known.Command is not (Command.Health or Command.Activity))
        {
            problem = "--follow only applies to 'health' or 'activity'.";
            return null;
        }

        problem = string.Empty;

        return new Invocation(
            known.Command,
            arguments == 1 ? words[1] : string.Empty,
            json || follow,
            follow,
            runtime);
    }
}
