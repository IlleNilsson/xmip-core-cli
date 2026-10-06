using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// <c>xmip-cli journey</c>: the Journeys that failed, listed, and Retry or
/// Dismiss on one (runtime-model.md section 13; ADR-0013). A Journey leads to
/// one Send Port; when every Send Location of its Port failed its tries it is
/// written Failed and waits in its Port's queue. With no Journey named, every
/// one that failed at or beneath <c>--location</c> is listed, Port by Port —
/// how many wait, and a page of them with why, read from Xmip Storage in the
/// process that runs the node or as its publication carries them; with one
/// named, the act is the command, as the PowerShell module takes it as a
/// parameter. How the list is read and the act reaches the node is the
/// surface's; only the rendering is here. A list exits 0 when it is an
/// answer, none failing among them, and 1 when it is not: the surface
/// cannot list failed Journeys, or asked and Xmip Storage did not answer.
/// An act exits 0 when it was applied or left for the node, 1 when it was
/// not, 2 for a line that names no node.
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

    /// <summary>
    /// List the Journeys that failed at the Send Ports at or beneath
    /// <paramref name="location"/> — the surface's root where none is named —
    /// a page of each Port's from <c>page.From</c>, at most <c>page.Most</c>
    /// (0: a hundred). Exit 0 for an answer, none failing among them; 1, said
    /// on <paramref name="error"/>, where there is none — the surface cannot
    /// list them, or asked and was not answered — for nothing is then known of
    /// the queue.
    /// </summary>
    public static int List(
        IOperatorSurface surface,
        string? location,
        (ulong From, uint Most) page,
        bool json,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        string scope = location is { Length: > 0 } ? location : surface.Root();
        FailedJourneyList failed = surface.FailedJourneys(scope, page.From, page.Most);

        if (json)
        {
            output.WriteLine(Document(failed));
            return failed.Listed ? 0 : 1;
        }

        if (!failed.Listed)
        {
            error.WriteLine(JourneyOperation.Unlisted(failed, scope, surface.Source));
            return 1;
        }

        if (failed.Ports.Count == 0)
        {
            output.WriteLine($"No Journey that failed waits at or beneath {scope}.");
            return 0;
        }

        foreach (FailedJourneyPort port in failed.Ports)
        {
            output.WriteLine(
                $"{port.Node} Send Port {port.SendPort}: {port.Count} failed in its queue");

            foreach (FailedJourneyRecord journey in port.Journeys)
            {
                output.WriteLine($"  {journey.Journey}  place {journey.Sequence}  {journey.Reason}");
            }

            if (port.Next is { } next)
            {
                output.WriteLine($"  more: --offset {next}");
            }
        }

        return 0;
    }

    /// <summary>The Journeys that failed, as one document: whether it is an
    /// answer and, where it was asked and not answered, why; each Port's,
    /// with its count, the place its next page reads from and its
    /// Journeys.</summary>
    public static string Document(FailedJourneyList failed)
    {
        ArgumentNullException.ThrowIfNull(failed);

        return JsonText.Document(writer =>
        {
            writer.WriteBoolean("listed", failed.Listed);
            writer.WriteString("failure", failed.Failure);
            writer.WriteStartArray("failed_journeys");

            foreach (FailedJourneyPort port in failed.Ports)
            {
                writer.WriteStartObject();
                writer.WriteString("node", port.Node);
                writer.WriteString("send_port", port.SendPort);
                writer.WriteNumber("count", port.Count);

                if (port.Next is { } next)
                {
                    writer.WriteNumber("next", next);
                }
                else
                {
                    writer.WriteNull("next");
                }

                writer.WriteStartArray("journeys");

                foreach (FailedJourneyRecord journey in port.Journeys)
                {
                    writer.WriteStartObject();
                    writer.WriteString("journey", journey.Journey);
                    writer.WriteNumber("sequence", journey.Sequence);
                    writer.WriteString("reason", journey.Reason);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
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
