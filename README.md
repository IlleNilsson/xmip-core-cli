# xmip-core-cli

The .NET 11 `xmip-cli` executable — the command line over a running Xmip,
named as every System Process Xmip owns is named (ADR-0053). A
surface module (ADR-0011, ADR-0012 clause 11) among the operator surfaces
ADR-0014 names, and the executable ADR-0052 clause 5 describes: text for a
person, `--json` for a program, `--follow` as JSON Lines, the runtime found by
one rule rather than typed.

It is argument parsing and rendering over two libraries in xmip-core-abi:
`Xmip.Abi`, the one .NET declaration of `xmip_module.h` and `xmip_operate.h`,
and `Xmip.Surface`, the model every .NET operator surface shares (ADR-0052).
The PowerShell module and the two GUI hosts read the same surface, so what
`xmip-cli` says and what a screen says cannot disagree.

```text
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
                          the Subscriptions the nodes route by; pause or resume one
xmip-cli event-subscriptions [pattern]
                          the Event subscriptions the nodes hold; pause, resume or
                          remove one
xmip-cli dead-messages [pattern]
                          the Messages no Subscription matched, in each node's Dead
                          Message Queue; open one, or replay it
xmip-cli journey [id]     list the Journeys that failed, or retry or dismiss one
xmip-cli help             this text

--json                    one JSON document instead of text
--follow                  with health or measure: JSON Lines as they change
--runtime <path>          the runtime library, instead of finding it
--remote <url>            a web host to follow, instead of a runtime here
--snapshot <path>         a published snapshot to read, instead of the document's
--who <name>              with pause, or the act of subscriptions, event-subscriptions,
                          dead-messages or journey: who acts, instead of the current user;
                          over --remote the host takes the act as your certificate's subject
```

Which surface a command reads is stated in `xmip.cli.toml` beside the
executable, with the same `[Xmip]` keys as the GUI hosts and the PowerShell
module — `Surface = "native" | "snapshot" | "remote"`, `RuntimeLibrary`,
`Snapshot`, `Url` — and never guessed (ADR-0052 clause 3). As shipped, in a
developer's clone, it follows the Playground roll started as cluster C1 — a
name the walkthrough picked, and nothing Xmip knows — the same file the
PowerShell prompt follows, so `xmip-cli show xmip:///C1` answers while a roll
runs and says `SNAPSHOT — no file at ...` before one has. Roll under another
name and `Snapshot` names that file instead. With
no surface named, the runtime library is found by the one rule every surface
uses: `Xmip:RuntimeLibrary` in the document, else the `XMIP_RUNTIME_LIBRARY`
environment variable, else the library beside the executable. The line wins
over the document, in one order — `--remote`, then `--snapshot`, then
`--runtime` — which is `SurfaceChoice.Stated` in `Xmip.Surface`, the same
precedence `Get-XmipHealth -Remote -Snapshot -Library` follows in PowerShell.
`--snapshot` reads one cluster's publication, `--runtime` loads that library —
what each of the three answers, and whether an act applies at once or is
submitted as an order, is the table *What each source answers* in
`module/foundation/abi/README.md` — and `--remote
https://host:5443` reads no library at all: it follows that web host's surface
hub over SignalR and is told when the host's surface changes, so `--follow` on
another machine never polls (ADR-0052, amendment 2026-09-15). It is TLS,
presenting the certificate the document's `Certificate` and `PrivateKey` name
and checking the host's against `TrustAnchor` (else `XMIP_CERTIFICATE`,
`XMIP_PRIVATE_KEY`, `XMIP_TRUST_ANCHOR`); plain http is refused to anything
but this machine (ADR-0063 clause 1). `validate`
stays local, since it asks a runtime. Text
goes to stdout for a person, column-aligned;
`--json` emits one document; `--follow` subscribes to the shared operator
change stream and emits one JSON Lines record each time the snapshot it reads
changes, until interrupted (ADR-0014 clause 10). Complaints go to stderr with a
non-zero exit: 2 when the line could not be obeyed, 1 when the thing asked
about is wrong.

