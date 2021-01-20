using System.Collections.Immutable;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AsyncFixer.UnnecessaryAsync
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnnecessaryAsyncAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.UnnecessaryAsyncTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.UnnecessaryAsyncMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.UnnecessaryAsyncDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticIds.UnnecessaryAsync,
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

        private bool HasUsingOrTryParent(SyntaxNode node, SyntaxNode root)
        {
            if (node == root) return false;
            var parent = node.Parent;
            var kind = parent.Kind();

            if (kind == SyntaxKind.TryStatement || kind == SyntaxKind.UsingStatement)
            {
                return true;
            }

            return HasUsingOrTryParent(parent, root);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (MethodDeclarationSyntax)context.Node;
            var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node.Identifier.Text);

            if (!node.IsAsync() || node.HasEventArgsParameter() || node.HasObjectStateParameter() || node.IsTestMethod())
            {
                return;
            }

            // Retrieve all await expressions excluding the ones under lambda functions.
            var awaitExpressions = node.DescendantNodes().OfType<AwaitExpressionSyntax>().Where(a => a.FirstAncestorOrSelfUnderGivenNode<LambdaExpressionSyntax>(node) == null).ToList();

            if (node.Body == null && 
                node.ExpressionBody?.Expression.Kind() == SyntaxKind.AwaitExpression)
            {
                // Expression-bodied members 
                // e.g. public static async Task Foo() => await Task.FromResult(true);

                var returnExpressionType = context.SemanticModel.GetTypeInfo(node.ExpressionBody?.Expression);
                if (returnExpressionType.IsImplicitTypeCasting())
                {
                    // Task does not support covariance: Task<int> cannot be converted to Task<object>.
                    // That's why we cannot remove async/await keywords.
                    return;
                }

                if (awaitExpressions.Count() > 1)
                {
                    // If there is more than await expressions, we cannot safely remove async/await keywords
                    return;
                }

                context.ReportDiagnostic(diagnostic);
                return;
            }


            if (node.Body == null || context.SemanticModel == null)
            {
                return;
            }

            var controlFlow = context.SemanticModel.AnalyzeControlFlow(node.Body);
            var returnStatements = controlFlow?.ReturnStatements ?? ImmutableArray<SyntaxNode>.Empty;

            var numAwait = 0;
            if (returnStatements.Any())
            {
                foreach (var temp in returnStatements)
                {
                    var returnStatement = temp as ReturnStatementSyntax;
                    if (returnStatement?.Expression?.Kind() != SyntaxKind.AwaitExpression)
                    {
                        return;
                    }

                    if (HasUsingOrTryParent(returnStatement, node))
                    {
                        return;
                    }

                    var returnExpressionType = context.SemanticModel.GetTypeInfo(returnStatement.Expression);
                    if (returnExpressionType.IsImplicitTypeCasting())
                    {
                        // Task does not support covariance.
                        return;
                    }

                    numAwait++;
                }
            }
            else
            {
                // if awaitExpression is the last statement's expression
                var lastStatement = node.Body.Statements.LastOrDefault();
                if ((lastStatement as ExpressionStatementSyntax)?.Expression?.Kind() != SyntaxKind.AwaitExpression)
                {
                    return;
                }

                if (HasUsingOrTryParent(lastStatement, node))
                {
                    return;
                }

                numAwait++;
            }

            if (numAwait < awaitExpressions.Count())
            {
                return;
            }

            // Make sure that we do not give a warning about the await statement involving a disposable object.

            // Retrieve the disposable object identifiers from the using statements. 
            // For instance, for the following statement, we'd like to return 'source'.
            //      using FileStream source = File.Open("data", FileMode.Open);
            var disposableObjectNames = node.Body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>()
                .Where(a => a.UsingKeyword.Kind() != SyntaxKind.None)
                .SelectMany(a => a.DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(b => b.Identifier.ValueText));
            if (disposableObjectNames.Any())
            {
                // There are disposable objects.
                // Let's check whether at least one await expression uses one of those disposable objects.
                if (awaitExpressions.SelectMany(a => a.DescendantNodes().OfType<IdentifierNameSyntax>())
                    .Any(a => disposableObjectNames.Contains(a.Identifier.ValueText)))
                {
                    return;
                }
            }

            context.ReportDiagnostic(diagnostic);
        }
    }
}
