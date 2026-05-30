using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Bipolar.EventSerialization.SourceGeneration
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal sealed class SerializeEventAnalyzer : DiagnosticAnalyzer
    {
        // -------------------------------------------------------------------------
        // Diagnostics
        // -------------------------------------------------------------------------

        /// <summary>
        /// Fired when [SerializeEvent] is placed on a field/property whose type is
        /// NOT void delegate (e.g. System.Action or System.Action<> or delegate void).
        /// </summary>
        public static readonly DiagnosticDescriptor InvalidDelegateType = new(
            id: "BSE001",
            title: "Invalid type for [SerializeEvent]",
            messageFormat: "'{0}' must be a void delegate (e.g. System.Action) to use [SerializeEvent]",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description:
                "[SerializeEvent] may only be applied to events, or to fields/properties whose " +
                "type is a delegate with signature 'void' (no return value).");


        /// <summary>
        /// Fired when [SerializeEvent] is placed on a member whose containing class
        /// is not declared as partial.
        /// </summary>
        public static readonly DiagnosticDescriptor NotPartialClass = new(
            id: "BSE002",
            title: "Containing class must be partial",
            messageFormat: "Class '{0}' must be declared as partial because it contains a [SerializeEvent] member",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description:
                "The source generator for [SerializeEvent] emits code into a separate partial class file. " +
                "Every class that contains a [SerializeEvent] member must therefore be declared partial.");

        /// <summary>
        /// Fired when [SerializeEvent] is placed on a member whose invoke function
        /// requires more than 4 parameters.
        /// </summary>
        public static readonly DiagnosticDescriptor TooManyParameters = new(
            id: "BSE003",
            title: "Containing class must be partial",
            messageFormat: "'{0}' has {1} parameters, but UnityEvent supports at most 4",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description:
                "[SerializeEvent] may only be applied to events, or to fields/properties whose " +
                "type is a delegate with at most 4 parameters.");


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(InvalidDelegateType, NotPartialClass, TooManyParameters);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // We analyse completed symbols, not syntax nodes, so we get full type info.
            context.RegisterSymbolAction(AnalyzeFieldOrProperty,
                SymbolKind.Field,
                SymbolKind.Property);

            context.RegisterSymbolAction(AnalyzeEvent, SymbolKind.Event);
        }

        private static void AnalyzeFieldOrProperty(SymbolAnalysisContext ctx)
        {
            var symbol = ctx.Symbol;
            if (Utility.HasSerializeEventAttribute(symbol) == false)
                return;

            ITypeSymbol type = symbol switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null!
            };

            if (IsVoidDelegate(type) == false)
            {
                ReportInvalidDelegateTypeDiagnosticOnMember(ctx, symbol);
            }

            if (IsContainingTypeCorrect(symbol) == false)
            {
                ReportDiagnosticOnContainingClass(ctx, symbol);
            }

            int parametersCount = GetParametersCount(symbol);
            if (parametersCount > 4)
            {
                ReportTooManyParametersDiagnosticOnMember(ctx, symbol, parametersCount);
            }
        }

        private static int GetParametersCount(ISymbol symbol)
        {
            var type = symbol switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                IEventSymbol @event => @event.Type,
                _ => null
            };

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

        private static void AnalyzeEvent(SymbolAnalysisContext ctx)
        {
            if (Utility.HasSerializeEventAttribute(ctx.Symbol) == false)
                return;

            if (IsContainingTypeCorrect(ctx.Symbol) == false)
            {
                ReportDiagnosticOnContainingClass(ctx, ctx.Symbol);
            }
        }


        /// <summary>
        /// Returns true for any delegate type whose Invoke method has:
        ///   – return type  : void
        ///   – parameter list: empty
        ///
        /// This covers System.Action as well as custom `delegate void Foo()` declarations.
        /// </summary>
        private static bool IsVoidDelegate(ITypeSymbol? type)
        {
            if (type is null)
                return false;

            // Must be a delegate.
            if (type.TypeKind != TypeKind.Delegate)
                return false;

            // Retrieve the Invoke method — every delegate has exactly one.
            var namedType = (INamedTypeSymbol)type;
            IMethodSymbol? invokeMethod = null;

            foreach (var member in namedType.GetMembers("Invoke"))
            {
                if (member is IMethodSymbol method)
                {
                    invokeMethod = method;
                    break;
                }
            }

            if (invokeMethod is null)
                return false;

            return invokeMethod.ReturnType.SpecialType == SpecialType.System_Void;
        }

        private static bool IsContainingTypeCorrect(ISymbol memberSymbol)
        {
            INamedTypeSymbol? containingType = memberSymbol.ContainingType;
            if (containingType is null)
                return true; // top-level statement or unexpected context — skip

            foreach (var syntaxRef in containingType.DeclaringSyntaxReferences)
                if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                    if (typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword) == false)
                        return false;

            return true;
        }

        private static void ReportInvalidDelegateTypeDiagnosticOnMember(SymbolAnalysisContext ctx, ISymbol symbol)
        {
            // Report on every declaring syntax reference so the squiggle appears in the
            // right file when partial classes are involved.
            foreach (SyntaxReference syntaxRef in symbol.DeclaringSyntaxReferences)
            {
                SyntaxNode syntax = syntaxRef.GetSyntax(ctx.CancellationToken);

                // For field declarations the symbol points to the variable declarator;
                // walk up to get the full FieldDeclarationSyntax for a better span.
                Location location = syntax switch
                {
                    VariableDeclaratorSyntax declarator
                        when declarator.Parent?.Parent is FieldDeclarationSyntax field
                        => field.GetLocation(),

                    _ => syntax.GetLocation()
                };

                ctx.ReportDiagnostic(Diagnostic.Create(InvalidDelegateType, location, symbol.Name));
            }
        }

        private static void ReportTooManyParametersDiagnosticOnMember(SymbolAnalysisContext ctx, ISymbol symbol, int parametersCount)
        {
            foreach (SyntaxReference syntaxRef in symbol.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax(ctx.CancellationToken);
                var location = syntax switch
                {
                    VariableDeclaratorSyntax declarator
                        when declarator.Parent?.Parent is FieldDeclarationSyntax field
                        => field.GetLocation(),

                    _ => syntax.GetLocation()
                };

                ctx.ReportDiagnostic(Diagnostic.Create(TooManyParameters, location, symbol.Name, parametersCount));
            }
        }

        private static void ReportDiagnosticOnContainingClass(SymbolAnalysisContext ctx, ISymbol memberSymbol)
        {
            if (memberSymbol.ContainingType is null)
                return;

            foreach (var syntaxRef in memberSymbol.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax(ctx.CancellationToken);

                var attributeSyntax = syntax
                    .DescendantNodes()
                    .OfType<AttributeSyntax>()
                    .FirstOrDefault(a => a.Name.ToString() is "SerializeEvent" or "SerializeEventAttribute");

                var location = attributeSyntax?.GetLocation() ?? syntax.GetLocation();
                ctx.ReportDiagnostic(Diagnostic.Create(NotPartialClass, location, memberSymbol.ContainingType.Name));
            }
        }
    }
}
