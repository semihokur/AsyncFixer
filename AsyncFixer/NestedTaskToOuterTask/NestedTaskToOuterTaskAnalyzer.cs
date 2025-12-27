using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AsyncFixer.NestedTaskToOuterTask
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NestedTaskToOuterTaskAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.NestedTaskToOuterTaskTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.NestedTaskToOuterTaskMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.NestedTaskToOuterTaskDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticIds.NestedTaskToOuterTask,
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

            context.RegisterSyntaxNodeAction(AnalyzeSimpleAssignmentExpr, SyntaxKind.SimpleAssignmentExpression);
            context.RegisterSyntaxNodeAction(AnalyzeAwait, SyntaxKind.AwaitExpression);
            context.RegisterSyntaxNodeAction(AnalyzeVariableDecl, SyntaxKind.VariableDeclarator);
            context.RegisterSyntaxNodeAction(AnalyzeReturnStatement, SyntaxKind.ReturnStatement);
        }

        private void AnalyzeAwait(SyntaxNodeAnalysisContext context)
        {
            var node = (AwaitExpressionSyntax)context.Node;

            // Ignore WhenAny() because WhenAny always returns Task<Task>.
            var isWhenAny = node.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(a => a.Name?.Identifier != null && a.Name.Identifier.ValueText.Equals("WhenAny"));
            if (isWhenAny)
            {
                return;
            }

            var awaitedType = context.SemanticModel.GetTypeInfo(node.Expression).Type as INamedTypeSymbol;
            if (awaitedType == null || !awaitedType.IsGenericType || awaitedType.TypeArguments == null)
            {
                return;
            }

            // Only flag if the awaited type itself is a Task/ValueTask.
            // This avoids false positives for tuple awaiters like (task1, task2) from TaskTupleAwaiter.
            if (!awaitedType.IsTask())
            {
                return;
            }

            var typeArgument = awaitedType.TypeArguments.OfType<INamedTypeSymbol>().FirstOrDefault();
            if (typeArgument == null || !typeArgument.IsTask())
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, node.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        private void AnalyzeVariableDecl(SyntaxNodeAnalysisContext context)
        {
            var node = (VariableDeclaratorSyntax)context.Node;
            if (node.Initializer == null || node.Initializer.Value == null)
            {
                return;
            }

            var local = context.SemanticModel.GetDeclaredSymbol(node) as ILocalSymbol;
            if (local == null || local.Type == null)
            {
                return;
            }

            var leftType = local.Type as INamedTypeSymbol;
            if (leftType == null || !leftType.IsTask() || leftType.IsGenericType)
            {
                return;
            }

            var methodSymbol = context.SemanticModel.GetSymbolInfo(node.Initializer.Value).Symbol as IMethodSymbol;

            var rightType = methodSymbol?.ReturnType as INamedTypeSymbol;
            if (rightType?.BaseType?.IsTask() == true && rightType.IsGenericType
                && rightType.TypeArguments != null && rightType.TypeArguments.FirstOrDefault()?.IsTask() == true)
            {
                var diagnostic = Diagnostic.Create(Rule, node.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeSimpleAssignmentExpr(SyntaxNodeAnalysisContext context)
        {
            var node = (AssignmentExpressionSyntax)context.Node;

            var left = node.Left;
            var right = node.Right;

            var leftType = context.SemanticModel.GetTypeInfo(left).Type as INamedTypeSymbol;
            if (leftType == null || !leftType.IsTask() || leftType.IsGenericType)
            {
                return;
            }

            var methodSymbol = context.SemanticModel.GetSymbolInfo(right).Symbol as IMethodSymbol;

            var rightType = methodSymbol?.ReturnType as INamedTypeSymbol;
            if (rightType?.BaseType?.IsTask() == true && rightType.IsGenericType
                && rightType.TypeArguments != null && rightType.TypeArguments.FirstOrDefault()?.IsTask() == true)
            {
                var diagnostic = Diagnostic.Create(Rule, left.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeReturnStatement(SyntaxNodeAnalysisContext context)
        {
            var node = (ReturnStatementSyntax)context.Node;
            if (node.Expression == null)
            {
                return;
            }

            // Get the return type of the containing method
            var containingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (containingMethod == null)
            {
                return;
            }

            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod);
            if (methodSymbol == null)
            {
                return;
            }

            var returnType = methodSymbol.ReturnType as INamedTypeSymbol;
            if (returnType == null || !returnType.IsTask() || returnType.IsGenericType)
            {
                return;
            }

            // Check if the expression returns Task<Task>
            var expressionMethodSymbol = context.SemanticModel.GetSymbolInfo(node.Expression).Symbol as IMethodSymbol;

            var expressionReturnType = expressionMethodSymbol?.ReturnType as INamedTypeSymbol;
            if (expressionReturnType?.BaseType?.IsTask() == true && expressionReturnType.IsGenericType
                && expressionReturnType.TypeArguments != null && expressionReturnType.TypeArguments.FirstOrDefault()?.IsTask() == true)
            {
                var diagnostic = Diagnostic.Create(Rule, node.Expression.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
