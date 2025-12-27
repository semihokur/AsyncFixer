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

        [Fact]
        public void GenericTaskType()
        {
            var test = @"
using System.Threading.Tasks;

class Program
{
    async void main()
    {
        await Task.Factory.StartNew(() => foo());
    }

    Task<int> foo()
    {
        return Task.FromResult(3);
    }
}";

            var expected = new DiagnosticResult { Id = DiagnosticIds.NestedTaskToOuterTask };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning for tuple awaiter pattern like TaskTupleAwaiter (GitHub issue #31)
        /// </summary>
        [Fact]
        public void NoWarn_TupleAwaiterPattern()
        {
            // When using TaskTupleAwaiter, you can await tuples of tasks.
            // The awaited type is ValueTuple<Task<T1>, Task<T2>>, not Task<Task<T>>.
            // This should not trigger AsyncFixer05.
            var test = @"
using System;
using System.Threading.Tasks;

// Simulating TaskTupleAwaiter extension methods
public static class TupleAwaiterExtensions
{
    public static TupleAwaiter<T1, T2> GetAwaiter<T1, T2>(this (Task<T1>, Task<T2>) tasks)
    {
        return new TupleAwaiter<T1, T2>(tasks.Item1, tasks.Item2);
    }
}

public struct TupleAwaiter<T1, T2> : System.Runtime.CompilerServices.INotifyCompletion
{
    private readonly Task<T1> _task1;
    private readonly Task<T2> _task2;

    public TupleAwaiter(Task<T1> task1, Task<T2> task2)
    {
        _task1 = task1;
        _task2 = task2;
    }

    public bool IsCompleted => _task1.IsCompleted && _task2.IsCompleted;

    public (T1, T2) GetResult() => (_task1.Result, _task2.Result);

    public void OnCompleted(Action continuation) => Task.WhenAll(_task1, _task2).ContinueWith(_ => continuation());
}

class Program
{
    async Task TestMethod()
    {
        var task1 = Task.FromResult(1);
        var task2 = Task.FromResult(""hello"");
        var (a, b) = await (task1, task2);
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
