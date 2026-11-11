using System.Text.RegularExpressions;

namespace Xmip.Cli.Test;

/// <summary>
/// The text behind <c>xmip help</c> (<see cref="Usage"/>) reads as one
/// table: every command's description, and every line that goes on with
/// it, starts at the column the first command's does.
/// </summary>
public sealed partial class UsageTest
{
    [Fact]
    public void EveryCommandsDescriptionStartsAtOneColumn()
    {
        string[] lines = [.. Usage.Text.Split('\n').Select(line => line.TrimEnd('\r'))];
        int first = Array.FindIndex(lines, line => Command(line) == "xmip abi");
        int last = Array.FindIndex(lines, line => Command(line) == "xmip help");
        int column = Described(lines[first]);

        Assert.True(first >= 0 && last > first && column > 0, "the commands are listed");

        for (int at = first; at <= last; at++)
        {
            string line = lines[at];

            if (line.Trim().Length == 0 || (Command(line) is not null && Described(line) < 0))
            {
                // A command alone on its line: its description is below it.
                continue;
            }

            int starts = Command(line) is null
                ? line.Length - line.TrimStart().Length
                : Described(line);

            Assert.True(column == starts, $"'{line.Trim()}' starts at {starts}, not {column}");
        }
    }

    // The command a line names, its arguments with it; null for a line that
    // goes on with the one above.
    private static string? Command(string line)
    {
        Match command = CommandLine().Match(line);
        return command.Success ? command.Groups["command"].Value.TrimEnd() : null;
    }

    // Where a command line's description starts; -1 where it has none.
    private static int Described(string line)
    {
        Match command = CommandLine().Match(line);
        return command.Success && command.Groups["gap"].Success ? command.Length : -1;
    }

    [GeneratedRegex(@"^\s*(?<command>xmip(?: \S+)*)(?<gap>\s{2,}(?=\S))?")]
    private static partial Regex CommandLine();
}
