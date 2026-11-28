# xmip-core-cli

The `xmip` executable — the command line over a running Xmip, in Rust and
needing no .NET (the owner, 2026-10-12: *the CLI has to be usable without
.NET. If you are on an MCU or any kind of edge device, .NET might not be the
way to go*), named as every System Process Xmip owns is named (ADR-0053). A
surface module (ADR-0011, ADR-0012 clause 11) among the operator surfaces
ADR-0014 names, and the executable ADR-0052 clause 5 describes: text for a
person, `--json` for a program, `--follow` as JSON Lines.

It is argument parsing and rendering over `operate::surface` in
xmip-core-operate, the surface every operator surface reads (ADR-0052), and
the runtime's crates beneath it, linked and called Rust to Rust through
traits — across a language boundary everything goes through the C ABI, and
Rust to Rust through traits (ADR-0014, amendments 2026-10-12 and
2026-11-17). The PowerShell module and the two GUI hosts reach the same
rules through the C ABI, so what `xmip` says and what a screen says cannot
disagree.

```text
xmip abi                  the two boundaries this build speaks
xmip status <code>        what a status code means
xmip probe <library>      load a module and report what it says it is
xmip health <scope>       health at and beneath a scope, from the runtime
xmip measure [scope]      streams, messages, journeys, bytes, retrying, failed
xmip list [scope]         the direct children of a scope, the cluster by default,
                          a node's row with what the node is for
xmip show <scope>         one scope: its mood, its worst leaf and why, its
                          figures, and for a node what it is for
xmip pause <scope>        pause everything at and beneath a scope
xmip resume <scope>       resume everything at and beneath a scope
xmip validate <toml>      check a node configuration without starting it
xmip certificate [file]
                          a person's key and request (--operator) or its signing
                          (--sign); a node's key and certificate from the
                          cluster's authority (--node), or its request, signing
                          and import across an air gap; the authority set up,
                          inspected, revoked from, exported and backed up
xmip database <server>
                          the scripts that upgrade each database on a server from
                          the schema version it holds (--from, --to, --out)
xmip audit [pattern]      what the audit recorded, newest first
xmip subscriptions [pattern]
                          the Subscriptions the nodes route by; pause or resume one
xmip event-subscriptions [pattern]
                          the Event subscriptions the nodes hold; pause, resume or
                          remove one
xmip dead-messages [pattern]
                          the Messages no Subscription matched, in each node's Dead
                          Message Queue; open one, or replay it
xmip message [id]         list the Messages that failed, or retry or dismiss one,
                          at a Send Port or a Work Process
xmip instances [work-process]
                          the Instances of each Work Process, running, waiting,
                          failed or done, with their values, and the Messages that
                          failed in its queue; with --instance <id>, one with its
                          history
xmip secret [name]        the secrets a node keeps, never a value; with --set,
                          set one through the node
xmip report [name]        every Report, quick or taking longer; with
                          --configuration, its Quest probes a configuration
xmip mcp                  a Model Context Protocol server on stdin and stdout
xmip help                 this text

--json                    one JSON document instead of text
--follow                  with health or measure: JSON Lines as they change
--remote <host:port>      a node on another machine, at its operate address
--local <endpoint>        a node on this machine, at its local endpoint;
                          on Linux and macOS in /run/xmip or /var/run/xmip,
                          or the folder XMIP_RUNTIME_DIRECTORY names
--snapshot <path>         a published snapshot to read, instead of the document's
--who <name>              with pause, or the act of subscriptions, event-subscriptions,
                          dead-messages or message: who acts, instead of the current user;
                          over --remote the node takes the act as your certificate's
                          subject, over --local as the user you run as
```

