using Xmip.Cli;
using Xmip.Cli.Interop;

// The Xmip command line. ADR-0014: every user-interfacing module is .NET 11,
// and xmip-core-abi is the exception. This surface drives Xmip through the C
// ABI and links no Rust.

return args switch
{
    [] or ["--help"] or ["-h"] or ["help"] => Usage(),
    ["abi"] => ShowAbi(),
    ["status", var code] => ExplainStatus(code),
    ["probe", var library] => ProbeModule(library),
    [var unknown, ..] => Unknown(unknown),
};

static int Usage()
{
    Console.WriteLine("""
        xmip — the Xmip command line

          xmip abi                 the module boundary this build speaks
          xmip status <code>       what a status code means
          xmip probe <library>     load a module and report what it says it is

        Every command answers over the C ABI in xmip-core-abi. Nothing here
        links Xmip's Rust, which is what makes the boundary worth having.
        """);

    return 0;
}

static int ShowAbi()
{
    Console.WriteLine($"handshake version   {XmipAbi.AbiVersion}");
    Console.WriteLine($"entrypoint symbol   {XmipAbi.Entrypoint}");
    Console.WriteLine($"module file name    {XmipAbi.LibraryFileName("xmip_core_transport_file")}");

    return 0;
}

static int ExplainStatus(string code)
{
    if (!int.TryParse(code, out var value))
    {
        Console.Error.WriteLine($"'{code}' is not a number. Status codes are integers, zero or negative.");
        return 2;
    }

    var status = (XmipStatus)value;
    var known = Enum.IsDefined(status);

    Console.WriteLine($"{value,5}  {(known ? status.ToString() : "unknown")}");
    Console.WriteLine($"       {status.Explain()}");

    if (!known)
    {
        return 1;
    }

    Console.WriteLine($"       retryable: {Yes(status.IsRetryable())}   terminal: {Yes(status.IsTerminal())}");

    return 0;
}

static int ProbeModule(string library)
{
    if (!File.Exists(library))
    {
        Console.Error.WriteLine($"No file at {library}.");
        return 2;
    }

    ModuleProbe.Result result;

    try
    {
        result = ModuleProbe.Probe(Path.GetFullPath(library));
    }
    catch (Exception failure) when (failure is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
    {
        // The three ways loading fails that say something specific about the
        // module rather than about this process.
        Console.Error.WriteLine(failure.Message);
        return 1;
    }

    if (result.Status != XmipStatus.Ok)
    {
        Console.Error.WriteLine($"{XmipAbi.Entrypoint} returned {result.Status} — {result.LastError}");

        if (result.Status == XmipStatus.Unsupported)
        {
            Console.Error.WriteLine(
                $"That is the answer a module gives when it cannot speak abi_version {XmipAbi.AbiVersion}.");
        }

        return 1;
    }

    Console.WriteLine($"provider            {Shown(result.Provider)}");
    Console.WriteLine($"module              {Shown(result.Module)}");
    Console.WriteLine($"standard            {Shown(result.Standard, "(none)")}");
    Console.WriteLine($"abi version         {result.AbiVersion}");
    Console.WriteLine($"trait version       {result.TraitVersion}");
    Console.WriteLine($"module version      {result.ModuleVersion}");

    if (result.AbiVersion != XmipAbi.AbiVersion)
    {
        Console.Error.WriteLine(
            $"Loaded, and disagrees: the module says {result.AbiVersion}, this build speaks {XmipAbi.AbiVersion}.");
        return 1;
    }

    if (result.Provider == "core" && result.Standard.Length > 0)
    {
        // Section 4: standard is empty only when provider is "core".
        Console.Error.WriteLine(
            $"A core module named a standard ('{result.Standard}'). ADR-0011 leaves that slot empty for core.");
        return 1;
    }

    return 0;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"'{command}' is not an xmip command. Try 'xmip help'.");
    return 2;
}

static string Yes(bool value) => value ? "yes" : "no";

static string Shown(string value, string whenEmpty = "(empty)") =>
    value.Length == 0 ? whenEmpty : value;
