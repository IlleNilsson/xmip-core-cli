# xmip-core-cli

The .NET 11 `xmip` executable — the command line over a running Xmip.

It is argument parsing and rendering over two libraries in xmip-core-abi:
`Xmip.Abi`, the one .NET declaration of `xmip_module.h` and `xmip_operate.h`,
and `Xmip.Surface`, the model every .NET operator surface shares (ADR-0052).
The PowerShell module and the two GUI hosts read the same surface, so what
`xmip` says and what a screen says cannot disagree.

```text
xmip abi                  the two boundaries this build speaks
xmip status <code>        what a status code means
xmip probe <library>      load a module and report what it says it is
xmip health <scope>       health at and beneath a scope, from the runtime
xmip validate <toml>      check a node configuration without starting it
xmip help                 this text

--json                    one JSON document instead of text
--follow                  with health: JSON Lines as health changes
--runtime <path>          the runtime library, instead of finding it
```

The runtime library is found by the one rule every surface uses:
`Xmip:RuntimeLibrary` in the configuration, else the `XMIP_RUNTIME_LIBRARY`
environment variable, else the library beside the executable. `--runtime`
overrides all three. Text goes to stdout for a person, column-aligned;
`--json` emits one document; `--follow` subscribes to the shared operator
change stream and emits one JSON Lines record each time health changes, until interrupted
(ADR-0014 clause 10). Complaints go to stderr with a non-zero exit: 2 when the
line could not be obeyed, 1 when the thing asked about is wrong.

`src/Xmip.Cli.Test` covers argument parsing, the text and JSON renderings over
a fake surface, and which runtime wins when several are named.

Status: pre-alpha.
