using Xmip.Abi.Module;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip probe &lt;library&gt;</c>: load a module through the C ABI and
/// report what it says it is. The first of the seven conformance rules in
/// section 11 of the ABI's specification, driven from outside the module. A
/// module's log lines go to stderr as they arrive. Whether it conforms is
/// <see cref="ModuleProbe.Result.Complaint"/>'s judgement, the one
/// <c>Get-XmipModuleDescriptor</c> reports too.
/// </summary>
public static class ProbeCommand
{
    /// <summary>Probe one module library.</summary>
    public static int Run(string library, bool json, TextWriter output, TextWriter error)
    {
        if (!File.Exists(library))
        {
            error.WriteLine($"No file at {library}.");
            return 2;
        }

        ModuleProbe.Result result;

        try
        {
            result = ModuleProbe.Probe(
                Path.GetFullPath(library), line => error.WriteLine($"  {line}"));
        }
        catch (Exception failure) when (failure
            is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            // The three ways loading fails that say something specific about
            // the module rather than about this process.
            error.WriteLine(failure.Message);
            return 1;
        }

        if (result.Status != XmipStatus.Ok)
        {
            error.WriteLine(
                $"{ModuleAbi.Entrypoint} returned {result.Status} — {result.LastError}");

            if (result.Status == XmipStatus.Unsupported)
            {
                error.WriteLine(
                    "That is the answer a module gives when it cannot speak " +
                    $"abi_version {ModuleAbi.AbiVersion}.");
            }

            return 1;
        }

        string complaint = result.Complaint;

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteString("library", library);
                writer.WriteString("provider", result.Provider);
                writer.WriteString("module", result.Module);
                writer.WriteString("standard", result.Standard);
                writer.WriteNumber("abi_version", result.AbiVersion);
                writer.WriteString("trait_version", result.TraitVersion);
                writer.WriteString("module_version", result.ModuleVersion);
                writer.WriteBoolean("conforms", result.Conforms);
            }));
        }
        else
        {
            output.WriteLine($"{"provider",-20}{Shown(result.Provider)}");
            output.WriteLine($"{"module",-20}{Shown(result.Module)}");
            output.WriteLine($"{"standard",-20}{Shown(result.Standard, "(none)")}");
            output.WriteLine($"{"abi version",-20}{result.AbiVersion}");
            output.WriteLine($"{"trait version",-20}{result.TraitVersion}");
            output.WriteLine($"{"module version",-20}{result.ModuleVersion}");
        }

        if (result.Conforms)
        {
            return 0;
        }

        error.WriteLine(complaint);
        return 1;
    }

    private static string Shown(string value, string whenEmpty = "(empty)")
    {
        return value.Length == 0 ? whenEmpty : value;
    }
}
