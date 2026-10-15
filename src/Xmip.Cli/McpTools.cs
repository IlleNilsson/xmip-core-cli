using System.Globalization;
using System.Text.Json;

namespace Xmip.Cli;

/// <summary>
/// The tools <c>xmip-cli mcp</c> offers (ADR-0071): exactly the command
/// line's read commands, each named by its word on the line, its arguments
/// the command's options without their dashes. A call becomes the line an
/// operator would type — the word, its argument, its options, and
/// <see cref="McpServer.SessionLine"/> — and that line is parsed by <see cref="Invocation.Parse"/>
/// and answered by <see cref="CommandRun"/>, as typed. Which options a
/// command takes are its arguments' lists (<see cref="AuditArguments"/>,
/// <see cref="NounArguments"/> and the rest); nothing here repeats them.
/// </summary>
/// <remarks>
/// No act is a tool: <c>pause</c>, <c>resume</c>, and the acts of the nouns
/// (<see cref="NounArguments.Options"/>) are left out, and so is
/// <c>--who</c>. Read only is the Observer's reach (ADR-0009, amendment
/// 2026-09-06). <c>probe</c> is not a tool either: it loads the library it is
/// given, and running a library's code is not a reading; nor is
/// <c>certificate</c>, which makes a key or signs.
/// </remarks>
public static class McpTools
{
    // A tool: its word on the line, what it answers, the name and the
    // requiredness of its one argument where it takes one, the options that
    // take a value, and the options that are a flag.
    private sealed record Tool(
        string Name,
        string Description,
        string? Argument,
        bool Required,
        IReadOnlyList<string> Valued,
        IReadOnlyList<string> Flags);

    // The options whose value is a whole number.
    private static readonly string[] Counted = ["--offset", "--limit", "--id"];

    private static readonly Tool[] Tools =
    [
        new("abi", "The two boundaries this build speaks.", null, false, [], []),
        new(
            "status", "What an Xmip status code means.", "code", true, [], []),
        new(
            "health",
            "Health at and beneath a scope, worst first, with the rollup and the worst "
                + "leaf's evidence. scope is an Xmip URI, or a wildcard over the scopes "
                + "that exist (* and ?).",
            "scope",
            true,
            [],
            []),
        new(
            "measure",
            "The six figures at a scope: Streams, Messages, Journeys, bytes, Retrying "
                + "and Failed; an unpublished figure is null, never zero. The cluster "
                + "when no scope is named.",
            "scope",
            false,
            [],
            []),
        new(
            "list",
            "The direct children of a scope, one row each: its mood, its worst leaf "
                + "and why, its figures. The cluster when no scope is named.",
            "scope",
            false,
            [],
            []),
        new(
            "show",
            "One scope as a row: its mood, its worst leaf and why, its figures.",
            "scope",
            true,
            [],
            []),
        new(
            "validate",
            "Check a node configuration file without starting it.",
            "toml",
            true,
            [],
            []),
        new(
            "audit",
            "What the audit recorded, newest first; verify walks each writer's audit "
                + "chain and says where it breaks.",
            "pattern",
            false,
            AuditArguments.Options,
            [AuditArguments.IncludeHidden, AuditArguments.Verify]),
        new(
            "subscriptions",
            "The Subscriptions the cluster's nodes route by.",
            "pattern",
            false,
            [.. NounArguments.Shared, .. SubscriptionArguments.Options],
            []),
        new(
            "event-subscriptions",
            "The Event subscriptions the cluster's nodes hold.",
            "pattern",
            false,
            [.. NounArguments.Shared, .. EventSubscriptionArguments.Options],
            []),
        new(
            "dead-messages",
            "The Messages no Subscription matched, in each node's Dead Message Queue; "
                + "message opens one.",
            "pattern",
            false,
            [.. NounArguments.Shared, .. DeadMessageArguments.Options],
            []),
        new(
            "journey",
            "The Journeys that failed at the Send Ports at or beneath location, Port "
                + "by Port.",
            null,
            false,
            JourneyArguments.Shared,
            []),
    ];

    /// <summary>The tool names, in the order <c>tools/list</c> gives them.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Tools.Select(tool => tool.Name)];

