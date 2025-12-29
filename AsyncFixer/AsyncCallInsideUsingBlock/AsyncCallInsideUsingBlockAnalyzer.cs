using System;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AsyncFixer.AsyncCallInsideUsingBlock
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AsyncCallInsideUsingBlockAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AsyncCallInsideUsingBlockTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AsyncCallInsideUsingBlockMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AsyncCallInsideUsingBlockDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticIds.AsyncCallInsideUsingBlock,
            Title,
            MessageFormat,
            Constants.DiagnosticCategory,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            customTags: WellKnownDiagnosticTags.Telemetry,
            helpLinkUri: Constants.AsyncFixer04HelpLink);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeUsingBlock, SyntaxKind.UsingStatement);
        }

        private static string[] BlockingCalls = new string[] { "GetAwaiter", "Result", "Wait" };

        private void AnalyzeUsingBlock(SyntaxNodeAnalysisContext context)
        {
            var node = (UsingStatementSyntax)context.Node;
            var declaration = node.Declaration;

            if (declaration == null)
            {
                return;
            }

            var declarator = declaration.Variables.FirstOrDefault();
            var identifier = declarator?.Identifier;

            if (identifier == null)
            {
                return;
            }

            var locationsOfDisposableObjects = node.Statement.DescendantNodes().OfType<IdentifierNameSyntax>().Where(a => a.Identifier.ValueText.Equals(identifier.Value.ValueText));
            foreach (var location in locationsOfDisposableObjects)
            {
                var invocation = location.FirstAncestorOrSelfUnderGivenNode<InvocationExpressionSyntax>(node);
                if (invocation == null)
                {
                    continue;
                }

                var invokeMethod = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (invokeMethod == null || !invokeMethod.ReturnTask())
                {
                    continue;
                }

                var isAwaited = invocation.Ancestors().OfType<AwaitExpressionSyntax>().Any();
                if (isAwaited)
                {
                    continue;
                }

                var isUnderLambda = location.FirstAncestorOrSelfUnderGivenNode<LambdaExpressionSyntax>(node) != null;
                if (isUnderLambda)
                {
                    continue;
                }

                // Check if the task is assigned to a variable and awaited later
                if (IsTaskAssignedAndAwaitedLater(invocation, node))
                {
                    continue;
                }

                // Check whether the async call will be synchronously waited.
                bool isInvocationWaited = false;

                foreach (var parent in invocation.Ancestors())
                {
                    var parentMemberAccess = parent as MemberAccessExpressionSyntax;
                    if (parentMemberAccess?.Name != null)
                    {
                        if (BlockingCalls.Any(a => a.Equals(parentMemberAccess.Name.Identifier.ValueText, StringComparison.OrdinalIgnoreCase)))
                        {
                            isInvocationWaited = true;
                            break;
                        }
                    }

                    var parentInvocation = parent as InvocationExpressionSyntax;
                    if (parentInvocation != null)
                    {
                        var parentSymbol = context.SemanticModel.GetSymbolInfo(parentInvocation).Symbol as IMethodSymbol;
                        if (parentSymbol?.GetAttributes().Any(a => a.AttributeClass.Name.Equals("BlockCaller")) == true)
                        {
                            isInvocationWaited = true;
                            break;
                        }
                    }
                }

                if (isInvocationWaited)
                {
                    continue;
                }

                var diagnostic = Diagnostic.Create(Rule, location.GetLocation(), identifier.Value.ValueText);
                context.ReportDiagnostic(diagnostic);
            }
        }

        /// <summary>
        /// Checks if the invocation result is assigned to a variable and that variable is later awaited.
        /// This handles patterns like:
        ///   var task = stream.CopyToAsync(...);
        ///   await task;
        /// or:
        ///   var task = taskFactory(cts.Token);
        ///   await Task.WhenAny(task, ...);
        /// </summary>
        private static bool IsTaskAssignedAndAwaitedLater(InvocationExpressionSyntax invocation, UsingStatementSyntax usingNode)
        {
            // Check if invocation is part of a variable declaration or assignment
            string assignedVariableName = null;

            // Case 1: var task = invocation();
            var variableDeclarator = invocation.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
            if (variableDeclarator != null)
            {
                assignedVariableName = variableDeclarator.Identifier.ValueText;
            }

            // Case 2: task = invocation();
            if (assignedVariableName == null)
            {
                var assignment = invocation.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
                if (assignment?.Left is IdentifierNameSyntax identifier)
                {
                    assignedVariableName = identifier.Identifier.ValueText;
                }
            }

            if (assignedVariableName == null)
            {
                return false;
            }

            // Now check if this variable is awaited within the using block
            var awaitExpressions = usingNode.Statement.DescendantNodes().OfType<AwaitExpressionSyntax>();
            foreach (var awaitExpr in awaitExpressions)
            {
                // Check if the variable is directly awaited: await task;
                if (awaitExpr.Expression is IdentifierNameSyntax awaitedIdentifier &&
                    awaitedIdentifier.Identifier.ValueText == assignedVariableName)
                {
                    return true;
                }

                // Check if the variable is used in a method call that is awaited: await Task.WhenAny(task, ...)
                var identifiersInAwait = awaitExpr.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>();
                if (identifiersInAwait.Any(id => id.Identifier.ValueText == assignedVariableName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