`measure`, `list`, `show`, `pause` and `resume` render the `Figures`,
`ScopeItem` and `ScopeOperation` shapes of `Xmip.Surface`, the same ones the
PowerShell module emits as objects; `validate` renders its
`ConfigurationVerdict`, `status` the `StatusMeaning` and `abi` the
`AbiBoundaries` of `Xmip.Abi`, and `probe` says whether a module loaded and
conforms by `ModuleProbe.Result.Unloadable` and `Complaint` — each the object
the matching cmdlet emits. A mood is the word `English.Mood` gives it, lower
case, in text and JSON alike, and a row with no health recorded is `nothing
recorded`; a figure is `English.Figure`'s, and one the runtime has not
published is a dash, never a zero; `Nothing at`, `Nothing beneath` and
`Nothing measured at` are `English`'s sentences too. A wildcard scope selects
through `ScopeSelection`, the one the cmdlets use, and which rows `show`
gives — the scopes that exist, worst first under a wildcard — is
`ScopeItem.Selected`, the one `Get-XmipScope` calls. Whatever a wildcard
answered is one JSON document of one shape for every command: `pattern`,
`source`, `matched`, and `scopes`, an object per scope. Pause and resume are
the two acts the operator boundary carries (ADR-0027 clause 5); there is no
start, stop or restart, because the thing that watches must not be able to
stop the thing it watches. Who paused is `--who`, else the user the command
runs as — `ScopeOperation.Who`, the rule `Suspend-XmipScope -Who` and the GUI
follow. Over `--remote` no name crosses the wire: the web host takes an act as
the subject of the client certificate this side presented, only where its role
may act, and refuses it in words otherwise (ADR-0009, amendment 2026-10-03).

**The drill.** `list` with no scope lists what is beneath the cluster the
surface publishes at (`IOperatorSurface.Root`), never the one row of the
cluster itself, and every row carries its figures; a node and a stage have
figures of their own. A row that is not fine says on the line beneath it
which leaf explains it and why — `worst <scope>: <evidence>`, the next scope
to type — and `show` always does, so `list`, `list <that scope>`, … reaches
the cause the way the web's drill and `Get-XmipScope` do (ADR-0052; the
owner, 2026-09-26: *drill-down does not work*). JSON carries it as `worst`.

## What it audits

