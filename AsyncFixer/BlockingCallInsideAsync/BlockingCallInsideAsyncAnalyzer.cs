using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AsyncFixer.BlockingCallInsideAsync
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class BlockingCallInsideAsyncAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.BlockingCallInsideAsyncTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.BlockingCallInsideAsyncMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.BlockingCallInsideAsyncDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticIds.BlockingCallInsideAsync,
            Title,
            MessageFormat,
            DiagnosticIds.Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            customTags: WellKnownDiagnosticTags.Telemetry);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var method = (MethodDeclarationSyntax)context.Node;
            string methodName = method.Identifier.ValueText;

            if (!method.IsAsync())
            {
                return;
            }

            foreach (var node in method.DescendantNodes())
            {
                var kind = node.Kind();
                if (kind == SyntaxKind.SimpleMemberAccessExpression)
                {
                    var memberAccess = (MemberAccessExpressionSyntax)node;
                    // To detect 't.Result', we need to analyze SimpleMemberAccess expressions.
                    AnalyzeMemberAccess(context, method, memberAccess);
                }
                else if (kind == SyntaxKind.InvocationExpression)
                {
                    var invocation = (InvocationExpressionSyntax)node;
                    // To detect 't.Wait() or ReadToEnd()', we need to analyze Invocation expressions.
                    AnalyzeInvocation(context, invocation, methodName);
                }
            }
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, string enclosingMethodName)
        {

            var invokeMethod = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (invokeMethod == null || invokeMethod.Name == "Invoke")
            {
                return;
            }

            var replacement = DetectSynchronousUsages(invocation.GetLocation(), invokeMethod.OriginalDefinition, context.SemanticModel);
            if (replacement == null)
            {
                return;
            }

            // Do not accept Thread.Sleep(x), x < 50
            if (replacement == "Task.Delay")
            {
                var argument = invocation.ArgumentList?.Arguments.FirstOrDefault();
                int delay;
                if (argument != null && int.TryParse(argument.ToString(), out delay) && delay < 50)
                {
                    return;
                }
            }

            // Do not accept the enclosing method name as a replacement.
            if (replacement.Equals(enclosingMethodName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), replacement, invocation.Expression.ToString());
            context.ReportDiagnostic(diagnostic);
        }

        private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method, MemberAccessExpressionSyntax memberAccess)
        {

            var property = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol as IPropertySymbol;
            if (property?.OriginalDefinition.ContainingType == null ||
                property.OriginalDefinition.ContainingType.Name != "Task" ||
                property.OriginalDefinition.Name != "Result")
            {
                return;
            }

            // Check whether that task is awaited before!
            var name = memberAccess.Expression.ToString();
            if (method.DescendantNodes().OfType<AwaitExpressionSyntax>().Any(awaitExpr =>
                awaitExpr.DescendantNodes().OfType<IdentifierNameSyntax>().Any(identifier =>
                    identifier.ToString() == name)))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), "await", name);
            context.ReportDiagnostic(diagnostic);
        }

        private static string DetectSynchronousUsages(Location location, IMethodSymbol methodCallSymbol, SemanticModel semanticModel)
        {
            var methodName = methodCallSymbol.Name;

            var typeName = methodCallSymbol.ContainingType.Name;

            if (methodCallSymbol.ContainingType == null)
            {
                return null;
            }

            if (typeName == "MemoryStream")
            {
                // Do not replace the synchronous ones under MemoryStream.
                return null;
            }

            // Thread.Sleep => Symbol name of the class is RuntimeThread for netcore and Thread for full framework. 
            if (typeName.EndsWith("Thread") && methodName == "Sleep")
            {
                return "Task.Delay";
            }
            // the number of parameters should be zero
            if (typeName == "Task" && methodName == "Wait")
            {
                return "await";
            }
            if (typeName == "Task" && methodName == "WaitAll")
            {
                return "Task.WhenAll";
            }
            if (typeName == "Task" && methodName == "WaitAny")
            {
                return "Task.WhenAny";
            }

            var list = semanticModel.LookupSymbols(location.SourceSpan.Start, methodCallSymbol.ContainingType,
                includeReducedExtensionMethods: true, name: methodName + "Async");
            return
                list.OfType<IMethodSymbol>()
                    .Where(m => IsSignatureCompatible(methodCallSymbol, m))
                    .Select(m => m.Name)
                    .FirstOrDefault();
        }

        private static bool IsSignatureCompatible(IMethodSymbol old, IMethodSymbol replacement)
        {
            if (old.TypeParameters.Length != replacement.TypeParameters.Length)
            {
                return false;
            }

            var oldParams = old.Parameters;
            var newParams = replacement.Parameters;
            if (oldParams.Length > newParams.Length)
            {
                return false;
            }

            for (var i = 0; i < oldParams.Length; i++)
            {
                if (!Equals(newParams[i].Type, oldParams[i].Type))
                {
                    return false;
                }
            }

            for (var i = oldParams.Length; i < newParams.Length; i++)
            {
                if (!newParams[i].IsOptional)
                {
                    return false;
                }
            }

            return
                !replacement.GetAttributes().Any(a => a.AttributeClass.ToDisplayString() == "System.ObsoleteAttribute");
        }
    }
}
