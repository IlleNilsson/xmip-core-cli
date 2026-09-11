using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip abi</c>: the two boundaries this build speaks — the module boundary
/// a Module plugs into and the operator boundary a surface drives from
/// (ADR-0027 clause 2, versioned apart). Answered from the binding's
/// constants; nothing is loaded.
/// </summary>
public static class AbiCommand
{
    /// <summary>The module name shown as the example of platform naming.</summary>
    private const string Example = "xmip_core_transport_file";

    /// <summary>Print the boundaries.</summary>
    public static int Run(bool json, TextWriter output)
    {
        if (json)
        {
            output.WriteLine(JsonText.Document(writer =>
            {
                writer.WriteStartObject("module");
                writer.WriteNumber("version", ModuleAbi.AbiVersion);
                writer.WriteString("entrypoint", ModuleAbi.Entrypoint);
                writer.WriteString("library_file_name", ModuleAbi.LibraryFileName(Example));
                writer.WriteEndObject();
                writer.WriteStartObject("operate");
                writer.WriteNumber("version", OperateAbi.Version);
                writer.WriteString("entrypoint", OperateAbi.Entrypoint);
                writer.WriteEndObject();
            }));

            return 0;
        }

        output.WriteLine($"{"module version",-20}{ModuleAbi.AbiVersion}");
        output.WriteLine($"{"module entrypoint",-20}{ModuleAbi.Entrypoint}");
        output.WriteLine($"{"module file name",-20}{ModuleAbi.LibraryFileName(Example)}");
        output.WriteLine($"{"operate version",-20}{OperateAbi.Version}");
        output.WriteLine($"{"operate entrypoint",-20}{OperateAbi.Entrypoint}");

        return 0;
    }
}
