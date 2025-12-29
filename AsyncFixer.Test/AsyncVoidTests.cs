using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using Xunit;
using AsyncFixer.AsyncVoid;
using Verify = AsyncFixer.Test.Helpers.CSharpCodeFixVerifier<
    AsyncFixer.AsyncVoid.AsyncVoidAnalyzer,
    AsyncFixer.AsyncVoid.AsyncVoidFixer>;

namespace AsyncFixer.Test
{
    public class AsyncVoidTests : CodeFixVerifier
    {
        [Fact]
        public void NoWarn_AsyncVoidMethodTest1()
        {
            //No diagnostics expected to show up

            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    static async Task<bool> foo()
    {
        await Task.Delay(1);
        return true;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void AsyncVoidMethodTest2()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    private static async void foo()
    {
        await Task.Delay(1);
        await Task.Delay(1);
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncVoid };
            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
using System;
using System.Threading.Tasks;

class Program
{
    private static async Task foo()
    {
        await Task.Delay(1);
        await Task.Delay(1);
    }
}";
            VerifyCSharpFix(test, fixtest);
        }

        [Fact]
        public void AsyncVoidAnonymousFunction()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    private static void foo()
    {
        bar(async i => await Task.FromResult(i));
    }

    private static void bar(Action<int> action)
    {
        action(3);
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncVoid };
            VerifyCSharpDiagnostic(test, expected);

            // No fix should be suggested.
            VerifyCSharpFix(test, test);
        }

        [Fact]
        public void AsyncVoidAnonymousFunctionWithIntermediateMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    public static void Do()
    {
        Process(async input => await ProcessAsync());
    }

    private static void Process(Action<int> action)
    {
        action(12);
    }

    private static Task ProcessAsync()
    {
        return Task.CompletedTask;
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.AsyncVoid };
            VerifyCSharpDiagnostic(test, expected);

            // No fix should be suggested.
            VerifyCSharpFix(test, test);
        }

        [Fact]
        public Task NoWarn_EventHandler()
        {
            //No diagnostics expected to show up

            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    public void foo()
    {
        AppDomain.CurrentDomain.ProcessExit += async (a, b) =>
        {
            await Task.Delay(100);
        };
    }
}";

            return Verify.VerifyAsync(test);
        }

        [Fact]
        public void NoWarn_EventArgsParameter()
        {
            // No diagnostics expected - method has EventArgs parameter
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    async void OnEvent(object sender, EventArgs e)
    {
        await Task.Delay(100);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_GenericEventArgsParameter()
        {
            // No diagnostics expected - method has generic EventArgs parameter (issue #39)
            var test = @"
using System;
using System.Threading.Tasks;

class CurrentItemChangedEventArgs<T> : EventArgs
{
    public T CurrentItem { get; set; }
}

class Program
{
    async void OnCurrentItemChanged(object sender, CurrentItemChangedEventArgs<string> e)
    {
        await Task.Delay(100);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NoWarn_DerivedEventArgsParameter()
        {
            // No diagnostics expected - method has derived EventArgs parameter
            var test = @"
using System;
using System.Threading.Tasks;

class MyEventArgs : EventArgs
{
    public string Message { get; set; }
}

class Program
{
    async void OnMyEvent(object sender, MyEventArgs e)
    {
        await Task.Delay(100);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public Task NoWarn_ActionEventWithAsyncLambda_Issue16()
        {
            // No diagnostics expected - async lambda used as event handler
            // Even though the event uses Action delegate instead of EventHandler,
            // it's still an event subscription pattern (issue #16)
            var test = @"
using System;
using System.Threading.Tasks;

class ComponentBase { }

class AppState
{
    public event Action<ComponentBase, string> StateChanged;
}

class Program : ComponentBase
{
    private AppState _appState = new AppState();

    protected void OnInitialized()
    {
        _appState.StateChanged += async (Source, Property) =>
            await AppState_StateChanged(Source, Property);
    }

    private Task AppState_StateChanged(ComponentBase source, string property)
    {
        return Task.CompletedTask;
    }
}";

            return Verify.VerifyAsync(test);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new AsyncVoidFixer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AsyncVoidAnalyzer();
        }
    }
}
