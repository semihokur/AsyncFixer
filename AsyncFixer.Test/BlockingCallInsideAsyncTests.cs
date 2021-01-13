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
        public void BlockingCallInsideAsyncTest1()
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
        public void BlockingCallInsideAsyncTest5()
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

            var expected = new DiagnosticResult { Id = DiagnosticIds.BlockingCallInsideAsync };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{   
    async Task foo()
    {
        // comment
        await this.booAsync(0);
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

            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void BlockingCallInsideAsyncTest6()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{   
    async Task foo()
    {
        // comment
        boo(0);
    }
    public int boo(int a)
    {
        return 3;
    }
    public async Task<int> booAsync(int a)
    {
        return a;
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
        // comment
        await booAsync(0);
    }
    public int boo(int a)
    {
        return 3;
    }
    public async Task<int> booAsync(int a)
    {
        return a;
    }
}";

            VerifyCSharpFix(test, fixtest);
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
        public void BlockingCallInsideAsyncTest8()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{   
    async Task foo()
    {
        boo(0);
    }
    int boo(int a)
    {
        return a;
    }
    async Task<int> booAsync(int a)
    {
        return 3;
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
        await booAsync(0);
    }
    int boo(int a)
    {
        return a;
    }
    async Task<int> booAsync(int a)
    {
        return 3;
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
        public void BlockingCallInsideAsyncTest10()
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
        public void BlockingCallInsideAsyncTest11()
        {
            // fooAsync is the itself
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
        return null;
    }
}";

            VerifyCSharpDiagnostic(test);
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
