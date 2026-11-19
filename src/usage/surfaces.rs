/// Which surface is read, and how the node a line names is reached.
pub const USAGE: &str = r"One invocation reads one cluster. Two rolls are two clusters, each
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

The runtime is linked into this executable, which needs no .NET. A
node is operated over gRPC to its operate listener, on this machine at
its local endpoint and on another at its operate address, through the
runtime's own client; validate and abi ask the runtime itself, with no
node behind them.";
