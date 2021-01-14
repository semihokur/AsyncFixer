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
        public void AsyncCallInsideUsingBlockTest1()
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
        public void AsyncCallInsideUsingBlockTest4()
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
        public void AsyncCallInsideUsingBlockTest5()
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
        public void UsingStatementNoWarning()
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

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AsyncCallInsideUsingBlockAnalyzer();
        }
    }
}
