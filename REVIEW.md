# Review Notes

## Generator concerns

The generators are the part of the PR I am most concerned about because their quality directly determines how well the property tests discover edge cases.

`DigestHash` currently builds random SHA-looking strings from hex characters. That is probably enough for parity tests where digests are treated as opaque strings, but it does not generate content-derived or adversarial digest cases. The `Layer_GeneratesValidDigestFormat` smoke test could also be simpler and clearer as a regex-style assertion for `sha256:[a-f0-9]{64}`.

`SimpleTag` may be too narrow. It only generates version/OS/architecture-shaped tags, but Docker tags can be much broader. The merge and identity code mostly cares about tag emptiness, ordering, union, and replacement, so this may be sufficient for some parity checks, but it under-samples weird-but-valid tag strings.

More generally, the generators appear optimized for realistic image-info graphs rather than adversarial inputs. That is reasonable for old-vs-new parity, but the suite may benefit from a second edge-biased generator layer covering duplicates, strange sort orders, empty/null combinations, broader tag shapes, identity collisions, and empty platform lists.

## Property-testing and metamorphic-testing concerns

Some tests are property-based in style, but a few important behaviors are still covered only with hand-picked examples:

- `AreProductVersionsEquivalent_MatchesKnownCases`
- `GetMajorMinorVersion_ExtractsCorrectly`

Both look like good candidates for actual generated property coverage.

The tests are also metamorphic in spirit because they compare old behavior to new behavior, but I do not see use of CsCheck's `GenMetamorphic<T>` or `SampleMetamorphic<T>`. If the goal is explicitly metamorphic testing, it is worth discussing whether the suite should use CsCheck's metamorphic APIs directly.

## Merge migration test shape

The merge migration tests currently compare old mutable merge behavior with new immutable merge behavior by converting generated old-model objects to V2 through local `ConvertToV2()` helpers.

That works as scaffolding, but it means the conversion helper becomes part of the proof. A cleaner equivalence boundary would be to compare two pure functions shaped roughly like:

```text
(sourceJson, targetJson) => mergedJson
```

One function would deserialize and merge through the old implementation; the other would deserialize and merge through the new V2 implementation. That would test the externally observable merge behavior more directly and reduce the risk that `ConvertToV2()` accidentally hides differences.

### `ImageInfoMerger` has an invalid comparer for empty-platform images

`CompareFirstPlatforms` returns `1` both when `firstA` is null and `firstB` is non-null, and when `firstA` is non-null and `firstB` is null:

- `src/ImageBuilder/Services/ImageInfoMerger.cs:258-263`

That violates comparer antisymmetry. As a result, `result.Sort(CompareImages)` can behave unpredictably or throw if image data includes an image with no platforms.

This case is reachable because `V2.ImageData.Platforms` defaults to an empty list, and existing serialization tests already treat empty-platform image data as valid serialized input. The current generators do not catch this because they always generate `ImageData` with one to three platforms.

The likely fix is to make the non-null-vs-null branch return the opposite sign, e.g. `firstB is null ? -1 : ComparePlatforms(firstA, firstB)`, and add a test that sorts/merges images where one side has an empty platform list.
