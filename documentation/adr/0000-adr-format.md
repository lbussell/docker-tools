# ADR format and conventions

We record architecturally significant decisions as Architecture Decision Records (ADRs) in `documentation/adr/`, using lightweight, sequentially numbered files. This ADR documents the format itself so future ADRs stay consistent. (This is ADR 0000, the meta-ADR.)

## Format

- ADRs live in `documentation/adr/` and use sequential numbering with a slug: `0001-slug.md`, `0002-slug.md`, etc.
- Find the next number by scanning `documentation/adr/` for the highest existing number and incrementing by one.
- The body is short — often a single paragraph of 1-3 sentences answering: what's the context, what did we decide, and why. The value is in recording *that* a decision was made and *why*, not in filling out sections.

Template:

```md
# {Short title of the decision}

{1-3 sentences: what's the context, what did we decide, and why.}
```

## Optional sections

Only include these when they add genuine value; most ADRs won't need them.

- **Status** frontmatter (`proposed | accepted | deprecated | superseded by ADR-NNNN`) — useful when decisions are revisited.
- **Considered Options** — only when the rejected alternatives are worth remembering.
- **Consequences** — only when non-obvious downstream effects need to be called out.

## When to write an ADR

Write one only when **all three** are true:

1. **Hard to reverse** — the cost of changing your mind later is meaningful.
2. **Surprising without context** — a future reader will look at the code and wonder "why on earth did they do it this way?"
3. **The result of a real trade-off** — there were genuine alternatives and you picked one for specific reasons.

If a decision is easy to reverse, not surprising, or had no real alternative, skip it.

### What qualifies

- **Architectural shape** — e.g. monorepo, how read/write models are split.
- **Integration patterns between components** — e.g. communicating via events instead of synchronous calls.
- **Technology choices that carry lock-in** — database, message bus, auth provider, deployment target. Not every library, just the ones that would take a quarter to swap out.
- **Boundary and scope decisions** — who owns what data; the explicit no-s are as valuable as the yes-s.
- **Deliberate deviations from the obvious path** — anything where a reasonable reader would assume the opposite, so the next engineer doesn't "fix" something that was intentional.
- **Constraints not visible in the code** — compliance, performance contracts, build-context limitations.
- **Rejected alternatives when the rejection is non-obvious** — so the same suggestion doesn't resurface in six months.

This format is adapted from <https://github.com/mattpocock/skills/blob/main/skills/engineering/grill-with-docs/ADR-FORMAT.md>. Note we use `documentation/adr/` (matching this repo's existing `documentation/` directory) rather than the `docs/adr/` path in the source.
