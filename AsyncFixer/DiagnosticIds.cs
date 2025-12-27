namespace AsyncFixer
{
    public static class DiagnosticIds
    {
        // Async method issues: These rules analyze async methods and delegates.
        public const string UnnecessaryAsync = "AsyncFixer01";         // Detects unnecessary async/await in async methods
        public const string BlockingCallInsideAsync = "AsyncFixer02";  // Detects blocking calls inside async methods
        public const string AsyncVoid = "AsyncFixer03";                // Detects async void methods and delegates

        // Task type issues: These rules analyze Task usage patterns in any context.
        public const string AsyncCallInsideUsingBlock = "AsyncFixer04"; // Detects unawaited async calls in using blocks
        public const string NestedTaskToOuterTask = "AsyncFixer05";     // Detects Task<Task> nested task issues
        public const string ImplicitTaskTypeMismatch = "AsyncFixer06";  // Detects Task<T> to Task implicit conversion
    }
}
