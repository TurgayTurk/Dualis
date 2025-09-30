namespace Dualis;

/// <summary>
/// Assembly-level opt-in attribute that enables the Dualis source generators in the current project.
/// Use <c>[assembly: Dualis.EnableDualisGeneration]</c> to force-enable generation, regardless of MSBuild gating.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class EnableDualisGenerationAttribute : System.Attribute
{
}
