internal static class Initializer
{
    [ModuleInitializer]
    public static void SetDefaults()
    {
        // AssertionOptions.AssertEquivalencyUsing(
        //    options => { <configure here> });
        AssertionOptions.FormattingOptions.MaxLines = 250;
    }
}