Which surface a command reads is stated in `cli.toml` beside the
executable, with the same `[Xmip]` keys as the GUI hosts and the PowerShell
module — `Surface = "local" | "snapshot" | "remote"`, `Local`, `Address`,
`Snapshot` — and never guessed (ADR-0052 clause 3). As shipped, in a
developer's clone, it follows the System Tests roll of the Reference
Environment, `<cluster>-snapshot.xmip`, `<cluster>` the short name that file
declares, which names its cluster — the same file the PowerShell prompt
follows, so `xmip show xmip:///<cluster>` answers while a roll runs and says
`SNAPSHOT — no file at ...` before one has. With no surface named, no node
is named, and a command says so. The document is read by configure's one
reader (ADR-0031, amendment 2026-11-17), and the build copies `cli.toml` from
this repository beside the executable it makes, `target/<profile>`, where it
is newer than the one there. An installed `xmip` finds the one
`xmip-service --install` wrote beside the programs where there was none —
`Surface = "local"` at the installed node's local endpoint, and no
`AuditDirectory` — in the words this executable reads it by
(`operate::surface::choice::table`), and kept, as the node's configuration
is, when Xmip is removed; a link on the path, `/usr/bin/xmip`, finds the
document beside the program it names, `/opt/xmip/bin` (`surface_open::folder`,
which follows links on Unix). The runtime is linked, not found: no library is
loaded and no option names one. The line wins
over the document, in one order — `--remote`, then `--local`, then
`--snapshot` — which is `operate::surface::choice::stated`, the same
precedence `Get-XmipHealth -Remote -Local -Snapshot` follows in PowerShell.
`--snapshot` reads one cluster's publication — what each source answers, and
whether an act applies at once or is submitted as an order, is the table
*What each source answers* in `module/core/operation/operate/README.md` — and
`--local xmip-node1` follows the node on this machine whose `[operate]
local` names that endpoint, over gRPC on a named pipe on Windows and a Unix
domain socket elsewhere (ADR-0014, amendment 2026-10-13): no certificate,
the operating system proving to the node which user you run as, and the
node taking every act as that user where the role its `[operate]` grants
may act. On Linux and macOS the socket is in its node's folder, named as the
endpoint is, `/run/xmip/xmip-node1/xmip-node1.sock` or
`/var/run/xmip/xmip-node1/xmip-node1.sock`, or in the folder
`XMIP_RUNTIME_DIRECTORY` names for a node no service manager started, which
the shell you type in must name as the node's did
(ADR-0014, amendment 2026-11-15). `--remote node1:7401` reads the
node at that operate address over gRPC (ADR-0014, amendment 2026-10-12): the
runtime's remote surface reads its publication and is told when it
advances, so `--follow` on another machine never polls, and an act is taken
by the node as your certificate's subject where the role its `[operate]`
grants may act. It is Xmip's mutual TLS always, presenting the certificate
the document's `Certificate` and `PrivateKey` name and taking the node only
where its certificate reaches `TrustAnchor` (else `XMIP_CERTIFICATE`,
`XMIP_PRIVATE_KEY`, `XMIP_TRUST_ANCHOR`; ADR-0063). At a node that gathers
its cluster's view — a node declaring the monitoring role — `--remote` and
`--local` answer for the whole cluster, every node's records and whether
each was reached, and `source`, in `health`, `measure`, `list`, `show` and
the other listings alike, says after where the node is reached what the
view says of itself: the cluster, gathered by which node, and how many of
its nodes it reached, the runtime's words (the design approved 2026-11-12,
section 3.5; `GatheredClusterTests`). A node gathering none answers for
itself; which of the two, its answer decides, never its name. `xmip status`
says what a status code means and reads no surface. `validate` asks the
runtime itself, since no node stands behind it, each Location held to its
technology's declaration as the desktop's library holds it
(`operate::catalogue::carried`; ADR-0064, amendment 2026-11-26), though
the command line loads no technology. Text
goes to stdout for a person, column-aligned, each line ended by a line feed;
`--json` emits one document; `--follow` subscribes to the shared operator
change stream and emits one JSON Lines record each time the snapshot it reads
changes, until Ctrl+C ends it cleanly (ADR-0014 clause 10). A document
escapes what the command line always escaped — a quote, `<`, `>`, `&`, `'`,
`+`, `` ` ``, every control character and everything beyond ASCII — so what a
pipe reads stays what it read. Complaints go to stderr with a
non-zero exit: 2 when the line could not be obeyed, 1 when the thing asked
about is wrong.

`measure`, `list`, `show`, `pause` and `resume` render the `Figures`,
`ScopeItem` and `ScopeOperation` of `operate::surface`, the shapes the
PowerShell module emits as objects; `validate` renders its
`ConfigurationVerdict`, `status` abi's `StatusMeaning` and `abi` its
`Boundaries`, and `probe` says whether a module loaded and
conforms by the runtime loader's own answer, the one `xmip_module_probe_v1`
hands the cmdlet. A mood is the word `english::mood` gives it, lower
case, in text and JSON alike, and a row with no health recorded is `nothing
recorded`; a figure is `english::figure`'s, and one the runtime has not
published is a dash, never a zero; `Nothing at`, `Nothing beneath` and
`Nothing measured at` are `english`'s sentences too. A wildcard scope selects
through `ScopeSelection`, the one the cmdlets use, and which rows `show`
gives — the scopes that exist, worst first under a wildcard — is
`ScopeItem::selected`, the one `Get-XmipScope` calls. Whatever a wildcard
answered is one JSON document of one shape for every command: `pattern`,
`source`, `matched`, and `scopes`, an object per scope. Pause and resume are
the two acts the operator boundary carries (ADR-0027 clause 5); there is no
start, stop or restart, because the thing that watches must not be able to
stop the thing it watches. Who paused is `--who`, else the user the command
runs as — `acted::who`, the rule `Suspend-XmipScope -Who` and the GUI
follow. Over `--remote` no name crosses the wire: the web host takes an act as
the subject of the client certificate this side presented, only where its role
may act, and refuses it in words otherwise (ADR-0009, amendment 2026-10-03).

**A person's certificate.** `certificate` makes and signs the certificate a
person's surface — the phone first — presents to a node's operate listener
(ADR-0034, amendment 2026-10-15): client authentication alone, its subject
the person, `CN=<person>`, the caller the listener proves and grants the
role its `[operate]` lists `CN=<person>` with, none where none does.
`xmip certificate --operator alice@example.org --out
phone` makes a fresh key and a request naming the person as
`phone/operator.key` and `phone/operator.csr`; `xmip certificate
phone/operator.csr --sign node.xmip --out phone/operator.pem` signs it by the
authority that node configuration's `[provision.operator]` names, for its
`lifetime`, the chain to stdout where `--out` names no file. The file is the
node's signed slice, read as its trust admits it, so one edited by hand is
refused (ADR-0031, amendment 2026-11-19). Running `--sign`
is the approval, out of band. A request asking more than client
authentication for a person is refused in the runtime's words, nothing is
overwritten, and both exit 1 when refused. The key and the request are written
by the runtime as private files — readable by the user it runs as alone, the
key never in the CLI's memory — and the chain whole and synced by the runtime
too; they and the rules are the runtime's
(`xmip_operate.h` section 19), called in `runtime::operator_certificate`
and `operate::certificate`, the same `New-XmipCertificate` reaches through
the C ABI. It is no MCP tool: it makes a key or signs.

**A node's certificate.** `xmip certificate --node RX1 --cluster CL1
--authority <folder> --names RX1.xmip.test,CL1 --out <folder>` issues a node
put on a machine of its own a fresh key and a certificate for server and
client authentication from the cluster's own authority kept in that folder,
made there where none is, for its own name and every one of `--names`,
written as `node.pem`, `node.key` and `anchors.pem` — the files its
`[operate]` names — in place of what was there (ADR-0015, amendment
2026-10-21; `xmip_operate.h` section 22, the rule `New-XmipCertificate
-Node` reaches through the C ABI).

**The cluster's authority** (ADR-0034, amendment 2026-10-22), the premises'
own ACME service. `xmip certificate --authority <folder> --cluster CL1
--publish http://ca.xmip.test:8080` sets it up — kept there, made where none
is — and the plain-HTTP address its revocation list and certificate are
fetched from, which every certificate it signs from then names.
`--inspect <folder>` says the authority, its SHA-256, its list and every
certificate it signed with its standing; `--revoke <serial> --authority
<folder> --reason keyCompromise` revokes one, listed at once; `--export
<file> --authority <folder> --format pem|der|crl` writes its certificate
for a Linux or Windows trust store, or its list, never over a file;
`--backup <folder> --authority <folder>` copies the whole authority, its
key among it, into an empty folder.

