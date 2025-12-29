using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace AsyncFixer.UnnecessaryAsync
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnnecessaryAsyncFixer)), Shared]
    public class UnnecessaryAsyncFixer : CodeFixProvider
    {
        private static readonly string Title = Resources.ResourceManager.GetString(nameof(Resources.UnnecessaryAsyncFixerTitle), CultureInfo.InvariantCulture);

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DiagnosticIds.UnnecessaryAsync); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (root == null || semanticModel == null)
            {
                return;
            }

            var diagnostic = context.Diagnostics.First();
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            // Try to find a local function declaration first (more specific than method)
            // Local functions are nested inside methods, so we need to check for them first
            var localFunctionDeclaration = node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
            if (localFunctionDeclaration != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => RemoveAsyncAwaitFromLocalFunction(context.Document, semanticModel, localFunctionDeclaration, c),
                        equivalenceKey: Title),
                    diagnostic);
                return;
            }

            // Try to find a method declaration
            var methodDeclaration = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (methodDeclaration != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => RemoveAsyncAwaitFromMethod(context.Document, semanticModel, methodDeclaration, c),
                        equivalenceKey: Title),
                    diagnostic);
            }
        }

        /// <summary>
        /// Transforms an async method by removing the async keyword and converting await expressions to return statements.
        /// Handles expression-bodied methods, methods with return statements, and methods ending with await or if-else.
        /// </summary>
        /// <param name="document">The document containing the method.</param>
        /// <param name="semanticModel">The semantic model for type analysis.</param>
        /// <param name="methodDecl">The method declaration to transform.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The modified document with async/await removed.</returns>
        private async Task<Document> RemoveAsyncAwaitFromMethod(Document document, SemanticModel semanticModel, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            var pairs = new List<SyntaxReplacementPair>();

            if (methodDecl.ExpressionBody != null)
            {
                // Expression-bodied methods

                // Find the first descendant await expression excluding the inner ones.
                var awaitExpr = methodDecl.DescendantNodes().OfType<AwaitExpressionSyntax>().FirstOrDefault();
                var newExpr = RemoveAwaitFromExpression(awaitExpr);
                pairs.Add(new SyntaxReplacementPair(awaitExpr, newExpr));
            }
            else
            {
                // For regular methods.
                CollectReplacementsFromBody(semanticModel, methodDecl.Body, pairs);
            }

            MethodDeclarationSyntax newMethodDecl = methodDecl.ReplaceAll(pairs);

            // Remove async keyword
            var asyncModifier = newMethodDecl.Modifiers.First(a => a.IsKind(SyntaxKind.AsyncKeyword));
            newMethodDecl = asyncModifier.HasLeadingTrivia
                ? newMethodDecl.WithModifiers(newMethodDecl.Modifiers.Remove(asyncModifier))
                    .WithLeadingTrivia(asyncModifier.LeadingTrivia)
                : newMethodDecl.WithModifiers(newMethodDecl.Modifiers.Remove(asyncModifier));

            // If void, convert it to Task
            if (newMethodDecl.ReturnsVoid())
            {
                var newType =
                    SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task")
                        .WithAdditionalAnnotations(Simplifier.Annotation)
                        .WithTrailingTrivia(newMethodDecl.ReturnType.GetTrailingTrivia());
                newMethodDecl = newMethodDecl.WithReturnType(newType);
            }

            // Replace the old method with the new one.
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
            return document.WithSyntaxRoot(newRoot);
        }

        /// <summary>
        /// Transforms an async local function by removing the async keyword and converting await expressions to return statements.
        /// </summary>
        /// <param name="document">The document containing the local function.</param>
        /// <param name="semanticModel">The semantic model for type analysis.</param>
        /// <param name="localFunc">The local function declaration to transform.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The modified document with async/await removed.</returns>
        private async Task<Document> RemoveAsyncAwaitFromLocalFunction(Document document, SemanticModel semanticModel, LocalFunctionStatementSyntax localFunc, CancellationToken cancellationToken)
        {
            var pairs = new List<SyntaxReplacementPair>();

            if (localFunc.ExpressionBody != null)
            {
                // Expression-bodied local functions

                // Find the first descendant await expression excluding the inner ones.
                var awaitExpr = localFunc.DescendantNodes().OfType<AwaitExpressionSyntax>().FirstOrDefault();
                var newExpr = RemoveAwaitFromExpression(awaitExpr);
                pairs.Add(new SyntaxReplacementPair(awaitExpr, newExpr));
            }
            else
            {
                // For regular local functions.
                CollectReplacementsFromBody(semanticModel, localFunc.Body, pairs);
            }

            LocalFunctionStatementSyntax newLocalFunc = localFunc.ReplaceAll(pairs);

            // Remove async keyword
            var asyncModifier = newLocalFunc.Modifiers.First(a => a.IsKind(SyntaxKind.AsyncKeyword));
            newLocalFunc = asyncModifier.HasLeadingTrivia
                ? newLocalFunc.WithModifiers(newLocalFunc.Modifiers.Remove(asyncModifier))
                    .WithLeadingTrivia(asyncModifier.LeadingTrivia)
                : newLocalFunc.WithModifiers(newLocalFunc.Modifiers.Remove(asyncModifier));

            // If void, convert it to Task
            var returnsVoid = newLocalFunc.ReturnType is PredefinedTypeSyntax predefined &&
                              predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);
            if (returnsVoid)
            {
                var newType =
                    SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task")
                        .WithAdditionalAnnotations(Simplifier.Annotation)
                        .WithTrailingTrivia(newLocalFunc.ReturnType.GetTrailingTrivia());
                newLocalFunc = newLocalFunc.WithReturnType(newType);
            }

            // Replace the old local function with the new one.
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(localFunc, newLocalFunc);
            return document.WithSyntaxRoot(newRoot);
        }

        /// <summary>
        /// Collects replacement pairs from a function body (used by both methods and local functions).
        /// </summary>
        private void CollectReplacementsFromBody(SemanticModel semanticModel, BlockSyntax body, List<SyntaxReplacementPair> pairs)
        {
            if (body == null) return;

            var controlFlow = semanticModel.AnalyzeControlFlow(body);
            var returnStatements = controlFlow?.ReturnStatements ?? ImmutableArray<SyntaxNode>.Empty;
            if (returnStatements.Any())
            {
                // For functions with return statements
                foreach (var temp in returnStatements)
                {
                    var awaitExpr = (temp as ReturnStatementSyntax)?.Expression;
                    if (awaitExpr?.Kind() == SyntaxKind.AwaitExpression)
                    {
                        var newExpr = RemoveAwaitFromExpression((AwaitExpressionSyntax)awaitExpr);
                        pairs.Add(new SyntaxReplacementPair(awaitExpr, newExpr));
                    }
                }
            }
            else
            {
                // For functions that do not have return statements. They just have await statements

                // if awaitExpression is the last statement's expression
                var lastStatement = body.Statements.LastOrDefault();

                if (lastStatement is BlockSyntax block)
                {
                    lastStatement = block.Statements.LastOrDefault();
                }

                var exprStmnt = lastStatement as ExpressionStatementSyntax;

                if (exprStmnt?.Expression?.Kind() == SyntaxKind.AwaitExpression)
                {
                    var newExpr = RemoveAwaitFromExpression((AwaitExpressionSyntax)exprStmnt.Expression);

                    var newStatement =
                        SyntaxFactory.ReturnStatement(newExpr)
                            .WithAdditionalAnnotations(Formatter.Annotation)
                            .WithLeadingTrivia(lastStatement.GetLeadingTrivia())
                            .WithTrailingTrivia(lastStatement.GetTrailingTrivia());
                    pairs.Add(new SyntaxReplacementPair(lastStatement, newStatement));
                }
                else if (lastStatement is IfStatementSyntax ifStatement)
                {
                    // Handle if-else with terminal awaits in each branch
                    CollectIfElseReplacements(ifStatement, pairs);
                }
            }
        }

        /// <summary>
        /// Removes the await keyword from an await expression and strips any ConfigureAwait call.
        /// For example, transforms "await Task.Delay(1).ConfigureAwait(false)" to "Task.Delay(1)".
        /// </summary>
        /// <param name="awaitExpr">The await expression to transform.</param>
        /// <returns>The inner expression without await or ConfigureAwait.</returns>
        private ExpressionSyntax RemoveAwaitFromExpression(AwaitExpressionSyntax awaitExpr)
        {
            var trimmedExpr = Helpers.RemoveConfigureAwait(awaitExpr.Expression);

            // TODO: Check whether it is ConfigureAwait(false) or ConfigureAwait(true);
            var newExpr = awaitExpr.ReplaceNode(awaitExpr.Expression, trimmedExpr);
            return newExpr.Expression.WithAdditionalAnnotations(Simplifier.Annotation);
        }

        /// <summary>
        /// Collects replacement pairs for if-else statements with terminal awaits.
        /// Recursively processes else-if chains, transforming each terminal await into a return statement.
        /// </summary>
        /// <param name="ifStatement">The if statement to process.</param>
        /// <param name="pairs">The list to add replacement pairs to.</param>
        private void CollectIfElseReplacements(IfStatementSyntax ifStatement, List<SyntaxReplacementPair> pairs)
        {
            // Process "then" branch
            CollectBranchReplacement(ifStatement.Statement, pairs);

            // Process "else" branch
            if (ifStatement.Else != null)
            {
                var elseStatement = ifStatement.Else.Statement;
                if (elseStatement is IfStatementSyntax nestedIf)
                {
                    // else if - recurse
                    CollectIfElseReplacements(nestedIf, pairs);
                }
                else
                {
                    // Regular else block
                    CollectBranchReplacement(elseStatement, pairs);
                }
            }
        }

        /// <summary>
        /// Collects the replacement pair for a single branch's terminal await.
        /// Transforms "await expr;" into "return expr;" for the last statement in the branch.
        /// </summary>
        /// <param name="branchStatement">The branch body (block or single statement).</param>
        /// <param name="pairs">The list to add replacement pairs to.</param>
        private void CollectBranchReplacement(StatementSyntax branchStatement, List<SyntaxReplacementPair> pairs)
        {
            StatementSyntax lastStatement = branchStatement;

            // If it's a block, get the last statement
            if (branchStatement is BlockSyntax block)
            {
                lastStatement = block.Statements.LastOrDefault();
            }

            if (lastStatement is ExpressionStatementSyntax exprStmt &&
                exprStmt.Expression is AwaitExpressionSyntax awaitExpr)
            {
                var newExpr = RemoveAwaitFromExpression(awaitExpr);

                var newStatement =
                    SyntaxFactory.ReturnStatement(newExpr)
                        .WithAdditionalAnnotations(Formatter.Annotation)
                        .WithLeadingTrivia(lastStatement.GetLeadingTrivia())
                        .WithTrailingTrivia(lastStatement.GetTrailingTrivia());

                pairs.Add(new SyntaxReplacementPair(lastStatement, newStatement));
            }
        }
    }
}
