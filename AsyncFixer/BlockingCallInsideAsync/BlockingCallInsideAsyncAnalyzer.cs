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
            Constants.DiagnosticCategory,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            customTags: WellKnownDiagnosticTags.Telemetry,
            helpLinkUri: Constants.RepositoryLink);

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

            // Do not travel nodes under QueryExpression nodes
            foreach (var node in method.DescendantNodes(n=> !(n is QueryExpressionSyntax)))
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

        /// <summary>
        /// Checks if the given node is inside an async context where 'await' can be used.
        /// Returns false if the node is inside a synchronous local function or lambda.
        /// </summary>
        private static bool IsInAsyncContext(SyntaxNode node)
        {
            var current = node.Parent;
            while (current != null)
            {
                // Check for local functions - async ones are valid contexts, sync ones are not
                if (current is LocalFunctionStatementSyntax localFunc)
                {
                    return localFunc.Modifiers.Any(SyntaxKind.AsyncKeyword);
                }

                // Check for lambda expressions - async ones are valid contexts, sync ones are not
                if (current is LambdaExpressionSyntax lambda)
                {
                    return lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
                }

                // Check for anonymous methods - async ones are valid contexts, sync ones are not
                if (current is AnonymousMethodExpressionSyntax anonymousMethod)
                {
                    return anonymousMethod.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword);
                }

                // Stop at the enclosing method declaration - we know it's async from the caller
                if (current is MethodDeclarationSyntax)
                {
                    return true;
                }

                current = current.Parent;
            }
            return true;
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, string enclosingMethodName)
        {
            // Skip if the invocation is not in an async context (e.g., inside a synchronous local function or lambda)
            if (!IsInAsyncContext(invocation))
            {
                return;
            }

            var invokeMethod = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (invokeMethod == null || invokeMethod.Name == "Invoke" || invokeMethod.Name == "Dispose")
            {
                // Do not suggest InvokeAsync which is common with old asynchrony patterns.
                // Do not also suggest DisposeAsync which is rarely needed as most of DisposeAsync implementations are not truly asynchronous.
                return;
            }

            var receiverExpr = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;
            var receiverType = receiverExpr != null ? context.SemanticModel.GetTypeInfo(receiverExpr).Type : null;

            var replacement = DetectSynchronousUsages(invocation.GetLocation(), invokeMethod.OriginalDefinition, receiverType, context.SemanticModel);
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
            // Skip if the member access is not in an async context (e.g., inside a synchronous local function or lambda)
            if (!IsInAsyncContext(memberAccess))
            {
                return;
            }

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

            // Check if the task is a foreach iteration variable from a collection that was awaited via Task.WhenAll
            if (IsTaskFromAwaitedWhenAll(context.SemanticModel, method, memberAccess.Expression))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), "await", name);
            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Checks if the given task expression is an iteration variable from a foreach loop
        /// where the collection was previously awaited via Task.WhenAll.
        /// </summary>
        private static bool IsTaskFromAwaitedWhenAll(SemanticModel semanticModel, MethodDeclarationSyntax method, ExpressionSyntax taskExpression)
        {
            // Get the symbol for the task expression
            var taskSymbol = semanticModel.GetSymbolInfo(taskExpression).Symbol as ILocalSymbol;
            if (taskSymbol == null)
            {
                return false;
            }

            // Check if this symbol is declared as a foreach iteration variable
            var declaringSyntax = taskSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (declaringSyntax == null)
            {
                return false;
            }

            // The declaring syntax for a foreach variable is the ForEachStatementSyntax itself
            var foreachStatement = declaringSyntax.FirstAncestorOrSelf<ForEachStatementSyntax>();
            if (foreachStatement == null)
            {
                return false;
            }

            // Get the collection being iterated
            var collectionExpression = foreachStatement.Expression;
            var collectionSymbol = semanticModel.GetSymbolInfo(collectionExpression).Symbol;
            if (collectionSymbol == null)
            {
                return false;
            }

            // Now check if this collection was passed to an awaited Task.WhenAll before the foreach
            return IsCollectionAwaitedViaWhenAll(semanticModel, method, foreachStatement, collectionSymbol);
        }

        /// <summary>
        /// Checks if the given collection symbol was passed to Task.WhenAll and awaited
        /// before the specified foreach statement.
        /// </summary>
        private static bool IsCollectionAwaitedViaWhenAll(SemanticModel semanticModel, MethodDeclarationSyntax method, ForEachStatementSyntax foreachStatement, ISymbol collectionSymbol)
        {
            // Find all await expressions in the method that occur before the foreach
            var foreachSpanStart = foreachStatement.SpanStart;

            foreach (var awaitExpr in method.DescendantNodes().OfType<AwaitExpressionSyntax>())
            {
                // Only consider awaits that occur before the foreach
                if (awaitExpr.SpanStart >= foreachSpanStart)
                {
                    continue;
                }

                // Check if this await is on a Task.WhenAll invocation
                var awaitedExpression = Helpers.RemoveConfigureAwait(awaitExpr.Expression);

                if (!(awaitedExpression is InvocationExpressionSyntax invocation))
                {
                    continue;
                }

                var invokedMethod = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (invokedMethod?.ContainingType?.Name != "Task" || invokedMethod.Name != "WhenAll")
                {
                    continue;
                }

                // Check if our collection is passed as an argument to WhenAll
                foreach (var argument in invocation.ArgumentList.Arguments)
                {
                    var argumentSymbol = semanticModel.GetSymbolInfo(argument.Expression).Symbol;
                    if (SymbolEqualityComparer.Default.Equals(argumentSymbol, collectionSymbol))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string DetectSynchronousUsages(Location location, IMethodSymbol methodCallSymbol, ITypeSymbol receiverTypeCandidate, SemanticModel semanticModel)
        {
            if (methodCallSymbol.ContainingType == null)
            {
                return null;
            }

            // If we do not succeed retrieving the exact type of the receiver, proceed with the containing type of the method symbol.
            var receiverType = receiverTypeCandidate ?? methodCallSymbol.ContainingType;

            if (!methodCallSymbol.ContainingAssembly.ToDisplayString().StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            {
                // Only look at the symbols from the System assemblies.
                return null;
            }

            var methodName = methodCallSymbol.Name;

            var typeName = methodCallSymbol.ContainingType.Name;

            if (receiverType.Name == "MemoryStream")
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

            var list = semanticModel.LookupSymbols(location.SourceSpan.Start, receiverType,
                includeReducedExtensionMethods: true, name: methodName + "Async");

            return
                list.OfType<IMethodSymbol>()
                    .Where(a => !a.IsVirtual && !a.IsAbstract)
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
