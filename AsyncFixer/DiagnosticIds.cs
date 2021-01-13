namespace AsyncFixer
{
    public static class DiagnosticIds
    {
        public const string Category = "AsyncUsage";

        // The rules below are only applied under async method declarations.
        public const string UnnecessaryAsync = "AsyncFixer01";
        public const string BlockingCallInsideAsync = "AsyncFixer02";
        public const string AsyncVoid = "AsyncFixer03";

        // The rules below are applied under every method declaration.
        public const string AsyncCallInsideUsingBlock = "AsyncFixer04";
        public const string NestedTaskToOuterTask = "AsyncFixer05";
    }
}
