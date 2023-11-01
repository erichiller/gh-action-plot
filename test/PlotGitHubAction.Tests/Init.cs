using System.Runtime.CompilerServices;

using FluentAssertions;

namespace PlotGitHubAction.Tests;

internal static class Initializer {
    [ModuleInitializer]
    public static void SetDefaults() {
        AssertionOptions.FormattingOptions.MaxLines = 250;
    }
}