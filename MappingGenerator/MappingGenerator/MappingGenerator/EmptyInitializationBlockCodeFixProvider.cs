using System;
using System.Composition;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace MappingGenerator
{

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyInitializationBlockCodeFixProvider)), Shared]
    public class EmptyInitializationBlockCodeFixProvider : CodeFixProvider
    {
        private const string TitleForLocal = "Initialize with local variables";
        private const string TitleForLambda = "Initialize with lambda parameter";
        private const string TitleForScaffolding = "Initialize with sample values";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EmptyInitializationBlockAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            var objectInitializer = token.Parent.FindContainer<InitializerExpressionSyntax>();
            if (objectInitializer == null)
            {
                return;
            }
            
            context.RegisterCodeFix(CodeAction.Create(title: TitleForLocal, createChangedDocument: c => InitizalizeWithLocals(context.Document, objectInitializer, c), equivalenceKey: TitleForLocal), diagnostic);
            context.RegisterCodeFix(CodeAction.Create(title: TitleForScaffolding, createChangedDocument: c => InitizalizeWithDefaults(context.Document, objectInitializer, c), equivalenceKey: TitleForScaffolding), diagnostic);

            var lambda = objectInitializer.Parent.FindContainer<LambdaExpressionSyntax>();

            switch (lambda)
            {
                case ParenthesizedLambdaExpressionSyntax parenthesizedLambda when  parenthesizedLambda.ParameterList.Parameters.Count == 1:
                case SimpleLambdaExpressionSyntax _:
                    context.RegisterCodeFix(CodeAction.Create(title: TitleForLambda, createChangedDocument: c => InitizalizeWithLambdaParameter(context.Document, lambda, objectInitializer, c), equivalenceKey: TitleForLambda), diagnostic);
                    break;
            }
        }

        private async Task<Document> InitizalizeWithDefaults(Document document, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var mappingSourceFinder = new ScaffoldingSourceFinder(SyntaxGenerator.GetGenerator(document), semanticModel);
            return await ReplaceEmptyInitializationBlock(document, objectInitializer, cancellationToken, semanticModel, mappingSourceFinder);
        }

        private async Task<Document> InitizalizeWithLocals(Document document, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var mappingSourceFinder = new LocalScopeMappingSourceFinder(semanticModel, objectInitializer, SyntaxGenerator.GetGenerator(document));
            return await ReplaceEmptyInitializationBlock(document, objectInitializer, cancellationToken, semanticModel, mappingSourceFinder);
        }

        private async Task<Document> InitizalizeWithLambdaParameter(Document document, LambdaExpressionSyntax lambdaSyntax, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var generator = SyntaxGenerator.GetGenerator(document);

            var lambdaSymbol = semanticModel.GetSymbolInfo(lambdaSyntax).Symbol as IMethodSymbol;
            var firstArgument = lambdaSymbol.Parameters.First();
            var mappingSourceFinder = new ObjectMembersMappingSourceFinder(firstArgument.Type, generator.IdentifierName(firstArgument.Name), generator, semanticModel);
            return await ReplaceEmptyInitializationBlock(document, objectInitializer, cancellationToken, semanticModel, mappingSourceFinder);
        }

        private static async Task<Document> ReplaceEmptyInitializationBlock(Document document, InitializerExpressionSyntax objectInitializer, CancellationToken cancellationToken, SemanticModel semanticModel,  IMappingSourceFinder mappingSourceFinder)
        {
            var oldObjCreation = objectInitializer.FindContainer<ObjectCreationExpressionSyntax>();
            var createdObjectType = ModelExtensions.GetTypeInfo(semanticModel, oldObjCreation).Type;
            var newObjectCreation = oldObjCreation.AddInitializerWithMapping(mappingSourceFinder,createdObjectType);
            return await document.ReplaceNodes(oldObjCreation, newObjectCreation, cancellationToken);
        }
    }
}
