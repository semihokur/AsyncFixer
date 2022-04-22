using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AsyncFixer.AsyncVoid
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AsyncVoidAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AsyncVoidTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AsyncVoidMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AsyncVoidDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticIds.AsyncVoid,
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

            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
            context.RegisterOperationAction(AnalyzeAnonymousFunction, OperationKind.AnonymousFunction);
        }

        // It detects "async void methodA(){}" declarations.
        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (MethodDeclarationSyntax)context.Node;

            if (!node.IsAsync() || !node.ReturnsVoid() || node.HasEventArgsParameter() || node.HasObjectStateParameter())
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, node.ReturnType.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        private void AnalyzeAnonymousFunction(OperationAnalysisContext context)
        {
            var operation = (IAnonymousFunctionOperation)context.Operation;
            var symbol = operation.Symbol;
            if (symbol == null || !symbol.IsAsync || !symbol.ReturnsVoid)
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, operation.Syntax.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        // TODO: Create a new rule for the method below: it analyzes fire&forget async calls under any method (including async and non-async methods).
        private void AnalyzeExpression(SyntaxNodeAnalysisContext context)
        {
            var node = (InvocationExpressionSyntax)context.Node;
            var methodSyntax = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            if (methodSyntax == null) return;

            if (methodSyntax.Modifiers.Any(m => m.Text == "async")) return;

            var info = context.SemanticModel.GetSymbolInfo(context.Node);
            if (info.Symbol == null || info.Symbol.Kind != SymbolKind.Method) return;
            var calledMethod = (IMethodSymbol)info.Symbol;

            if (!calledMethod.ReturnType.ToDisplayString().StartsWith("System.Threading.Tasks.Task")) return;

            if (!node.Parent.IsKind(SyntaxKind.ExpressionStatement)) return;

            //var diagnostic = Diagnostic.Create(CallRule, node.GetLocation(), node.ToString());
            //context.ReportDiagnostic(diagnostic);
        }
    }
}
