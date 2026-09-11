using Xmip.Abi.Operate;
using Xmip.Surface;

namespace Xmip.Cli.Test;

/// <summary>
/// An <see cref="IOperatorSurface"/> that answers from a list the test wrote,
/// filtered and ordered the way every real surface answers, and counts how
/// often it was asked. Not a sample surface: it invents nothing a test did
/// not put there.
/// </summary>
public sealed class FakeSurface(IReadOnlyList<HealthRecord> records) : IOperatorSurface
{
    /// <summary>A fixed instant, so a rendering is the same text every run.</summary>
    public static readonly DateTimeOffset Seen = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The records the next <see cref="Health"/> answers from.</summary>
    public IReadOnlyList<HealthRecord> Records { get; set; } = records;

    /// <summary>How many times <see cref="Health"/> was asked.</summary>
    public int Reads { get; private set; }

    /// <summary>Called after each read, for a test to change the records or
    /// stop a follow.</summary>
    public Action<int>? OnRead { get; init; }

    /// <inheritdoc />
    public string Source => "FAKE — a test wrote these";

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        Reads++;
        OnRead?.Invoke(Reads);

        return ScopeTree.WorstFirst(
            Records.Where(record => ScopeTree.Beneath(record.Scope, scope)));
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return null;
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return "a fake cannot be paused";
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return "a fake cannot be resumed";
    }

    /// <summary>One leaf at <paramref name="scope"/>.</summary>
    public static HealthRecord Leaf(
        string scope, HealthState state, byte severity = 0, string evidence = "")
    {
        return new HealthRecord(scope, state, severity, evidence, Seen);
    }
}
