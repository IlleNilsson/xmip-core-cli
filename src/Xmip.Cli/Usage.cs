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
          xmip activity [scope]     received, processed, sent, retrying, failed
          xmip list [scope]         direct children of a scope (cluster by default)
          xmip show <scope>         health and activity for one scope
          xmip pause <scope>        pause a node, service, process or location
          xmip resume <scope>       resume a paused scope
          xmip start <scope>        start a scope when its runtime supports it
          xmip stop <scope>         stop a scope when its runtime supports it
          xmip restart <scope>      restart a scope when its runtime supports it
          xmip validate <toml>      check a node configuration without starting it
          xmip help                 this text

          --json                    one JSON document instead of text
          --follow                  with health/activity: JSON Lines as Xmip changes
          --runtime <path>          the runtime library, instead of finding it

        The runtime is found by one rule, the same for every surface:
        Xmip:RuntimeLibrary in the configuration, else XMIP_RUNTIME_LIBRARY,
        else the library beside this executable. Every command answers over
        the C ABI in xmip-core-abi; nothing here links Xmip's Rust.
        """;

    /// <summary>Print the usage text.</summary>
    public static int Print(TextWriter output)
    {
        output.WriteLine(Text);
        return 0;
    }
}
