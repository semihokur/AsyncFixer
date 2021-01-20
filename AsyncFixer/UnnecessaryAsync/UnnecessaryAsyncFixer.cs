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

            var methodDeclaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<MethodDeclarationSyntax>();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => RemoveAsyncAwait(context.Document, semanticModel, methodDeclaration, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> RemoveAsyncAwait(Document document, SemanticModel semanticModel, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
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

                var controlFlow = semanticModel.AnalyzeControlFlow(methodDecl.Body);
                var returnStatements = controlFlow?.ReturnStatements ?? ImmutableArray<SyntaxNode>.Empty;
                if (returnStatements.Any())
                {
                    // For methods with return statements
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
                    // For methods that do not have return statements. They just have await statements

                    // if awaitExpression is the last statement's expression
                    var lastStatement = methodDecl.Body.Statements.LastOrDefault() as ExpressionStatementSyntax;
                    if (lastStatement?.Expression?.Kind() == SyntaxKind.AwaitExpression)
                    {
                        var newExpr = RemoveAwaitFromExpression((AwaitExpressionSyntax)lastStatement.Expression);

                        var newStatement =
                            SyntaxFactory.ReturnStatement(newExpr)
                                .WithAdditionalAnnotations(Formatter.Annotation)
                                .WithLeadingTrivia(lastStatement.GetLeadingTrivia())
                                .WithTrailingTrivia(lastStatement.GetTrailingTrivia());
                        pairs.Add(new SyntaxReplacementPair(lastStatement, newStatement));
                    }
                }
            }

            MethodDeclarationSyntax newMethodDecl = methodDecl.ReplaceAll(pairs);

            // Remove async keyword
            var asyncModifier = newMethodDecl.Modifiers.First(a => a.Kind() == SyntaxKind.AsyncKeyword);
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

        private ExpressionSyntax RemoveAwaitFromExpression(AwaitExpressionSyntax awaitExpr)
        {
            var newExpr = awaitExpr;

            // If there is some ConfigureAwait(false), remove it 
            var invoc = awaitExpr.Expression as InvocationExpressionSyntax;
            if (invoc != null)
            {
                var expr = invoc.Expression as MemberAccessExpressionSyntax;

                // TODO: Check whether it is ConfigureAwait(false) or ConfigureAwait(true);
                if (expr != null && expr.Name.Identifier.ValueText == "ConfigureAwait")
                {
                    newExpr = awaitExpr.ReplaceNode(awaitExpr.Expression, expr.Expression);
                }
            }

            return newExpr.Expression.WithAdditionalAnnotations(Simplifier.Annotation);
        }
    }
}
