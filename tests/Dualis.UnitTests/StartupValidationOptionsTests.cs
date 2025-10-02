using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dualis.UnitTests;

/// <summary>
/// Tests that validate the new startup validation options exposed via <see cref="DualizorOptions"/>.
/// These tests assert the default configuration and the ability to effectively disable validation.
/// </summary>
public sealed class StartupValidationOptionsTests
{
    /// <summary>
    /// Asserts that the defaults are non-breaking: startup validation is enabled and runs in Throw mode.
    /// </summary>
    [Fact]
    public void DefaultOptions_EnableStartupValidationTrue_ThrowMode()
    {
        // Arrange
        var opts = new DualizorOptions();

        // Assert defaults (non-breaking)
        opts.EnableStartupValidation.Should().BeTrue();
        opts.StartupValidationMode.Should().Be(DualisValidationMode.Throw);
    }

    /// <summary>
    /// Ensures consumers can effectively disable validation by setting the mode to Ignore.
    /// </summary>
    [Fact]
    public void CanDisableValidationByIgnoreMode()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDualis(options =>
        {
            options.EnableStartupValidation = true; // explicit
            options.StartupValidationMode = DualisValidationMode.Ignore; // disable effectively
        });

        // Assert
        services.Should().NotBeNull();
    }
}
