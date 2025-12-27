using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using AsyncFixer.UnnecessaryAsync;
using Xunit;
using Verify = AsyncFixer.Test.Helpers.CSharpCodeFixVerifier<
    AsyncFixer.UnnecessaryAsync.UnnecessaryAsyncAnalyzer,
    AsyncFixer.UnnecessaryAsync.UnnecessaryAsyncFixer>;

namespace AsyncFixer.Test
{
    public class UnnecessaryAsyncTests : CodeFixVerifier
    {
        [Fact]
        public void NoWarn_UnnecessaryAsyncTest1()
        {
            //No diagnostics expected to show up

            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> foo(int a)
    {
        if (a == 0)
            return 0;
        return await Task.Run(()=>2);
    }

    async Task boo()
    {
        await Task.Delay(1);
        await Task.Delay(1);
    }

    async Task foo2()
    {
        using (new StreamReader(""""))
        {
            await Task.Delay(1);
        }
    }

    static async Task<int> boo2()
    {
        try
        {
            return await Task.Run(() => 1);
        }
        catch (Exception)
        {
            return await Task.Run(() => 1);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [Fact]
        public void UnnecessaryAsyncTest2()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    static async void foo()
    {
        await Task.Delay(2).ConfigureAwait(false);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        return Task.Delay(2);
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void UnnecessaryAsyncTest3()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> foo()
    {
        return await Task.Run(()=> 3);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task<int> foo()
    {
        return Task.Run(()=> 3);
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void UnnecessaryAsyncTest4()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> foo(int b)
    {
        if (b > 5)
        {
            return await Task.Run(() => 3);
        }
        return await foo(b);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task<int> foo(int b)
    {
        if (b > 5)
        {
            return Task.Run(() => 3);
        }
        return foo(b);
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void UnnecessaryAsyncTest5()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    static async Task foo()
    {
        var t = Task.Delay(2);
        // comment
        await t;
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    static Task foo()
    {
        var t = Task.Delay(2);
        // comment
        return t;
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void GenericTask()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> foo(int i)
    {
        return await Task.Run(()=>3);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task<int> foo(int i)
    {
        return Task.Run(()=>3);
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void NoWarn_TaskNotCovariant()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<object> foo(int i)
    {
        return await Task.Run(()=>3);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        // TODO: remove the awaits even though they do not use return statements.
        [Fact(Skip = "Remove awaits")]
        public void UnnecessaryAsyncTest7()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo(int i)
    {
        if (i > 1)
        {
            await Task.Delay(2);
        }
        else
        {
            await Task.Delay(1);
        }
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo(int i)
    {
        if (i > 1)
        {
            return Task.Delay(2);
        }

        return Task.Delay(1);
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void UnnecessaryAsyncTest8()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task Test2Async(string str)
    {
        await Task.Delay(1);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task Test2Async(string str)
    {
        return Task.Delay(1);
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void AwaitExpressionsUnderLambda()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        await Task.Run(async () => await Task.FromResult(true));
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        return Task.Run(async () => await Task.FromResult(true));
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void AwaitExpressionsUnderLambdaWithIntermediateMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        await bar(async () => await Task.FromResult(true)).ConfigureAwait(false);
    }

    async Task bar(Func<Task<bool>> action)
    {
        await action();
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        return bar(async () => await Task.FromResult(true));
    }

    Task bar(Func<Task<bool>> action)
    {
        return action();
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// Do not remove await expressions involving disposable objects
        /// Otherwise, exceptions can occur as the task continues with the disposed objects.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitAfterUsingStatement()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using System.IO;

class Program
{
    async Task foo()
    {
        MemoryStream destination = new MemoryStream();
        using FileStream source = File.Open(""data"", FileMode.Open);
        await source.CopyToAsync(destination);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Do not remove await expressions in using scopes
        /// Otherwise, the object is disposed early which may have unexpected
        /// consequences or be semantically incorrect.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitAfterUsingStatement2()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using System.IO;

class Program
{
    async Task foo()
    {
        using var sw = new DisposableStopwatch(onDispose: () => Console.WriteLine(""Finished!"");
        await Task.Delay(1000);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public Task OutOfScopeSiblingUsingBlock()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using System.IO;

class Program
{
    async Task foo(bool cond)
    {
        using (var stream = FileStream.Null)
        {
            int x = 5;
        }

        {
            var stream = ""Stream"";
            await Task.Run(() => { Console.WriteLine(stream.Length); });
        }
    }
}";

            var fixtest = @"
using System;
using System.Threading.Tasks;
using System.IO;

class Program
{
    Task foo(bool cond)
    {
        using (var stream = FileStream.Null)
        {
            int x = 5;
        }

        {
            var stream = ""Stream"";
            return Task.Run(() => { Console.WriteLine(stream.Length); });
        }
    }
}";

            return new Verify.Test
            {
                TestState =
                {
                    Sources = { test },
                    ExpectedDiagnostics =
                    {
                        Verify.Diagnostic().WithSpan(8, 5, 19, 6).WithArguments("foo"),
                    },
                },
                FixedState =
                {
                    Sources = { fixtest },
                },
            }.RunAsync();
        }

        /// <summary>
        /// Do not remove await expressions involving disposable objects inside using block
        /// </summary>
        [Fact]
        public void NoWarn_AwaitInsideUsingBlock()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using System.IO;

class Program
{
    static async Task foo()
    {
        MemoryStream destination = new MemoryStream();
        using(FileStream source = File.Open(""data"", FileMode.Open))
        {
            await source.CopyToAsync(destination);  // LEAKING TASK REFERENCE OUTSIDE
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void ExpressionBody()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static async Task Foo() => await Task.FromResult(true);
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static Task Foo() => Task.FromResult(true);
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void ExpressionBodyWithGenericTask()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static async Task<bool> Foo() => await Task.FromResult(true);
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static Task<bool> Foo() => Task.FromResult(true);
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void ExpressionBodyWithAsyncLambda()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static async Task<bool> foo() => await bar(async a => await Task.FromResult(a));

    public static async Task<bool> bar(Func<bool, Task<bool>> action) => await Task.FromResult(true).ConfigureAwait(false);
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected, expected); // Expect two diagnostics. One for foo and one for bar.

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static Task<bool> foo() => bar(async a => await Task.FromResult(a));

    public static Task<bool> bar(Func<bool, Task<bool>> action) => Task.FromResult(true);
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void NoWarn_ExpressionBodyWithMultipleAwaitExprs()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    public async Task<bool> OuterAsync() => await InnerAsync(await Task.FromResult(true));

    public Task<bool> InnerAsync(bool parameter) => Task.FromResult(parameter);
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_ExpressionBodyTaskNotCovariant()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static async Task<object> Foo() => await Task.FromResult(3);
}";

            VerifyCSharpDiagnostic(test);
        }
        
        [Fact]
        public void NoWarn_UsingStatement()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        using var stream = FileStream.Null;
        var stream2 = FileStream.Null;
        await Task.Run(() => stream2.CopyToAsync(stream));
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_UsingStatement2()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        using var stream = FileStream.Null;
        await stream.ReadAsync(null);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_UsingStatement3()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        using var stream = new MemoryStream();
        int streamOperation()
        {
            return stream.Read(null);
        }

        await Task.Run(() => streamOperation());
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_UsingStatement4()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        using var stream = new MemoryStream();
        int streamOperation()
        {
            return stream.Read(null);
        }

        var t = Task.Run(() => streamOperation());
        await t;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_UsingStatement5()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        using var stream = new MemoryStream();
        int streamOperation()
        {
            return stream.Read(null);
        }

        Task t = null;
        t = Task.Run(() => streamOperation());
        await t;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void ValueTaskSupport()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async ValueTask<int> foo(int x)
    {
        if (x == 0)
            return x;
        await Task.Delay(1);
        return x;
    }

    async ValueTask<int> bar(int x)
    {
        if (x == 0)
            return x;
        await Task.Delay(1);
        return x;
    }

    async ValueTask<int> boo(bool c) {
        if (c)
        {
            return await foo(1).ConfigureAwait(true);
        }
        
        return await bar(2);
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fix = @"
using System;
using System.Threading.Tasks;

class Program
{
    async ValueTask<int> foo(int x)
    {
        if (x == 0)
            return x;
        await Task.Delay(1);
        return x;
    }

    async ValueTask<int> bar(int x)
    {
        if (x == 0)
            return x;
        await Task.Delay(1);
        return x;
    }

    ValueTask<int> boo(bool c) {
        if (c)
        {
            return foo(1);
        }
        
        return bar(2);
    }
}";
            VerifyCSharpFix(test, fix);
        }

        [Fact]
        public void DontCastValueTaskToTask()
        {
            // No diagnostics expected to show up
            // We can't remove async to return ValueTask if the method returns a Task
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async ValueTask<int> foo(int x)
    {
        if (x == 0)
            return x;
        await Task.Delay(1);
        return x;
    }

    async Task<int> boo() {
        return await foo(1);
    }
}";

            VerifyCSharpDiagnostic(test);
        }


        [Fact]
        public void DontCastValueTaskToTaskExpressionBodiedMember()
        {
            //No diagnostics expected to show up
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async ValueTask<int> foo(int x)
    {
        if (x == 0)
            return x;
        await Task.Delay(1);
        return x;
    }

    async Task<int> boo() => await foo(1);
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void MixOfValueTaskAndTaskIsNotFixable()
        {
            //No diagnostics expected to show up
            // We can't remove async to return ValueTask if the method returns a Task
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async ValueTask<int> foo(int x)
    {
        if (x == 0)
            return x;
        await Task.Delay(1);
        return x;
    }

    async Task<int> bar(int x)
    {
        if (x == 0)
            return x;
        await Task.Delay(1);
        return x;
    }

    async Task<int> boo(bool c) {
        if (c)
        {
            return await foo(1);
        }
        
        return await bar(2);
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_ValueTaskOfUnitToValueTask()
        {
            // No diagnostics expected to show up.
            // This pattern is sometimes used to "discard" a Unit result, e.g. Mediator's Unit.
            // Removing async/await would require an implicit conversion from ValueTask<Unit> to ValueTask,
            // which does not exist.
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mediator
{
    public readonly struct Unit { }
}

public interface ISender
{
    ValueTask<Mediator.Unit> Send(object request, CancellationToken cancellationToken);
}

public sealed class Program
{
    public async ValueTask EditStorageAsync(ISender sender, object command, CancellationToken cancellationToken) =>
        await sender.Send(command, cancellationToken);
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_AsyncVoidAwaitingValueTask()
        {
            // No diagnostics expected to show up.
            // The fixer changes `async void` to `Task`, but `ValueTask` is not implicitly convertible to `Task`.
            var test = @"
using System;
using System.Threading.Tasks;

public sealed class Program
{
    private static async ValueTask WorkAsync()
    {
        await Task.Delay(1);
    }

    public static async void FireAndForget()
    {
        await WorkAsync();
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public Task NoWarn_AwaitForEach()
        {
            //No diagnostics expected to show up

            var test = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal class Program
{
    public async Task MyFunction()
    {
        await foreach (var i in RangeAsync(10, 3))
        {
            Console.WriteLine(i);
        }

        await Task.Delay(1);
    }

    static async IAsyncEnumerable<int> RangeAsync(int start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(i);
            yield return start + i;
        }
    }
}";
            return Verify.VerifyAsync(test);
        }

        [Fact]
        public Task AwaitForEachUnderLambda()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal class Program
{
    public async Task MyFunction()
    {
        await Task.Run(async () =>
        {
            await foreach (var i in RangeAsync(10, 3))
            {
                Console.WriteLine(i);
            }
        });
    }

    static async IAsyncEnumerable<int> RangeAsync(int start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(i);
            yield return start + i;
        }
    }
}";

            var fixtest = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal class Program
{
    public Task MyFunction()
    {
        return Task.Run(async () =>
        {
            await foreach (var i in RangeAsync(10, 3))
            {
                Console.WriteLine(i);
            }
        });
    }

    static async IAsyncEnumerable<int> RangeAsync(int start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(i);
            yield return start + i;
        }
    }
}";
            return new Verify.Test
            {
                TestState =
                {
                    Sources = { test },
                    ExpectedDiagnostics =
                    {
                        Verify.Diagnostic().WithSpan(8, 5, 17, 6).WithArguments("MyFunction"),
                    },
                },
                FixedState =
                {
                    Sources = { fixtest },
                },
            }.RunAsync();
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new UnnecessaryAsyncFixer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new UnnecessaryAsyncAnalyzer();
        }
    }
}