**A node's certificate across an air gap.** On the node, `xmip certificate
--node RX1 --names RX1.xmip.test --out <folder>` — no `--authority` — writes
a fresh key, `node.next.key`, which stays, and the request `node.csr`, to
carry. Where the authority is kept, `xmip certificate node.csr --authority
<folder> --names RX1,RX1.xmip.test --out node.pem` verifies and signs it for
ninety days. Back on the node, `xmip certificate node.pem --import
<folder>` checks every signature, the key, the names and the anchor, and
only then writes `node.pem`, `node.key` and `anchors.pem`; where the node
held no anchor it says the authority's SHA-256 to compare by another way.
`--inspect <file>` says whether a certificate's renewal is due. Each act is
the runtime's (`xmip_operate.h` section 23, `operate::authority`, the same
`New-XmipCertificate` and `Get-XmipCertificate` reach through the C ABI),
audited as every command is, and no MCP tool.

**The drill.** `list` with no scope lists what is beneath the cluster the
surface publishes at (`OperatorSurface::root`), never the one row of the
cluster itself, and every row carries its figures; a node and a stage have
figures of their own. A row that is not fine says on the line beneath it
which leaf explains it and why — `worst <scope>: <evidence>`, the next scope
to type — and `show` always does, so `list`, `list <that scope>`, … reaches
the cause the way the web's drill and `Get-XmipScope` do (ADR-0052; the
owner, 2026-09-26: *drill-down does not work*). JSON carries it as `worst`.

## What it audits

Every invocation is audited as program `xmip` through the audit
capability, linked (ADR-0062; `command_audit` over `xaudit`'s
`ProgramAudit`, handed the file sink and the operating system's log of its
platform): the command as it begins (action the command's name, lower case
— `eventsubscriptions` and `deadmessages` for the two hyphened words, as it
always was — phase `Begin`, the arguments and options as properties — a
web host's user and password are left out by the capability, as they are
from every program's record), its end (`Finished`, with `exit`), a
non-zero exit as a `Failure` with its exit code and what it said on stderr, a
line that could not be obeyed as `refused`, and anything unhandled as
`unhandled` before the process ends as it would have. Records go to
`<AuditDirectory>/audit.toml`, `AuditDirectory` in `cli.toml`'s `[Xmip]`
table resolved from beside the executable; unset, the capability decides —
`XMIP_AUDIT_DIRECTORY`, else the operating system's log, which also takes a
record the directory cannot.

## MCP

`xmip mcp` is a Model Context Protocol server (ADR-0071): an MCP client
starts it and speaks JSON-RPC 2.0 on its standard input and output, one
message a line — the messages and the framing `net::rpc`'s, the one the VS
Code language server speaks — protocol revision `2025-11-25` (and `2025-06-18` and
`2025-03-26` when a client asks for one of them). Stdout carries protocol
messages and nothing else; what the server says of itself goes to stderr. It
answers `initialize`, `ping`, `tools/list`, `tools/call`, `resources/list`,
`resources/templates/list` and `resources/read`, takes
`notifications/initialized` and every other notification unanswered, and
answers any other method with JSON-RPC's *method not found*.

**Its tools are the read commands**, named by their word on the line —
`abi`, `status`, `health`, `measure`, `list`, `show`, `validate`, `audit`,
`subscriptions`, `event-subscriptions`, `dead-messages`, `message` and
`report` — each taking its options without the dashes as arguments
(`mcp::tools`: `scope` or `pattern`, `location`, `severity`, `verify`, `name`,
`configuration` and the rest). `report` is one because a Quest reads what it
probes and changes nothing (ADR-0072, amendment 2026-11-10). A call is the line
an operator would type, `--json` added, parsed by `Invocation::parse` and
answered by `run::answer`, the dispatch the executable runs; its text is the command's
JSON document, `isError` where the command exits non-zero, with what it said
on stderr beneath. No act is a tool: `pause`, `resume`, the nouns' acts and
`--who` are not arguments, a line that would act is REFUSED, and `probe` is
left out because loading a library runs its code. Read only is the
Observer's reach (ADR-0009, amendment 2026-09-06).

**Its resources are the scopes.** Every scope is an Xmip URI (ADR-0027
clause 3): `resources/list` gives every scope the surface publishes, the
templates are the tree — `xmip:///{cluster}`, `xmip:///{cluster}/node/{node}`
and the path beneath a node — and `resources/read` drills as an operator
does: the scope's row as `show` gives it, then the rows beneath it as `list`
does, two JSON documents under the URI. A URI with a wildcard is not a
resource; the tools take wildcards. A scope with nothing at it is MCP's
*resource not found*, in `show`'s words.

