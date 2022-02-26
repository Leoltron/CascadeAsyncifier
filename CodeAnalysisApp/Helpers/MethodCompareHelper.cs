﻿using System;
using System.Linq;
using CodeAnalysisApp.Utils;
using Microsoft.CodeAnalysis;

namespace CodeAnalysisApp.Helpers
{
    public class MethodCompareHelper
    {
        private readonly AwaitableChecker awaitableChecker;

        public MethodCompareHelper(AwaitableChecker awaitableChecker)
        {
            this.awaitableChecker = awaitableChecker;
        }

        public bool IsAsyncVersionOf(IMethodSymbol method, IMethodSymbol asyncMethod, bool ignoreName = false)
        {
            if (method.IsAsync)
                throw new ArgumentException("Argument must be a sync method", nameof(method));

            if (!ignoreName && method.Name + "Async" != asyncMethod.Name)
                return false;

            if (!IsReturnTypeAnAsyncVersionOf(method, asyncMethod))
                return false;

            if (!CompareArguments(method, asyncMethod))
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

        private static bool CompareArguments(IMethodSymbol one, IMethodSymbol other)
        {
            if (one.IsGenericMethod != other.IsGenericMethod)
                return false;

            return one.Parameters.SequencesEqual(other.Parameters, one.IsGenericMethod
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
