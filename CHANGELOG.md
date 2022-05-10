# Release History

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