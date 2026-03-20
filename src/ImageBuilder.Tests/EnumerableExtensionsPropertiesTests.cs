// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using CsCheck;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public sealed class EnumerableExtensionsPropertiesTests
{
    private const int PropertyIterationCount = 250;

    private static readonly Gen<int> SmallIntGen = Gen.Int[-10, 10];
    private static readonly Gen<int[]> SmallIntArrayGen = SmallIntGen.Array[0, 20];

    [Fact]
    public void AreEquivalent_IsReflexive()
    {
        Check.Sample(
            SmallIntArrayGen,
            source => source.AreEquivalent(source).ShouldBeTrue(),
            iter: PropertyIterationCount);
    }

    [Fact]
    public void AreEquivalent_IsSymmetric()
    {
        Check.Sample(
            Gen.Select(SmallIntArrayGen, SmallIntArrayGen),
            input =>
            {
                int[] source = input.Item1;
                int[] items = input.Item2;

                source.AreEquivalent(items).ShouldBe(items.AreEquivalent(source));
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void AreEquivalent_IgnoresOrdering()
    {
        Check.Sample(
            SmallIntArrayGen,
            source =>
            {
                int[] reversed = [.. source.Reverse()];

                source.AreEquivalent(reversed).ShouldBeTrue();
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void AreEquivalent_ReturnsFalseWhenCountsDiffer()
    {
        Check.Sample(
            Gen.Select(SmallIntArrayGen, SmallIntGen),
            input =>
            {
                int[] source = input.Item1;
                int item = input.Item2;

                source.AreEquivalent([.. source, item]).ShouldBeFalse();
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void IsSubsetOf_IsReflexive()
    {
        Check.Sample(
            SmallIntArrayGen,
            source => source.IsSubsetOf(source).ShouldBeTrue(),
            iter: PropertyIterationCount);
    }

    [Fact]
    public void IsSubsetOf_IsTransitiveForConstructedSupersets()
    {
        Check.Sample(
            Gen.Select(SmallIntArrayGen, SmallIntArrayGen, SmallIntArrayGen),
            input =>
            {
                int[] subset = input.Item1;
                int[] middle = [.. subset, .. input.Item2];
                int[] superset = [.. middle, .. input.Item3];

                subset.IsSubsetOf(middle).ShouldBeTrue();
                middle.IsSubsetOf(superset).ShouldBeTrue();
                subset.IsSubsetOf(superset).ShouldBeTrue();
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void AppendIf_AppendsTheItemWhenTheConditionIsTrue()
    {
        Check.Sample(
            Gen.Select(SmallIntArrayGen, SmallIntGen),
            input =>
            {
                int[] source = input.Item1;
                int item = input.Item2;
                int[] appended = [.. source.AppendIf(item, () => true)];

                appended.ShouldBe([.. source, item]);
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void AppendIf_LeavesTheSequenceUnchangedWhenTheConditionIsFalse()
    {
        Check.Sample(
            Gen.Select(SmallIntArrayGen, SmallIntGen),
            input =>
            {
                int[] source = input.Item1;
                int item = input.Item2;
                int[] notAppended = [.. source.AppendIf(item, () => false)];

                notAppended.ShouldBe(source);
            },
            iter: PropertyIterationCount);
    }
}
