// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CsCheck;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public sealed class StringExtensionsPropertiesTests
{
    private const int PropertyIterationCount = 250;
    private const string LetterCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string AlphaNumericCharacters = LetterCharacters + "0123456789";
    private const string KeyValueCharacters = AlphaNumericCharacters + "._-/";
    private const string LineEndingCharacters = AlphaNumericCharacters + "\r\n";

    private static readonly string[] LineEndingFormats =
    [
        "target",
        "target\n",
        "target\r\n",
        "target\n\n",
        "target\r\n\r\n",
    ];

    private static readonly Gen<string> AlphaNumericStringGen = Gen.Char[AlphaNumericCharacters]
        .Array[0, 40]
        .Select(characters => new string(characters));

    private static readonly Gen<string> NonEmptyAlphaNumericStringGen = Gen.Char[AlphaNumericCharacters]
        .Array[1, 8]
        .Select(characters => new string(characters));

    private static readonly Gen<string> NonEmptyLetterStringGen = Gen.Char[LetterCharacters]
        .Array[1, 40]
        .Select(characters => new string(characters));

    private static readonly Gen<string> KeyValueStringGen = Gen.Char[KeyValueCharacters]
        .Array[0, 40]
        .Select(characters => new string(characters));

    private static readonly Gen<string> LineEndingInputGen = Gen.Char[LineEndingCharacters]
        .Array[1, 40]
        .Select(characters => new string(characters));

    private static readonly Gen<string> LineEndingFormatGen = Gen.Int[0, LineEndingFormats.Length - 1]
        .Select(index => LineEndingFormats[index]);

    [Fact]
    public void TrimEndString_IsIdempotentAndRemovesTrailingCopies()
    {
        Check.Sample(
            Gen.Select(AlphaNumericStringGen, NonEmptyAlphaNumericStringGen),
            input =>
            {
                string source = input.Item1;
                string trimString = input.Item2;
                string onceTrimmed = source.TrimEndString(trimString);
                string twiceTrimmed = onceTrimmed.TrimEndString(trimString);

                twiceTrimmed.ShouldBe(onceTrimmed);
                onceTrimmed.EndsWith(trimString, StringComparison.Ordinal).ShouldBeFalse();
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void TrimStartString_IsIdempotentAndRemovesLeadingCopies()
    {
        Check.Sample(
            Gen.Select(AlphaNumericStringGen, NonEmptyAlphaNumericStringGen),
            input =>
            {
                string source = input.Item1;
                string trimString = input.Item2;
                string onceTrimmed = source.TrimStartString(trimString);
                string twiceTrimmed = onceTrimmed.TrimStartString(trimString);

                twiceTrimmed.ShouldBe(onceTrimmed);
                onceTrimmed.StartsWith(trimString, StringComparison.Ordinal).ShouldBeFalse();
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void FirstCharToUpper_UppercasesOnlyTheFirstCharacter()
    {
        Check.Sample(
            NonEmptyLetterStringGen,
            source =>
            {
                string transformed = source.FirstCharToUpper();

                transformed.Length.ShouldBe(source.Length);
                transformed[0].ShouldBe(char.ToUpper(source[0]));
                transformed[1..].ShouldBe(source[1..]);
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void ToCamelCase_LowercasesOnlyTheFirstCharacter()
    {
        Check.Sample(
            NonEmptyLetterStringGen,
            source =>
            {
                string transformed = source.ToCamelCase();

                transformed.Length.ShouldBe(source.Length);
                transformed[0].ShouldBe(char.ToLowerInvariant(source[0]));
                transformed[1..].ShouldBe(source[1..]);
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void DiffersAtIndex_ReturnsMinusOneForIdenticalStrings()
    {
        Check.Sample(
            AlphaNumericStringGen,
            source => source.DiffersAtIndex(source).ShouldBe(-1),
            iter: PropertyIterationCount);
    }

    [Fact]
    public void DiffersAtIndex_IdentifiesTheFirstDifferenceOrLengthMismatch()
    {
        Check.Sample(
            Gen.Select(AlphaNumericStringGen, AlphaNumericStringGen),
            input =>
            {
                string source = input.Item1;
                string other = input.Item2;
                int differenceIndex = source.DiffersAtIndex(other);

                if (differenceIndex == -1)
                {
                    source.ShouldBe(other);
                    return;
                }

                differenceIndex.ShouldBeInRange(0, Math.Max(source.Length, other.Length) - 1);
                source[..differenceIndex].ShouldBe(other[..differenceIndex]);

                if (differenceIndex == Math.Min(source.Length, other.Length))
                {
                    source.Length.ShouldNotBe(other.Length);
                    return;
                }

                source[differenceIndex].ShouldNotBe(other[differenceIndex]);
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void NormalizeLineEndings_IsIdempotentForTheSameTargetFormat()
    {
        Check.Sample(
            Gen.Select(LineEndingInputGen, LineEndingFormatGen),
            input =>
            {
                string value = input.Item1;
                string targetFormat = input.Item2;
                string normalized = value.NormalizeLineEndings(targetFormat);
                string renormalized = normalized.NormalizeLineEndings(targetFormat);

                renormalized.ShouldBe(normalized);

                if (targetFormat[^1] == '\n')
                {
                    normalized[^1].ShouldBe('\n');
                }
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void ParseKeyValuePair_RoundTripsTheOriginalKeyAndValue()
    {
        Check.Sample(
            Gen.Select(KeyValueStringGen, KeyValueStringGen),
            input =>
            {
                string key = input.Item1;
                string value = input.Item2;
                string pair = $"{key}={value}";
                (string Key, string Value) parsed = pair.ParseKeyValuePair('=');

                parsed.Key.ShouldBe(key);
                parsed.Value.ShouldBe(value);
            },
            iter: PropertyIterationCount);
    }
}
