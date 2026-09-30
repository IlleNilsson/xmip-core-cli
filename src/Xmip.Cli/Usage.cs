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
          xmip-cli subscriptions [pattern]
                                    the Subscriptions the nodes route by; with an
                                    act, pause or resume one
          xmip-cli event-subscriptions [pattern]
                                    the Event subscriptions the nodes hold; with an
                                    act, pause, resume or remove one
          xmip-cli help             this text

          --json                    one JSON document instead of text
          --follow                  with health or measure: JSON Lines as they change
          --runtime <path>          the runtime library, instead of finding it
          --remote <url>            a web host to follow, instead of a runtime here
          --snapshot <path>         a published snapshot to read, instead of the document's
          --who <name>              with pause, or the act of subscriptions or
                                    event-subscriptions: who acts, instead of the
                                    current user

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

        subscriptions lists the Subscriptions the cluster's nodes route by
        (ADR-0013, amendment 2026-09-30). A Subscription picks a published
        Message up and opens a Journey: its name, the cluster and the node that
        routes by it, its filter, where it leads, its state, what it picked up
        and what it holds. [pattern] is * and ? over each one's node, or its
        node and name as xmip:///<cluster>/node/<node>/subscription/<name>. It
        shares --location, --sort (subscription, cluster, node, filter,
        destination, state, picked-up, held, since) and --order with audit,
        and adds:

          --name <subscription>     one Subscription, by its name on its node
          --pause                   hold what it matches: kept in the node's
                                    runtime store, counted, not picked up; a
                                    pause survives a restart of the node
          --resume                  pick up what it held, oldest first

          xmip-cli subscriptions --location xmip:///C1/node/P1
          xmip-cli subscriptions --location xmip:///C1/node/P1 --name edi --pause --who ilian

        There is no --remove: a Subscription is added and removed in the TOML
        configuration of the Xmip Application that draws it, and the line is
        REFUSED in those words, exit 2.

        event-subscriptions lists the Event subscriptions the cluster's nodes
        hold (ADR-0065): the subscriber, a Party; the cluster and the node
        whose hub holds it; the action it subscribes to; its state, and what
        its queue queued, delivered and missed. [pattern] is * and ? over each
        one's node and the scope it reaches. It shares --location, --sort
        (subscriber, cluster, node, action, state, queued, delivered, missed,
        since) and --order with audit, and adds:

          --id <n>                  one Event subscription, by its number on its node
          --pause                   hold its delivery; its queue keeps filling
          --resume                  deliver again, what queued first
          --remove                  unsubscribe it

          xmip-cli event-subscriptions --location xmip:///C1/node/R1
          xmip-cli event-subscriptions --location xmip:///C1/node/R1 --id 2 --pause --who ilian

        An act names one, by --location at its node and --name or --id; the
        node applies it and audits it, or — read through a snapshot — takes it
        within a round from where its publication says. An act not taken is
        REFUSED in words and exits 1.
        """;

    /// <summary>Print the usage text.</summary>
    public static int Print(TextWriter output)
    {
        output.WriteLine(Text);
        return 0;
    }
}
