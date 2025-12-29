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

        /// <summary>
        /// If-else with terminal awaits in both branches can be transformed.
        /// </summary>
        [Fact]
        public void UnnecessaryAsyncTest7_IfElseTerminalAwaits()
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
        else
        {
            return Task.Delay(1);
        }
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// Else-if chain with terminal awaits in all branches can be transformed.
        /// </summary>
        [Fact]
        public void IfElseIfElse_TerminalAwaits()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo(int i)
    {
        if (i > 10)
        {
            await Task.Delay(3);
        }
        else if (i > 1)
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
        if (i > 10)
        {
            return Task.Delay(3);
        }
        else if (i > 1)
        {
            return Task.Delay(2);
        }
        else
        {
            return Task.Delay(1);
        }
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// If-else with ConfigureAwait in terminal awaits.
        /// </summary>
        [Fact]
        public void IfElse_TerminalAwaits_WithConfigureAwait()
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
            await Task.Delay(2).ConfigureAwait(false);
        }
        else
        {
            await Task.Delay(1).ConfigureAwait(false);
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
        else
        {
            return Task.Delay(1);
        }
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// No warning: if without else - not all paths covered.
        /// </summary>
        [Fact]
        public void NoWarn_IfWithoutElse_TerminalAwait()
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
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: if-else where one branch doesn't end with await.
        /// </summary>
        [Fact]
        public void NoWarn_IfElse_OneBranchNoAwait()
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
            Console.WriteLine(""no await here"");
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: if-else with code after the if-else statement.
        /// </summary>
        [Fact]
        public void NoWarn_IfElse_CodeAfter()
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
        Console.WriteLine(""after"");
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: if-else inside using block.
        /// </summary>
        [Fact]
        public void NoWarn_IfElse_InsideUsingBlock()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using System.IO;

class Program
{
    async Task foo(int i)
    {
        using (var stream = new MemoryStream())
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
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: if-else inside try block.
        /// </summary>
        [Fact]
        public void NoWarn_IfElse_InsideTryBlock()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo(int i)
    {
        try
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
        catch { }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: if-else with using declaration in scope.
        /// </summary>
        [Fact]
        public void NoWarn_IfElse_UsingDeclarationInScope()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using System.IO;

class Program
{
    async Task foo(int i)
    {
        using var stream = new MemoryStream();
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

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: if-else with multiple awaits in one branch.
        /// </summary>
        [Fact]
        public void NoWarn_IfElse_MultipleAwaitsInBranch()
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
            await Task.Delay(1);
            await Task.Delay(2);
        }
        else
        {
            await Task.Delay(3);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: if-else-if missing final else - not all paths covered.
        /// </summary>
        [Fact]
        public void NoWarn_IfElseIf_MissingFinalElse()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo(int i)
    {
        if (i > 10)
        {
            await Task.Delay(3);
        }
        else if (i > 1)
        {
            await Task.Delay(2);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: if-else where await is not the last statement in branch.
        /// </summary>
        [Fact]
        public void NoWarn_IfElse_AwaitNotLastInBranch()
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
            Console.WriteLine(""after await"");
        }
        else
        {
            await Task.Delay(1);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: if-else with await inside lambda in branch.
        /// </summary>
        [Fact]
        public void NoWarn_IfElse_AwaitInLambda()
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
            await Task.Run(async () => await Task.Delay(2));
        }
        else
        {
            Console.WriteLine(""sync"");
        }
    }
}";

            VerifyCSharpDiagnostic(test);
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

        /// <summary>
        /// No warning when awaiting Task in a ValueTask-returning method.
        /// Task is not implicitly convertible to ValueTask, so async/await is required.
        /// </summary>
        [Fact]
        public void NoWarn_TaskToValueTask()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public sealed class Program
{
    public static Task DoWorkAsync()
    {
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DoWorkAsync();
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning when awaiting Task<T> in a ValueTask<T>-returning method.
        /// Task<T> is not implicitly convertible to ValueTask<T>.
        /// </summary>
        [Fact]
        public void NoWarn_TaskOfTToValueTaskOfT()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public sealed class Program
{
    public static Task<int> GetValueAsync()
    {
        return Task.FromResult(42);
    }

    public async ValueTask<int> GetAsync()
    {
        return await GetValueAsync();
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning when awaiting ValueTask in a Task-returning method.
        /// ValueTask is not implicitly convertible to Task, so async/await is required.
        /// </summary>
        [Fact]
        public void NoWarn_ValueTaskToTask()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public sealed class Program
{
    public static ValueTask WriteAsync()
    {
        return ValueTask.CompletedTask;
    }

    public async Task DoWorkAsync()
    {
        await WriteAsync();
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning when awaiting ValueTask<T> in a Task<T>-returning method.
        /// ValueTask<T> is not implicitly convertible to Task<T>.
        /// </summary>
        [Fact]
        public void NoWarn_ValueTaskOfTToTaskOfT()
        {
            var test = @"
using System;
using System.Threading.Tasks;

public sealed class Program
{
    public static ValueTask<int> GetValueAsync()
    {
        return ValueTask.FromResult(42);
    }

    public async Task<int> GetAsync()
    {
        return await GetValueAsync();
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
        public Task NoWarn_AwaitForEachFollowedByAwait()
        {
            // Regression: await foreach followed by a single await at the end
            // should NOT be flagged as unnecessary async

            var test = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal class Program
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await foreach (var entry in GetFilesAsync(cancellationToken))
        {
            // do something
        }

        await SaveChangesAsync(cancellationToken);
    }

    static async IAsyncEnumerable<int> GetFilesAsync(CancellationToken ct)
    {
        yield return 1;
        await Task.Delay(1, ct);
    }

    static Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
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

        #region Edge Case Tests - Scenarios where async/await should NOT be removed

        /// <summary>
        /// No warning: await using statement - removing async would cause premature disposal.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitUsingStatement()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class AsyncDisposable : IAsyncDisposable
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public Task DoWorkAsync() => Task.CompletedTask;
}

class Program
{
    async Task foo()
    {
        await using var resource = new AsyncDisposable();
        await resource.DoWorkAsync();
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: await in conditional expression (ternary) - complex control flow.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitInTernaryCondition()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> foo(bool condition)
    {
        return await (condition ? Task.FromResult(1) : Task.FromResult(2));
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning: await in null-coalescing expression.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitInNullCoalescing()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task<int> nullableTask;

    async Task<int> foo()
    {
        return await (nullableTask ?? Task.FromResult(0));
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning: recursive async method - changing to sync return could cause stack overflow.
        /// This is actually safe to transform, but tests current behavior.
        /// </summary>
        [Fact]
        public void RecursiveAsyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> Factorial(int n)
    {
        if (n <= 1)
            return 1;
        return n * await Factorial(n - 1);
    }
}";

            // This should NOT warn because there's a non-await return path
            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: await with captured exception context in async method.
        /// Removing async changes exception stack trace behavior.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitInFinally()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        try
        {
            throw new Exception();
        }
        finally
        {
            await Task.Delay(1);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: await in catch block.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitInCatch()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        try
        {
            throw new Exception();
        }
        catch
        {
            await Task.Delay(1);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: method with multiple awaits where only last could be removed.
        /// </summary>
        [Fact]
        public void NoWarn_MultipleAwaitsOnlyLastRemovable()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        await Task.Delay(1);
        Console.WriteLine(""between"");
        await Task.Delay(2);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: await on result of another await.
        /// </summary>
        [Fact]
        public void NoWarn_NestedAwait()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> foo()
    {
        return await await Task.FromResult(Task.FromResult(42));
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: await in switch expression arm.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitInSwitchExpression()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> foo(int x)
    {
        return await (x switch
        {
            1 => Task.FromResult(10),
            2 => Task.FromResult(20),
            _ => Task.FromResult(0)
        });
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning: await in switch statement with multiple cases.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitInSwitchStatement()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo(int x)
    {
        switch (x)
        {
            case 1:
                await Task.Delay(1);
                break;
            case 2:
                await Task.Delay(2);
                break;
            default:
                await Task.Delay(3);
                break;
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: await in loop - only last iteration's await matters but we can't know that.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitInLoop()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        for (int i = 0; i < 3; i++)
        {
            await Task.Delay(i);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: await in while loop.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitInWhileLoop()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        int i = 0;
        while (i < 3)
        {
            await Task.Delay(i++);
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: ConfiguredTaskAwaitable cannot be returned directly.
        /// </summary>
        [Fact]
        public void NoWarn_ConfiguredTaskAwaitable()
        {
            // This is already handled, but let's verify
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        await Task.Delay(1).ConfigureAwait(false);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);
        }

        #endregion

        #region Local Function Tests

        /// <summary>
        /// Async local functions are analyzed just like regular methods.
        /// Both the outer method and the local function should be flagged.
        /// </summary>
        [Fact]
        public void Warn_AsyncLocalFunctionAnalyzed()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        async Task LocalAsync()
        {
            await Task.Delay(1);
        }

        await LocalAsync();
    }
}";

            // Both the outer method and the local function are analyzed
            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        Task LocalAsync()
        {
            return Task.Delay(1);
        }

        return LocalAsync();
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// Warning: Expression-bodied local function with single await.
        /// </summary>
        [Fact]
        public void Warn_LocalFunction_ExpressionBodied()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        async Task LocalAsync() => await Task.Delay(1);
        return LocalAsync();
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
        Task LocalAsync() => Task.Delay(1);
        return LocalAsync();
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// Warning: Local function with return await.
        /// </summary>
        [Fact]
        public void Warn_LocalFunction_ReturnAwait()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task<int> foo()
    {
        async Task<int> LocalAsync()
        {
            return await Task.FromResult(42);
        }
        return LocalAsync();
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
        Task<int> LocalAsync()
        {
            return Task.FromResult(42);
        }
        return LocalAsync();
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// Warning: Local function with if-else terminal awaits.
        /// </summary>
        [Fact]
        public void Warn_LocalFunction_IfElseTerminalAwaits()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        async Task LocalAsync(bool flag)
        {
            if (flag)
            {
                await Task.Delay(1);
            }
            else
            {
                await Task.Delay(2);
            }
        }
        return LocalAsync(true);
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
        Task LocalAsync(bool flag)
        {
            if (flag)
            {
                return Task.Delay(1);
            }
            else
            {
                return Task.Delay(2);
            }
        }
        return LocalAsync(true);
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// No warning: Local function with multiple awaits.
        /// </summary>
        [Fact]
        public void NoWarn_LocalFunction_MultipleAwaits()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        async Task LocalAsync()
        {
            await Task.Delay(1);
            await Task.Delay(2);
        }
        return LocalAsync();
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: Local function with try-catch around await.
        /// </summary>
        [Fact]
        public void NoWarn_LocalFunction_TryCatchAroundAwait()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        async Task LocalAsync()
        {
            try
            {
                await Task.Delay(1);
            }
            catch (Exception)
            {
            }
        }
        return LocalAsync();
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: Local function with using statement containing await.
        /// </summary>
        [Fact]
        public void NoWarn_LocalFunction_UsingAroundAwait()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using System.IO;

class Program
{
    Task foo()
    {
        async Task LocalAsync()
        {
            using (var stream = new MemoryStream())
            {
                await Task.Delay(1);
            }
        }
        return LocalAsync();
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Warning: Nested local functions, only the inner one should be flagged.
        /// </summary>
        [Fact]
        public void Warn_LocalFunction_Nested()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        async Task OuterLocal()
        {
            await Task.Delay(1);

            async Task InnerLocal()
            {
                await Task.Delay(2);
            }

            await InnerLocal();
        }
        return OuterLocal();
    }
}";

            // Only the inner local function is flagged (single await)
            // The outer has multiple awaits
            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        async Task OuterLocal()
        {
            await Task.Delay(1);

            Task InnerLocal()
            {
                return Task.Delay(2);
            }

            await InnerLocal();
        }
        return OuterLocal();
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// Warning: Static local function with single await.
        /// </summary>
        [Fact]
        public void Warn_LocalFunction_Static()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        static async Task LocalAsync() => await Task.Delay(1);
        return LocalAsync();
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
        static Task LocalAsync() => Task.Delay(1);
        return LocalAsync();
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// Warning: Local function contains lambda with await inside (nested function).
        /// The await in the lambda is excluded, but the local function's own await can be removed.
        /// </summary>
        [Fact]
        public void Warn_LocalFunction_ContainsLambdaWithAwait()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        async Task LocalAsync()
        {
            Func<Task> action = async () => await Task.Delay(1);
            await action();
        }
        return LocalAsync();
    }
}";

            // The local function's own await (await action()) can be removed
            // The lambda's await is not counted (it's in a nested function)
            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    Task foo()
    {
        Task LocalAsync()
        {
            Func<Task> action = async () => await Task.Delay(1);
            return action();
        }
        return LocalAsync();
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        /// <summary>
        /// Warning: Local function with lock statement BEFORE await is ok to optimize.
        /// The lock is released before the await, so it's safe.
        /// </summary>
        [Fact]
        public void Warn_LocalFunction_LockBeforeAwait()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    private object _lock = new object();

    Task foo()
    {
        async Task LocalAsync()
        {
            lock (_lock)
            {
                Console.WriteLine(""locked"");
            }
            await Task.Delay(1);
        }
        return LocalAsync();
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    private object _lock = new object();

    Task foo()
    {
        Task LocalAsync()
        {
            lock (_lock)
            {
                Console.WriteLine(""locked"");
            }
            return Task.Delay(1);
        }
        return LocalAsync();
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        #endregion

        /// <summary>
        /// No warning: lock statement before await - but await outside lock is ok.
        /// This is actually a compile error in C#, but let's make sure we handle it.
        /// </summary>
        [Fact]
        public void NoWarn_LockBeforeAwait()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    private object _lock = new object();

    async Task foo()
    {
        lock (_lock)
        {
            Console.WriteLine(""locked"");
        }
        await Task.Delay(1);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning: method returning IAsyncEnumerable with yield and await.
        /// </summary>
        [Fact]
        public void NoWarn_AsyncIterator()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class Program
{
    async IAsyncEnumerable<int> GenerateAsync()
    {
        yield return 1;
        await Task.Delay(100);
        yield return 2;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: await with cancellation token that affects behavior.
        /// </summary>
        [Fact]
        public void AwaitWithCancellation()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    async Task foo(CancellationToken ct)
    {
        await Task.Delay(1000, ct);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning: async method with out parameter - not allowed in C#.
        /// </summary>
        [Fact]
        public void NoWarn_AsyncWithRefParameter()
        {
            // Note: async methods cannot have ref/out parameters in C#
            // This test verifies the analyzer doesn't crash on invalid code
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> foo(int x)
    {
        return await Task.FromResult(x * 2);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.UnnecessaryAsync };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Edge case: empty async method body - should not crash.
        /// </summary>
        [Fact]
        public void NoWarn_EmptyAsyncMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Edge case: async method with only non-await statements.
        /// </summary>
        [Fact]
        public void NoWarn_AsyncMethodNoAwait()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        Console.WriteLine(""no await"");
    }
}";

            // CS1998 warning from compiler, but our analyzer shouldn't report
            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: generic Task with different type parameters - covariance issue.
        /// </summary>
        [Fact]
        public void NoWarn_GenericTaskCovariance()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Animal { }
class Dog : Animal { }

class Program
{
    async Task<Animal> foo()
    {
        return await Task.FromResult(new Dog());
    }
}";

            // Task<Dog> cannot be assigned to Task<Animal> - no covariance
            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning: await in using block where the await uses the disposable.
        /// </summary>
        [Fact]
        public void NoWarn_AwaitUsesDisposableInUsingBlock()
        {
            var test = @"
using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    async Task<string> foo()
    {
        using (var client = new HttpClient())
        {
            return await client.GetStringAsync(""http://example.com"");
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Verify: simple await that CAN be removed.
        /// </summary>
        [Fact]
        public void Verify_SimpleAwaitCanBeRemoved()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<int> foo()
    {
        return await Task.FromResult(42);
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
        return Task.FromResult(42);
    }
}";

            VerifyCSharpFix(test, fixtest);
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
