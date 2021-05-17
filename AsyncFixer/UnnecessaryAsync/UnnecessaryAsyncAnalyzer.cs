using System.Collections.Immutable;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System;

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

        private bool IsInUsingOrTryScope(SyntaxNode node, SyntaxNode root)
        {
            if (node == root) return false;
            var parent = node.Parent;
            var kind = parent.Kind();

            if (kind == SyntaxKind.TryStatement || kind == SyntaxKind.UsingStatement)
            {
                return true;
            }

            // Check sibling nodes for using declarations which will be in scope
            // Example: 
            //  public async Task Foo() 
            //  {
            //      using var x = new Disposable();   <-- We want to detect this statement
            //      await Task.Delay(1);
            //  }
            foreach (var sibling in parent.ChildNodes())
            {
                if (sibling == node)
                {
                    // Reached the statement we are considering
                    // No need to keep going
                    break;
                }

                if (sibling is LocalDeclarationStatementSyntax declaration)
                {
                    if (declaration.UsingKeyword.Kind() == SyntaxKind.UsingKeyword)
                    {
                        // Variable declaration with using,
                        // which will be in scope in the await statement (as in the above example)
                        return true;
                    }
                }

            }
            
            return IsInUsingOrTryScope(parent, root);
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

                    if (!IsSafeToRemoveAsyncAwait(returnStatement))
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
                // using (var stream = new MemoryStream())
                // {
                //    await Task.FromResult(3); ==> this is NOT the last statement because of the using block.
                // }

                var lastStatement = node.Body.Statements.LastOrDefault();
                if ((lastStatement as ExpressionStatementSyntax)?.Expression?.Kind() != SyntaxKind.AwaitExpression)
                {
                    return;
                }

                if (!IsSafeToRemoveAsyncAwait(lastStatement))
                {
                    return;
                }

                numAwait++;
            }

            if (numAwait < awaitExpressions.Count())
            {
                return;
            }

            context.ReportDiagnostic(diagnostic);

            bool IsSafeToRemoveAsyncAwait(StatementSyntax statement)
            {
                if (IsInUsingOrTryScope(statement, node))
                {
                    // If it is under 'using' or 'try' block, it is not safe to remove async/await.
                    return false;
                }

                return true;
            }
        }

    }
}
