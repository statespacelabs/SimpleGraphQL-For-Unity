using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using SimpleGraphQL.GraphQLParser;
using SimpleGraphQL.GraphQLParser.AST;

// ifdef for different unity versions
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;

#elif UNITY_2017_1_OR_NEWER
using UnityEditor.Experimental.AssetImporters;
#endif

namespace SimpleGraphQL
{
    [ScriptedImporter(1, "graphqlfrag")]
    public class GraphQLFragmentImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var lexer = new Lexer();
            var parser = new Parser(lexer);
            string contents = File.ReadAllText(ctx.assetPath);
            var queryFile = ScriptableObject.CreateInstance<GraphQLFragmentFile>();

            GraphQLDocument graphQLDocument = parser.Parse(new Source(contents));

            List<GraphQLFragmentDefinition> operations = graphQLDocument.Definitions
                .FindAll(x => x.Kind == ASTNodeKind.FragmentDefinition)
                .Select(x => (GraphQLFragmentDefinition) x)
                .ToList();

            if (operations.Count > 0)
            {
                foreach (GraphQLFragmentDefinition operation in operations)
                {
                    queryFile.Fragment = new Fragment
                                         {
                                             Name = operation.Name?.Value,
                                             TypeCondition = operation.TypeCondition?.Name?.Value,
                                             Source = contents
                                         };
                }
            }
            else
            {
                throw new ArgumentException(
                    $"There were no operation definitions inside this graphql: {ctx.assetPath}\nPlease ensure that there is at least one operation defined!");
            }

            ctx.AddObjectToAsset("FragmentScriptableObject", queryFile);
            ctx.SetMainObject(queryFile);
        }
    }
}