using System.Collections.Immutable;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
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
            helpLinkUri: Constants.AsyncFixer01HelpLink);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeLocalFunctionDeclaration, SyntaxKind.LocalFunctionStatement);
        }

        /// <summary>
        /// Determines whether a node is within a using statement, using declaration, or try block.
        /// Removing async/await in these scopes would cause premature disposal or change exception handling.
        /// </summary>
        /// <param name="node">The syntax node to check.</param>
        /// <param name="root">The root node (method body) to stop traversal at.</param>
        /// <returns>True if the node is in a scope where async/await removal would be unsafe.</returns>
        private bool IsInUsingOrTryScope(SyntaxNode node, SyntaxNode root)
        {
            if (node == root) return false;
            var parent = node.Parent;
            var kind = parent.Kind();

            if (kind == SyntaxKind.TryStatement || kind == SyntaxKind.UsingStatement)
            {
                return true;
            }

            // Check sibling nodes for using declarations which will be in scope
            // Example: 
            //  public async Task Foo() 
            //  {
            //      using var x = new Disposable();   <-- We want to detect this statement
            //      await Task.Delay(1);
            //  }
            foreach (var sibling in parent.ChildNodes())
            {
                if (sibling == node)
                {
                    // Reached the statement we are considering
                    // No need to keep going
                    break;
                }

                if (sibling is LocalDeclarationStatementSyntax declaration)
                {
                    if (declaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
                    {
                        // Variable declaration with using,
                        // which will be in scope in the await statement (as in the above example)
                        return true;
                    }
                }

            }
            
            return IsInUsingOrTryScope(parent, root);
        }

        /// <summary>
        /// Analyzes an async method declaration to determine if the async/await keywords are unnecessary.
        /// Reports a diagnostic if the method can be simplified by removing async/await and returning the task directly.
        /// </summary>
        /// <param name="context">The syntax node analysis context containing the method declaration.</param>
        private void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (MethodDeclarationSyntax)context.Node;

            if (!node.IsAsync() || node.HasEventArgsParameter(context.SemanticModel) || node.HasObjectStateParameter() || node.IsTestMethod())
            {
                return;
            }

            AnalyzeAsyncFunction(
                context,
                node,
                node.Identifier.Text,
                node.Body,
                node.ExpressionBody,
                node.ReturnType,
                node.ReturnsVoid());
        }

        /// <summary>
        /// Analyzes an async local function declaration to determine if the async/await keywords are unnecessary.
        /// Reports a diagnostic if the local function can be simplified by removing async/await and returning the task directly.
        /// </summary>
        /// <param name="context">The syntax node analysis context containing the local function declaration.</param>
        private void AnalyzeLocalFunctionDeclaration(SyntaxNodeAnalysisContext context)
        {
            var node = (LocalFunctionStatementSyntax)context.Node;

            if (!node.Modifiers.Any(SyntaxKind.AsyncKeyword))
            {
                return;
            }

            // Check if return type is void (for async void local functions)
            var returnsVoid = node.ReturnType is PredefinedTypeSyntax predefined &&
                              predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

            AnalyzeAsyncFunction(
                context,
                node,
                node.Identifier.Text,
                node.Body,
                node.ExpressionBody,
                node.ReturnType,
                returnsVoid);
        }

        /// <summary>
        /// Common analysis logic for both method declarations and local function declarations.
        /// </summary>
        private void AnalyzeAsyncFunction(
            SyntaxNodeAnalysisContext context,
            SyntaxNode functionNode,
            string functionName,
            BlockSyntax body,
            ArrowExpressionClauseSyntax expressionBody,
            TypeSyntax returnType,
            bool returnsVoid)
        {
            var diagnostic = Diagnostic.Create(Rule, functionNode.GetLocation(), functionName);

            // Exclude await expressions under lambdas and nested local functions
            bool IsUnderNestedFunction(SyntaxNode node)
            {
                var current = node.Parent;
                while (current != null && current != functionNode)
                {
                    if (current is LambdaExpressionSyntax || current is LocalFunctionStatementSyntax)
                    {
                        return true;
                    }
                    current = current.Parent;
                }
                return false;
            }

            var awaitForEachStatements = functionNode.DescendantNodes().OfType<ForEachStatementSyntax>()
                .Where(a => a.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword) && !IsUnderNestedFunction(a)).ToList();

            if (awaitForEachStatements.Any())
            {
                return;
            }

            // Retrieve all await expressions excluding the ones under lambda functions or nested local functions.
            var awaitExpressions = functionNode.DescendantNodes().OfType<AwaitExpressionSyntax>()
                .Where(a => !IsUnderNestedFunction(a)).ToList();

            if (body == null && expressionBody?.Expression.Kind() == SyntaxKind.AwaitExpression)
            {
                // Expression-bodied members
                // e.g. public static async Task Foo() => await Task.FromResult(true);
                var returnExpressionType = context.SemanticModel.GetTypeInfo(expressionBody?.Expression);
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

            if (body == null || context.SemanticModel == null)
            {
                return;
            }

            var controlFlow = context.SemanticModel.AnalyzeControlFlow(body);
            var returnStatements = controlFlow?.ReturnStatements ?? ImmutableArray<SyntaxNode>.Empty;
            var numAwaitsToRemove = 0;

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

                    numAwaitsToRemove++;
                }
            }
            else
            {
                // if awaitExpression is the last statement's expression
                // using (var stream = new MemoryStream())
                // {
                //    await Task.FromResult(3); ==> this is NOT the last statement because of the using block.
                // }

                var lastStatement = body.Statements.LastOrDefault();

                if (lastStatement is BlockSyntax block)
                {
                    lastStatement = block.Statements.LastOrDefault();
                }

                if ((lastStatement as ExpressionStatementSyntax)?.Expression?.Kind() == SyntaxKind.AwaitExpression)
                {
                    if (!IsSafeToRemoveAsyncAwait(lastStatement))
                    {
                        return;
                    }

                    if (!WillFixedExpressionBeCorrect((lastStatement as ExpressionStatementSyntax)?.Expression as AwaitExpressionSyntax))
                    {
                        return;
                    }

                    numAwaitsToRemove++;
                }
                else if (lastStatement is IfStatementSyntax)
                {
                    // Check if awaits are in terminal if-else branches
                    var terminalIfElseAwaits = GetTerminalIfElseAwaits(body, functionNode, awaitExpressions, context);
                    if (terminalIfElseAwaits == null)
                    {
                        return;
                    }

                    numAwaitsToRemove += terminalIfElseAwaits.Count;
                }
                else
                {
                    return;
                }
            }

            if (numAwaitsToRemove < awaitExpressions.Count())
            {
                return;
            }

            context.ReportDiagnostic(diagnostic);

            bool IsSafeToRemoveAsyncAwait(StatementSyntax statement)
            {
                if (IsInUsingOrTryScope(statement, functionNode))
                {
                    // If it is under 'using' or 'try' block, it is not safe to remove async/await.
                    return false;
                }

                return true;
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
                var fixedExpressionType = context.SemanticModel.GetTypeInfo(fixedExpression).Type;
                if (fixedExpressionType == null)
                {
                    return false;
                }

                ITypeSymbol desiredReturnType;
                if (returnsVoid)
                {
                    // The fixer changes `async void` to `Task`.
                    desiredReturnType = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
                    if (desiredReturnType == null)
                    {
                        return false;
                    }
                }
                else
                {
                    desiredReturnType = context.SemanticModel.GetTypeInfo(returnType).Type;
                    if (desiredReturnType == null)
                    {
                        return false;
                    }
                }

                // Only report when removing await/async would still compile.
                // This correctly allows `Task<T>` -> `Task` (inheritance), but disallows
                // `ValueTask<T>` -> `ValueTask` (no implicit conversion).
                var conversion = context.SemanticModel.Compilation.ClassifyConversion(fixedExpressionType, desiredReturnType);
                return conversion.Exists && conversion.IsImplicit;
            }
        }

        /// <summary>
        /// Gets await expressions that are terminal statements in if-else branches where
        /// the if-else is the last statement in the function body.
        /// Returns null if the pattern doesn't match or is unsafe to transform.
        /// </summary>
        private List<AwaitExpressionSyntax> GetTerminalIfElseAwaits(
            BlockSyntax body,
            SyntaxNode functionNode,
            List<AwaitExpressionSyntax> allAwaitExpressions,
            SyntaxNodeAnalysisContext context)
        {
            var lastStatement = body.Statements.LastOrDefault();
            if (lastStatement == null)
            {
                return null;
            }

            // Handle case where last statement is wrapped in a block
            if (lastStatement is BlockSyntax block)
            {
                lastStatement = block.Statements.LastOrDefault();
                if (lastStatement == null)
                {
                    return null;
                }
            }

            if (!(lastStatement is IfStatementSyntax ifStatement))
            {
                return null;
            }

            // Get return type for type compatibility checks
            TypeSyntax returnType = null;
            bool returnsVoid = false;
            if (functionNode is MethodDeclarationSyntax method)
            {
                returnType = method.ReturnType;
                returnsVoid = method.ReturnsVoid();
            }
            else if (functionNode is LocalFunctionStatementSyntax localFunc)
            {
                returnType = localFunc.ReturnType;
                returnsVoid = localFunc.ReturnType is PredefinedTypeSyntax predefined &&
                              predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);
            }

            var terminalAwaits = new List<AwaitExpressionSyntax>();
            if (!CollectTerminalAwaitsFromIfElse(ifStatement, terminalAwaits, functionNode, returnType, returnsVoid, allAwaitExpressions, context))
            {
                return null;
            }

            return terminalAwaits;
        }

        /// <summary>
        /// Recursively collects terminal await expressions from if-else branches.
        /// Returns false if any branch doesn't end with an await or is unsafe.
        /// </summary>
        private bool CollectTerminalAwaitsFromIfElse(
            IfStatementSyntax ifStatement,
            List<AwaitExpressionSyntax> terminalAwaits,
            SyntaxNode functionNode,
            TypeSyntax returnType,
            bool returnsVoid,
            List<AwaitExpressionSyntax> allAwaitExpressions,
            SyntaxNodeAnalysisContext context)
        {
            // Check the "then" branch
            if (!CollectTerminalAwaitFromBranch(ifStatement.Statement, terminalAwaits, functionNode, returnType, returnsVoid, allAwaitExpressions, context))
            {
                return false;
            }

            // Must have an else branch for all paths to be covered
            if (ifStatement.Else == null)
            {
                return false;
            }

            // Check the "else" branch - could be another if-else (else if) or a regular statement
            var elseStatement = ifStatement.Else.Statement;
            if (elseStatement is IfStatementSyntax nestedIf)
            {
                // else if - recurse
                return CollectTerminalAwaitsFromIfElse(nestedIf, terminalAwaits, functionNode, returnType, returnsVoid, allAwaitExpressions, context);
            }
            else
            {
                // Regular else block
                return CollectTerminalAwaitFromBranch(elseStatement, terminalAwaits, functionNode, returnType, returnsVoid, allAwaitExpressions, context);
            }
        }

        /// <summary>
        /// Collects the terminal await from a single branch (if body or else body).
        /// Returns false if the branch doesn't end with a single await or is unsafe.
        /// </summary>
        private bool CollectTerminalAwaitFromBranch(
            StatementSyntax branchStatement,
            List<AwaitExpressionSyntax> terminalAwaits,
            SyntaxNode functionNode,
            TypeSyntax returnType,
            bool returnsVoid,
            List<AwaitExpressionSyntax> allAwaitExpressions,
            SyntaxNodeAnalysisContext context)
        {
            StatementSyntax lastStatement = branchStatement;

            // If it's a block, get the last statement
            if (branchStatement is BlockSyntax block)
            {
                lastStatement = block.Statements.LastOrDefault();
                if (lastStatement == null)
                {
                    return false;
                }
            }

            // The last statement must be an expression statement with an await
            if (!(lastStatement is ExpressionStatementSyntax exprStmt))
            {
                return false;
            }

            if (!(exprStmt.Expression is AwaitExpressionSyntax awaitExpr))
            {
                return false;
            }

            // Make sure this await is in our list (not under a lambda or nested local function)
            if (!allAwaitExpressions.Contains(awaitExpr))
            {
                return false;
            }

            // Check if it's safe (not in using/try scope)
            if (IsInUsingOrTryScope(lastStatement, functionNode))
            {
                return false;
            }

            // Check type compatibility
            var fixedExpression = Helpers.RemoveConfigureAwait(awaitExpr.Expression);
            var fixedExpressionType = context.SemanticModel.GetTypeInfo(fixedExpression).Type;
            if (fixedExpressionType == null)
            {
                return false;
            }

            ITypeSymbol desiredReturnType;
            if (returnsVoid)
            {
                desiredReturnType = context.SemanticModel.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
                if (desiredReturnType == null)
                {
                    return false;
                }
            }
            else
            {
                desiredReturnType = context.SemanticModel.GetTypeInfo(returnType).Type;
                if (desiredReturnType == null)
                {
                    return false;
                }
            }

            var conversion = context.SemanticModel.Compilation.ClassifyConversion(fixedExpressionType, desiredReturnType);
            if (!conversion.Exists || !conversion.IsImplicit)
            {
                return false;
            }

            // Check that the await is the ONLY await in this branch (no other awaits before it)
            // Exclude awaits under lambdas or nested local functions
            var awaitsInBranch = branchStatement.DescendantNodes()
                .OfType<AwaitExpressionSyntax>()
                .Where(a => !IsUnderNestedFunction(a, functionNode))
                .ToList();

            if (awaitsInBranch.Count != 1)
            {
                return false;
            }

            terminalAwaits.Add(awaitExpr);
            return true;
        }

        /// <summary>
        /// Checks if a node is under a lambda or nested local function within the given function node.
        /// </summary>
        private static bool IsUnderNestedFunction(SyntaxNode node, SyntaxNode functionNode)
        {
            var current = node.Parent;
            while (current != null && current != functionNode)
            {
                if (current is LambdaExpressionSyntax || current is LocalFunctionStatementSyntax)
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }

    }
}
