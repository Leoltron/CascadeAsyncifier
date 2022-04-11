using System;
using System.Linq;
using System.Threading;
using CascadeAsyncifier.Utils;
using CascadeAsyncifier.Extensions;
using Microsoft.CodeAnalysis;

namespace CascadeAsyncifier.Helpers
{
    public class MethodCompareHelper
    {
        private readonly AwaitableChecker awaitableChecker;
        private readonly INamedTypeSymbol cancellationTokenSymbol;

        public MethodCompareHelper(Compilation compilation)
        {
            awaitableChecker = new AwaitableChecker(compilation);
            cancellationTokenSymbol = compilation.GetTypeByMetadataName(typeof(CancellationToken).FullName!);
        }

        public bool IsAsyncVersionOf(IMethodSymbol method, IMethodSymbol asyncMethod, bool ignoreName = false, bool treatExtensionMethodsAsReduced = true)
        {
            if (method.IsAsync)
                throw new ArgumentException("Argument must be a sync method", nameof(method));

            if (!ignoreName && method.Name + "Async" != asyncMethod.Name)
                return false;

            if (!IsReturnTypeAnAsyncVersionOf(method, asyncMethod))
                return false;

            if (!CompareArguments(method, asyncMethod, treatExtensionMethodsAsReduced))
                return false;

            return true;
        }

        private bool IsReturnTypeAnAsyncVersionOf(IMethodSymbol method, IMethodSymbol asyncMethod)
        {
            if (method.ReturnsVoid && awaitableChecker.IsTask(asyncMethod.ReturnType))
                return true;

            if (!awaitableChecker.IsGenericTask(asyncMethod.ReturnType))
                return false;


            if (!CompareTypeSymbolsIgnoreGenericConstraints(method.ReturnType,
                                                            ((INamedTypeSymbol)asyncMethod.ReturnType).TypeArguments
                                                           .First()))
                return false;

            return true;
        }

        private static bool CompareTypeSymbolsIgnoreGenericConstraints(ITypeSymbol one, ITypeSymbol other)
        {
            if (one == null || other == null)
                return false;

            if (one.GetType() != other.GetType())
                return false;

            switch (one)
            {
                case INamedTypeSymbol namedOne:
                    var namedOther = (INamedTypeSymbol)other;
                    if (namedOne.IsGenericType)
                    {
                        return namedOne.Arity == namedOther.Arity &&
                               namedOne.OriginalDefinition.SymbolEquals(namedOther.OriginalDefinition) &&
                               namedOne.TypeArguments.Zip(
                                   namedOther.TypeArguments,
                                   CompareTypeSymbolsIgnoreGenericConstraints).All(e => e);
                    }
                    else
                    {
                        return namedOne.SymbolEquals(namedOther);
                    }
                case ITypeParameterSymbol:
                    return true;
                case IArrayTypeSymbol arrayTypeSymbol:
                    var otherArrayTypeSymbol = (IArrayTypeSymbol)other;
                    return arrayTypeSymbol.Rank == otherArrayTypeSymbol.Rank &&
                           CompareTypeSymbolsIgnoreGenericConstraints(arrayTypeSymbol.ElementType,
                                                                      otherArrayTypeSymbol.ElementType);
                default:
                    return false;
            }
        }

        private bool CompareArguments(IMethodSymbol one, IMethodSymbol other, bool treatExtensionMethodsAsReduced)
        {
            if (one.IsGenericMethod != other.IsGenericMethod)
                return false;
            
            var oneParameters = one.Parameters;
            if (one.IsExtensionMethod && !other.IsExtensionMethod && treatExtensionMethodsAsReduced)
            {
                oneParameters = oneParameters.RemoveAt(0);
            }
            var otherParameters = other.Parameters;
            if (other.IsExtensionMethod && !one.IsExtensionMethod && treatExtensionMethodsAsReduced)
            {
                otherParameters = otherParameters.RemoveAt(0);
            }

            if (oneParameters.Length != otherParameters.Length)
            {
                if (Math.Abs(oneParameters.Length - otherParameters.Length) > 1)
                    return false;

                var extraParam = oneParameters.Length > otherParameters.Length
                    ? oneParameters.Last()
                    : otherParameters.Last();

                if (extraParam.Type.SymbolEquals(cancellationTokenSymbol))
                {
                    if (oneParameters.Length > otherParameters.Length)
                    {
                        oneParameters = oneParameters.RemoveAt(oneParameters.Length - 1);
                    }
                    else
                    {
                        otherParameters = otherParameters.RemoveAt(otherParameters.Length - 1);
                    }
                }
            }

            return oneParameters.SequencesEqual(otherParameters, one.IsGenericMethod
                                                     ? (oneParam, otherParam) =>
                                                         CompareParameterSymbolsModifiers(oneParam, otherParam) &&
                                                         CompareTypeSymbolsIgnoreGenericConstraints(
                                                             oneParam.Type, otherParam.Type)
                                                     : (oneParam, otherParam) =>
                                                         CompareParameterSymbolsModifiers(oneParam, otherParam) &&
                                                         oneParam.Type.SymbolEquals(otherParam.Type));
        }

        private static bool CompareParameterSymbolsModifiers(IParameterSymbol one, IParameterSymbol other)
        {
            return one.IsOptional == other.IsOptional && one.IsParams == other.IsParams;
        }
    }
}