`--remote`, `--local` and `--snapshot` on the `mcp` line choose the
surface every answer reads, as they do for any command. Every answer is
audited as its command (`command_audit`), the session as `mcp`. On another
machine it is the same line over SSH, as every command is (ADR-0014 clause
6): a client configured to start `ssh <host> xmip mcp` reaches that
machine's Xmip with SSH's authentication, and Xmip adds no listener.

## Subscriptions

`xmip subscriptions` lists the Subscriptions the cluster's nodes route by
(ADR-0013, amendment 2026-09-30; ADR-0052). A Subscription picks a published
Message up and opens a Journey; it is drawn in an Xmip Application and bound
in a node's TOML. Each one's name, the cluster, the node, its filter, where
it leads, its state, what it picked up, what it holds and since. One command
for the noun, and the act an option on it:

```text
xmip subscriptions [pattern] --location <scope> --name <subscription>
  --sort <column> --order ascending|descending --json
xmip subscriptions --location <node scope> --name <subscription> --pause|--resume
  --who <name> --json
```

The pattern is `*` and `?` over each Subscription's node, or its node and
name as `xmip:///<cluster>/node/<node>/subscription/<name>`; `--location`,
`--sort` and `--order` mean what they mean for `audit`, and the columns are
subscription, cluster, node, filter, destination, send-ports — the Send
Ports its destination reaches, `send_ports` in a document (ADR-0013,
amendment 2026-11-11) — state, picked-up, held and since. `xmip health`,
`list` and `show` say on each record and row where its work came from and
went, `from <from> → to <to>`, and a document's `from` and `to`, the
runtime's words for the nearest thing the publication draws at or above it
(`OperatorSurface::ends`; ADR-0052, amendment 2026-11-11). `xmip health`
says the cluster as every surface shows it, `cluster <short name>` and, in
parentheses where the publisher names it, what a tooltip would carry; a
document's `cluster` holds both, `shown` and `title` (`cluster_named`;
ADR-0028, amendment 2026-11-28). On a node's own
row, `xmip:///<cluster>/node/<name>`, `list` and `show` say beneath it what
the node is for, each role it declares with its meaning, a line each —
`receiving: takes Streams in and makes Messages` — and a document its
`purpose`, the same words (`ScopeItem.Purpose`, the runtime's
`node::NodeRole::purpose`); no other row has one (ADR-0056, amendment
2026-11-13; the owner, 2026-11-12: *what are nodes purpose*). Which
Subscriptions a line selects and in what order is
`operate::surface::subscription_query`'s, the one the web's Subscriptions view
and `Get-XmipSubscription` ask. An act names one Subscription, by
`--location` at its node and `--name`, or it is REFUSED before any node is
asked (exit 2). Paused, the Messages a Subscription matches are held — kept
in the node's runtime store, counted, not picked up — and a pause survives a
restart of the node; resumed, it picks up what it held, oldest first. There
is no `--remove`: a Subscription is added and removed in the TOML
configuration of the Xmip Application that draws it, and the line is REFUSED
in those words (exit 2). Over a live node the act is asked at the node's
door, written to Xmip Storage and taken by the node that routes by the
Subscription, audited there as `subscription.pause` or `subscription.resume`
(ADR-0014, amendment 2026-11-13); a snapshot is never acted on, and the act
is declined in words. `OK.` and exit 0 when it was taken, `ASKED.` and exit 0
when it was asked and its node has not taken it yet — it takes it as soon as
it runs — `REFUSED:` and exit 1 when it was not.

