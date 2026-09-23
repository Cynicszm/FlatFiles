# Conventions

How code in this repository is written. Anything not covered here follows the surrounding file.

Most of these rules match what the tree already does. Two do not — British spelling and the spacing
inside parentheses — because they were adopted after most of the code was written. **Convert on
touch.** Bring a file into line when you are already changing it for another reason; do not sweep the
codebase to apply a style rule on its own, and do not reformat a file you are only reading. Public API
is the exception: it is never renamed for style, because that breaks callers.

## Target and language

- `net10.0`, single target. The library, the tests and the benchmarks all build against it.
- C# 14, which is the language default for `net10.0`. There is no `LangVersion` pin, so the language
  moves with the SDK.
- Nullable reference types are enabled in the library. Do not silence a warning with `!` unless you
  have actually established the value is non-null — narrow it or add a guard clause instead.
- Prefer collection expressions to the older initialiser forms: `[]`, `[x, y]`, `[.. items]` rather
  than `new[] { … }`, `new List<T> { … }` or `Array.Empty<T>()`. This includes materialising a LINQ
  chain — `List<T> found = [.. items.Where( … )];` rather than a trailing `.ToList()` — which is the
  case most often missed, because there is no `new` keyword to notice.

## Spelling

British English in identifiers, comments, messages and documentation. `Initialise`, not `Initialize`.
`Serialise`, `Behaviour`, `Recognised`.

Two exceptions, both about names that are not ours to change.

Names somebody else owns keep the spelling their author gave them, so `[AssemblyInitialize]` stays as
MSTest declares it even where our own `AssemblyInitialiser` and `Initialise` sit directly around it.
The same goes for BCL members such as `CultureInfo.DefaultThreadCurrentCulture`.

**Published API keeps its spelling until a major version is being cut anyway.** Renaming a public
member breaks every caller for no functional gain, so `QuoteBehavior`, `OptimizeMapping` and the
`isOptimized` parameter kept their American spelling for the whole 7.x line, and a new member added
beside one of them was still spelled the British way — a mixed pair is better than a broken consumer.
8.0.0 was breaking the API for other reasons, which is the condition this rule named, so they became
`QuoteBehaviour`, `OptimiseMapping` and `isOptimised`. No public name is spelled the American way now.
The next one found waits the same way: recorded under **Breaking** in the changelog's planned section,
not fixed on sight. The documentation comments were swept to match in the same release, so nothing the library
ships is spelled the American way now.

The changelog entries written before this rule was adopted are left as their authors wrote them.

## Formatting

- Four spaces, never tabs. CRLF line endings, final newline present, UTF-8 with BOM for `.cs`.
- Block-scoped namespaces, not file-scoped.
- `using` directives outside the namespace.
- **A space inside non-empty parentheses**, in calls and declarations alike:

  ```csharp
  public static void Initialise( TestContext context )
  {
      var culture = new CultureInfo( "en-US" );
  }
  ```

  Empty parameter and argument lists stay tight: `CreateAll()`, `new StringWriter()`.
- XML doc content is indented four spaces past the `///`:

  ```csharp
  /// <summary>
  ///     Pins the culture used by every test in the assembly.
  /// </summary>
  ```

- `var` for every local whose type the initialiser gives, built-in types included: `var count = 0;`,
  `var reader = new StringReader( text );`, `for (var index = 0; ...)`. A local spells its type only when the
  initialiser cannot supply one - a collection expression, `null` or `default` - or when the declared type is
  deliberately the interface rather than the implementation.
- The C# keyword aliases for the built-in types, `string`, `int`, `bool`, `char`, `object`, in declarations and
  static member access alike: `string.Empty`, `int.Parse`, not `String.Empty`, `Int32.Parse`. The only
  exceptions are type names that are part of an identifier, such as `Int32Column`.
- Target-typed `new()` where the target gives the type: an element of a typed collection expression, a property
  initialiser, an argument. A `var` declaration cannot target-type, so it spells the type after `new`.
- A literal that never changes is a `const`, including test data strings.
- Return early rather than nest the rest of a method inside an `if`. `if (x is null) { return; }` followed by
  the work reads better than the work wrapped in `if (x is not null)`.
- A class whose constructor only stores its parameters uses a primary constructor; one with validation, ordering
  or side effects keeps the explicit form.
- A lambda parameter that is not used is a discard: `( _, e ) => Handle( e )`.
- A local or parameter never shares a field's name. `schema` in a method body must mean one thing; where a
  method needs its own, it is `currentSchema`, `currentValues`, `currentContext`. When the parameter belongs to a
  public member whose name callers may rely on, the field is renamed instead: `cachedValues`, `valueFactory`.
- Do not convert a method to an expression body just because it fits on one line.

## Null checks and slicing

- `is null` and `is not null`, not `== null` and `!= null`. The pattern form cannot be intercepted by
  an overloaded `operator ==`, so it means what it says regardless of the type. No type here overloads
  the operator today, which is exactly why the rule is cheap to keep.
