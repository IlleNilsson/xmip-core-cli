namespace Xmip.Cli;

/// <summary>The text behind <c>xmip-cli help</c>.</summary>
public static class Usage
{
    /// <summary>The usage text, one line per command and one per option.</summary>
    public const string Text = """
        xmip-cli — the Xmip command line

          xmip-cli abi              the two boundaries this build speaks
          xmip-cli status <code>    what a status code means
          xmip-cli probe <library>  load a module and report what it says it is
          xmip-cli health <scope>   health at and beneath a scope, from the runtime
          xmip-cli measure [scope]  streams, messages, journeys, bytes, retrying, failed
          xmip-cli list [scope]     the direct children of a scope, the cluster by default
          xmip-cli show <scope>     one scope: its mood, its worst leaf and why, its figures
          xmip-cli pause <scope>    pause everything at and beneath a scope
          xmip-cli resume <scope>   resume everything at and beneath a scope
          xmip-cli validate <toml>  check a node configuration without starting it
          xmip-cli audit [pattern]  what the audit recorded, newest first
          xmip-cli help             this text

          --json                    one JSON document instead of text
          --follow                  with health or measure: JSON Lines as they change
          --runtime <path>          the runtime library, instead of finding it
          --remote <url>            a web host to follow, instead of a runtime here
          --snapshot <path>         a published snapshot to read, instead of the document's
          --who <name>              with pause: who is pausing, instead of the current user

        A <scope> is one scope, or a wildcard over the scopes that exist:
        * for any run of characters, ? for exactly one, everything else
        literal, case-insensitive — what PowerShell's -like matches and what
        the GUI's filter box matches (ADR-0059 clauses 7 and 8).

          xmip-cli health "xmip:///C1/node/R*"  every node of cluster C1 named R…
          xmip-cli list "xmip:///C1/*/receive"  beneath every receive

        C1 and R1 are names a tester gave; they mean nothing to Xmip, and a
        node receives because it declared receive, never because of its name.

        health, measure, list and show answer for each scope a pattern names,
        one after the other, and never add them together; pause and resume act
        on each. A pattern that matches nothing is REFUSED, naming the pattern
        and what there is, and exits 1 — never 0, which would read as all
        clear. <toml>, <library> and <code> name a thing, not a selection, and
        take no wildcard.

        One invocation reads one cluster. Two rolls are two clusters, each
        publishing its own snapshot, and a rollup or a sum over both would be
        at a scope in neither tree — so --snapshot names which one, and the
        web monitor is where an operator moves between them.

        The runtime is found by one rule, the same for every surface:
        Xmip:RuntimeLibrary in the configuration, else XMIP_RUNTIME_LIBRARY,
        else the library beside this executable. Every command answers over
        the C ABI in xmip-core-abi; nothing here links Xmip's Rust.

        Every invocation is audited as program xmip-cli (ADR-0062): the
        command as it begins and ends, a non-zero exit as a failure with what
        it said here, a line that could not be obeyed, anything unhandled.
        Records go to Xmip:AuditDirectory in the configuration, else
        XMIP_AUDIT_DIRECTORY, else the operating system's log.

        audit reads that file back, every program's records in it, through
        the audit capability's one reader — the one the web's Audit view and
        Get-XmipAudit read. Its options are the query's words:

          --location <scope>        at and beneath a scope a process declared
          --host <name>             programs that declared no location, on that machine
          --program <name>          one program, exactly
          --record <id>             one record, every field and property
          --severity <word>         information, warning or error
          --action <word>           one action, exactly
          --from <time>             at or after; RFC 3339, or a date and time read as UTC
          --to <time>               at or before, the same
          --sort <column>           at, location, node, program, host, action, phase,
                                    severity or summary
          --order <word>            ascending or descending, newest first by default
          --offset <n>              where the page starts
          --limit <n>               how long a page: 100 by default, 1000 at most

          xmip-cli audit "xmip:///C1/*" --severity error
          xmip-cli audit --location xmip:///C1 --from 2026-09-29

        [pattern] is * and ? over the location each record's process declared;
        a record with none is at the root, which only * names. Beneath the
        header come the groups one step down — clusters and hosts, a
        location's nodes and programs, a node's programs — then the records.
        A query the capability does not take is REFUSED in its words and
        exits 2; with no audit directory stated there is nothing to read,
        which is said, and exits 1.
        """;

    /// <summary>Print the usage text.</summary>
    public static int Print(TextWriter output)
    {
        output.WriteLine(Text);
        return 0;
    }
}
