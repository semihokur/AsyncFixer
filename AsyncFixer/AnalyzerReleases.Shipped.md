; Shipped analyzer releases

## Release 2.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AsyncFixer01 | AsyncUsage | Warning | Detects unnecessary async/await in async methods
AsyncFixer02 | AsyncUsage | Warning | Detects blocking calls inside async methods
AsyncFixer03 | AsyncUsage | Warning | Detects async void methods and delegates
AsyncFixer04 | AsyncUsage | Warning | Detects unawaited async calls in using blocks
AsyncFixer05 | AsyncUsage | Warning | Detects Task<Task> nested task issues
AsyncFixer06 | AsyncUsage | Warning | Detects Task<T> to Task implicit conversion
