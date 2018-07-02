using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{
    public class ScaffoldingSourceFinder:IMappingSourceFinder
    {
        private readonly SyntaxGenerator syntaxGenerator;
        private readonly SemanticModel semanticModel;

        public ScaffoldingSourceFinder(SyntaxGenerator syntaxGenerator, SemanticModel semanticModel)
        {
            this.syntaxGenerator = syntaxGenerator;
            this.semanticModel = semanticModel;
        }

        public MappingElement FindMappingSource(string targetName, ITypeSymbol targetType)
        {
            return new MappingElement(syntaxGenerator, semanticModel)
            {
                ExpressionType = targetType,
                Expression = (ExpressionSyntax) GetDefaultExpression(targetType)
            };  
        }

        internal SyntaxNode GetDefaultExpression(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol namedTypeSymbol)
            {
                var enumOptions = namedTypeSymbol.MemberNames.ToList();
                if (enumOptions.Count > 0)
                {
                    return syntaxGenerator.MemberAccessExpression(syntaxGenerator.IdentifierName(namedTypeSymbol.Name), syntaxGenerator.IdentifierName(enumOptions[0]));
                }

                return syntaxGenerator.DefaultExpression(type);
            }

            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return syntaxGenerator.LiteralExpression(true);
                case SpecialType.System_SByte:
                    return syntaxGenerator.LiteralExpression(1);
                case SpecialType.System_Int16:
                    return  syntaxGenerator.LiteralExpression(16);
                case SpecialType.System_Int32:
                    return syntaxGenerator.LiteralExpression(32);
                case SpecialType.System_Int64:
                    return syntaxGenerator.LiteralExpression(64);
                case SpecialType.System_Byte:
                    return syntaxGenerator.LiteralExpression(1);
                case SpecialType.System_UInt16:
                    return syntaxGenerator.LiteralExpression(16u);
                case SpecialType.System_UInt32:
                    return syntaxGenerator.LiteralExpression(32u);
                case SpecialType.System_UInt64:
                    return syntaxGenerator.LiteralExpression(64u);
                case SpecialType.System_Single:
                    return syntaxGenerator.LiteralExpression(1.0f);
                case SpecialType.System_Double:
                    return syntaxGenerator.LiteralExpression(1.0);
                case SpecialType.System_Char:
                    return syntaxGenerator.LiteralExpression('a');
                case SpecialType.System_String:
                    return syntaxGenerator.LiteralExpression("lorem ipsum");
                case SpecialType.System_Decimal:
                    return syntaxGenerator.LiteralExpression("2.0m");
                case SpecialType.System_Object:
                    return  null;
                default:
                    return syntaxGenerator.LiteralExpression("ccc");
            }
        }

    }
}