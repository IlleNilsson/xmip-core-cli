using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip-cli journey</c>: Retry or Dismiss on one Journey that failed
/// (runtime-model.md section 13; ADR-0013). A Journey leads to one Send Port;
/// when every Send Location of its Port failed its tries it is written Failed
/// and waits in its Port's queue, and the node publishes at the Port's scope
/// the last Journey that failed there and why — the identifier an operator
/// names here. There is no list: the act is the command, as the PowerShell
/// module takes it as a parameter. How the act reaches the node is the
/// surface's; only the rendering is here. Exit 0 when it was applied or left
/// for the node, 1 when it was not, 2 for a line that names no node.
/// </summary>
public static class JourneyCommand
{
    /// <summary>Take <paramref name="act"/> on <paramref name="journey"/>, sent
    /// by the node at <paramref name="location"/> — the node, or its Send
    /// Port's scope.</summary>
    public static int Over(
        IOperatorSurface surface,
        string? location,
        string journey,
        JourneyAct act,
        string who,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (location is not { Length: > 0 } || ScopeTree.Node(location).Length == 0)
        {
            error.WriteLine(
                $"REFUSED: to {JourneyOperation.Word(act)} a Journey, name the node that sends "
                + "its Send Port with --location xmip:///<cluster>/node/<name>, or the Send "
                + "Port's scope beneath it.");
            return 2;
        }

        JourneyOperation done = surface.Act(location, journey, act, who);

        if (json)
        {
            output.WriteLine(Document(done));
        }
        else
        {
            (done.Applied ? output : error).WriteLine(
                done.Applied ? $"OK. {done.Result}" : $"REFUSED: {done.Result}");
        }

        return done.Applied ? 0 : 1;
    }

    /// <summary>What came of the act, as one document.</summary>
    public static string Document(JourneyOperation done)
    {
        ArgumentNullException.ThrowIfNull(done);

        return JsonText.Document(writer =>
        {
            writer.WriteString("node", done.Node);
            writer.WriteString("journey", done.Journey);
            writer.WriteString("act", JourneyOperation.Word(done.Act));
            writer.WriteBoolean("applied", done.Applied);
            writer.WriteString("result", done.Result);
        });
    }
}
