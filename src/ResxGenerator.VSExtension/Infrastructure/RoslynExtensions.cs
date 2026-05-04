using Microsoft.CodeAnalysis;

namespace ResxGenerator.VSExtension.Infrastructure
{
    public static class RoslynExtensions
    {
        private static readonly SymbolDisplayFormat _fullyQualifiedFormat = new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
        );

        public static string ToFullyQualifiedName(this ITypeSymbol symbol)
        {
            return symbol.ToDisplayString(_fullyQualifiedFormat);
        }
    }
}