Every invocation is audited as program `xmip-cli` through the audit
capability, reached through the runtime's library (ADR-0062; `CommandAudit`
over `ProgramAudit` in `Xmip.Surface`): the command as it begins (action the
command's word, phase `Begin`, the arguments and options as properties — a
web host's user and password are left out by the capability, as they are
from every program's record), its end (`Finished`, with `exit`), a
non-zero exit as a `Failure` with its exit code and what it said on stderr, a
line that could not be obeyed as `refused`, and anything unhandled as
`unhandled` before the process ends as it would have. The records go through
the runtime library the line states — `--runtime` reaches the audit as it
reaches `validate` (`ProgramAudit.Library`). Records go to
`<AuditDirectory>/audit.toml`, `AuditDirectory` in `xmip.cli.toml`'s `[Xmip]`
table resolved from beside the executable; unset, the capability decides —
`XMIP_AUDIT_DIRECTORY`, else the operating system's log, which also takes a
record the directory cannot.

## Subscriptions

`xmip-cli subscriptions` lists the Subscriptions the cluster's nodes route by
(ADR-0013, amendment 2026-09-30; ADR-0052). A Subscription picks a published
Message up and opens a Journey; it is drawn in an Xmip Application and bound
in a node's TOML. Each one's name, the cluster, the node, its filter, where
it leads, its state, what it picked up, what it holds and since. One command
for the noun, and the act an option on it:

```text
xmip-cli subscriptions [pattern] --location <scope> --name <subscription>
  --sort <column> --order ascending|descending --json
xmip-cli subscriptions --location <node scope> --name <subscription> --pause|--resume
  --who <name> --json
```

The pattern is `*` and `?` over each Subscription's node, or its node and
name as `xmip:///<cluster>/node/<node>/subscription/<name>`; `--location`,
`--sort` and `--order` mean what they mean for `audit`, and the columns are
subscription, cluster, node, filter, destination, state, picked-up, held and
since. Which Subscriptions a line selects and in what order is
`SubscriptionQuery`'s in `Xmip.Surface`, the one the web's Subscriptions view
and `Get-XmipSubscription` ask. An act names one Subscription, by
`--location` at its node and `--name`, or it is REFUSED before any node is
asked (exit 2). Paused, the Messages a Subscription matches are held — kept
in the node's runtime store, counted, not picked up — and a pause survives a
restart of the node; resumed, it picks up what it held, oldest first. There
is no `--remove`: a Subscription is added and removed in the TOML
configuration of the Xmip Application that draws it, and the line is REFUSED
in those words (exit 2). Over a live node the act is applied in its process
and audited there as `subscription.pause` or `subscription.resume`; over a
snapshot it is left where the publication says, for the node to take within
a round. `OK.` and exit 0 when it was applied or left, `REFUSED:` and exit 1
when it was not.

## Event subscriptions

`xmip-cli event-subscriptions` lists the Event subscriptions the cluster's
nodes hold (ADR-0065, amendment 2026-09-29): each one's number on its node,
the subscriber (a Party), the cluster, the node, the action it subscribes to,
its state, queued against capacity, delivered, missed and since. The node is
the one its subscriber connected to, not the one it hears: an Event
subscription on any node hears the matching Events of every node of the
cluster (ADR-0065, amendment 2026-10-02), and the links that carry them
between nodes are the cluster's, not Event subscriptions, and never listed.
A member a node there does not hear is said read-only under the table, one
line each — `R1: not hearing xmip:///C1/node/S1 since
2026-10-02T12:00:00Z: connection refused` — and as `unheard` in `--json`,
so no Event is missing silently.
An Event subscription is not a Subscription: it hands Events to a Party, and picks no
Message up. One command for the noun, and the act an option on it:

```text
xmip-cli event-subscriptions [pattern] --location <scope> --id <n>
  --sort <column> --order ascending|descending --json
xmip-cli event-subscriptions --location <node scope> --id <n>
  --pause|--resume|--remove --who <name> --json
```

The pattern is `*` and `?` over each Event subscription's node and the scope
it reaches; `--location`, `--sort` and `--order` mean what they mean for
`audit`, and the columns are subscriber, cluster, node, action, state, queued,
delivered, missed and since. Which Event subscriptions a line selects and in
what order is `EventSubscriptionQuery`'s in `Xmip.Surface`, the one the web's
Event subscriptions view and `Get-XmipEventSubscription` ask. An act names
one Event subscription, by `--location` at its node and `--id`, or it is
REFUSED before any node is asked (exit 2); paused, an Event subscription
keeps queuing and hands nothing over, resumed it hands over what queued,
removed it is gone. Over a live node the act is applied in its process and
audited as `event.pause`, `event.resume` or `event.remove`; over a snapshot
it is left where the publication says, for the node to take within a round.
`OK.` and exit 0 when it was applied or left, `REFUSED:` and exit 1 when it
was not.

## Dead Message Queue

`xmip-cli dead-messages` lists what each node's Dead Message Queue keeps
(ADR-0052, amendment 2026-10-01): every accepted Message that no
Subscription matched, kept in the Ledger with its receive context, what its
gates concluded, its promoted properties and every Subscription's reason for
declining. It is not a dead letter queue: a failed Journey never goes there.
A row is when it was received, the Message's identifier, the cluster, the
node whose queue keeps it, the Receive Location it arrived at and how many
Subscriptions declined it; a node publishes its oldest hundred. One command
for the noun, and the act an option on it:

```text
xmip-cli dead-messages [pattern] --location <scope> --message <id>
  --sort <column> --order ascending|descending --json
xmip-cli dead-messages --location <node scope> --message <id> --replay
  --who <name> --json
```

The pattern is `*` and `?` over each entry's node, or its node and Message as
`xmip:///<cluster>/node/<node>/dead-message/<message>`; `--location`,
`--sort` and `--order` mean what they mean for `audit`, and the columns are
received (the default, oldest first), message, cluster, node,
receive-location and declines. Which entries a line selects and in what
order is `DeadMessageQuery`'s in `Xmip.Surface`, the one the web's Dead
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
2). Over a live node the Replay is applied in its process and audited there
as `dead-message.replay`; over a snapshot it is left where the publication
says, for the node to take within a round. `OK.` and exit 0 when it was
applied or left, `REFUSED:` and exit 1 when it was not.