## Event subscriptions

`xmip event-subscriptions` lists the Event subscriptions the cluster's
nodes hold (ADR-0065, amendment 2026-09-29): each one's number on its node,
the subscriber (a Party), the cluster, the node, the action it subscribes to,
its state, queued against capacity, delivered, missed and since. The node is
the one its subscriber connected to, not the one it hears: an Event
subscription on any node hears the matching Events of every node of the
cluster (ADR-0065, amendment 2026-10-02), and the links that carry them
between nodes are the cluster's, not Event subscriptions, and never listed.
A member a node there does not hear is said read-only under the table, one
line each — `RX1: not hearing xmip:///CL1/node/TX1 since
2026-10-02T12:00:00Z: connection refused` — and as `unheard` in `--json`,
so no Event is missing silently.
An Event subscription is not a Subscription: it hands Events to a Party, and picks no
Message up. One command for the noun, and the act an option on it:

```text
xmip event-subscriptions [pattern] --location <scope> --id <n>
  --sort <column> --order ascending|descending --json
xmip event-subscriptions --location <node scope> --id <n>
  --pause|--resume|--remove --who <name> --json
```

The pattern is `*` and `?` over each Event subscription's node and the scope
it reaches; `--location`, `--sort` and `--order` mean what they mean for
`audit`, and the columns are subscriber, cluster, node, action, state, queued,
delivered, missed and since. Which Event subscriptions a line selects and in
what order is `operate::surface::event_subscription_query`'s, the one the web's
Event subscriptions view and `Get-XmipEventSubscription` ask. An act names
one Event subscription, by `--location` at its node and `--id`, or it is
REFUSED before any node is asked (exit 2); paused, an Event subscription
keeps queuing and hands nothing over, resumed it hands over what queued,
removed it is gone. Over a live node the act is applied in its process and
audited as `event.pause`, `event.resume` or `event.remove`; a snapshot is
never acted on, and the act is declined in words. `OK.` and exit 0 when it
was taken, `REFUSED:` and exit 1 when it was not.

## Dead Message Queue

`xmip dead-messages` lists what each node's Dead Message Queue keeps
(ADR-0052, amendment 2026-10-01): every accepted Message that no
Subscription matched, kept in the Ledger with its receive context, what its
gates concluded, its promoted properties and every Subscription's reason for
declining. It is not a dead letter queue: a failed Message never goes there.
A row is when it was received, the Message's identifier, the cluster, the
node whose queue keeps it, the Receive Location it arrived at and how many
Subscriptions declined it; a node publishes its oldest hundred. One command
for the noun, and the act an option on it:

```text
xmip dead-messages [pattern] --location <scope> --message <id>
  --sort <column> --order ascending|descending --json
xmip dead-messages --location <node scope> --message <id> --replay
  --who <name> --json
```

The pattern is `*` and `?` over each entry's node, or its node and Message as
`xmip:///<cluster>/node/<node>/dead-message/<message>`; `--location`,
`--sort` and `--order` mean what they mean for `audit`, and the columns are
received (the default, oldest first), message, cluster, node,
receive-location and declines. Which entries a line selects and in what
order is `operate::surface::dead_message_query`'s, the one the web's Dead
Message Queue view and `Get-XmipDeadMessage` ask. `--message` alone opens
the one Message: its gate verdicts, its promoted properties and every
Subscription's decline, in the order written; `--json` carries them as
`validation`, `promoted` and `declines`, each a list of `name` and `value`.
`--replay`, once a Subscription is added or fixed, routes the Message again
against the node's Subscriptions of now, opens a Journey for each match and
takes it out of the queue, once; a Message that still matches nothing stays
and the Replay is REFUSED in words. A Replay names one Message, by
`--location` at its node and `--message`, or it is REFUSED before any node is
asked (exit 2); `--pause`, `--resume` and `--remove` are no act on it (exit
2). Over a live node the Replay is asked at the node's door, written to Xmip
Storage and taken by the node whose queue keeps the Message, audited there
as `dead-message.replay` (ADR-0014, amendment 2026-11-13); a snapshot is
never acted on, and the Replay is declined in words. `OK.` and exit 0 when it
was taken, `ASKED.` and exit 0 when its node has not taken it yet, `REFUSED:`
and exit 1 when it was not.

## A Work Process's Instances

`xmip instances [work-process]` lists the Instances of each Work Process the
nodes at or beneath `--location` run (runtime-model.md section 22; ADR-0066,
amendment 2026-11-25; the owner, 2026-11-25: *A Work Process is like an
Orchestration in BizTalk. So it is aware via subscriptions, even when running
or not*), the one its argument names where it names one:

```text
xmip instances [work-process] --location <scope> --instance <id> --json
```

