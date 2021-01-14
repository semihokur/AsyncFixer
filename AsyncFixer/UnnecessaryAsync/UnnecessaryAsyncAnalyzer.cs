using System.Collections.Immutable;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

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

            // If it is a test method, still do it!!! 
            if (node.IsAsync() && node.Body != null && !node.HasEventArgsParameter() && !node.HasObjectStateParameter() &&
                !node.IsTestMethod())
            {
                if (context.SemanticModel == null)
                {
                    return;
                }

                var controlFlow = context.SemanticModel.AnalyzeControlFlow(node.Body);
                if (controlFlow == null)
                {
                    return;
                }

                var returnStatements = controlFlow.ReturnStatements;
                if (returnStatements == null)
                {
                    return;
                }

                var numAwait = 0;
                if (returnStatements.Any())
                {
                    foreach (var temp in returnStatements)
                    {
                        var returnStatement = temp as ReturnStatementSyntax;
                        if (returnStatement == null)
                        {
                            return;
                        }

                        if (returnStatement.Expression == null ||
                            returnStatement.Expression.Kind() != SyntaxKind.AwaitExpression)
                        {
                            return;
                        }

                        if (HasUsingOrTryParent(returnStatement, node))
                        {
                            return;
                        }

                        var returnExpressionType = context.SemanticModel.GetTypeInfo(returnStatement.Expression);
                        if (returnExpressionType.Type != returnExpressionType.ConvertedType)
                        {
                            // Task does not support covariance: Task<int> cannot be converted to Task<object>.
                            return;
                        }

                        numAwait++;
                    }
                }
                else
                {
                    // if awaitExpression is the last statement's expression
                    var lastStatement = node.Body.Statements.LastOrDefault();
                    if (lastStatement == null)
                    {
                        return;
                    }

                    var exprStmt = lastStatement as ExpressionStatementSyntax;
                    if (exprStmt == null || exprStmt.Expression == null ||
                        exprStmt.Expression.Kind() != SyntaxKind.AwaitExpression)
                    {
                        return;
                    }

                    if (HasUsingOrTryParent(exprStmt, node))
                    {
                        return;
                    }

                    numAwait++;
                }

                var totalAwait = node.Body.DescendantNodes().OfType<AwaitExpressionSyntax>().Count();

                if (numAwait < totalAwait)
                {
                    return;
                }

                // Make sure that we do not give a warning about the await statement involving a disposable object.
                var localDeclarationStatements = node.Body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>();
                if (node.Body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Count() > 0)
                {
                    return;
                }

                var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
