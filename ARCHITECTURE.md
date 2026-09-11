# Repository architecture

Status: Accepted
Classification: Surface module
Maturity: pre-alpha
Owning capability: operator surfaces (ADR-0014)

## Responsibility

The Xmip command line. It reports the boundaries it speaks, probes a module,
reads health from a running Xmip and validates a node configuration, and it
does all of it over the C ABI in `xmip-core-abi`. ADR-0052 clause 5: the
executable ADR-0014 describes — text for a person, `--json` for a program,
`--follow` as JSON Lines, the runtime found by one rule rather than typed.

## Why this is .NET and not Rust

ADR-0014, amended 2026-08-26: every user-interfacing module is .NET 11, and
`xmip-core-abi` is the exception. The CLI, the PowerShell module, the MAUI
desktop GUI and the Blazor web GUI are four surfaces over one boundary, and
writing one of them in a different language means maintaining the binding
twice.

This repository held a Rust template stub until 2026-08-26. It never
implemented anything — its `main` called `xmip_service::startup_sequence()`,
in a crate that no longer exists.

## Shape

`src/Xmip.Cli` is the executable. `Invocation` parses the line into a command,
its one argument and the three options; one class per command renders over a
`TextWriter`, so every rendering is tested without a console. `Program.cs`
wires the console, the runtime and Ctrl+C, and nothing else.

`src/Xmip.Cli.Test` is xunit, over a fake `IOperatorSurface`. Nothing in this
repository loads a native library under test; what crosses the C ABI is the
binding's to verify.

## Public contracts

None. A command line is an operator surface; nothing depends on it as a
library. The JSON it emits is what the PowerShell module and a pipe read
(ADR-0014 clause 10), and it is pre-alpha.

## Dependencies

Two projects in `xmip-core-abi`, referenced by path inside the composed
estate (ADR-0014, amendment of 2026-09-09): `Xmip.Abi`, the binding, and
`Xmip.Surface`, the model every .NET surface shares — the operator surface,
the scope tree, runtime discovery and the English (ADR-0052 clause 1). This
repository builds inside the estate, which is where `xgit` builds it.

**No Xmip Rust crate is referenced, and none may be.** ADR-0012 clause 2 makes
the header normative and the bindings a convenience; a surface that linked Rust
would be proof that the boundary does not work. That this project compiles
without a single Xmip source file is the test.

## Non-responsibilities

- Not a runtime. It drives a runtime, and holds no execution state.
- Not a place for domain logic. A rule that belongs in a Process does not
  belong in a subcommand.
- Not the PowerShell surface. `xmip-core-powershell` reads the same
  `Xmip.Surface` directly; it does not shell out to this.
- Not where the scope tree, the discovery rule or the English live. A fix to
  any of those goes to `Xmip.Surface` and reaches every surface at once.

## Compatibility

Bound to `XMIP_ABI_VERSION` and `XMIP_OPERATE_VERSION` through the binding. A
module that reports a different handshake version is reported as a
disagreement, not silently accepted.

The commands and their output are pre-alpha and unstable.

## Verification

`dotnet build` and `dotnet test` on `src/Xmip.Cli.Test`; the workflow in
`.github/workflows/verify.yml` does the same with `xmip-core-abi` checked out
beside this repository. `xmip probe` against a conforming module is the first
of the seven conformance rules in section 11 of `docs/specification.md` in
xmip-core-abi.