Each Work Process says on which node it runs, how many Instances the list
holds and how many Messages that failed wait in its queue; each Instance its
identifier — the Journey of the Message that started it — and whether it is
running, waiting, failed, dismissed or completed, what it waits for —
`message`, `time` or both — until its earliest deadline, each Correlation it
holds a key in with the key, its own values and its turns. Each Message that
failed in the queue is listed with its Journey, its place and why, and the
line that retries it, `xmip message <journey> --retry --location <node>`:
an Instance takes no act of its own (exit 2), and a Message that failed it is
retried or dismissed as every failed Message is, by the node that runs the
Work Process. `--instance` opens one: its state, its code and why where it
failed, its keys, values and turns, and its history of Messages — when, what
(started, arrived, due, made, published, failed, retried or dismissed), the
Message and the Journey — the newest sixty-four, the earlier counted. A node
publishes the newest twenty of each Work Process it runs; which are listed is
`operate::surface::instance_query`'s, the one the web's Instances view and
`Get-XmipInstance` ask. `--json` writes `xmip_operate.h` section 29's shape,
each pair as `name` and `value`.

## A Message that failed

`xmip message` lists the Messages that failed and retries or dismisses
one (runtime-model.md section 13; ADR-0013). A Message travels to one Send
Port on each Journey; when every Send Location of its Port failed its tries it is written
Failed, with why, and waits in its Port's queue. The node publishes, at the
Port's scope `xmip:///<cluster>/node/<node>/send/<Port>`, what the Port
sent, what failed, how many failed wait in its queue and the last that
failed and why. With no Journey named, every Message that failed at or beneath
`--location` — a cluster, a node or one Send Port's scope — is listed Port by
Port: how many wait, and a page of them, each its Journey's identifier, its place in
the queue and why — read from Xmip Storage where the node runs in the
process, or the oldest its publication carries (`[tuning] failed_window` of
each Port, a hundred by default). `--offset` is where each Port's page
starts, 0 the oldest, and `--limit` the most of each Port. Read from Xmip
Storage, the list is the Port's index of failed Messages, in the order they
failed: `--offset` is a place in that index, as the last page's `more:`
said. A publication carries only its node's window, each Message with its
place in the Port's queue and none in the index, so read from one
`--offset` is a place in the Port's queue and no `more:` follows — the two
lists are kept in two places, and a `more:` is only ever given back to the
surface that said it:

```text
xmip message --location <scope> --offset <place> --limit <count> --json
```

A list says plainly which of three it is. An answer exits 0, and where none
fails it says *No Message that failed waits at or beneath* and the scope. A surface
that cannot list failed Messages — no node reached, no runtime loaded, no
publication read yet — says `NOT LISTED:` on stderr, that whether any wait is
not known and not that none do, and exits 1. A surface that asked and was
not answered, Xmip Storage not answering the node, says `FAILED:` with the
runtime's words and exits 1. `--json` carries the same as `listed` and
`failure` beside `failed_messages`, with the same exit.

With a Message's Journey named, the identifier is the argument, `--location` names the node that
sends the Port or the Port's scope beneath it, and one act is required:

```text
xmip message <id> --location <node or Send Port scope> --retry|--dismiss
  --who <name> --json
```

`--retry` writes it Active, its tries begun anew, and sends it again from
the end of its Port's queue — or from its place, where it blocks its
sequence; `--dismiss` writes it Dismissed, its history, Message
and Stream kept, and takes it out of the queue. A Journey named with no
act, an act with none named, or `--pause`, `--resume`, `--remove` or
`--replay` cannot be obeyed (exit 2),
and `--retry` and `--dismiss` apply to no other command; one with no
`--location` on a node is REFUSED before any node is asked (exit 2). Over a
live node the act is asked at the node's door, written to Xmip Storage and
taken by the node that sends the Port, audited there as `message.retry` or
`message.dismiss` (ADR-0014, amendment 2026-11-13); a snapshot is never
acted on, and the act is declined in words. A Message that is no longer
Failed, or a node that does not send its Port, is REFUSED in the node's
words. `OK.` and exit 0 when it was taken, `ASKED.` and exit 0 when its node
has not taken it yet, `REFUSED:` and exit 1 when it was not; `--json` carries `node`, `journey`, `act`, `applied`
and `result`.

## A secret

A Location names the proof it presents to the party it connects to with
`secret = "<name>"`; the secret is kept in the Administration Database, its
value sealed by Xmip, and never in the TOML (ADR-0019 and ADR-0063,
amendments 2026-10-29).

```text
xmip secret --location <node scope> --json
xmip secret <name> --set --kind password|key|certificate --file <path>
  --location <node scope> --who <name>
```

`secret` alone lists what the node keeps: each secret's name, kind, and when
and by whom it was set — never a value; no surface reads one back. `--set`
sets the secret the argument names, through the node at `--location` — the
one node the surface reaches where it names none — admitted by role and
audited with the name, never the value (`xmip_operate.h` section 24,
`xmip_secret_v1` and `xmip_secrets_v1`). The value is never typed on the
line, where every process on the machine could read it: it is read from
`--file`, or from standard input where the line names none, its line end
taken off. Exit 0 when it was set, 1 when it was not, 2 for a line the
command does not take — a name with no `--set`, a set with no `--kind`, or
`--set` on another noun. `Set-XmipSecret` is the same act in PowerShell.

