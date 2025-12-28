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

namespace AsyncFixer.BlockingCallInsideAsync
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlockingCallInsideAsyncFixer)), Shared]
    public class BlockingCallInsideAsyncFixer : CodeFixProvider
    {
        private static readonly string Title = Resources.ResourceManager.GetString(nameof(Resources.BlockingCallInsideAsyncFixerTitle), CultureInfo.InvariantCulture);

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DiagnosticIds.BlockingCallInsideAsync); }
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

            // Use getInnermostNodeForTie: true to ensure we get the most specific node
            // when the span exactly matches multiple nested nodes
            var node = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true);

            var memberAccess = node.FirstAncestorOrSelf<MemberAccessExpressionSyntax>();
            if (memberAccess != null && memberAccess.Name.Identifier.ValueText.Equals("Result"))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => InsertAsyncCallForTaskResult(context.Document, memberAccess, c),
                        equivalenceKey: Title),
                    diagnostic);
                return;
            }

            // Find the invocation that exactly matches the diagnostic span.
            // This is important for Fix All scenarios where nested invocations exist
            // (e.g., Assert.AreEqual(1, dbContext.Person.Count()) - we want Count(), not Assert.AreEqual)
            var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();

            // Ensure we have the correct invocation by checking the span matches
            while (invocation != null && invocation.Span != diagnosticSpan)
            {
                // If this invocation's span doesn't match, check if there's a nested one that does
                var nestedInvocation = invocation.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault(inv => inv.Span == diagnosticSpan);

                if (nestedInvocation != null)
                {
                    invocation = nestedInvocation;
                    break;
                }

                // No nested match found, keep the current invocation
                break;
            }

            if (invocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => InsertAsyncCallForInvocation(context.Document, invocation, c),
                        equivalenceKey: Title),
                    diagnostic);
            }
        }

        private async Task<Document> InsertAsyncCallForTaskResult(Document document, MemberAccessExpressionSyntax memberAccess,
            CancellationToken cancellationToken)
        {
            // Replace 't.Result' with await t'
            var oldNode = memberAccess;
            var taskVariable = memberAccess.Expression;

            ExpressionSyntax newNode = MakeItAwaited(taskVariable, oldNode);

            // t.Result.ToString() -> (await t).ToString()
            if (oldNode.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                newNode = SyntaxFactory.ParenthesizedExpression(newNode);
            }

            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(oldNode, newNode);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> InsertAsyncCallForInvocation(Document document, InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken)
        {
            // newExpression will replace the invocation expression.
            ExpressionSyntax newExpression;
            var oldNode = invocation;

            var expression = invocation.Expression;
            var identifier = expression as IdentifierNameSyntax;
            if (identifier != null)
            {
                // foo(); will be changed to fooAsync();
                newExpression =
                    invocation.WithExpression(SyntaxFactory.ParseName(identifier.Identifier.ValueText + "Async"));
            }
            else
            {
                var memberAccess = (MemberAccessExpressionSyntax)expression;
                var blockingCallName = memberAccess.Name.Identifier.ValueText;
                if (blockingCallName.Equals("Wait"))
                {
                    // t.Wait() -> t
                    newExpression = memberAccess.Expression;
                }
                else
                {
                    MemberAccessExpressionSyntax newMemberAccess;
                    if (blockingCallName.Equals("WaitAny"))
                    {
                        newMemberAccess = memberAccess.WithName((SimpleNameSyntax)SyntaxFactory.ParseName("WhenAny"));
                    }
                    else if (blockingCallName.Equals("WaitAll"))
                    {
                        newMemberAccess = memberAccess.WithName((SimpleNameSyntax)SyntaxFactory.ParseName("WhenAll"));
                    }
                    else if (blockingCallName.Equals("Sleep"))
                    {
                        newMemberAccess = (MemberAccessExpressionSyntax)SyntaxFactory.ParseExpression("Task.Delay");
                    }
                    else
                    {
                        newMemberAccess =
                            memberAccess.WithName(
                                (SimpleNameSyntax)
                                    SyntaxFactory.ParseName(memberAccess.Name.Identifier.ValueText + "Async"));
                    }

                    newExpression = invocation.WithExpression(newMemberAccess);
                }
            }

            ExpressionSyntax newNode = MakeItAwaited(newExpression, oldNode);

            // obj.First().Id -> (await obj.FirstAsync()).Id
            if (oldNode.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                newNode = SyntaxFactory.ParenthesizedExpression(newNode);
            }

            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var newRoot = root.ReplaceNode(oldNode, newNode);
            return document.WithSyntaxRoot(newRoot);
        }

        private AwaitExpressionSyntax MakeItAwaited(ExpressionSyntax expression, SyntaxNode oldNode)
        {
            return SyntaxFactory.AwaitExpression(expression.WithoutLeadingTrivia().WithoutTrailingTrivia())
                    .WithAdditionalAnnotations(Formatter.Annotation)
                    .WithLeadingTrivia(oldNode.GetLeadingTrivia())
                    .WithTrailingTrivia(oldNode.GetTrailingTrivia()); ;
        }
    }
}