## A Journey that failed

`xmip-cli journey` lists the Journeys that failed and retries or dismisses
one (runtime-model.md section 13; ADR-0013). A Journey leads to one Send
Port; when every Send Location of its Port failed its tries it is written
Failed, with why, and waits in its Port's queue. The node publishes, at the
Port's scope `xmip:///<cluster>/node/<node>/send/<Port>`, what the Port
sent, what failed, how many failed wait in its queue and the last that
failed and why. With no Journey named, every one that failed at or beneath
`--location` — a cluster, a node or one Send Port's scope — is listed Port by
Port: how many wait, and a page of them, each its identifier, its place in
the queue and why — read from Xmip Storage where the node runs in the
process, or the oldest hundred its publication carries. `--offset` is the
place to read from, as the last page's `more:` said, and `--limit` the most
of each Port:

```text
xmip-cli journey --location <scope> --offset <place> --limit <count> --json
```

A list says plainly which of three it is. An answer exits 0, and where none
fails it says *No Journey that failed waits at or beneath* and the scope. A surface
that cannot list failed Journeys — no node reached, no runtime loaded, no
publication read yet — says `NOT LISTED:` on stderr, that whether any wait is
not known and not that none do, and exits 1. A surface that asked and was
not answered, Xmip Storage not answering the node, says `FAILED:` with the
runtime's words and exits 1. `--json` carries the same as `listed` and
`failure` beside `failed_journeys`, with the same exit.

With a Journey named, it is the argument, `--location` names the node that
sends the Port or the Port's scope beneath it, and one act is required:

```text
xmip-cli journey <id> --location <node or Send Port scope> --retry|--dismiss
  --who <name> --json
```

`--retry` writes it Active, its tries begun anew, and sends it again from
the end of its Port's queue — or from its place, where it blocks a
Sequential Send Port; `--dismiss` writes it Dismissed, its history, Message
and Stream kept, and takes it out of the queue. A Journey named with no
act, an act with no Journey, or `--pause`, `--resume`, `--remove` or
`--replay` cannot be obeyed (exit 2),
and `--retry` and `--dismiss` apply to no other command; one with no
`--location` on a node is REFUSED before any node is asked (exit 2). Over a
live node the act is applied in its process and audited there as
`journey.retry` or `journey.dismiss`; over a snapshot it is left where the
publication says, for the node to take within a round. A Journey that is no
longer Failed, or a node that does not send its Port, is REFUSED in the
node's words. `OK.` and exit 0 when it was applied or left, `REFUSED:` and
exit 1 when it was not; `--json` carries `node`, `journey`, `act`, `applied`
and `result`.

## Reading the audit

`xmip-cli audit` reads that file back — every program's records in it, not
only this one's — through the audit capability's one reader
(`ProgramAudit.Read` in `Xmip.Surface`, over `xmip_audit_read_v1`; ADR-0062,
amendment 2026-09-29), the reader the web's Audit view and `Get-XmipAudit`
call. Its argument and options are the query's words, and what each means is
the capability's:

```text
xmip-cli audit [pattern] --location <scope> --host <name> --program <name>
  --record <id> --severity <word> --action <word> --from <time> --to <time>
  --sort <column> --order ascending|descending --offset <n> --limit <n>
  --include-hidden --verify --json
```

