namespace Xmip.Cli;

// The part of the text behind xmip help that says which surface is read.
public static partial class Usage
{
    private const string Surfaces = """
        One invocation reads one cluster. Two rolls are two clusters, each
        publishing its own snapshot, and a rollup or a sum over both would be
        at a scope in neither tree — so --snapshot names which one, and the
        web monitor is where an operator moves between them.

        --remote or --local at a node that gathers its cluster's view — a node
        declaring the monitoring role — answers for the whole cluster: every
        node's records, and whether each one was reached, as that node saw
        them. The source line says so after where the node is reached, in the
        node's own words: the cluster, gathered by which node, and how many of
        its nodes it reached. A node that gathers none answers for itself.
        Which of the two, the node's answer decides, never its name.

        The runtime is found by one rule, the same for every surface:
        Xmip:RuntimeLibrary in the configuration, else XMIP_RUNTIME_LIBRARY,
        else the library beside this executable. A node is operated over gRPC
        to its operate listener, on this machine at its local endpoint and on
        another at its operate address, through the runtime's client in that
        library; validate and abi ask the library itself, with no node behind
        them. Nothing here links Xmip's Rust.
        """;
}