- Prefer the range operator where the intent is a prefix, a suffix or everything after a point:
  `value[..length]`, `value[^width..]`, `value[separator.Length..]`. Keep `Substring` for a slice taken
  from an offset with an unrelated length, where the two-argument form still reads better.
- Switch expressions over switch statements when every arm produces a value.
- Pass the `IFormatProvider` explicitly rather than passing `null` and relying on the ambient culture.
  `null` and `CultureInfo.CurrentCulture` behave identically, but only one of them tells the next
  reader that the current culture was the intent rather than an oversight.

## Naming

- Interfaces take an `I` prefix: `IColumnContext`.
- Private instance fields are plain `camelCase`, no underscore — that is what all 229 of them in the
  library do, and consistency beats the prefix.
- Private properties, should any be added, are `_camelCase`; private static properties and private
  static readonly fields are `PascalCase`. The library currently has none of the three.
- Local constants and type parameters are `PascalCase`.
- Everything public is `PascalCase`, as the BCL does it.

## Comments

Write the constraint, not its provenance.

**No ticket references.** Not an issue key, not "Finding 7", not "Phase 4". A ticket key is meaningful
for roughly as long as the ticket is open; the comment survives for years, and the next reader has to
leave the code and reconstruct a conversation to learn something the comment could have said outright.
History belongs in the commit message, which `git blame` already reaches.

```csharp
// Before - the reader has to go and look the ticket up, and it still will not tell them why
// Skip the header after the null guard (PROJ-1234), see the ticket for why the order matters.

// After
// Skip the header after the null guard: a file that failed the guard has no schema to skip past.
```

The test is whether removing the reference loses anything. If the comment collapses without it, it was
carrying history rather than meaning.

A link to a public issue somewhere else — `// Workaround for dotnet/runtime#12345` — is fine. It stays
resolvable by anyone, and it names a constraint the code genuinely cannot show.

**No attribution lines.** Comments do not carry `Co-Authored-By` or any similar credit line.

**No file header banners.** This repository is distributed under UNLICENSE, and no source file carries
a copyright or licence header. Do not add one.

## Commits

- Say what changed and why. The why is the part a reader cannot recover from the diff.
- **No `Co-Authored-By` trailers** or other attribution lines.
- Do not commit directly to `master`; branch, then merge.

## Tests

MSTest, one `*Tester.cs` class per type under test, in `FlatFiles.Test`.

**Do not set the culture in a test class.** Roughly 26 tests round-trip dates, decimals and currency
through the current culture and only hold under `en-US`. That is pinned once for the whole assembly by
`AssemblyInitialiser`, which sets `CultureInfo.DefaultThreadCurrentCulture` before any test runs.

Setting it per class is what this replaced, and it was quietly broken: the result depended on which
class happened to be constructed first on a given thread, so a class that set nothing passed only while
a sibling leaked `en-US` onto the same thread. Tests that depend on a specific culture for their own
sake should pass an explicit `IFormatProvider` rather than lean on the ambient one.

Prefer `Assert.ThrowsExactly<T>` over `Assert.ThrowsException<T>` and over `[ExpectedException]`; both
older forms are flagged by the MSTest analysers and removed in MSTest 4.

## Coverage

Line and branch coverage of the library both stay at or above 90%. Measure them with `dotnet-coverage` over
the test project, scoped to the library assembly so the tests and benchmarks never count towards their own
figure:

    dotnet-coverage collect -f cobertura -o coverage.xml --include-files "**/FlatFiles.dll" -- dotnet test FlatFiles.Test -c Release

The root `coverage` element of the Cobertura report carries `line-rate` and `branch-rate`; the figures quoted
in a PR are those two, rounded. `dotnet-coverage` is Microsoft's collector, installed as a global tool
(`dotnet tool install -g dotnet-coverage`); nothing in the test project is needed, and its command line has
been stable across releases, which is why it replaced dotCover here.

The `Build and test` workflow runs the same build, tests, coverage measurement and pack on every pull
request and fails if either rate is below 90%, so the figures in a PR description are the ones the workflow
printed. The pack
step also runs package validation against the last released version named in the project file
(`PackageValidationBaselineVersion`). A public API change that fails it is declared in the same PR, with the
reason in the changelog: after a release, by moving the baseline; before one, by regenerating
`FlatFiles/CompatibilitySuppressions.xml` with `dotnet pack -p:ApiCompatGenerateSuppressionFile=true` and
reviewing that every entry is the change intended. The one break accepted without a major version is a new
member on a configuration interface (`IDelimitedTypeConfiguration<TEntity>`, `IFixedLengthTypeConfiguration<TEntity>`
and the two dynamic ones), because the library's own mappers are their only implementations. Removing or
changing a member, or adding one to `IReader`, `IWriter`, `ISchema`, `IColumnDefinition` or the typed reader
and writer interfaces, gets a default implementation or a major version instead. The suppression file is
emptied when the baseline moves to the release that shipped the members.

