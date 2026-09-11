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
          xmip validate <toml>      check a node configuration without starting it
          xmip help                 this text

          --json                    one JSON document instead of text
          --follow                  with health: JSON Lines as health changes
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
