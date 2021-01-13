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

            context.RegisterSyntaxNodeAction(AnalyzeSimpleAssignmentExpr, SyntaxKind.SimpleAssignmentExpression);
            context.RegisterSyntaxNodeAction(AnalyzeAwait, SyntaxKind.AwaitExpression);
            context.RegisterSyntaxNodeAction(AnalyzeVariableDecl, SyntaxKind.VariableDeclarator);
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
    }
}
