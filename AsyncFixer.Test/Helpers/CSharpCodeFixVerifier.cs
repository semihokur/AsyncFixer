using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;

namespace AsyncFixer.Test.Helpers
{
    internal static class AdditionalMetadataReferences
    {
        public static ReferenceAssemblies ReferenceAssemblies { get; } = ReferenceAssemblies.Default
            .AddPackages(ImmutableArray.Create(
                new PackageIdentity("System.Collections.Immutable", "1.7.0"),
                new PackageIdentity("System.Threading.Tasks.Extensions", "4.5.4")));
    }

    public static partial class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
           where TAnalyzer : DiagnosticAnalyzer, new()
           where TCodeFix : CodeFixProvider, new()
    {
        public static DiagnosticResult Diagnostic()
            => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, XUnitVerifier>.Diagnostic();

        public static DiagnosticResult Diagnostic(string diagnosticId)
            => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, XUnitVerifier>.Diagnostic(diagnosticId);

        public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
            => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, XUnitVerifier>.Diagnostic(descriptor);

        public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new Test
            {
                TestCode = source,
                LanguageVersion = LanguageVersion.Latest
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        public static Task VerifyCodeFixAsync(string source, string fixedSource)
            => VerifyCodeFixAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);

        public static Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
            => VerifyCodeFixAsync(source, new[] { expected }, fixedSource);

        public static async Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource)
        {
            var test = new Test
            {
                TestCode = source,
                FixedCode = fixedSource,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }

        public static Task VerifyAsync(string code)
        {
            return new Test
            {
                TestState = { Sources = { code } }

            }.RunAsync();
        }

        public class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, XUnitVerifier>
        {
            public Test()
            {
                ReferenceAssemblies = AdditionalMetadataReferences.ReferenceAssemblies;

                SolutionTransforms.Add((solution, projectId) =>
                {
                    var project = solution.GetProject(projectId);
                    var parseOptions = (CSharpParseOptions?)project?.ParseOptions;
                    if (parseOptions == null)
                    {
                        return solution;
                    }

                    solution = solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion));

                    return solution;
                });
            }

            public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.CSharp8;
        }
    }
}
