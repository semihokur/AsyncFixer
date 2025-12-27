using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using AsyncFixer.BlockingCallInsideAsync;
using Xunit;

namespace AsyncFixer.Test
{
    public class BlockingCallInsideAsyncTests : CodeFixVerifier
    {
        //No diagnostics expected to show up
        [Fact]
        public void NoWarn_BlockingCallInsideAsyncTest1()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        var t = Task.Run(() => 4);
        await t;
        var b = t.Result;
    }
    async Task foo2()
    {
        Thread.Sleep(10);
        boo(0);
        boo2(0);
    }
    public int boo(int a)
    {
        return a;
    }
    public async Task<int> booAsync(string a)
    {
        return 3;
    }
    public int boo2(int a)
    {
        return a;
    }
    [Obsolete("""")]
    public async Task<int> boo2Async(int a)
    {
        return a;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void BlockingCallInsideAsyncTest2()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        Thread.Sleep(1000);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.BlockingCallInsideAsync };
            VerifyCSharpDiagnostic(test, expected);

//            var fixtest = @"
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//class Program
//{
//    Thread t;
//    async Task foo()
//    {
//        await Task.Delay(1);
//        await Task.Delay(100);
//    }
//}";

//            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void BlockingCallInsideAsyncTest3()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        var t = Task.Run(() => 4);
        // comment
        var b = t.Result;
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.BlockingCallInsideAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        var t = Task.Run(() => 4);
        // comment
        var b = await t;
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void BlockingCallInsideAsyncTest4()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        StreamReader reader = null;
        var a = reader.ReadToEnd();
        var b = await GetRequestStreamAsync();
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.BlockingCallInsideAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        StreamReader reader = null;
        var a = await reader.ReadToEndAsync();
        var b = await GetRequestStreamAsync();
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void NoWarn_BlockingCallInsideAsyncTest5()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        // comment
        this.boo(0);
        // end comment
    }
    public int boo(int a)
    {
        return 3;
    }
    public Task<int> booAsync(int a)
    {
        return Task.Run(()=>4);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void BlockingCallInsideAsyncTest7()
        {
            var test = @"
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        Task<int> t = null;
        var r = t.Result.CompareTo(5);
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.BlockingCallInsideAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        Task<int> t = null;
        var r = (await t).CompareTo(5);
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void BlockingCallInsideAsyncTest9()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        Task.Run(()=>2).Wait();
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.BlockingCallInsideAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task foo()
    {
        await Task.Run(() => 2);
    }
}";

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void NoWarn_BlockingCallInsideAsyncTest10()
        {
            // fooAsync is the itself
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async Task<object> fooAsync(int i)
    {
        await Task.Delay(100);
        return await Task.Run(()=>foo(i));
    }

    object foo(int i)
    {
        return i;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_InsideQueries()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

class Program
{
    async Task<object> fooAsync(int i)
    {
        await Task.Delay(100);
        Task<int> t = null;
        var list = new List<string>();
        var result = from str in list
                        select new
                        {
                            a = t.Result
                        };
        return null;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning for fast MemoryStream operations
        /// </summary>
        [Fact]
        public void NoWarn_MemoryStream()
        { 
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    async Task<object> fooAsync(int i)
    {
        await Task.Delay(100);
        var ms = new MemoryStream();
        ms.Write(null, 0, 0);
        ms.Dispose();
        return null;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning for virtual DisposeAsync existence
        /// </summary>
        [Fact]
        public void NoWarn_Dispose()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    async Task<object> fooAsync(int i)
    {
        await Task.Delay(100);
        var ms = new MemoryStream();
        ms.Write(null, 0, 0);
        ms.Dispose();
        return null;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning for blocking calls inside synchronous local functions (GitHub issue #42)
        /// </summary>
        [Fact]
        public void NoWarn_SynchronousLocalFunction()
        {
            var test = @"
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

class Program
{
    private async Task Wrapper()
    {
        await Task.Run(cpuBoundJob);

        void cpuBoundJob()
        {
            using var memoryStream = new MemoryStream();
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true);
            gzipStream.Flush();
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning for blocking calls inside synchronous lambdas
        /// </summary>
        [Fact]
        public void NoWarn_SynchronousLambda()
        {
            var test = @"
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

class Program
{
    private async Task Wrapper()
    {
        await Task.Run(() =>
        {
            using var memoryStream = new MemoryStream();
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true);
            gzipStream.Flush();
        });
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning for Task.Result inside synchronous local functions
        /// </summary>
        [Fact]
        public void NoWarn_TaskResultInSynchronousLocalFunction()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    private async Task Wrapper()
    {
        await Task.Run(cpuBoundJob);

        void cpuBoundJob()
        {
            var t = Task.Run(() => 4);
            var result = t.Result;
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Still warn for blocking calls in async local functions
        /// </summary>
        [Fact]
        public void Warn_AsyncLocalFunction()
        {
            var test = @"
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    private async Task Wrapper()
    {
        await asyncLocalFunc();

        async Task asyncLocalFunc()
        {
            StreamReader reader = null;
            var a = reader.ReadToEnd();
        }
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.BlockingCallInsideAsync };
            VerifyCSharpDiagnostic(test, expected);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new BlockingCallInsideAsyncFixer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new BlockingCallInsideAsyncAnalyzer();
        }
    }
}
