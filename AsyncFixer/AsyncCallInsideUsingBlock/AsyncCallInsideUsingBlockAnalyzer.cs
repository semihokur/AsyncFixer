using System;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Operations;

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
            helpLinkUri: Constants.RepositoryLink);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            
            context.RegisterSyntaxNodeAction(AnalyzeUsingBlock, SyntaxKind.UsingStatement);
            context.RegisterOperationAction(AnalyzeUsing, OperationKind.UsingDeclaration);
        }

        private static string[] BlockingCalls = new string[] { "GetAwaiter", "Result", "Wait" };

        private void AnalyzeUsing(OperationAnalysisContext context)
        {
            var operation = (IUsingDeclarationOperation) context.Operation;
            var symbols = operation.DeclarationGroup.GetDeclaredVariables();

            IOperation methodOperation = operation;
            while (methodOperation != null && !(methodOperation is IMethodBodyOperation))
            {
                methodOperation = methodOperation.Parent;
            }

            if (methodOperation == null)
            {
                return;
            }

            foreach (var symbol in symbols)
            {
                var localRefs = methodOperation.Descendants().OfType<ILocalReferenceOperation>().ToList();
                foreach (var localRef in localRefs)
                {
                    if (SymbolEqualityComparer.Default.Equals(localRef.Local, symbol))
                    {

                        if (IsProblematicDisposableUsage(localRef))
                        { 
                        }
                        // whether it returns task
                        // await or waited. 
                    }
                }


            }

            return;
        }

        private bool IsProblematicDisposableUsage(ILocalReferenceOperation localRef)
        {
            IOperation invocationOp = localRef;
            while (!(invocationOp is IInvocationOperation))
            {
                if (invocationOp == null)
                {
                    return false;
                }

                invocationOp = invocationOp.Parent;
            }

            // wheter the invocation operation returns a Task.
            if (!(invocationOp as IInvocationOperation).Type.IsTask())
            {
                return false;
            }

            return true;

        }

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
                if (IsProblematicDisposableUsage(context.SemanticModel, location, node))
                {
                    var diagnostic = Diagnostic.Create(Rule, location.GetLocation(), identifier.Value.ValueText);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private bool IsProblematicDisposableUsage(SemanticModel semanticModel, SyntaxNode disposableRef, SyntaxNode block)
        {
            var invocation = disposableRef.FirstAncestorOrSelfUnderGivenNode<InvocationExpressionSyntax>(block);
            if (invocation == null)
            {
                return false;
            }

            var invokeMethod = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (invokeMethod == null || !invokeMethod.ReturnTask())
            {
                return false;
            }

            var isAwaited = invocation.Ancestors().OfType<AwaitExpressionSyntax>().Any();
            if (isAwaited)
            {
                return false;
            }

            var isUnderLambda = disposableRef.FirstAncestorOrSelfUnderGivenNode<LambdaExpressionSyntax>(block) != null;
            if (isUnderLambda)
            {
                return false;
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
                    var parentSymbol = semanticModel.GetSymbolInfo(parentInvocation).Symbol as IMethodSymbol;
                    if (parentSymbol?.GetAttributes().Any(a => a.AttributeClass.Name.Equals("BlockCaller")) == true)
                    {
                        isInvocationWaited = true;
                        break;
                    }
                }
            }

            if (isInvocationWaited)
            {
                return false;
            }

            return true;
        }
    }
}
