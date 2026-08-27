using System.Runtime.InteropServices;
using System.Text;
using Xmip.Cli.Interop;

namespace Xmip.Cli;

/// <summary>
/// Loads a module through the C ABI and reports what it says about itself.
/// </summary>
/// <remarks>
/// ADR-0012 leaves one thing open: "a conformance suite able to drive the seven
/// rules in its section 11 from outside a module". This is the first of those
/// rules — that the module exports the entrypoint, accepts the host's
/// abi_version, fills the descriptor, and destroys cleanly.
///
/// It links no Xmip code. A module written in C, Go or C# is probed exactly the
/// same way, which is the property the boundary exists to have.
/// </remarks>
internal static unsafe class ModuleProbe
{
    /// <summary>What a module said when asked.</summary>
    internal sealed record Result(
        XmipStatus Status,
        string Provider,
        string Module,
        string Standard,
        uint AbiVersion,
        string TraitVersion,
        string ModuleVersion,
        string LastError);

    public static Result Probe(string libraryPath)
    {
        var handle = NativeLibrary.Load(libraryPath);

        try
        {
            if (!NativeLibrary.TryGetExport(handle, XmipAbi.Entrypoint, out var symbol))
            {
                throw new EntryPointNotFoundException(
                    $"{libraryPath} exports no {XmipAbi.Entrypoint}. " +
                    "Every conforming module exports exactly that symbol.");
            }

            var create =
                (delegate* unmanaged[Cdecl]<XmipHost*, XmipModule*, XmipStatus>)symbol;

            var host = new XmipHost
            {
                AbiVersion = XmipAbi.AbiVersion,
                Ctx = null,
                Log = &OnLog,
                Cancelled = &OnCancelled,
                JourneyId = &OnJourneyId,
            };

            var module = default(XmipModule);
            var status = create(&host, &module);

            if (status != XmipStatus.Ok)
            {
                // The module returned a status and left *out untouched, so
                // there is nothing to read and nothing to destroy.
                return Failed(status);
            }

            try
            {
                return Describe(module);
            }
            finally
            {
                // Section 7. The module frees its own state; the host never
                // does, because no allocator is shared across this boundary.
                if (module.Destroy is not null)
                {
                    module.Destroy(module.State);
                }
            }
        }
        finally
        {
            NativeLibrary.Free(handle);
        }
    }

    private static Result Describe(XmipModule module)
    {
        var descriptor = module.Descriptor;

        var lastError = module.LastError is null
            ? string.Empty
            : module.LastError(module.State).Read();

        return new Result(
            XmipStatus.Ok,
            descriptor.Provider.Read(),
            descriptor.Module.Read(),
            descriptor.Standard.Read(),
            descriptor.AbiVersion,
            $"{descriptor.TraitMajor}.{descriptor.TraitMinor}",
            $"{descriptor.ModuleMajor}.{descriptor.ModuleMinor}.{descriptor.ModulePatch}",
            lastError);
    }

    private static Result Failed(XmipStatus status) => new(
        status,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        string.Empty,
        string.Empty,
        status.Explain());

    // ----------------------------------------------------------------------
    // Host callbacks. These are called from native code, so nothing may throw
    // across them — an exception unwinding into C is undefined behaviour, the
    // same rule the header applies to a Rust panic.
    // ----------------------------------------------------------------------

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnLog(void* ctx, int level, XmipStr target, XmipStr message)
    {
        try
        {
            var name = (XmipLogLevel)level;
            Console.Error.WriteLine($"  [{name}] {target.Read()}: {message.Read()}");
        }
        catch
        {
            // Swallowed on purpose. A failure to print is not worth taking the
            // process down, and there is no way to report it from here.
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int OnCancelled(void* ctx) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static XmipStr OnJourneyId(void* ctx)
    {
        // Empty: a probe runs outside any Journey, which is exactly the case
        // the header says to answer empty for.
        return default;
    }

    private enum XmipLogLevel
    {
        Error = 1,
        Warn = 2,
        Info = 3,
        Debug = 4,
        Trace = 5,
    }
}