The pattern is `*` and `?` over the location each record's process declared
(ADR-0053 clause 3); a record with none is at the root, which only `*` names.
`--location` is a scope and everything beneath it, `--host` the records of
programs that declared no location on that machine, `--program` one program
exactly: who a record is, is never read out of a program's name. `--from` and
`--to` are RFC 3339, or a date and time with no zone, read as UTC. Newest
first, 100 records a page and 1000 at most. What a run that declared itself
hidden recorded — `Start-XmipTest -Hidden`, an assistant's test run — is left
out unless `--include-hidden` is given, and then each such record and group
says `· test` after its node or name, and `"hidden": true` in JSON (ADR-0028,
amendment 2026-09-30).

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
5); `chains` in JSON, each with `writer`, `records`, `whole` and `said`. A
chain that breaks exits 1. It reads no payload, so an Observer may run it
(ADR-0009, amendment 2026-09-06). A query the capability
does not take is its REFUSED sentence on stderr, exit 2; with no audit
directory stated the records went to the operating system's log, which is
said, exit 1.

## Why this is .NET and not Rust

ADR-0014, amended 2026-08-26: every user-interfacing module is .NET 11, and
`xmip-core-abi` is the exception. The CLI, the PowerShell module, the MAUI
desktop GUI and the Blazor web GUI are four surfaces over one boundary, and
writing one of them in a different language means maintaining the binding
twice. This repository held a Rust template stub until 2026-08-26; it never
implemented anything.

## Shape

`src/Xmip.Cli` is the executable. `Invocation` parses the line into a command,
its one argument and the three options; one class per command renders over a
`TextWriter`, so every rendering is tested without a console. `Program.cs`
wires the console, the runtime and Ctrl+C, and nothing else.

`src/Xmip.Cli.Test` is xunit, over a fake `IOperatorSurface`: argument
parsing, what the line states about the surface, and the text and JSON
renderings. The rules the renderings rest on — which surface and runtime win
when several are named, what a wildcard selects, what a status means, whether
a module conforms — are tested once, beside them in `xmip-core-abi`, and
what crosses the C ABI is the binding's to verify. The one exception is the
audit: `CommandAuditTests` records through the runtime's library, which
`Xmip.Abi` copies beside the tests, and runs the executable once to prove a
refused line lands as a record; `AuditCommandTests` reads back, through the
same library, records it wrote to a directory of its own.

## Public contracts and compatibility

None as a library. A command line is an operator surface; nothing depends on
it as a library. The JSON it emits is what a pipe reads (ADR-0014 clause 10),
and the commands and their output are pre-alpha and unstable.

Bound to `XMIP_ABI_VERSION` and `XMIP_OPERATE_VERSION` through the binding. A
module that reports a different handshake version is reported as a
disagreement, not silently accepted.

## Dependencies

Two projects in `xmip-core-abi`, referenced by path inside the composed
estate (ADR-0014, amendment of 2026-09-09): `Xmip.Abi`, the binding, and
`Xmip.Surface`, the model every .NET surface shares — the operator surface,
the scope tree, runtime discovery and the English (ADR-0052 clause 1). This
repository builds inside the estate, which is where `xgit` builds it.

**No Xmip Rust crate is referenced, and none may be.** ADR-0012 clause 2 makes
the header normative and the bindings a convenience; that this project
compiles without a single Xmip source file is the test.

## Not this repository's

- Not a runtime. It drives a runtime, and holds no execution state.
- Not a place for domain logic. A rule that belongs in a Work Process does
  not belong in a subcommand.
- Not the PowerShell surface. `xmip-core-powershell` reads the same
  `Xmip.Surface` directly; it does not shell out to this.
- Not where the scope tree, the discovery rule, the surface precedence, the
  wildcard selection or the English live, nor what a status code means or
  whether a module conforms. A fix to any of those goes to `Xmip.Surface` or
  `Xmip.Abi` and reaches every surface at once.

## Verification

`dotnet build` and `dotnet test` on `src/Xmip.Cli.Test`; the workflow in
`.github/workflows/verify.yml` does the same with `xmip-core-abi` checked out
beside this repository. `xmip-cli probe` against a conforming module is the first
of the seven conformance rules in section 11 of `doc/specification.md` in
xmip-core-abi.

Status: pre-alpha. Until 2026-09-14 an `ARCHITECTURE.md` beside this file
said the same things a second time; ADR-0020 clause 1 is one document per
subject, and this is it.
