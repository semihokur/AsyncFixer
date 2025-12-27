using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace AsyncFixer
{
    public static class Helpers
    {
        public static bool IsTaskCreationMethod(this IMethodSymbol symbol)
        {
            return symbol.ToString().Contains("System.Threading.Tasks.Task.Start")
                   || symbol.ToString().Contains("System.Threading.Tasks.Task.Run")
                   || symbol.ToString().Contains("System.Threading.Tasks.TaskFactory.StartNew")
                   || symbol.ToString().Contains("System.Threading.Tasks.TaskEx.RunEx")
                   || symbol.ToString().Contains("System.Threading.Tasks.TaskEx.Run")
                   || symbol.ToString().Contains("StartNewTask")
                   || symbol.ToString().Contains("StartNewTaskWithoutExceptionHandling");
        }

        public static bool IsTask(this ITypeSymbol type)
        {
            return type.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" &&
                (type.Name == "Task" || type.Name == "ValueTask");
        }

        public static bool ReturnTask(this IMethodSymbol symbol)
        {
            return !symbol.ReturnsVoid && symbol.ReturnType.IsTask();
        }

        public static bool IsAsync(this MethodDeclarationSyntax method)
        {
            return method.Modifiers.ToString().Contains("async");
        }

        public static bool IsTestMethod(this MethodDeclarationSyntax method)
        {
            return method.AttributeLists.Any(a => a.Attributes.ToString().Contains("TestMethod"));
        }

        public static bool HasEventArgsParameter(this MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            if (method.ParameterList == null)
            {
                return false;
            }

            foreach (var param in method.ParameterList.Parameters)
            {
                if (param.Type == null)
                {
                    continue;
                }

                var typeInfo = semanticModel.GetTypeInfo(param.Type);
                var type = typeInfo.Type;

                if (type == null)
                {
                    continue;
                }

                // Check if the type inherits from System.EventArgs
                var current = type;
                while (current != null)
                {
                    if (current.ContainingNamespace?.ToDisplayString() == "System" && current.Name == "EventArgs")
                    {
                        return true;
                    }
                    current = current.BaseType;
                }
            }

            return false;
        }

        public static bool HasObjectStateParameter(this MethodDeclarationSyntax method)
        {
            // If method in this form async void Xyz(object state) { ..}, ignore it!
            return method.ParameterList != null && method.ParameterList.Parameters.Count == 1 &&
                   method.ParameterList.Parameters.First().Type.ToString() == "object";
        }

        public static bool ReturnsVoid(this MethodDeclarationSyntax method)
        {
            return method.ReturnType.ToString() == "void";
        }

        public static bool IsImplicitTypeCasting(this TypeInfo typeInfo)
        {
            return !SymbolEqualityComparer.Default.Equals(typeInfo.Type, typeInfo.ConvertedType);
        }

        public static ExpressionSyntax RemoveConfigureAwait(ExpressionSyntax expression)
        {
            var invoc = expression as InvocationExpressionSyntax;
            if (invoc != null)
            {
                var expr = invoc.Expression as MemberAccessExpressionSyntax;
                if (expr != null && expr.Name.Identifier.ValueText == "ConfigureAwait")
                {
                    return expr.Expression;
                }
            }

            return expression;
        }

        public static bool IsEventHandler(this IOperation operation)
        {
            while (operation != null)
            {
                if (operation is IEventAssignmentOperation)
                {
                    return true;
                }

                operation = operation.Parent;
            }

            return false;
        }

        public static T FirstAncestorOrSelfUnderGivenNode<T>(this SyntaxNode node, SyntaxNode parent)
            where T : SyntaxNode
        {
            var current = node;

            while (current != null && current != parent)
            {
                var temp = current as T;
                if (temp != null)
                {
                    return temp;
                }

                current = current.Parent;
            }

            return null;
        }

        /// <summary>
        /// Replace all old nodes in the given pairs with their corresponding new nodes.
        /// </summary>
        /// <typeparam name="T">
        /// Subtype of SyntaxNode that supports the
        /// replacement of descendent nodes.
        /// </typeparam>
        /// <param name="node">
        /// The SyntaxNode or subtype to operate on.
        /// </param>
        /// <param name="syntaxReplacementPairs">
        /// The SyntaxNodeReplacementPair
        /// instances that each contain both the old node that is to be
        /// replaced, and the new node that will replace the old node.
        /// </param>
        /// <returns>
        /// The SyntaxNode that contains all the replacmeents.
        /// </returns>
        public static T ReplaceAll<T>(this T node, params SyntaxReplacementPair[] syntaxReplacementPairs)
            where T : SyntaxNode
        {
            return node.ReplaceNodes(
                syntaxReplacementPairs.Select(pair => pair.OldNode),
                (oldNode, newNode) => syntaxReplacementPairs.First(pair => pair.OldNode == oldNode).NewNode
                );
        }

        /// <summary>
        /// Replace all old nodes in the given pairs with their corresponding new nodes.
        /// </summary>
        /// <typeparam name="T">
        /// Subtype of SyntaxNode that supports the
        /// replacement of descendent nodes.
        /// </typeparam>
        /// <param name="node">
        /// The SyntaxNode or subtype to operate on.
        /// </param>
        /// <param name="replacementPairs">
        /// The SyntaxNodeReplacementPair
        /// instances that each contain both the old node that is to be
        /// replaced, and the new node that will replace the old node.
        /// </param>
        /// <returns>
        /// The SyntaxNode that contains all the replacmeents.
        /// </returns>
        public static T ReplaceAll<T>(this T node, IEnumerable<SyntaxReplacementPair> replacementPairs)
            where T : SyntaxNode
        {
            return node.ReplaceNodes(
                replacementPairs.Select(pair => pair.OldNode),
                (oldNode, newNode) => replacementPairs.First(pair => pair.OldNode == oldNode).NewNode
                );
        }

        /// <summary>
        /// Return the names of all variables that are read-accessed in the given statement.
        /// </summary>
        /// <remarks>
        /// The method iteratively goes through the definitions to find implicitly-accessed variables. 
        /// </remarks>
        public static IEnumerable<string> GetAccessedVariableNamesWithPointsToAnalysis(SemanticModel semanticModel, SyntaxNode root, SyntaxNode node, int depth = 0)
        {
            if (depth == 5 || node == null || root == null)
            {
                // Put a limit for the call stack frame
                yield break;
            }

            var dataFlowResult = semanticModel.AnalyzeDataFlow(node);
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

    /// <summary>
    /// Pair of old and new SyntaxNodes for ReplaceAll.
    /// </summary>
    public sealed class SyntaxReplacementPair
    {
        /// <summary>
        /// The node that will replace the old node.
        /// </summary>
        public readonly SyntaxNode NewNode;

        /// <summary>
        /// The node that must be replaced.
        /// </summary>
        public readonly SyntaxNode OldNode;

        public SyntaxReplacementPair(SyntaxNode oldNode, SyntaxNode newNode)
        {
            if (oldNode == null) throw new ArgumentNullException(nameof(oldNode));
            if (newNode == null) throw new ArgumentNullException(nameof(newNode));

            OldNode = oldNode;
            NewNode = newNode;
        }
    }
}
