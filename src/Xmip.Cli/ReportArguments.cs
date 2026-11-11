using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// What <c>xmip report</c> takes (ADR-0072, amendment 2026-11-10): the
/// Report's name as the command's argument, every Report listed where it
/// names none; and, to open a Report that presents what a Quest probes,
/// <c>--configuration &lt;path&gt;</c> — a node's configuration, or a
/// cluster's <c>cluster.xmip</c>, which its Quest probes — with
/// <c>--node</c> for the node a cluster's file is sliced for (shared with
/// <c>certificate</c>, where it names the node a certificate is issued to),
/// <c>--parameter</c>, the choice the Report is opened for, and
/// <c>--row</c>, the row of its second level opened in full. What each
/// Report takes, and what it refuses, is the runtime's to say.
/// </summary>
public static class ReportArguments
{
    /// <summary>The options only <c>report</c> takes.</summary>
    public static IReadOnlyList<string> Options { get; } =
        ["--configuration", "--parameter", "--row"];

    /// <summary>
    /// Take <c>--configuration</c>, <c>--parameter</c> or <c>--row</c> at
    /// <paramref name="index"/>, and its value; false where the option is
    /// another. A problem where its value is missing.
    /// </summary>
    public static bool Take(
        IReadOnlyList<string> args, ref int index, ref ReportAsk? asked, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);

        problem = null;
        string option = args[index];

        if (!Options.Contains(option, StringComparer.Ordinal))
        {
            return false;
        }

        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            problem = option == "--configuration"
                ? "--configuration needs the path of a node's configuration or a cluster.xmip."
                : $"{option} needs a value.";
            return true;
        }

        string value = args[++index];
        ReportAsk taken = asked ?? new ReportAsk();
        asked = option switch
        {
            "--configuration" => taken with { Configuration = value },
            "--parameter" => taken with { Parameter = value },
            _ => taken with { Row = value },
        };
        return true;
    }

    /// <summary>
    /// What the line asks of <c>report</c>: the Report
    /// <paramref name="name"/>, every one where it is empty, probed where a
    /// configuration is given, for the node <c>--node</c> named in
    /// <paramref name="certificate"/>; null with <paramref name="problem"/>
    /// saying why where a probe names no Report, or <c>--parameter</c>,
    /// <c>--row</c> or <c>--node</c> is given with nothing to probe.
    /// </summary>
    public static ReportAsk? Ask(
        string name, ReportAsk? asked, CertificateQuery? certificate, out string? problem)
    {
        ReportAsk ask = (asked ?? new ReportAsk()) with { Name = name, Node = certificate?.Node };

        problem = ask switch
        {
            { Configuration: not null, Name.Length: 0 } =>
                "'report --configuration <path>' needs the Report to open: "
                    + "'report <name> --configuration <path>'.",
            { Configuration: null } when (ask.Parameter ?? ask.Row ?? ask.Node) is not null =>
                "--parameter, --row and --node open a Report that presents what a Quest "
                    + "probes, and its Quest probes a configuration: "
                    + "'report <name> --configuration <path>'.",
            _ => null,
        };
        return problem is null ? ask : null;
    }
}

/// <summary>What <c>xmip report</c> was asked: the Report
/// <paramref name="Name"/>, every one where it is empty; and, where
/// <paramref name="Configuration"/> names what its Quest probes, the
/// <paramref name="Parameter"/>, <paramref name="Row"/> and
/// <paramref name="Node"/> it is opened for.</summary>
public sealed record ReportAsk(
    string Name = "",
    string? Configuration = null,
    string? Parameter = null,
    string? Row = null,
    string? Node = null)
{
    /// <summary>The probe asked, or null where nothing is probed.</summary>
    public ReportProbe? Probe => Configuration is { } configuration
        ? new ReportProbe(Name, configuration) { Parameter = Parameter, Row = Row, Node = Node }
        : null;
}
