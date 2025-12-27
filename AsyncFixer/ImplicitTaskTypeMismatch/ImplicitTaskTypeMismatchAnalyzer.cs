using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AsyncFixer.ImplicitTaskTypeMismatch
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ImplicitTaskTypeMismatchAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ImplicitTaskTypeMismatchTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ImplicitTaskTypeMismatchMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ImplicitTaskTypeMismatchDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticIds.ImplicitTaskTypeMismatch,
            Title,
            MessageFormat,
            Constants.DiagnosticCategory,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            customTags: WellKnownDiagnosticTags.Telemetry,
            helpLinkUri: Constants.RepositoryLink);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            // Analyze lambda expressions using syntax
            context.RegisterSyntaxNodeAction(AnalyzeSimpleLambda, SyntaxKind.SimpleLambdaExpression);
            context.RegisterSyntaxNodeAction(AnalyzeParenthesizedLambda, SyntaxKind.ParenthesizedLambdaExpression);
            context.RegisterSyntaxNodeAction(AnalyzeAnonymousMethod, SyntaxKind.AnonymousMethodExpression);
        }

        private void AnalyzeSimpleLambda(SyntaxNodeAnalysisContext context)
        {
            var lambda = (SimpleLambdaExpressionSyntax)context.Node;

            // Skip async lambdas - compiler handles this
            if (lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword))
            {
                return;
            }

            AnalyzeLambdaExpression(context, lambda, lambda.Body);
        }

        private void AnalyzeParenthesizedLambda(SyntaxNodeAnalysisContext context)
        {
            var lambda = (ParenthesizedLambdaExpressionSyntax)context.Node;

            // Skip async lambdas - compiler handles this
            if (lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword))
            {
                return;
            }

            AnalyzeLambdaExpression(context, lambda, lambda.Body);
        }

        private void AnalyzeAnonymousMethod(SyntaxNodeAnalysisContext context)
        {
            var anonymousMethod = (AnonymousMethodExpressionSyntax)context.Node;

            // Skip async anonymous methods
            if (anonymousMethod.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword))
            {
                return;
            }

            AnalyzeLambdaExpression(context, anonymousMethod, anonymousMethod.Body);
        }

        private void AnalyzeLambdaExpression(SyntaxNodeAnalysisContext context, SyntaxNode lambda, CSharpSyntaxNode body)
        {
            // Get the type info for the lambda
            var typeInfo = context.SemanticModel.GetTypeInfo(lambda, context.CancellationToken);

            // Get the converted type (delegate type)
            var delegateType = typeInfo.ConvertedType as INamedTypeSymbol;
            if (delegateType?.DelegateInvokeMethod == null)
            {
                return;
            }

            var expectedReturnType = delegateType.DelegateInvokeMethod.ReturnType;

            // Check if the expected return type is Task (non-generic)
            if (!IsNonGenericTask(expectedReturnType))
            {
                return;
            }

            // Find the actual return type from the lambda body
            ITypeSymbol actualReturnType = null;

            if (body is ExpressionSyntax expression)
            {
                // Expression-bodied lambda: () => Task.FromResult(true)
                var exprTypeInfo = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken);
                actualReturnType = exprTypeInfo.Type;
            }
            else if (body is BlockSyntax block)
            {
                // Block-bodied lambda: () => { return Task.FromResult(true); }
                var returnStatement = block.DescendantNodes()
                    .OfType<ReturnStatementSyntax>()
                    .FirstOrDefault(r => r.Expression != null);

                if (returnStatement?.Expression != null)
                {
                    var returnTypeInfo = context.SemanticModel.GetTypeInfo(returnStatement.Expression, context.CancellationToken);
                    actualReturnType = returnTypeInfo.Type;
                }
            }

            if (actualReturnType == null)
            {
                return;
            }

            // Check if actual return type is Task<T> (generic Task)
            if (!IsGenericTask(actualReturnType))
            {
                return;
            }

            // We have a mismatch: lambda returns Task<T> but delegate expects Task
            var diagnostic = Diagnostic.Create(Rule, lambda.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsNonGenericTask(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            // Check for Task or ValueTask (non-generic)
            if (type.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
            {
                if (type.Name == "Task" || type.Name == "ValueTask")
                {
                    // Must be non-generic
                    return type is INamedTypeSymbol namedType && !namedType.IsGenericType;
                }
            }

            return false;
        }

        private static bool IsGenericTask(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            // Check for Task<T> or ValueTask<T> (generic)
            if (type.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
            {
                if (type.Name == "Task" || type.Name == "ValueTask")
                {
                    // Must be generic
                    return type is INamedTypeSymbol namedType && namedType.IsGenericType;
                }
            }

            return false;
        }
    }
}
