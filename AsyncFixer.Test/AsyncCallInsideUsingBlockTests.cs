using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using AsyncFixer.AsyncCallInsideUsingBlock;
using Xunit;
using Verify = AsyncFixer.Test.Helpers.CSharpCodeFixVerifier<
    AsyncFixer.AsyncCallInsideUsingBlock.AsyncCallInsideUsingBlockAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using System.Threading.Tasks;

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
        public Task UsingStatementWarning()
        {
            var test = @"
using System;
using System.IO;

class Program
{
    static async void foo()
    {
        var newStream = new FileStream("""", FileMode.Create);

        using var stream3 = new FileStream("""", FileMode.Create);
        stream3.ReadAsync(null).GetAwaiter().GetResult();
        var res = stream3.ReadAsync(null).Result;
        newStream.CopyToAsync(stream3).Wait();
        await newStream.CopyToAsync(stream3);
        newStream.CopyToAsync(stream3);
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
                }
            }.RunAsync();
        }

        [Fact(Skip = "TODO for later as this requires dataflow analysis")]
        public void NoWarn_AwaitedLater()
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
            var task = stream.CopyToAsync(newStream);
            await task;
        }
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AsyncCallInsideUsingBlockAnalyzer();
        }
    }
}