The cheapest coverage is rarely the most valuable, so prefer tests that assert behaviour and let coverage
follow. The exception that proves it: the twenty-one property mapping classes are near-identical fluent
setters, and one reflective test drives every method on all of them precisely so that a new mapping or
setter is covered without anyone remembering to extend a test.

## Integration check

`FlatFiles.IntegrationTest` reads the six integration test files - three delimited, three fixed-length -
end to end, and compares what it measured against `FlatFiles.IntegrationTest/Baseline.json`. They are shaped after
real files: 379 columns, 344,352 records, every field quoted, fourteen record layouts chosen by one
character, a 3,500-character record.

The files are committed, compressed, in `FlatFiles.IntegrationTest/Files`, and unpacked into the build
output to be read. **Nothing regenerates them automatically.** `generate` is the only command that
writes one, and rebuilding is a deliberate act that invalidates the baseline, because the samples are
the input every figure is gated against: an input that rebuilt itself would turn a change in the
generator into what looks like a change in the library. The baseline pins a SHA-256 of each sample, and
a run that reads different bytes is refused rather than reported.

    dotnet run --project FlatFiles.IntegrationTest -c Release -- check

Beyond the samples themselves, the record count is exact: a change there is a change in what the library
does with a real-shaped file, whether or not anybody meant it. No sample is built to have a record
refused, so any refusal fails the check whatever else agrees. Bytes allocated per record is gated at 2%,
which is deterministic to the byte and has repeated to within 0.1% here. How long a read takes and how
much memory it peaks at are reported and never gated. Each sample is loaded five times, each in a
process that starts, reads the file once and exits, because that is how the library is mostly used;
the mean is reported with its range. That takes most of the machine noise out and still leaves more
movement than a gate could live with. Peak memory is a whole job's footprint, most of it runtime
start-up rather than the read.

It runs in `publish.yml` only, before a release is packed, and a release fails if anything moved. It is
deliberately not on the pull request build: what it guards is a release going out with something
changed, and the run costs about a minute and a half and 210 MB of unpacked disk on whatever performs
it - not worth spending on every push. When a change is intended, take
a new baseline with the `baseline` command and describe it in the changelog, in the same pull request
as the changelog entry and before the tag is cut.

**A release that takes a new baseline carries the full table**, all six samples and all three scenarios
apiece, so that what a version cost is recorded where it was released rather than only in a baseline
file the next release overwrites. The `baseline` command prints it - both tables and the notes saying
what each column measures - from the runs it took the baseline from, so the figures in the changelog
and the figures the gate compares are the same measurements:

    dotnet run --project FlatFiles.IntegrationTest -c Release -- baseline

Paste what it prints into the entry, in the pull request that finalises the changelog. `run --markdown`
prints the same block from a fresh set of runs, which is useful for looking but is not what the
baseline recorded.

**A release that does not change the baseline does not repeat the table.** It says the figures are
unchanged and names the release that last recorded them. Reprinting an identical table invites the
reader to hunt for a difference that is not there, and the numbers in it that are not gated - total
time and MB/s - would differ anyway, from the machine rather than from the library, which is worse
than saying nothing.

## Documentation

`README.md` is the library's documentation, and its **Table of Contents is part of it**: every `##` and
`###` heading has an entry, and the entries are in document order. The two are the same list or one of
them is wrong. A section added without an entry is a section nobody finds.

**Bring both up to date when a release is cut**, in the same pull request that dates the changelog
entry and before the tag. The changelog says what changed and why; the README says how to use what is
now there, and a release that added something a caller can reach - a way of mapping, an option, a
column type - is not finished until the README has a section for it and the contents point at that
section.

The same applies to what the repository does to verify itself. `FlatFiles.IntegrationTest` has its own
README carrying the tables, what each column measures and which figures are gated, and that is the
right home for the detail; the root README should say the project exists, what it is for, and link to
it, so that somebody reading about the library finds out how it is held to account.

Checking this is mechanical, and worth doing rather than assuming: list the headings, list the
contents entries, and compare them.

## Inspections

The three projects are kept clean against ReSharper's default inspection set, with the exceptions below, and a
full-solution inspection is run before a release. A new inspection that survives review is either fixed or added
to this list with its reason.

Left as they are, deliberately:

- "Never used" on public members. Fluent mapping methods, `IDataReader` members and the like exist for
  consumers the inspection cannot see.
- "Possible null reference" and "possible null assignment" in the test project, whose nullable analysis is off,
  and where a null is usually the point of the test.
- The `new[] { ... }` arrays passed to `CollectionAssert.AreEqual`, whose `ICollection` parameter gives a
  collection expression no type to take.
- Setters "never used" on benchmark data classes: the library sets them by reflection.
- The static per-`TEntity` type check in the two type mapper injectors, which is meant to be one per type.

## Analysers

Not yet enforced in this repository — there is no `.editorconfig` or `Directory.Build.props`, so no
`CA` rule can fire in a local build. If that changes, make it opt-in rather than opt-out:
`EnableNETAnalyzers` on with `AnalysisMode=None`, then enable rules by name a few at a time, measuring
the hit count before each one goes in. Enabling a category wholesale buries the build's real output.
