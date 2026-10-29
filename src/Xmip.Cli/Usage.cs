namespace Xmip.Cli;

/// <summary>The text behind <c>xmip help</c>.</summary>
public static class Usage
{
    /// <summary>The usage text, one line per command and one per option.</summary>
    public const string Text = """
        xmip — the Xmip command line

          xmip abi                  the two boundaries this build speaks
          xmip status <code>        what a status code means
          xmip probe <library>      load a module and report what it says it is
          xmip health <scope>       health at and beneath a scope, from the runtime
          xmip measure [scope]      streams, messages, journeys, bytes, retrying, failed
          xmip list [scope]         the direct children of a scope, the cluster by default
          xmip show <scope>         one scope: its mood, its worst leaf and why, its figures
          xmip pause <scope>        pause everything at and beneath a scope
          xmip resume <scope>       resume everything at and beneath a scope
          xmip validate <toml>      check a node configuration without starting it
          xmip certificate [file]
                                    a person's key and request (--operator) or
                                    its signing (--sign); a node's key and
                                    certificate from the cluster's authority
                                    (--node), or its request, signing and
                                    import across an air gap; the authority
                                    set up, inspected, revoked from, exported
                                    and backed up
          xmip database <server>
                                    the scripts that upgrade each database on
                                    a server from the schema version it holds
          xmip audit [pattern]      what the audit recorded, newest first
          xmip subscriptions [pattern]
                                    the Subscriptions the nodes route by; with an
                                    act, pause or resume one
          xmip event-subscriptions [pattern]
                                    the Event subscriptions the nodes hold; with an
                                    act, pause, resume or remove one
          xmip dead-messages [pattern]
                                    the Messages no Subscription matched, in each
                                    node's Dead Message Queue; open one, or
                                    replay it
          xmip journey [id]         list the Journeys that failed, or retry or
                                    dismiss one
          xmip secret [name]        the secrets a node keeps, never a value; with
                                    --set, set one through the node
          xmip mcp                  a Model Context Protocol server on stdin and
                                    stdout: the read commands as its tools, the
                                    scopes as its resources
          xmip help                 this text

          --json                    one JSON document instead of text
          --follow                  with health or measure: JSON Lines as they change
          --runtime <path>          the runtime library, instead of finding it
          --remote <host:port>      a node on another machine, at its operate address
          --local <endpoint>        a node on this machine, at its local endpoint
          --snapshot <path>         a published snapshot to read, instead of the document's
          --who <name>              with pause, or the act of subscriptions,
                                    event-subscriptions, dead-messages or
                                    journey: who acts, instead of the
                                    current user; over --remote the node takes
                                    the act as your certificate's subject, over
                                    --local as the user you run as

        A <scope> is one scope, or a wildcard over the scopes that exist:
        * for any run of characters, ? for exactly one, everything else
        literal, case-insensitive — what PowerShell's -like matches and what
        the GUI's filter box matches (ADR-0059 clauses 7 and 8).

          xmip health "xmip:///C1/node/R*"      every node of cluster C1 named R…
          xmip list "xmip:///C1/*/receive"      beneath every receive

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
        else the library beside this executable. A node is operated over gRPC
        to its operate listener, on this machine at its local endpoint and on
        another at its operate address, through the runtime's client in that
        library; validate and abi ask the library itself, with no node behind
        them. Nothing here links Xmip's Rust.

        mcp is for an MCP client that starts it (ADR-0071): JSON-RPC 2.0 on
        stdin and stdout, one message a line, and nothing else on stdout. Its
        tools are the read commands — abi, status, health, measure, list,
        show, validate, audit, subscriptions, event-subscriptions,
        dead-messages and journey, each with its options as arguments — and
        never an act. Its resources are the scopes, each an Xmip URI, read as
        an operator drills: the scope's row, then the rows beneath it.
        --runtime, --remote, --local and --snapshot on its line choose the surface
        every answer reads, and each answer is audited as its command. On
        another machine it is the same line over SSH.

        Every invocation is audited as program xmip (ADR-0062): the
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
          --severity <word>         Information, Warning or Error
          --action <word>           one action, exactly
          --from <time>             at or after; RFC 3339, or a date and time read as UTC
          --to <time>               at or before, the same
          --sort <column>           at, location, node, program, host, action, phase,
                                    severity or summary
          --order <word>            ascending or descending, newest first by default
          --offset <n>              where the page starts
          --limit <n>               how long a page: 100 by default, 1000 at most
          --include-hidden          the records of a run started hidden too, marked
                                    test; left out unless given
          --verify                  walk the audit chain of each writer of the records
                                    matched and say where it breaks, or OK, and
                                    say each record whose write was interrupted

          xmip audit "xmip:///C1/*" --severity Error
          xmip audit --location xmip:///C1 --from 2026-09-29

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

          xmip subscriptions --location xmip:///C1/node/P1
          xmip subscriptions --location xmip:///C1/node/P1 --name edi --pause --who ilian

        There is no --remove: a Subscription is added and removed in the TOML
        configuration of the Xmip Application that draws it, and the line is
        REFUSED in those words, exit 2.

        event-subscriptions lists the Event subscriptions the cluster's nodes
        hold (ADR-0065): the subscriber, a Party; the cluster and the node
        whose hub holds it; the action it subscribes to; its state, and what
        its queue queued, delivered and missed. The node is the one its
        subscriber connected to, not the one it hears: it hears the matching
        Events of every node of the cluster, and the links that carry them
        between nodes are the cluster's, never listed here; a member a
        node here does not hear is said under the table, one line each:
        "R1: not hearing <node> since <time>: <why>". [pattern] is * and ? over each
        one's node and the scope it reaches. It shares --location, --sort
        (subscriber, cluster, node, action, state, queued, delivered, missed,
        since) and --order with audit, and adds:

          --id <n>                  one Event subscription, by its number on its node
          --pause                   hold its delivery; its queue keeps filling
          --resume                  deliver again, what queued first
          --remove                  unsubscribe it

          xmip event-subscriptions --location xmip:///C1/node/R1
          xmip event-subscriptions --location xmip:///C1/node/R1 --id 2 --pause --who ilian

        dead-messages lists what each node's Dead Message Queue keeps
        (ADR-0052, amendment 2026-10-01): every accepted Message that no
        Subscription matched, kept in the Ledger with its receive context. It
        is not a dead letter queue: a failed Journey never goes there. A row
        is when it was received, the Message's identifier, the cluster and the
        node whose queue keeps it, the Receive Location it arrived at and how
        many Subscriptions declined it; a node lists its oldest hundred.
        [pattern] is * and ? over each one's node, or its node and Message as
        xmip:///<cluster>/node/<node>/dead-message/<message>. It shares
        --location, --sort (received, message, cluster, node,
        receive-location, declines) and --order with audit, and adds:

          --message <id>            one Message, by its identifier: opened, with
                                    its gate verdicts, its promoted properties
                                    and every Subscription's decline
          --replay                  once a Subscription is added or fixed: route
                                    it again, open a Journey for each match and
                                    take it out of the queue; a Message that
                                    still matches nothing stays

          xmip dead-messages --location xmip:///C1/node/R1
          xmip dead-messages --location xmip:///C1/node/R1 --message <id>
          xmip dead-messages --location xmip:///C1/node/R1 --message <id> --replay

        secret lists the secrets a node keeps — each by name, kind, and when
        and by whom it was set, never a value — and with --set sets one: the
        proof a Location presents to the party it connects to, named by its
        secret = "<name>" and kept in the Administration Database, sealed by
        Xmip (ADR-0019, amendment 2026-10-29). The value is never typed on
        the line: it is read from --file, or from standard input.

          --set                     set the secret [name] through the node
          --kind <kind>             password, key or certificate
          --file <path>             the file holding the value

          xmip secret
          xmip secret partner-x --set --kind key --file partner-x.key

        journey lists the Journeys that failed and acts on one
        (runtime-model.md section 13): a Journey leads to one Send Port, and
        when every Send Location of its Port failed its tries it is written
        Failed, with why, and waits in its Port's queue. With no <id>, every
        one at or beneath --location is listed, Port by Port: how many wait,
        and a page of them with why — read from Xmip Storage where the node
        runs in this process, or the oldest its publication carries
        ([tuning] failed_window of each Port, a hundred by default).

          --offset <place>          where each Port's page starts, 0 the
                                    oldest: read from Xmip Storage, a place
                                    in the Port's index of failed Journeys,
                                    as the last page's 'more' said; read
                                    from a publication, which carries only
                                    its node's window and no index places,
                                    a place in the Port's queue, and no
                                    'more' follows
          --limit <count>           the most of each Port; a hundred if unsaid

          xmip journey --location xmip:///C1/node/S1
          xmip journey --location xmip:///C1/node/S1/send/invoices --offset 40

        With an <id>, --location names the node that sends its Port, or the
        Port's scope beneath it, and one act is required:

          --retry                   send it again, its tries begun anew; where
                                    it blocks its sequence it keeps
                                    its place
          --dismiss                 give it up: written Dismissed, its history,
                                    Message and Stream kept, and taken out of
                                    the Port's queue

          xmip journey <id> --location xmip:///C1/node/S1/send/invoices --retry
          xmip journey <id> --location xmip:///C1/node/S1 --dismiss --who ilian

        certificate makes and signs the certificates a cluster's own
        authority stands behind (ADR-0034, amendments 2026-10-15 and
        2026-10-22). A person's, for reaching a node's operate listener —
        client authentication alone, CN=<person>:

          --operator <person>       make a fresh key and a request naming the
                                    person, as their directory knows them —
                                    alice@example.org — into --out, the
                                    current folder unless said:
                                    operator.key and operator.csr
          --sign <toml>             sign <request> by the authority the node
                                    configuration's [provision.operator]
                                    names, valid for its lifetime; a request
                                    asking more than client authentication
                                    for a person is REFUSED

        A node's, for server and client authentication, valid ninety days:

          --node <node>             with --authority <folder> and --cluster
                                    <cluster>, issue the node a fresh key and
                                    a certificate from the authority kept
                                    there, made where none is, for its own
                                    name and every one of --names
                                    <name,name>, into --out: node.pem,
                                    node.key and anchors.pem (ADR-0015,
                                    amendment 2026-10-21). Without
                                    --authority, on a node no network route
                                    reaches the authority from: write a fresh
                                    key, node.next.key, which stays, and a
                                    request, node.csr, into --out, to carry
          <request> --authority <folder>
                                    sign a carried node.csr by the authority
                                    kept there, never made: its signature
                                    verified, its names exactly --names where
                                    given; the chain to --out, never over a
                                    file, or to stdout, to carry back
          <chain> --import <folder> on the node, import the chain into the
                                    folder its request was made in: every
                                    signature checked, the leaf for
                                    node.next.key and the request's names,
                                    valid now, reaching anchors.pem where the
                                    node holds one; then node.pem, node.key
                                    and anchors.pem are written. Where the
                                    node held no anchor it says the
                                    authority's SHA-256: compare it by
                                    another way
          --inspect <path>          a certificate file: its leaf and whether
                                    its renewal is due — make the next request
                                    when it says RENEWAL DUE; an authority's
                                    folder: the authority and every
                                    certificate it signed with its standing

        The cluster's authority — the premises' own ACME service, which a
        node serves where [provision.node] names internal-ca and serve:

          --authority <folder> --cluster <cluster>
                                    set it up: kept there, made where none
                                    is; --publish http://host:port records
                                    the plain-HTTP address its revocation
                                    list (/crl) and certificate
                                    (/authority.cer) are fetched from, which
                                    every certificate it signs from then names
          --revoke <serial>         with --authority, revoke a certificate it
                                    signed, --reason <word> (keyCompromise,
                                    superseded, affiliationChanged,
                                    cessationOfOperation, privilegeWithdrawn;
                                    unspecified unless said), listed at once
          --export <file>           with --authority, write its certificate,
                                    --format pem (the default, a .crt for a
                                    Linux trust store), der (a .cer for
                                    Windows) or crl (its revocation list),
                                    never over a file
          --backup <folder>         with --authority, copy the whole
                                    authority, its key among it, into an
                                    empty folder; the copy is the same
                                    authority
          --out <path>              where a request, a chain or a node's files
                                    are written. Nothing of a person's is
                                    overwritten.

          xmip certificate --operator alice@example.org --out phone
          xmip certificate phone/operator.csr --sign node.toml --out phone/operator.pem
          xmip certificate --node R1 --cluster C1 --authority ca --names R1.xmip.test --out R1
          xmip certificate --node R1 --names R1.xmip.test --out tls
          xmip certificate tls/node.csr --authority ca --names R1,R1.xmip.test --out R1.pem
          xmip certificate R1.pem --import tls
          xmip certificate --inspect tls/node.pem
          xmip certificate --authority ca --cluster C1 --publish http://ca.xmip.test:8080
          xmip certificate --revoke 5B8E9CC6D36BF3DB --authority ca --reason keyCompromise
          xmip certificate --export ca.cer --authority ca --format der

        Running a signing is the approval: whoever may read the authority's
        key signs, after checking out of band that the request is the
        person's or the node's. Every act is audited.

        database writes the scripts that bring each Xmip database on a server
        IT runs — <server> is postgresql, mssql or mariadb — from the schema
        version it holds to this build's (ADR-0015, amendment 2026-10-20). Every
        database records its version, and every surface shows it, at
        xmip:///<cluster>/node/<node>/storage/<domain>. An upgrade only adds:
        tables, columns and indexes, and the version it reached. One script to
        each database, 04-<domain>-upgrade-<from>-<to>.sql, run as the schema's
        owner, as deploy/database/<server>/README.md says.

          --from <version>          the schema version the databases hold
          --to <version>            the version to bring them to; this build's
                                    unless said
          --out <folder>            write the scripts there, made where it is
                                    missing; printed unless said. Nothing is
                                    overwritten.

          xmip database postgresql --from 1 --out upgrade

        An act names one, by --location at its node and --name, --id,
        --message or journey's <id>; the node applies it and audits it, or
        — read through a snapshot — takes it within a round from where its
        publication says.
        An act not taken is REFUSED in words and exits 1.
        """;

    /// <summary>Print the usage text.</summary>
    public static int Print(TextWriter output)
    {
        output.WriteLine(Text);
        return 0;
    }
}
