# Repository architecture

Status: Accepted
Classification: Surface module
Maturity: pre-alpha
Owning capability: operator surfaces (ADR-0014)

## Responsibility

The Xmip command line. It configures, operates, reports on and diagnoses a
running Xmip, and it does so over the C ABI in `xmip-core-abi`.

## Why this is .NET and not Rust

ADR-0014, amended 2026-08-26: every user-interfacing module is .NET 11, and
`xmip-core-abi` is the exception. The CLI, the PowerShell module, the MAUI
desktop GUI and the Blazor web GUI are four surfaces over one boundary, and
writing one of them in a different language means maintaining the binding
twice.

This repository held a Rust template stub until 2026-08-26. It never
implemented anything — its `main` called `xmip_service::startup_sequence()`,
in a crate that no longer exists.

## Public contracts

None. A command line is an operator surface; nothing depends on it as a
library, and its output format is not an API.

## Dependencies

`include/xmip_module.h` from `xmip-core-abi`, and nothing else.

**No Xmip Rust crate is referenced, and none may be.** ADR-0012 clause 2 makes
the header normative and the bindings a convenience; a surface that linked Rust
would be proof that the boundary does not work. That this project compiles
without a single Xmip source file is the test.

## Non-responsibilities

- Not a runtime. It drives a runtime, and holds no execution state.
- Not a place for domain logic. A rule that belongs in a Process does not
  belong in a subcommand.
- Not the PowerShell surface. `xmip-core-powershell` binds the same ABI
  directly; it does not shell out to this.

## Compatibility

Bound to `XMIP_ABI_VERSION`. A module that reports a different handshake
version is reported as a disagreement, not silently accepted.

The commands and their output are pre-alpha and unstable.

## Verification

`dotnet build` and `dotnet test`. `xmip probe` against a conforming module is
the first of the seven conformance rules in section 11 of
`docs/specification.md` in xmip-core-abi — the header has twelve sections and
none of them is conformance; this line said "the header" until 2026-08-31 and
sent readers to the path trait.
