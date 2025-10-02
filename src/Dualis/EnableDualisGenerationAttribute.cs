namespace Dualis;

/// <summary>
/// Assembly-level opt-in attribute that enables the Dualis source generators in the current project.
/// Use <c>[assembly: Dualis.EnableDualisGeneration]</c> to force-enable generation, regardless of MSBuild gating.
/// </summary>
[AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class EnableDualisGenerationAttribute : Attribute;
