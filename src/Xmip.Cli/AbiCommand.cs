using Xmip.Abi;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip abi</c>: the two boundaries this build speaks — the module boundary
/// a Module plugs into and the operator boundary a surface drives from
/// (ADR-0027 clause 2, versioned apart). <see cref="AbiBoundaries"/> is the
/// answer, the one <c>Get-XmipAbi</c> emits too; only the rendering is here.
/// Nothing is loaded.
/// </summary>
public static class AbiCommand
{
    /// <summary>Print the boundaries.</summary>
    public static int Run(bool json, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        AbiBoundaries abi = AbiBoundaries.Current;

        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteStartObject("module");
                writer.WriteNumber("version", abi.ModuleVersion);
                writer.WriteString("entrypoint", abi.ModuleEntrypoint);
                writer.WriteString("library_file_name", abi.ModuleLibraryFileName);
                writer.WriteEndObject();
                writer.WriteStartObject("operate");
                writer.WriteNumber("version", abi.OperateVersion);
                writer.WriteString("entrypoint", abi.OperateEntrypoint);
                writer.WriteEndObject();
            }));

            return 0;
        }

        output.WriteLine($"{"module version",-20}{abi.ModuleVersion}");
        output.WriteLine($"{"module entrypoint",-20}{abi.ModuleEntrypoint}");
        output.WriteLine($"{"module file name",-20}{abi.ModuleLibraryFileName}");
        output.WriteLine($"{"operate version",-20}{abi.OperateVersion}");
        output.WriteLine($"{"operate entrypoint",-20}{abi.OperateEntrypoint}");

        return 0;
    }
}