    /// <summary>Write the <c>tools</c> array of a <c>tools/list</c> result.</summary>
    public static void WriteList(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartArray("tools");

        foreach (Tool tool in Tools)
        {
            writer.WriteStartObject();
            writer.WriteString("name", tool.Name);
            writer.WriteString("description", tool.Description);
            writer.WriteStartObject("inputSchema");
            writer.WriteString("type", "object");
            writer.WriteStartObject("properties");

            if (tool.Argument is { } argument)
            {
                writer.WriteStartObject(argument);
                writer.WriteString("type", argument == "code" ? "integer" : "string");
                writer.WriteEndObject();
            }

            foreach (string option in tool.Valued)
            {
                writer.WriteStartObject(option[2..]);
                writer.WriteString("type", Counted.Contains(option) ? "integer" : "string");
                writer.WriteEndObject();
            }

            foreach (string flag in tool.Flags)
            {
                writer.WriteStartObject(flag[2..]);
                writer.WriteString("type", "boolean");
                writer.WriteEndObject();
            }

            writer.WriteEndObject();

            if (tool.Required)
            {
                writer.WriteStartArray("required");
                writer.WriteStringValue(tool.Argument);
                writer.WriteEndArray();
            }

            writer.WriteBoolean("additionalProperties", false);
            writer.WriteEndObject();
            writer.WriteStartObject("annotations");
            writer.WriteBoolean("readOnlyHint", true);
            writer.WriteBoolean("openWorldHint", false);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// The command line a call of <paramref name="name"/> with
    /// <paramref name="arguments"/> is, <paramref name="after"/> after it —
    /// <see cref="McpServer.SessionLine"/>; null with
    /// <paramref name="problem"/> saying why where no tool has that name, an
    /// argument is not one it takes, a value is of the wrong kind, or a
    /// required argument is missing.
    /// </summary>
    public static IReadOnlyList<string>? Line(
        string name, JsonElement? arguments, IReadOnlyList<string> after, out string problem)
    {
        ArgumentNullException.ThrowIfNull(after);

        Tool? tool = Tools.FirstOrDefault(each => each.Name == name);

        if (tool is null)
        {
            problem = $"'{name}' is not a tool. The tools are: {string.Join(", ", Names)}.";
            return null;
        }

        List<string> line = [tool.Name];
        bool argued = false;

        if (arguments is { ValueKind: not (JsonValueKind.Object or JsonValueKind.Null) })
        {
            problem = "arguments is an object.";
            return null;
        }

        foreach (JsonProperty given in arguments is { ValueKind: JsonValueKind.Object } said
            ? said.EnumerateObject()
            : default)
        {
            string option = "--" + given.Name;

            if (given.Name == tool.Argument)
            {
                if (Value(given, tool.Argument == "code") is not { } word
                    || (word.StartsWith('-') && tool.Argument != "code"))
                {
                    problem = $"{given.Name} is a {Kind(tool.Argument == "code")}, "
                        + "and not an option.";
                    return null;
                }

                line.Insert(1, word);
                argued = true;
            }
            else if (tool.Valued.Contains(option))
            {
                if (Value(given, Counted.Contains(option)) is not { } value)
                {
                    problem = $"{given.Name} is a {Kind(Counted.Contains(option))}.";
                    return null;
                }

                line.Add(option);
                line.Add(value);
            }
            else if (tool.Flags.Contains(option))
            {
                if (given.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    problem = $"{given.Name} is true or false.";
                    return null;
                }

                if (given.Value.ValueKind == JsonValueKind.True)
                {
                    line.Add(option);
                }
            }
            else
            {
                problem = $"'{given.Name}' is not an argument of {tool.Name}.";
                return null;
            }
        }

        if (tool.Required && !argued)
        {
            problem = $"{tool.Name} needs {tool.Argument}.";
            return null;
        }

        line.AddRange(after);
        problem = string.Empty;
        return line;
    }

    // A value as the line types it: text as it is, a whole number in digits;
    // null where it is of another kind.
    private static string? Value(JsonProperty given, bool counted)
    {
        return (given.Value.ValueKind, counted) switch
        {
            (JsonValueKind.String, false) => given.Value.GetString(),
            (JsonValueKind.Number, true) when given.Value.TryGetInt64(out long number) =>
                number.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    private static string Kind(bool counted)
    {
        return counted ? "whole number" : "string";
    }
}
