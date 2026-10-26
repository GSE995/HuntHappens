namespace HuntHappens.Editor
{
    // Retain the existing check entry point after replacing hand carrying with cart transport.
    public static class DeerCarryPlayChecks
    {
        public static string Run() => DeerCartPlayChecks.Run();
    }
}
