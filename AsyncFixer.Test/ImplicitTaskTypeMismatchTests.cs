using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using AsyncFixer.ImplicitTaskTypeMismatch;
using Xunit;

namespace AsyncFixer.Test
{
    public class ImplicitTaskTypeMismatchTests : CodeFixVerifier
    {
        /// <summary>
        /// No warning when lambda returns Task and delegate expects Task
        /// </summary>
        [Fact]
        public void NoWarn_MatchingTaskType()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task FooAsync();

class Program
{
    void Call(FooAsync action) { }

    void Test()
    {
        Call(() => Task.CompletedTask);
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning when lambda returns Task<T> and delegate expects Task<T>
        /// </summary>
        [Fact]
        public void NoWarn_MatchingGenericTaskType()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task<bool> FooAsync();

class Program
{
    void Call(FooAsync action) { }

    void Test()
    {
        Call(() => Task.FromResult(true));
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning for async lambdas - compiler handles this (CS8031)
        /// </summary>
        [Fact]
        public void NoWarn_AsyncLambda()
        {
            // This would be caught by the compiler with CS8031
            // so we don't need to flag it
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task FooAsync();

class Program
{
    void Call(FooAsync action) { }

    void Test()
    {
        Call(async () => { await Task.Yield(); });
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning for void delegates
        /// </summary>
        [Fact]
        public void NoWarn_VoidDelegate()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    void Call(Action action) { }

    void Test()
    {
        Call(() => { });
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Warn when lambda returns Task<T> but delegate expects Task (GitHub issue #26)
        /// </summary>
        [Fact]
        public void Warn_TaskGenericToTask()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task FooAsync();

class Program
{
    void Call(FooAsync action) { }

    void Test()
    {
        Call(() => { return Task.FromResult(true); });
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.ImplicitTaskTypeMismatch };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Warn when lambda expression body returns Task<T> but delegate expects Task
        /// </summary>
        [Fact]
        public void Warn_ExpressionBody_TaskGenericToTask()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task FooAsync();

class Program
{
    void Call(FooAsync action) { }

    void Test()
    {
        Call(() => Task.FromResult(true));
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.ImplicitTaskTypeMismatch };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Warn when using Func<Task> with Task<T> return
        /// </summary>
        [Fact]
        public void Warn_FuncTask_TaskGenericReturn()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    void Call(Func<Task> action) { }

    void Test()
    {
        Call(() => Task.FromResult(42));
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.ImplicitTaskTypeMismatch };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning when using Func<Task<int>> with Task<int> return
        /// </summary>
        [Fact]
        public void NoWarn_FuncTaskGeneric_MatchingReturn()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Program
{
    void Call(Func<Task<int>> action) { }

    void Test()
    {
        Call(() => Task.FromResult(42));
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Warn with anonymous method syntax
        /// </summary>
        [Fact]
        public void Warn_AnonymousMethod()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task FooAsync();

class Program
{
    void Call(FooAsync action) { }

    void Test()
    {
        Call(delegate { return Task.FromResult(true); });
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.ImplicitTaskTypeMismatch };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Warn when lambda with parameter returns Task<T> but delegate expects Task
        /// </summary>
        [Fact]
        public void Warn_LambdaWithParameter()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task FooAsync(int x);

class Program
{
    void Call(FooAsync action) { }

    void Test()
    {
        Call(x => Task.FromResult(x > 0));
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.ImplicitTaskTypeMismatch };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// Warn when parenthesized lambda returns Task<T> but delegate expects Task
        /// </summary>
        [Fact]
        public void Warn_ParenthesizedLambda()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task FooAsync(int x, int y);

class Program
{
    void Call(FooAsync action) { }

    void Test()
    {
        Call((x, y) => Task.FromResult(x + y));
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.ImplicitTaskTypeMismatch };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning when returning a method that returns Task (not Task<T>)
        /// </summary>
        [Fact]
        public void NoWarn_MethodReturningTask()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task FooAsync();

class Program
{
    void Call(FooAsync action) { }

    Task DoSomething() => Task.CompletedTask;

    void Test()
    {
        Call(() => DoSomething());
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// Warn when returning a method call that returns Task<T>
        /// </summary>
        [Fact]
        public void Warn_MethodReturningGenericTask()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate Task FooAsync();

class Program
{
    void Call(FooAsync action) { }

    Task<bool> GetBoolAsync() => Task.FromResult(true);

    void Test()
    {
        Call(() => GetBoolAsync());
    }
}";
            var expected = new DiagnosticResult { Id = DiagnosticIds.ImplicitTaskTypeMismatch };
            VerifyCSharpDiagnostic(test, expected);
        }

        /// <summary>
        /// No warning when ValueTask is returned and ValueTask is expected
        /// </summary>
        [Fact]
        public void NoWarn_ValueTaskMatching()
        {
            var test = @"
using System;
using System.Threading.Tasks;

delegate ValueTask FooAsync();

class Program
{
    void Call(FooAsync action) { }

    void Test()
    {
        Call(() => new ValueTask());
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning for non-task return types
        /// </summary>
        [Fact]
        public void NoWarn_NonTaskReturnType()
        {
            var test = @"
using System;

delegate object FooFunc();

class Program
{
    void Call(FooFunc action) { }

    void Test()
    {
        Call(() => ""hello"");
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning when lambda is passed to Assert.ThrowsAsync - result is intentionally discarded
        /// </summary>
        [Fact]
        public void NoWarn_AssertThrowsAsync()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Assert
{
    public static Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception => throw null;
}

class Program
{
    Task<int> MethodThatThrows() => throw new InvalidOperationException();

    async Task Test()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => MethodThatThrows());
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        /// <summary>
        /// No warning when lambda is passed to Assert.ThrowsExceptionAsync - result is intentionally discarded
        /// </summary>
        [Fact]
        public void NoWarn_AssertThrowsExceptionAsync()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Assert
{
    public static Task<T> ThrowsExceptionAsync<T>(Func<Task> action) where T : Exception => throw null;
}

class Program
{
    Task<int> MethodThatThrows() => throw new InvalidOperationException();

    async Task Test()
    {
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => MethodThatThrows());
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ImplicitTaskTypeMismatchAnalyzer();
        }
    }
}
