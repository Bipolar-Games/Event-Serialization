using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Bipolar.EventSerialization.SourceGeneration
{
    public static class Utility
    {
        /// <summary>
        /// Returns true when the symbol carries [SerializeEvent] (or any attribute whose
        /// unqualified name matches — tolerant of namespace differences during early
        /// compilation stages).
        /// </summary>
        public static bool HasSerializeEventAttribute(ISymbol symbol)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass is null)
                    continue;

                if (attributeClass.ContainingNamespace.Name == "Bipolar"
                    && (attributeClass.Name is "SerializeEventAttribute" or "SerializeEvent"))
                    return true;
            }

            return false;
        }

        public static ITypeSymbol? GetSymbolType(ISymbol symbol)
        {
            return symbol switch
            {
                IEventSymbol evt => evt.Type,
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null
            };
        }

        public static bool IsVoidDelegate(ISymbol symbol)
        {
            if (symbol is IEventSymbol eventSymbol)
                return true;

            ITypeSymbol? type = GetSymbolType(symbol);

            if (type is null || type.TypeKind != TypeKind.Delegate)
                return false;

            foreach (var member in ((INamedTypeSymbol)type).GetMembers("Invoke"))
                if (member is IMethodSymbol invoke)
                    return invoke.ReturnType.SpecialType == SpecialType.System_Void;

            return false;
        }

        public static ImmutableArray<IParameterSymbol> GetDelegateParameters(ISymbol symbol)
        {
            ITypeSymbol? type = GetSymbolType(symbol);
            if (type is not INamedTypeSymbol named || type.TypeKind != TypeKind.Delegate)
                return ImmutableArray<IParameterSymbol>.Empty;

            foreach (var member in named.GetMembers("Invoke"))
                if (member is IMethodSymbol invoke)
                    return invoke.Parameters;

            return ImmutableArray<IParameterSymbol>.Empty;
        }
    }
}
