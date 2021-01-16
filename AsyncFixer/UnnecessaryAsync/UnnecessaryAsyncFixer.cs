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

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var methodDeclaration =
                root.FindToken(diagnosticSpan.Start).Parent.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => RemoveAsyncAwait(context.Document, methodDeclaration, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> RemoveAsyncAwait(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            MethodDeclarationSyntax newMethodDecl;

            // (1) Remove async keyword
            var asyncModifier = methodDecl.Modifiers.First(a => a.Kind() == SyntaxKind.AsyncKeyword);
            newMethodDecl = asyncModifier.HasLeadingTrivia
                ? methodDecl.WithModifiers(methodDecl.Modifiers.Remove(asyncModifier))
                    .WithLeadingTrivia(asyncModifier.LeadingTrivia)
                : methodDecl.WithModifiers(methodDecl.Modifiers.Remove(asyncModifier));

            // (2) If void, convert it to Task
            if (newMethodDecl.ReturnsVoid())
            {
                var newType =
                    SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task")
                        .WithAdditionalAnnotations(Simplifier.Annotation)
                        .WithTrailingTrivia(newMethodDecl.ReturnType.GetTrailingTrivia());
                newMethodDecl = newMethodDecl.WithReturnType(newType);
            }

            var methodBody = (CSharpSyntaxNode)newMethodDecl.Body ?? newMethodDecl.ExpressionBody;

            // (3) For all await expressions, remove await and insert return if there is none. 
            var awaitExprs = methodBody.DescendantNodes().OfType<AwaitExpressionSyntax>();

            var pairs = new List<SyntaxReplacementPair>();

            foreach (var awaitExpr in awaitExprs)
            {
                SyntaxNode oldNode;
                SyntaxNode newNode;
                var newAwaitExpr = awaitExpr;

                // If there is some ConfigureAwait(false), remove it 
                var invoc = awaitExpr.Expression as InvocationExpressionSyntax;
                if (invoc != null)
                {
                    var expr = invoc.Expression as MemberAccessExpressionSyntax;

                    // TODO: Check whether it is ConfigureAwait(false) or ConfigureAwait(true);
                    if (expr != null && expr.Name.Identifier.ValueText == "ConfigureAwait")
                    {
                        newAwaitExpr = awaitExpr.ReplaceNode(awaitExpr.Expression, expr.Expression);
                    }
                }

                if (awaitExpr.Parent.Kind() == SyntaxKind.ReturnStatement ||
                    awaitExpr.Parent.Kind() == SyntaxKind.ArrowExpressionClause)
                {
                    oldNode = awaitExpr;
                    newNode = newAwaitExpr.Expression.WithAdditionalAnnotations(Simplifier.Annotation);
                }
                else
                {
                    oldNode = awaitExpr.Parent;
                    newNode =
                        SyntaxFactory.ReturnStatement(newAwaitExpr.Expression)
                            .WithAdditionalAnnotations(Formatter.Annotation)
                            .WithLeadingTrivia(oldNode.GetLeadingTrivia())
                            .WithTrailingTrivia(oldNode.GetTrailingTrivia());
                }

                pairs.Add(new SyntaxReplacementPair(oldNode, newNode));
            }

            newMethodDecl = newMethodDecl.ReplaceAll(pairs);

            // (4) Replace the old method with the new one.
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
