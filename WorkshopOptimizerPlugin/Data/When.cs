namespace WorkshopOptimizerPlugin.Data;


internal enum When
{
    Never = 0,
    Weak = 1,
    Strong = 2,
    Either = 3,
    Always = 4,
    Required = 5,
}

internal static class WhenUtils
{
    public static readonly string[] WhenAsStrings =
    [
        When.Never.ToString(), When.Weak.ToString(), When.Strong.ToString(),
        When.Either.ToString(), When.Always.ToString(), When.Required.ToString(),
    ];
}
