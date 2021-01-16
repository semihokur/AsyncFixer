using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using AsyncFixer.AsyncVoid;
using AsyncFixer.AsyncCallInsideUsingBlock;
using AsyncFixer.NestedTaskToOuterTask;
using Xunit;

namespace AsyncFixer.Test
{
    public class NestedTaskToOuterTaskTests : CodeFixVerifier
    {
        [Fact]
        public void NoWarn_NestedTaskToOuterTaskTest1()
        {
            // nested task can be unwrapped and awaited.
            // For this scenario, overloads of Task.Run are provided
            // to accept async functions and automatically unwrap the nested task
            var test = @"
using System.Threading.Tasks;

class asd
{
    async void main()
    {
        Task task = Task.Run(() => foo());
        await Task.WhenAny(task);
        task = Task.Run(() => Task.Run(()=>3));
    }
    Task foo()
    {
        return Task.Delay(1);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void NestedTaskToOuterTaskTest2()
        {
            var test = @"
using System.Threading.Tasks;

class Program
{
    void main()
    {
        Task task = Task.Factory.StartNew(() => foo());
    }
    Task foo()
    {
        return Task.Delay(1);
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.NestedTaskToOuterTask };
            VerifyCSharpDiagnostic(test, expected);
        }

        [Fact]
        public void NestedTaskToOuterTaskTest3()
        {
            var test = @"
using System.Threading.Tasks;

class Program
{
    async void main()
    {
        await Task.Factory.StartNew(async () => await fooAsync());
    }
    Task fooAsync()
    {
        return Task.Delay(1);
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.NestedTaskToOuterTask };
            VerifyCSharpDiagnostic(test, expected);
        }

        [Fact]
        public void NoWarn_TaskCancelledException()
        {
            var test = @"
using System.Threading.Tasks;

class Program
{
    async void main()
    {
        await Task.Factory.StartNew(() => foo());
    }

    TaskCanceledException foo()
    {
        return new TaskCanceledException();
    }
}";
            VerifyCSharpDiagnostic(test);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new NestedTaskToOuterTaskAnalyzer();
        }
    }
}
