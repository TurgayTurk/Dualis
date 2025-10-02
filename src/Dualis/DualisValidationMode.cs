namespace Dualis;

/// <summary>
/// Controls how startup validation reports configuration issues.
/// </summary>
public enum DualisValidationMode
{
    /// <summary>
    /// Throw an exception during validation when issues are found.
    /// </summary>
    Throw,

    /// <summary>
    /// Only emit warnings (no exceptions) when issues are found.
    /// </summary>
    Warn,

    /// <summary>
    /// Do not perform validation or report issues.
    /// </summary>
    Ignore,
}