## Reports

A Report presents existing data; a Quest probes for data to present in a
Report. `xmip report` lists every Report in one list, Xmip's own and every
one a module adds, and each says which it is (ADR-0072, amendment
2026-11-10 and 2026-11-12): *Report*, it presents data that already exists,
or *Probing report*, it presents what a Quest probes first.

```text
xmip report [name] --json
xmip report <name> --configuration <path> [--node <node>]
  [--parameter <choice>] [--row <name>] --json
```

The list says both kinds in the runtime's words, then a row per Report: its
kind, its name, what to expect and what it covers, the long sentence last.
What to expect is the runtime's: *last probe took 4 ms*, read back from the
audit where this executable's records go, *not probed yet*, or *not yet
available* for one listed and not built. `xmip report <name>` shows one: a
quick one with the command that presents it — `xmip audit`, `xmip
dead-messages`, `xmip subscriptions`, `xmip list`, mapped here from the
reader's entrypoint the runtime names — and a Quest's with its parameter's
choices, its levels and the line that probes it; the name is the runtime's
to find, and one that is none is refused in its words, naming those there
are. `--configuration` opens a Report that presents what a Quest probes:
its Quest probes a node's configuration, or a cluster's `cluster.xmip`
sliced for `--node`, told apart by what the file holds, and changes
nothing. A quick Report, or one not yet available, is refused for what it
is before the file is read. It prints the tally, each state in the
runtime's words with its count, and a row per requirement under the
runtime's word for a row, or with `--row` one requirement in full, what is
behind it and the actions that close it; what a Quest does not answer is
left out. Where the audit cannot be read, every Report is listed all the
same, each Quest's entry says its last probe could not be read, and the
runtime's sentence follows the list. `--node` is `certificate`'s option too, read here as the node
a cluster's file is sliced for; `--configuration`, `--parameter` and
`--row` are this command's alone. `--json` is the runtime's document
whole: the list, one entry, or the probe.

The list, each entry's kind, what a probe finds and every refusal are the
runtime's (`xmip_operate.h` section 25, `runtime::report`, the same
`Get-XmipReport` and the Reports view read through the C ABI). Each
probe is audited by the runtime as `quest.probe` under program `xmip`,
where this executable's records go. A probe reads, so an Observer may run
it, and it is an MCP tool. A Report that is none, not yet available or
quick, a parameter or row it does not take, and a runtime that is a remote
build are REFUSED in words, exit 1, `{"refused": …}` under `--json`; a
probe naming another option than its own, or nothing to probe, is a line
that cannot be obeyed, exit 2.

## Reading the audit

`xmip audit` reads that file back — every program's records in it, not
only this one's — through the audit capability's one reader
(`operate::audit_read`, the one `xmip_audit_read_v1` answers by; ADR-0062,
amendment 2026-09-29), the reader the web's Audit view and `Get-XmipAudit`
call. Its argument and options are the query's words, and what each means is
the capability's:

```text
xmip audit [pattern] --location <scope> --host <name> --program <name>
  --record <id> --severity <word> --action <word> --from <time> --to <time>
  --sort <column> --order ascending|descending --offset <n> --limit <n>
  --verify --json
```

The pattern is `*` and `?` over the location each record's process declared
(ADR-0053 clause 3); a record with none is at the root, which only `*` names.
`--location` is a scope and everything beneath it, `--host` the records of
programs that declared no location on that machine, `--program` one program
exactly: who a record is, is never read out of a program's name. `--from` and
`--to` are RFC 3339, or a date and time with no zone, read as UTC. Newest
first, 100 records a page and 1000 at most.

