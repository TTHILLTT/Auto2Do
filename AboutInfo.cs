namespace Auto2Do;

internal static class AboutInfo
{
    public const string Attribution = "TTHILLTT Use Vibe Coding";
    public const string Model = "GPT-6-Luna";
    public const string RepositoryUrl = "https://github.com/TTHILLTT/Auto2Do";

    public static void WriteTo(TextWriter writer)
    {
        writer.WriteLine(Attribution);
        writer.WriteLine(Model);
        writer.WriteLine(RepositoryUrl);
    }
}
