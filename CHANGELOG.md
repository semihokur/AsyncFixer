# Release History

## 2.1.0 (2025-12)

- **AsyncFixer04**: Detect fire-and-forget async calls in `using var` declarations (C# 8.0+) and warn when a task is returned from a using scope without being awaited. The disposable resource will be disposed before the returned task completes. ([#13](https://github.com/semihokur/AsyncFixer/issues/13))
- **AsyncFixer04**: Fix false positive when task is assigned to a variable and awaited later within the same using block (e.g., `await Task.WhenAny(task, ...)`).
- **AsyncFixer06**: Fix false positive for `Assert.ThrowsAsync` and similar assertion methods where discarding the `Task<T>` result is intentional.
- **AsyncFixer03**: Add test coverage for event handlers using `Action<T>` delegates. ([#16](https://github.com/semihokur/AsyncFixer/issues/16))
- Improve diagnostic messages and README for better AI assistant compatibility.

## 2.0.0 (2025-12)

- **AsyncFixer06**: New analyzer to detect implicit `Task<T>` to `Task` conversion in non-async lambdas. Unlike async lambdas which produce compiler error CS8031, non-async lambdas silently discard the result. ([#26](https://github.com/semihokur/AsyncFixer/issues/26))
- **AsyncFixer01**: Fix false positive with `await foreach` statements. ([#41](https://github.com/semihokur/AsyncFixer/issues/41))
- **AsyncFixer01**: Fix false positives for `ValueTask<T>` return types.
- **AsyncFixer02**: Fix false positive with `nameof(Task.Result)`. ([#30](https://github.com/semihokur/AsyncFixer/issues/30))
- **AsyncFixer02**: Fix false positive after `Task.WhenAll` - accessing `.Result` on awaited `WhenAll` tasks is safe.
- **AsyncFixer02**: Fix false positives in synchronous local functions and lambdas inside async methods.
- **AsyncFixer03**: Fix false positive with generic `EventArgs` types. ([#39](https://github.com/semihokur/AsyncFixer/issues/39))
- **AsyncFixer05**: Fix false positive with `TaskTupleAwaiter` and similar custom awaiters. ([#31](https://github.com/semihokur/AsyncFixer/issues/31))
- **AsyncFixer05**: Detect nested `Task` in return statements. ([#27](https://github.com/semihokur/AsyncFixer/issues/27))
- **Code Fix**: Correct nested invocation handling in Fix All operations. ([#37](https://github.com/semihokur/AsyncFixer/issues/37))
- **Code Fix**: Add parentheses when async conversion is followed by member access. ([#38](https://github.com/semihokur/AsyncFixer/issues/38))
- Upgrade `Microsoft.CodeAnalysis` to v4.8.0.

## 1.6.0 (2022-05)
- 01.UnnecessaryAsync: Do not warn when the await expressions are in the scope of a using declaration.
- 01.UnnecessaryAsync: Fix crash due to InvalidCastExpression.
- Upgrade Visual Studio SDK packages for vsix extensions.
- Upgrade Microsoft.CodeAnalysis package to 3.11.0

## 1.5.1 (2021-01)
- 01.UnnecessaryAsync: Implemented a dataflow analysis for disposable objects to fix false-negatives and false-positives.

## 1.5.0 (2021-01)
- 01.UnnecessaryAsync: Fix false-negatives to cover more cases.
- 02.BlockingCallInsideAsync: Stop suggesting async calls for virtual calls and Dispose methods.
- Add support for ValueTask.

## 1.4.1 (2021-01)
- 01.UnnecessaryAsync: Take account of the lack of covariance in the Task class.
- 03.AsyncVoid: Add support for fire-and-forget delegates.
- 04.AsyncCallInsideUsingBlock: Fix a potential NRE.

## 1.4.0 (2021-01)
- 01.UnnecessaryAsync: Fix false warnings with `using` expression statements.
- 01.UnnecessaryAsync: Support for expression-bodied members.
- 02.BlockingCallInsideAsync: Stop suggesting async calls from non-system assemblies.

## 1.3.0 (2020-05)
- Updated Roslyn dependencies to v3.3.1.
- Fixed several performance bugs.

## 1.1.7 (2019-04)
- Fixed bugs in code-fixes.

## 1.1.5 (2017-02)
- Added a license. 
- Changed nuspec as a development dependency. 
- Fixed several bugs in fixing anti-patterns.

## 1.1.4 (2016-09)
- Fixed false positives.

## 1.1.3 (2016-03)
- Depending on CodeAnalysis 1.0.0 instead of 1.1.1 due to compatibility issues for some users.

## 1.1.2 (2016-03)
- Fixed false positives for new analyzers.

## 1.1.0 (2016-03)
- Added 2 new code analyzers and improved accuracy of existing analyzers.

## 1.0.0 (2015-07)
- 3 code analyzers to detect and fix very common async anti-patterns.