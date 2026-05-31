using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Linq;

namespace Bipolar.EventSerialization.SourceGeneration
{
    public static class Utility
    {
        public static bool IsSerializeEventAttributeName(string name)
        {
            return name is "SerializeEventAttribute" or "SerializeEvent";
        }

        public static bool IsSerializeEventAttribute(INamedTypeSymbol? attributeClass)
        {
            return attributeClass is not null
                && attributeClass.ContainingNamespace.Name == "Bipolar"
                && IsSerializeEventAttributeName(attributeClass.Name); 
        }

        public static bool HasSerializeEventAttribute(ISymbol symbol)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass is null)
                    continue;

                if (IsSerializeEventAttribute(attributeClass))
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
            if (symbol is IEventSymbol)
                return true;

            var type = GetSymbolType(symbol);
            if (type is null || type.TypeKind != TypeKind.Delegate)
                return false;

            bool isVoidReturnType = ((INamedTypeSymbol)type).GetMembers("Invoke")
                .OfType<IMethodSymbol>()
                .Any(m => m.ReturnType.SpecialType == SpecialType.System_Void);

            return isVoidReturnType;
        }

        public static ImmutableArray<IParameterSymbol> GetDelegateParameters(ISymbol symbol)
        {
            var type = GetSymbolType(symbol);
            if (type is INamedTypeSymbol named && type.TypeKind == TypeKind.Delegate)
                foreach (var member in named.GetMembers("Invoke"))
                    if (member is IMethodSymbol invoke)
                        return invoke.Parameters;

            return ImmutableArray<IParameterSymbol>.Empty;
        }

        public static string GetSerializedEventName(ISymbol symbol)
        {
            foreach (var attribute in symbol.GetAttributes())
            {

                if (IsSerializeEventAttribute(attribute.AttributeClass))
                {
                    var customName = attribute.ConstructorArguments.FirstOrDefault();
                    if (customName.IsNull == false && customName.Value is string name && SyntaxFacts.IsValidIdentifier(name)) 
                        return name;
                }
            }
            return $"_{symbol.Name}Event";
        }

        public static int GetParametersCount(ITypeSymbol? type)
        {
            if (type is not null && type.TypeKind == TypeKind.Delegate)
            {
                foreach (var member in ((INamedTypeSymbol)type).GetMembers("Invoke"))
                {
                    if (member is not IMethodSymbol invokeMethod)
                        continue;

                    var parameters = invokeMethod.Parameters;
                    return parameters.Length;
                }
            }

            return 0;
        }
    }
}
