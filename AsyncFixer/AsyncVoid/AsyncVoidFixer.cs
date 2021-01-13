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
using Microsoft.CodeAnalysis.Simplification;

namespace AsyncFixer.AsyncVoid
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncVoidFixer)), Shared]
    public class AsyncVoidFixer : CodeFixProvider
    {
        private static readonly string Title = Resources.ResourceManager.GetString(nameof(Resources.AsyncVoidFixerTitle), CultureInfo.InvariantCulture);

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DiagnosticIds.AsyncVoid); }
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

            var name = diagnostic.Id;
            var hash = diagnostic.GetHashCode();
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => ConvertToTask(context.Document, methodDeclaration, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> ConvertToTask(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            var newType =
                SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task")
                    .WithAdditionalAnnotations(Simplifier.Annotation)
                    .WithTrailingTrivia(methodDecl.ReturnType.GetTrailingTrivia());
            var newMethodDecl = methodDecl.WithReturnType(newType);

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
