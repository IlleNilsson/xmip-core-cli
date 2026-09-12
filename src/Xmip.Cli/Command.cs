namespace Xmip.Cli;

/// <summary>The commands <c>xmip</c> answers. ADR-0014 clause 8: one
/// executable, one word per command.</summary>
public enum Command
{
    /// <summary>Print the usage text.</summary>
    Help,

    /// <summary>The two boundaries this build speaks.</summary>
    Abi,

    /// <summary>What a status code means.</summary>
    Status,

    /// <summary>Load a module and report what it says it is.</summary>
    Probe,

    /// <summary>Health at and beneath a scope, from the runtime.</summary>
    Health,

    /// <summary>Received, processed, sent, retrying and failed.</summary>
    Activity,

    /// <summary>Direct children of a scope.</summary>
    List,

    /// <summary>One scope with health and activity.</summary>
    Show,

    /// <summary>Pause a scope.</summary>
    Pause,

    /// <summary>Resume a scope.</summary>
    Resume,

    /// <summary>Start a scope.</summary>
    Start,

    /// <summary>Stop a scope.</summary>
    Stop,

    /// <summary>Restart a scope.</summary>
    Restart,

    /// <summary>Check a node configuration without starting it.</summary>
    Validate,
}
