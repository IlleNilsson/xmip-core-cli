# xmip-core-cli

The .NET 11 `xmip` command-line interface over the Xmip application binary interface (ABI).

It owns operator commands, argument handling, and machine-readable output. It does not own runtime behavior; the graphical and PowerShell surfaces use the same ABI independently.

The ABI binding is not here: it is `Xmip.Abi` in xmip-core-abi (`dotnet/Xmip.Abi`), the one .NET declaration of `xmip_module.h` and `xmip_operate.h` that this executable, the PowerShell module and the GUI all reference, so the boundary is bound once (ADR-0014, amendment of 2026-08-26).

Status: planned.
