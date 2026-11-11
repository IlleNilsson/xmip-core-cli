namespace Xmip.Cli;

// The part of the text behind xmip help that says report.
public static partial class Usage
{
    private const string Reports = """
        report lists every Report, in one list (ADR-0072, amendment
        2026-11-10). A Report presents existing data; a Quest probes for data
        to present in a Report. Each entry says which it is: Quick, it
        presents data that already exists and is opened at the command that
        presents it — xmip audit, xmip dead-messages, xmip subscriptions,
        xmip list — or Takes longer, it presents what a Quest probes first,
        and says how long its last probe took. An entry not built yet is
        listed and says not yet available. [name] shows one entry; a Report
        that presents what a Quest probes is opened with:

          --configuration <path>    the node configuration its Quest probes,
                                    or a cluster's cluster.xmip with --node
          --node <node>             the node a cluster.xmip is sliced for
          --parameter <choice>      what it is opened for, as its entry names:
                                    the regulation, GDPR
          --row <name>              one row of its second level in full: a
                                    requirement, what is behind it and the
                                    actions that close it

          xmip report
          xmip report Regulation --configuration cluster.xmip --node <node> --parameter GDPR
          xmip report Regulation --configuration node.xmip --parameter GDPR --row <name>

        A probe reads and changes nothing, so an Observer may run it. It is
        audited as quest.probe where this executable's records go, and the
        list reads each Quest's last probe back from there. A Report that is
        none, not yet available or quick, and a parameter or a row it does
        not take, are REFUSED in the runtime's words and exit 1.
        """;
}
