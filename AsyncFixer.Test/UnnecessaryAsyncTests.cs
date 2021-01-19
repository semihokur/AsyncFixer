using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using AsyncFixer.UnnecessaryAsync;
using Xunit;

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
        [Fact(Skip ="Remove awaits")]
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
