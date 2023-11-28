using System.Collections.Generic;
using System.Linq;

namespace SimpleGraphQL.GraphQLParser.AST
{
    public abstract class ASTNode
    {
        public abstract ASTNodeKind Kind { get; }

        public GraphQLLocation Location { get; set; }

        public GraphQLComment Comment { get; set; }
        
    }
    
    public static class ASTNodeExtensions
    {
        public static IEnumerable<ASTNode> Descendants(this ASTNode root)
        {
            var nodes = new Stack<ASTNode>(new[] {root});
            while (nodes.Any())
            {
                ASTNode node = nodes.Pop();
                yield return node;

                GraphQLSelectionSet selectionSet = null;
                
                if (node is GraphQLFieldSelection fieldSelection)
                {
                    selectionSet = fieldSelection.SelectionSet;
                }

                if (node is GraphQLOperationDefinition operationDefinition)
                {
                    selectionSet = operationDefinition.SelectionSet;
                }
                
                if(selectionSet != null && selectionSet.Selections != null)
                { 
                    foreach (var n in selectionSet.Selections) 
                        nodes.Push(n);
                }
            }
        }

    }
}