Text is a header — `N of M records in <file>` — then the groups one step down
the drill (clusters and hosts at the top, a location's nodes and its own
programs, a node's programs), then a table: time, node, program, action,
phase, severity, summary. `--record <id>` prints that record's every field,
its scope and its properties. `--json` is the read whole: `file`, `read`,
`matched`, `offset`, `limit`, `groups`, `records`, and the `actions`,
`columns` and `severities` there are to choose from.

`--verify` walks the audit chain of each writer of the records matched —
a node's location, or a program's name — whole, and prints one sentence for
each after the records: OK and how many records, or FAILED and the first
place it breaks, a record deleted, changed or out of order (ADR-0070 clause
5); `chains` in JSON, each with `writer`, `records`, `whole` and `said`.
After them comes one sentence for each part of the file that is not a whole
record: FAILED for a record whose write was interrupted, which the next
append kept as written and sealed, in no chain; UNFINISHED for the file's
end, a write still going on or not yet sealed; `fragments` in JSON, each
with `offset`, `length`, `sealed` and `said`. A chain that breaks exits 1,
and so does a sealed fragment; an unfinished end does not. It reads no payload, so an Observer may run it
(ADR-0009, amendment 2026-09-06). A query the capability
does not take is its REFUSED sentence on stderr, exit 2; with no audit
directory stated the records went to the operating system's log, which is
said, exit 1.

## Why this is Rust and not .NET

The owner, 2026-10-12: *the CLI has to be usable without .NET. If you are
on an MCU or any kind of edge device, .NET might not be the way to go*;
and *everything will go through the ABI unless it is Rust2Rust, then it is
traits*. So the command line links xmip-core-operate and the runtime's
crates and calls their Rust, and no native library is loaded and no C ABI
crossed (ADR-0014, amendments 2026-10-12 and 2026-11-17). The PowerShell
module and the two GUI hosts stay .NET for their tooling, and reach the
same rules through the C ABI. Until 2026-11-17 this repository was the
.NET 11 executable, `src/Xmip.Cli`; it was ported in place, every command,
argument, output, exit and audit record kept, and deleted.

## Shape

The package `xmip-core-cli`, a member of the estate's one workspace and its
own root when checked out alone (ADR-0016, amendment 2026-11-20), with one
binary, `xmip`.
`src/invocation.rs` and `src/invocation/` parse the line into a command, its
one argument and its options, knowing nothing of the runtime or the
console; one module per command in `src/command.rs` and `src/command/`
renders over the writers it is handed, so every rendering is tested without
a console; `src/run.rs` answers a parsed line over those writers — the
executable's console, or an MCP call's own (`src/mcp/`). `src/main.rs` wires
the console, the process's declaration and the audit (`src/command_audit.rs`),
and nothing else. `src/json.rs` writes a document, `src/time.rs` a moment and
`src/text_table.rs` a column, each as the command line always wrote them;
`src/system/` is the one folder that asks the operating system anything —
Ctrl+C, and who the command runs as — and the one that may hold unsafe code.
`build.rs` copies `cli.toml` beside the executable and links the Xmip mark
into it as its Windows icon, `src/xmip-x.res`, which `Sync-XmipEstate
-Brand` copies here.

`tests/` holds one file per command and one each for parsing, the usage,
the surface a line opens, the audit and the MCP server, over
`tests/support/fake.rs`, a surface a test writes, and `tests/support/noun.rs`,
real snapshots written to a
temporary folder. The rules the renderings rest on — which surface wins
when several are named, what a wildcard selects, what a status means,
whether a module conforms — are tested once, beside them in
`xmip-core-operate`, `xmip-core-abi` and the runtime. `command_audit` runs
the built executable once to prove a refused line lands as a record;
`audit_command` reads back records it wrote to a folder of its own;
`mcp_server` scripts one MCP session against the test cluster's names; the
gathered-cluster tests serve a real local operate listener.

## Public contracts and compatibility

None as a library. A command line is an operator surface; nothing depends on
it as a library. The JSON it emits is what a pipe reads (ADR-0014 clause 10),
and the commands and their output are pre-alpha and unstable.

Bound to `XMIP_ABI_VERSION` and `XMIP_OPERATE_VERSION` through `abi`. A
module that reports a different handshake version is reported as a
disagreement, not silently accepted.

## Dependencies

The Xmip crates it calls, Rust to Rust: `xmip-core-operate`, the surface
every command reads and the operating rules; `xmip-core-runtime`, the node's
rules — its plane, its loader, its report and its declarations;
`xmip-core-observe` and `xmip-core-node`, the publication's records and the
scope's words; `xmip-core-abi`, the boundaries and what a status means;
`xmip-core-audit` with its file sink and the operating system's logs;
`xmip-core-library-net`, JSON-RPC 2.0 (`net::rpc`); `xmip-core-library-codec`,
the civil calendar; `xmip-core-provision`, a person's request; and
`xmip-core`, the Clock and an audit record's words. Beyond them: `serde_json`,
and on Windows `windows-sys` and elsewhere `nix` for Ctrl+C and the user.
Each estate crate is taken from the root's `[workspace.dependencies]`, by
path, its working tree (ADR-0005, amendment 2026-11-28).

## Not this repository's

- Not a runtime. It drives a runtime, and holds no execution state.
- Not a place for domain logic. A rule that belongs in a Work Process does
  not belong in a subcommand.
- Not the PowerShell surface. `xmip-core-powershell` reaches the same rules
  through the C ABI; it does not shell out to this.
- Not where the scope tree, the surface precedence, the wildcard selection or
  the English live, nor what a status code means or whether a module
  conforms. A fix to any of those goes to `operate::surface`, the runtime or
  `abi`, and reaches every surface at once.

## Verification

`cargo test` and `cargo clippy --all-targets -- -D warnings` in this
repository; in the estate `xgit` builds and tests it against every crate's
working tree. `xmip probe` against a conforming module, abi's `conforming`
example, is the first of the seven conformance rules in section 11 of
`doc/specification.md` in xmip-core-abi.

Status: pre-alpha. Until 2026-09-14 an `ARCHITECTURE.md` beside this file
said the same things a second time; ADR-0020 clause 1 is one document per
subject, and this is it.
