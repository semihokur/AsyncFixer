using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System;

namespace AsyncFixer.UnnecessaryAsync
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnnecessaryAsyncAnalyzer : DiagnosticAnalyzer
    {
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.UnnecessaryAsyncTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.UnnecessaryAsyncMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.UnnecessaryAsyncDescription), Resources.ResourceManager, typeof(Resources));

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticIds.UnnecessaryAsync,
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
        }

        private bool HasUsingOrTryParent(SyntaxNode node, SyntaxNode root)
        {
            if (node == root) return false;
            var parent = node.Parent;
            var kind = parent.Kind();

            if (kind == SyntaxKind.TryStatement || kind == SyntaxKind.UsingStatement)
            {
                return true;
            }

            return HasUsingOrTryParent(parent, root);
        }

        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (MethodDeclarationSyntax)context.Node;
            var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node.Identifier.Text);

            if (!node.IsAsync() || node.HasEventArgsParameter() || node.HasObjectStateParameter() || node.IsTestMethod())
            {
                return;
            }

            // Retrieve all await expressions excluding the ones under lambda functions.
            var awaitExpressions = node.DescendantNodes().OfType<AwaitExpressionSyntax>().Where(a => a.FirstAncestorOrSelfUnderGivenNode<LambdaExpressionSyntax>(node) == null).ToList();

            if (node.Body == null &&
                node.ExpressionBody?.Expression.Kind() == SyntaxKind.AwaitExpression)
            {
                // Expression-bodied members 
                // e.g. public static async Task Foo() => await Task.FromResult(true);

                var returnExpressionType = context.SemanticModel.GetTypeInfo(node.ExpressionBody?.Expression);
                if (returnExpressionType.IsImplicitTypeCasting())
                {
                    // Task does not support covariance: Task<int> cannot be converted to Task<object>.
                    // That's why we cannot remove async/await keywords.
                    return;
                }

                if (awaitExpressions.Count() > 1)
                {
                    // If there is more than await expressions, we cannot safely remove async/await keywords
                    return;
                }

                if (!WillFixedExpressionBeCorrect(awaitExpressions[0]))
                {
                    return;
                }

                context.ReportDiagnostic(diagnostic);
                return;
            }


            if (node.Body == null || context.SemanticModel == null)
            {
                return;
            }

            var controlFlow = context.SemanticModel.AnalyzeControlFlow(node.Body);
            var returnStatements = controlFlow?.ReturnStatements ?? ImmutableArray<SyntaxNode>.Empty;

            // Retrieve the disposable object identifiers from the using statements. 
            // For instance, for the following statement, we'd like to return 'source'.
            //      using FileStream source = File.Open("data", FileMode.Open);
            var disposableObjectNames = node.Body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>()
                .Where(a => a.UsingKeyword.Kind() != SyntaxKind.None)
                .SelectMany(a => a.DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(b => b.Identifier.ValueText)).ToList();

            var numAwait = 0;
            if (returnStatements.Any())
            {
                foreach (var temp in returnStatements)
                {
                    var returnStatement = temp as ReturnStatementSyntax;
                    if (returnStatement?.Expression?.Kind() != SyntaxKind.AwaitExpression)
                    {
                        return;
                    }

                    if (!IsSafeToRemoveAsyncAwait(returnStatement))
                    {
                        return;
                    }

                    var returnExpressionType = context.SemanticModel.GetTypeInfo(returnStatement.Expression);
                    if (returnExpressionType.IsImplicitTypeCasting())
                    {
                        // Task does not support covariance.
                        return;
                    }

                    if (!WillFixedExpressionBeCorrect(returnStatement.Expression as AwaitExpressionSyntax))
                    {
                        return;
                    }

                    numAwait++;
                }
            }
            else
            {
                // if awaitExpression is the last statement's expression
                // using (var stream = new MemoryStream())
                // {
                //    await Task.FromResult(3); ==> this is NOT the last statement because of the using block.
                // }

                var lastStatement = node.Body.Statements.LastOrDefault();
                if ((lastStatement as ExpressionStatementSyntax)?.Expression?.Kind() != SyntaxKind.AwaitExpression)
                {
                    return;
                }

                if (!IsSafeToRemoveAsyncAwait(lastStatement))
                {
                    return;
                }

                if (!WillFixedExpressionBeCorrect((lastStatement as ExpressionStatementSyntax)?.Expression as AwaitExpressionSyntax))
                {
                    return;
                }

                numAwait++;
            }

            if (numAwait < awaitExpressions.Count())
            {
                return;
            }

            context.ReportDiagnostic(diagnostic);

            bool IsSafeToRemoveAsyncAwait(StatementSyntax statement)
            {
                if (HasUsingOrTryParent(statement, node))
                {
                    // If it is under 'using' or 'try' block, it is not safe to remove async/await.
                    return false;
                }

                // Make sure that we do not give a warning about the await statement involving an object which is created by an using statement.
                // We use dataflow analysis to accurately detect a case like below:
                //  async Task foo()
                //  {
                //      using var stream = new MemoryStream();
                //      int streamOperation()
                //      {
                //          return stream.Read(null);
                //      }
                //      
                //      var t = Task.Run(() => streamOperation())
                //      await t;
                //  }
                // In the example above, we need to find out whether 'stream' is accessed in the last statement.

                try
                {
                    List<string> names = GetAccessedVariableNamesWithPointsToAnalysis(context.SemanticModel, node, statement).ToList();
                    return !names.Any(a => disposableObjectNames.Contains(a));
                }
                catch (InvalidCastException)
                {
/* 
   semanticModel.AnalyzeDataFlow(node).ReadInside throws the following exception in the following example: https://github.com/lupusbytes/AsyncFixerBugExample 

   System.InvalidCastException: Unable to cast object of type 'Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE.PEAssemblySymbol' to type 'Microsoft.CodeAnalysis.CSharp.Symbols.SourceAssemblySymbol'.
   at Microsoft.CodeAnalysis.CSharp.DefiniteAssignmentPass..ctor(CSharpCompilation compilation, Symbol member, BoundNode node, EmptyStructTypeCache emptyStructs, Boolean trackUnassignments, HashSet`1 initiallyAssignedVariables)
   at Microsoft.CodeAnalysis.CSharp.UnassignedVariablesWalker.Analyze(CSharpCompilation compilation, Symbol member, BoundNode node, Boolean convertInsufficientExecutionStackExceptionToCancelledByStackGuardException)
   at Microsoft.CodeAnalysis.CSharp.CSharpDataFlowAnalysis.get_UnassignedVariables()
   at Microsoft.CodeAnalysis.CSharp.CSharpDataFlowAnalysis.get_DataFlowsIn()

   This is a clearly bug in Microsoft.CodeAnalysis package. We just swallow the exception here.
*/
                    return false;
                }
            }

            bool WillFixedExpressionBeCorrect(AwaitExpressionSyntax fixableExpression)
            {
                // When fixing we remove calls to ConfigureAwait:
                var fixedExpression = Helpers.RemoveConfigureAwait(fixableExpression.Expression);

                // Let's check that the types match. There can be a mismatch, for example,
                // if we are awaiting a ValueTask in a Task-returning method, i.e. this compiles:
                //     ValueTask<int> Foo() => ValueTask.FromResult(0);
                //     Task<int> Bar() => await Foo();
                // But the 'fixed' version doesn't:
                //     Task<int> Bar => Foo();    // Error!
                var fixedExpressionValueType = context.SemanticModel.GetTypeInfo(fixedExpression);
                var methodReturnType = context.SemanticModel.GetTypeInfo(node.ReturnType);

                // Compare by name instead of by type, else we dont allow stuff compatible
                // types like Task and Task<T> (and we already checked for covariance beforehand). 
                return fixedExpressionValueType.Type.Name == methodReturnType.Type.Name
                        || fixedExpressionValueType.Type.IsTask() && node.ReturnsVoid();
            }
        }

        /// <summary>
        /// Return the names of all variables that are read-accessed in the given statement.
        /// </summary>
        /// <remarks>
        /// The method iteratively goes through the definitions to find implicitly-accessed variables. 
        /// </remarks>
        private IEnumerable<string> GetAccessedVariableNamesWithPointsToAnalysis(SemanticModel semanticModel, SyntaxNode root, SyntaxNode node, int depth = 0)
        {
            if (depth == 5 || node == null || root == null)
            {
                // Put a limit for the call stack frame
                yield break;
            }

            DataFlowAnalysis dataFlowResult = semanticModel.AnalyzeDataFlow(node);

            if (dataFlowResult?.Succeeded == true)
            {
                foreach (ISymbol symbol in dataFlowResult.ReadInside)
                {
                    yield return symbol.Name;

                    if (symbol.DeclaringSyntaxReferences == null)
                    {
                        continue;
                    }

                    foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
                    {
                        var expressions = root.FindNode(syntaxRef.Span, getInnermostNodeForTie: true).DescendantNodes((n) => !(n is ExpressionSyntax)).OfType<ExpressionSyntax>();

                        foreach (var expr in expressions)
                        {
                            var names = GetAccessedVariableNamesWithPointsToAnalysis(semanticModel, root, expr, depth + 1);
                            foreach (var name in names)
                            {
                                yield return name;
                            }
                        }
                    }
                }
            }
        }
    }
}
