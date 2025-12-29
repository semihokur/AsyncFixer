using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using AsyncFixer.AsyncVoid;
using AsyncFixer.AsyncCallInsideUsingBlock;
using Xunit;

namespace AsyncFixer.Test
{
    public class AsyncCallInsideUsingBlockTests : CodeFixVerifier
    {
        [Fact]
        public void NoWarn_AsyncCallInsideUsingBlockTest1()
        {
            //No diagnostics expected to show up

            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        using (var stream = new FileStream("""", FileMode.Open))
        {
            await stream.WriteAsync(new byte[] { }, 0, 0);
            await stream.WriteAsync(new byte[] { }, 0, 0).ConfigureAwait(false);
            Task t = Task.Run(async () =>
            {
                var s = await stream.ReadAsync(new byte[] { }, 0, 0);
            });
        }
    }
    void foo2()
    {
        using (var stream = new FileStream("""", FileMode.Open))
        {
            stream.WriteAsync(new byte[] { }, 0, 0).Wait();
            stream.WriteAsync(new byte[] { }, 0, 0).GetAwaiter().GetResult();
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void AsyncCallInsideUsingBlockTest2()
        {
            var test = @"
using System;
using System.IO

class Program
{
    static void foo()
    {
        var newStream = new FileStream("""", FileMode.Create);
        using (var stream = new FileStream(""existing"", FileMode.Open))
        {
            newStream.CopyToAsync(stream);
        }
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected);
        }

        [Fact]
        public void AsyncCallInsideUsingBlockTest3()
        {
            var test = @"
using System;
using System.IO

class Program
{
    static void foo()
    {
        var newStream = new FileStream("""", FileMode.Create);
        using (var stream = new FileStream(""existing"", FileMode.Open))
        {
            stream.CopyToAsync(newStream);
        }
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected);
        }

        [Fact]
        public void NoWarn_AsyncCallInsideUsingBlockTest4()
        {
            var test = @"
using System;
using System.IO;

class Program
{
    static void foo()
    {
        using (var stream = new FileStream("", FileMode.Open))
        {
            Task t2 = Task.Run(async () =>
            {
                var q = new A(stream);
                await Task.Delay(1);
            });
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_AsyncCallInsideUsingBlockTest5()
        {
            var test = @"
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AsyncFixer;

class Program
{
    static void foo()
    {
        var newStream = new FileStream(string.Empty, FileMode.Create);
        using (var stream = new FileStream(string.Empty, FileMode.Open))
        {
            stream.CopyToAsync(newStream).SyncResult();
        }
    }
}
public static class MyExtensions
{
    [BlockCaller]
    public static void SyncResult(this Task task)
    {
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void DisposableObjectAsArgument()
        {
            var test = @"
using System;
using System.IO

class Program
{
    static async void foo()
    {
        var newStream = new FileStream("""", FileMode.Create);
        using (var stream = new FileStream(""existing"", FileMode.Open))
        {
            await stream.CopyToAsync(newStream);
            newStream.CopyToAsync(stream); 
        }
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected);
        }

        [Fact]
        public void NoWarn_CaseSensitivity()
        {
            var test = @"
using System;
using System.IO

class Program
{
    static async void foo()
    {
        var newStream = new FileStream("""", FileMode.Create);
        using (var stream = new FileStream(""existing"", FileMode.Open))
        {
            await stream.CopyToAsync(newStream);
            newStream.CopyToAsync(Stream.Null); 
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_UsingStatementNoWarning()
        {
            var test = @"
using System;
using System.IO;

class Program
{
    static void foo()
    {
        using var stream = new FileStream("", FileMode.Open);
        stream.CopyToAsync(stream);
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_AwaitedLater()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task foo()
    {
        var newStream = new FileStream("""", FileMode.Create);
        using (var stream = new FileStream(""existing"", FileMode.Open))
        {
            var task = stream.CopyToAsync(newStream);
            await task;
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning when task is assigned and used in Task.WhenAny/WhenAll
        /// </summary>
        [Fact]
        public void NoWarn_AwaitedLaterWithWhenAny()
        {
            var test = @"
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task WithTimeoutAsync(CancellationToken token)
    {
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            var task = DoWorkAsync(cts.Token);
            await Task.WhenAny(task, Task.Delay(1000));
        }
    }

    static Task DoWorkAsync(CancellationToken token) => Task.CompletedTask;
}";
            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Issue #13: Warn when task is returned from using block without await.
        /// The disposable will be disposed before the task completes.
        /// </summary>
        [Fact]
        public void ReturnTaskFromUsingBlock()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        using (var stream = new FileStream("""", FileMode.Open))
        {
            return stream.CopyToAsync(new MemoryStream());
        }
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Issue #13: Warn when task is returned from using var declaration without await.
        /// The disposable will be disposed before the task completes.
        /// </summary>
        [Fact]
        public void ReturnTaskFromUsingDeclaration()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        using var stream = new FileStream("""", FileMode.Open);
        return stream.CopyToAsync(new MemoryStream());
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Issue #13: Warn when task is returned with multiple using var declarations.
        /// Both disposables will be disposed before the task completes.
        /// </summary>
        [Fact]
        public void ReturnTaskFromMultipleUsingDeclarations()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        using var destination = new MemoryStream();
        using var source = new FileStream("""", FileMode.Open);
        return source.CopyToAsync(destination);
    }
}";
            // Should report two diagnostics - one for each disposable used in the async call
            var expected1 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            var expected2 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected1, expected2);
        }

        /// <summary>
        /// No warning when task from using var is properly awaited
        /// </summary>
        [Fact]
        public void NoWarn_UsingDeclarationWithAwait()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task foo()
    {
        using var stream = new FileStream("""", FileMode.Open);
        await stream.CopyToAsync(new MemoryStream());
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Warn when using var has fire-and-forget async call (not returned, not awaited)
        /// </summary>
        [Fact]
        public void UsingDeclarationFireAndForget()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static void foo()
    {
        using var stream = new FileStream("""", FileMode.Open);
        stream.CopyToAsync(new MemoryStream());
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning when using var has blocking call (.Wait())
        /// </summary>
        [Fact]
        public void NoWarn_UsingDeclarationWithWait()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static void foo()
    {
        using var stream = new FileStream("""", FileMode.Open);
        stream.CopyToAsync(new MemoryStream()).Wait();
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning when using var task is assigned and awaited later
        /// </summary>
        [Fact]
        public void NoWarn_UsingDeclarationAwaitedLater()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task foo()
    {
        using var stream = new FileStream("""", FileMode.Open);
        var task = stream.CopyToAsync(new MemoryStream());
        await task;
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Issue #28: Warn when using declaration has task assigned to variable and returned via Task.WhenAny
        /// The disposable (cts) is used in Task.Delay, stored in a variable, and returned via Task.WhenAny without await.
        /// </summary>
        [Fact]
        public void ReturnTaskFromUsingDeclaration_WithCancellationToken()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        using var cts = new CancellationTokenSource();
        var a = Task.Delay(1, cts.Token);
        var b = Task.Delay(1, cts.Token);
        return Task.WhenAny(a, b);
    }
}";
            // Should warn for both usages of cts.Token since the tasks are returned without being awaited
            var expected1 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            var expected2 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected1, expected2);
        }

        /// <summary>
        /// Issue #28: This test confirms the exact scenario from the issue - task stored in variable, then returned.
        /// This is the pattern that was NOT being detected for using declarations.
        /// </summary>
        [Fact]
        public void ReturnTaskFromUsingDeclaration_TaskStoredThenReturned()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        using var stream = new FileStream("""", FileMode.Open);
        var task = stream.CopyToAsync(new MemoryStream());
        return task;
    }
}";
            // Should warn because the task using 'stream' is returned without await
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Issue #28: Test case where task is stored and then passed to Task.WhenAny -
        /// this is NOT returned directly but the variable is returned later.
        /// This tests the scenario where the return statement does not contain the disposable identifier.
        /// </summary>
        [Fact]
        public void Issue28_UsingDeclaration_TaskStoredAndReturnedViaThenAny()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        using var cts = new CancellationTokenSource();
        var a = Task.Delay(1, cts.Token);
        var b = Task.Delay(1, cts.Token);
        return Task.WhenAny(a, b);
    }
}";
            // Should warn for both usages of cts.Token - the tasks are NOT awaited within the using scope
            var expected1 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            var expected2 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected1, expected2);
        }

        /// <summary>
        /// Contrasting test - using block (with braces) for the same pattern should also warn
        /// </summary>
        [Fact]
        public void Issue28_UsingBlock_TaskStoredAndReturnedViaWhenAny()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        using (var cts = new CancellationTokenSource())
        {
            var a = Task.Delay(1, cts.Token);
            var b = Task.Delay(1, cts.Token);
            return Task.WhenAny(a, b);
        }
    }
}";
            // Should warn for both usages of cts.Token
            var expected1 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            var expected2 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected1, expected2);
        }

        /// <summary>
        /// Test to verify - if we expect NO warnings, the test should FAIL (confirming analyzer works)
        /// </summary>
        [Fact]
        public void Issue28_VerifyAnalyzerDetectsIssue()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        using var cts = new CancellationTokenSource();
        var a = Task.Delay(1, cts.Token);
        return a;
    }
}";
            // This should warn - if the analyzer works correctly
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Issue #28: Using statement with braces should warn (this already works)
        /// </summary>
        [Fact]
        public void ReturnTaskFromUsingBlock_WithCancellationToken()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        using (var cts = new CancellationTokenSource())
        {
            var a = Task.Delay(1, cts.Token);
            var b = Task.Delay(1, cts.Token);
            return Task.WhenAny(a, b);
        }
    }
}";
            // Should warn for both usages of cts.Token
            var expected1 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            var expected2 = new DiagnosticResult { Id = DiagnosticIds.AsyncCallInsideUsingBlock };
            VerifyCSharpDiagnostic(test, expected1, expected2);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AsyncCallInsideUsingBlockAnalyzer();
        }
    }
}
