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
xmip-cli help             this text

--json                    one JSON document instead of text
--follow                  with health or measure: JSON Lines as they change
--runtime <path>          the runtime library, instead of finding it
--remote <url>            a web host to follow, instead of a runtime here
--snapshot <path>         a published snapshot to read, instead of the document's
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
`--snapshot` reads one cluster's publication, `--runtime` loads that library,
and `--remote
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
`AbiBoundaries` of `Xmip.Abi`, and `probe` says whether a module conforms by
`ModuleProbe.Result.Complaint` — each the object the matching cmdlet emits. A
mood is the word `English.Mood` gives it, lower case, in text and JSON alike;
a figure is `English.Figure`'s, and one the runtime has not published is a
dash, never a zero. A wildcard scope selects through `ScopeSelection`, the
one the cmdlets use. Pause and resume are the two acts the operator boundary
carries (ADR-0027 clause 5); there is no start, stop or restart, because the
thing that watches must not be able to stop the thing it watches.

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
command's word, phase `begin`, the arguments and options as properties — a
web host's user and password are left out by the capability, as they are
from every program's record), its end (`finished`, with `exit`), a
non-zero exit as a `failure` with its exit code and what it said on stderr, a
line that could not be obeyed as `refused`, and anything unhandled as
`unhandled` before the process ends as it would have. Records go to
`<AuditDirectory>/audit.toml`, `AuditDirectory` in `xmip.cli.toml`'s `[Xmip]`
table resolved from beside the executable; unset, the capability decides —
`XMIP_AUDIT_DIRECTORY`, else the operating system's log, which also takes a
record the directory cannot.

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
refused line lands as a record.

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
- Not a place for domain logic. A rule that belongs in an Xmip Process does
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
