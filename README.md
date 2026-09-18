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
xmip-cli show <scope>     one scope: its mood, its evidence, its figures
xmip-cli pause <scope>    pause everything at and beneath a scope
xmip-cli resume <scope>   resume everything at and beneath a scope
xmip-cli validate <toml>  check a node configuration without starting it
xmip-cli help             this text

--json                    one JSON document instead of text
--follow                  with health or measure: JSON Lines as they change
--runtime <path>          the runtime library, instead of finding it
--remote <url>            a web host to follow, instead of a runtime here
```

Which surface a command reads is stated in `xmip.cli.toml` beside the
executable, with the same `[Xmip]` keys as the GUI hosts and the PowerShell
module — `Surface = "native" | "snapshot" | "remote"`, `RuntimeLibrary`,
`Snapshot`, `Url` — and never guessed (ADR-0052 clause 3). As shipped, in a
developer's clone, it follows the Playground roll started as cluster C1, the
same file the PowerShell prompt follows, so `xmip-cli show xmip:///C1` answers
while a roll runs and says `SNAPSHOT — no file at ...` before one has. With
no surface named, the runtime library is found by the one rule every surface
uses: `Xmip:RuntimeLibrary` in the document, else the `XMIP_RUNTIME_LIBRARY`
environment variable, else the library beside the executable. The line wins
over the document: `--runtime` loads that library, and `--remote
http://host:5087` reads no library at all: it follows that web host's surface
hub over SignalR and is told when the host's surface changes, so `--follow` on
another machine never polls (ADR-0052, amendment 2026-09-15); `validate`
stays local, since it asks a runtime. Text
goes to stdout for a person, column-aligned;
`--json` emits one document; `--follow` subscribes to the shared operator
change stream and emits one JSON Lines record each time the snapshot it reads
changes, until interrupted (ADR-0014 clause 10). Complaints go to stderr with a
non-zero exit: 2 when the line could not be obeyed, 1 when the thing asked
about is wrong.

`measure`, `list`, `show`, `pause` and `resume` render the `Figures`,
`ScopeItem` and `ScopeOperation` shapes of `Xmip.Surface`, the same ones the
PowerShell module emits as objects. A figure the runtime has not published is a
dash, never a zero. Pause and resume are the two acts the operator boundary
carries (ADR-0027 clause 5); there is no start, stop or restart, because the
thing that watches must not be able to stop the thing it watches.

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
parsing, the text and JSON renderings, and which runtime wins when several are
named. Nothing in this repository loads a native library under test; what
crosses the C ABI is the binding's to verify.

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
- Not where the scope tree, the discovery rule or the English live. A fix to
  any of those goes to `Xmip.Surface` and reaches every surface at once.

## Verification

`dotnet build` and `dotnet test` on `src/Xmip.Cli.Test`; the workflow in
`.github/workflows/verify.yml` does the same with `xmip-core-abi` checked out
beside this repository. `xmip-cli probe` against a conforming module is the first
of the seven conformance rules in section 11 of `doc/specification.md` in
xmip-core-abi.

Status: pre-alpha. Until 2026-09-14 an `ARCHITECTURE.md` beside this file
said the same things a second time; ADR-0020 clause 1 is one document per
subject, and this is it.
