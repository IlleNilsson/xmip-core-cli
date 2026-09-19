using System.Globalization;
using Xmip.Surface;

namespace Xmip.Cli;

/// <summary>
/// What a scope argument selects: the one scope an operator spelled, or the
/// several a wildcard names. ADR-0059 clause 7 is an estate rule, not a
/// PowerShell one — an argument that <em>selects among scopes that already
/// exist</em> takes a wildcard — and the match is <see cref="ScopePattern"/>,
/// the one every surface shares (ADR-0052 clause 1).
/// </summary>
/// <param name="Argument">What the operator typed.</param>
/// <param name="Patterned">Whether it carried a wildcard. An argument without
/// one is the scope itself, answered exactly as it always was, so nothing that
/// reads this executable's output today reads it differently tomorrow.</param>
/// <param name="Scopes">The scopes to answer for, topmost first and never one
/// beneath another: every command reads a scope <em>and everything beneath
/// it</em>, so a child of a match would be said twice.</param>
public sealed record ScopeSelection(
    string Argument, bool Patterned, IReadOnlyList<string> Scopes)
{
    /// <summary>
    /// What an argument selects against a surface, or null with the refusal in
    /// <paramref name="refusal"/> when a pattern names nothing at all. A
    /// pattern that matches nothing is never exit 0 and never silence: silence
    /// reads as <em>all clear</em>, and this is the opposite of that.
    /// </summary>
    public static ScopeSelection? Of(
        IOperatorSurface surface, string argument, out string refusal)
    {
        refusal = string.Empty;
        string pattern = string.IsNullOrEmpty(argument) ? ScopeTree.Root : argument;

        if (!ScopePattern.HasWildcard(pattern))
        {
            return new ScopeSelection(pattern, false, [pattern]);
        }

        ScopeIndex index = surface.Index();
        IReadOnlyList<string> chosen = ScopePattern.Topmost(index.Matching(pattern));

        if (chosen.Count == 0)
        {
            refusal = Refusal(index, pattern, surface.Source);
            return null;
        }

        return new ScopeSelection(pattern, true, chosen);
    }

    /// <summary>
    /// The same refusal for a program: <c>--json</c> promises structure, and a
    /// pattern that named nothing is an answer like any other. It goes to
    /// stderr with the same non-zero exit the words do, so a script that reads
    /// stdout alone still learns nothing it could mistake for all clear.
    /// </summary>
    public static string Document(string pattern, string refusal)
    {
        return JsonText.Document(writer =>
        {
            writer.WriteString("pattern", pattern);
            writer.WriteNumber("matched", 0);
            writer.WriteString("refused", refusal);
        });
    }

    /// <summary>The refusal, in the estate's words: what was asked for, what
    /// there is where it was asked, and where the answer came from.</summary>
    private static string Refusal(ScopeIndex index, string pattern, string source)
    {
        const int most = 12;
        string under = ScopePattern.Normal(Literal(pattern));
        IReadOnlyList<Branch> beneath = index.Branches(under);
        string named = string.Join(
            ", ",
            beneath.Select(branch => branch.Label).Order(StringComparer.Ordinal).Take(most));
        string rest = beneath.Count > most
            ? string.Create(CultureInfo.InvariantCulture, $" and {beneath.Count - most:N0} more")
            : string.Empty;
        string what = beneath.Count == 0
            ? string.Create(
                CultureInfo.InvariantCulture, $"{index.Scopes:N0} scope(s) published")
            : $"beneath {under} there is: {named}{rest}";

        return $"REFUSED  no scope matches {pattern}{Environment.NewLine}"
            + $"         {what}{Environment.NewLine}"
            + $"         source {source}";
    }

    /// <summary>The part of a pattern that is still literal: everything down to
    /// the last <c>/</c> before its first wildcard — the place to say what
    /// there is instead.</summary>
    private static string Literal(string pattern)
    {
        int wild = pattern.IndexOfAny(['*', '?']);

        if (wild < 0)
        {
            return pattern;
        }

        int slash = pattern.LastIndexOf('/', wild);

        return slash <= 0 ? ScopeTree.Root : pattern[..slash];
    }
}
