## 7.4.1 (2026-09-18)
**Summary** - Four defects found while raising the coverage bar to 90% of lines and branches: subscribing to both `RecordParsed` events threw, a dynamic `ComplexProperty` required a string member, an injector-based fixed-length writer wrote a blank line for its schema, and a delimited reader whose selector matched nothing returned the record anyway once the error was handled.

- Subscribing to a reader's `IReader.RecordParsed` and to its typed `RecordParsed` on the same instance threw "Delegates must be of the same type": the interface event was routed into the typed event's list, which cannot hold two delegate types. The interface event now has a list of its own and both are raised for every parsed record.
- `IDynamicDelimitedTypeConfiguration.ComplexProperty` and its fixed-length twin looked the member up as a `string`, so a nested object property of any other type was refused with "The actual property type did not match the indicated type". They look it up as the mapper's entity type.
- `FixedLengthWriter.WriteSchema` and `WriteSchemaAsync` wrote a record separator when the writer was built from an injector and so had no single schema; `DelimitedWriter` already wrote nothing. Both now write nothing.
- A `DelimitedReader` with a schema selector that matched nothing reported the record and then, if the handler let reading continue, returned it anyway, parsed with a schema built from its own field count; through a type mapper selector that ended in a `NullReferenceException`. The record is now skipped, as `FixedLengthReader` always did and as every other handled record error is. A reader given neither a schema nor a selector still builds one from the record, and a selector that should catch everything else declares it with `WithDefault`. Should a typed selector reader ever reach a record without a mapper, it throws `FlatFileException` rather than dereferencing null. `FixedLengthReader`'s `IReader.GetSchemaAsync( CancellationToken )` also ignored its token; it now throws when the token is cancelled, as the typed overload did.
- Two unused reflection helpers, `MemberAccessorBuilder.GetField` and a `GetMethod` overload, are removed; both were internal.

The `Build and test` workflow now fails below 90% of lines or branches, up from 80%; Conventions.md records the new bar. The suite gains 47 tests for the surfaces the feature tests did not reach: every dynamic configuration member on both mappers, auto-mapping of every column type, every data record accessor, the typed reader and writer wrappers, and the less travelled branches of the readers, writers and schemas.

## 7.4.0 (2026-09-18)
**Summary** - `DateOnly` and `TimeOnly` columns, property mappings and data reader accessors; mappers fall back to reflection where the runtime cannot generate code, so they work under Native AOT; a column is given a context only when something can look at it, a third less allocation on typed reads; the package carries its XML documentation and a symbol package with Source Link; pull requests are built, tested, coverage-checked and package-validated by a workflow.

**`DateOnly` and `TimeOnly`.** `DateOnlyColumn` and `TimeOnlyColumn` join the column set, each with `InputFormat`, `OutputFormat` and `FormatProvider` like `DateTimeColumn`, parsing exactly when a format is given and generically otherwise, and formatting straight into the record buffer. Both type mappers gain `Property` overloads for `DateOnly`, `DateOnly?`, `TimeOnly` and `TimeOnly?` returning `IDateOnlyPropertyMapping` and `ITimeOnlyPropertyMapping`, the dynamic mappers gain `DateOnlyProperty` and `TimeOnlyProperty`, and auto-mapping picks the new columns from the property types. `FlatFileDataReader` gains `GetDateOnly` and `GetTimeOnly` by ordinal, with the by-name and nullable forms as extensions; on `IFlatFileDataRecord` the two are default members that cast `GetValue`, so an implementation outside the library keeps compiling. The new `Property` overloads are required members of the four configuration interfaces, `IDelimitedTypeConfiguration<TEntity>`, `IFixedLengthTypeConfiguration<TEntity>` and the two dynamic ones, which package validation reports as a break; it is accepted, and suppressed in `CompatibilitySuppressions.xml`, because the library's own mappers are the only implementations of those interfaces. Until now a date without a time had to be read as a `DateTime` and converted, and a time of day as a `TimeSpan`.

**Native AOT.** An optimised mapper, which is the default, built its deserializer with `System.Reflection.Emit`, and on a runtime without dynamic code, Native AOT or any application with the `System.Runtime.IsDynamicCodeSupported` switch off, the first read threw `PlatformNotSupportedException`. The mappers now check `RuntimeFeature.IsDynamicCodeSupported` and use the reflection generator where emit is unavailable, so the same code runs published ahead of time; `OptimizeMapping( false )` remains for choosing reflection where emit would work.

**Column context on demand.** Every column of every record was given a `ColumnContext`, whether or not anything read it, so hooks and error handlers could see the column's indices. The library's own columns now report, through `IColumnDefinition.IsColumnContextRequired`, whether anything attached to them can look at a context: a parsing or formatting hook, a null formatter or default value from outside the library, a default value built from a delegate, a complex column, or a subclass declared outside the library. When nothing can, and the options carry no format provider, which reaches a column only through the context, no context is built. A column error still receives a full context, because one is built when the error occurs, so `ColumnError` handlers see exactly what they saw before. Measured on 10,000 records of 13 columns, a typed read allocated 1,677 bytes per record and now allocates 1,158; `IsColumnContextDisabled` remains for switching the context off entirely, hooks included. `IsColumnContextRequired` is a default interface member returning true, so a column implemented outside the library keeps its context.

Until now the package shipped without the XML documentation file, so the hundred and forty-odd public types had no IntelliSense text in a consumer's editor even though every member is documented in the source. The file is now generated and packed. A `.snupkg` symbol package is published alongside, with Source Link pointing at the tagged commit and untracked sources embedded, so a debugger can step into the library.

Pack now runs package validation against the previous release named in the project file, so a public API that loses or changes a member fails the build rather than reaching a consumer; a deliberate change moves the baseline in the same pull request. A `Build and test` workflow runs the build, the tests, the coverage measurement with the 80% bar on both lines and branches (`dotnet-coverage`, Microsoft's collector, chosen over dotCover because its command line does not change between releases), and the pack on every pull request and push to master.

The benchmark suite's quoted-field case had been built from the unquoted record since it was written, so it measured nothing about quoting; it now uses the quoted record. Delimited write, fixed-length read and write, and a CsvHelper write baseline are added to the suite.

## 7.3.0 (2026-09-17)
**Summary** - Ragged-right fixed-length files: with `FixedLengthOptions.IsRaggedRight` the last column runs from its offset to the end of the record, and a record that ends before it is read as far as it goes.

Until now a fixed-length record shorter than the total width of the schema's windows was refused with a `RecordProcessingException`, and a longer one was cut at the last window. Many systems write fixed-length files ragged right: every column but the last has a fixed width and the last runs to the end of the line, so records differ in length and only the offsets are fixed. Reading such a file meant padding every line before handing it to the reader, or handling the error for every record, and a long last value was silently truncated.

With `IsRaggedRight` set, the last column takes everything from its offset to the end of the record, however long or short, trimmed like any other column; its declared width is not used. A record that ends before the last column is read as far as it goes: a window the record ends inside takes the characters that are there, and a window the record never reaches yields an empty value, which the column's null handling turns into null. `IsLongRecordRejected` has no effect alongside the option, because no ragged-right record is too long. Schema selectors see the raw record as before, so a predicate on a record-type prefix or a position works unchanged across record types of different lengths, and a file without record separators reads its final partial record instead of refusing it.

When writing with `IsRaggedRight`, the last column of each record, and of the header, is written as formatted, neither padded nor truncated to its window; every other column is fitted as before. A file read ragged and written ragged keeps its shape.

The option defaults to false, so existing behaviour is unchanged.

## 7.2.0 (2026-09-17)
**Summary** - Every asynchronous read and write takes an optional `CancellationToken`, threaded through to the underlying `TextReader` and `TextWriter`.

Until now nothing asynchronous in the library could be cancelled: a `ReadAsync` waiting on a slow stream ran until the stream delivered. Each asynchronous method now has an overload that takes a `CancellationToken` and passes it to the one place it matters - `TextReader.ReadBlockAsync` when reading and `TextWriter.WriteAsync` when writing - and checks it once on entry, so a token cancelled before the call throws `OperationCanceledException` before the stream is touched and one cancelled during a wait ends the wait.

The overloads are on `IReader` (`ReadAsync`, `SkipAsync`, `GetSchemaAsync`), `IWriter` (`WriteAsync`, `WriteSchemaAsync`, `WriteRawAsync`), `ITypedReader<T>` and `ITypedWriter<T>`, the `ReadAllAsync` and `WriteAllAsync` extensions, and the type mappers' `ReadAsync` and `WriteAsync`, where the token follows the options: `mapper.ReadAsync( reader, options, token )`. `ReadAllAsync` also honours a token given through `await foreach (...) .WithCancellation( token )`. The overloads without a token are unchanged and hand the stream `CancellationToken.None`, as before.

On the interfaces the overload that takes a token is the required member, and the one without it is a default implementation that forwards `CancellationToken.None`, so a token handed to any implementation, inside the library or outside it, reaches the implementation rather than being dropped. A class outside the library that implements `IReader` or `ITypedWriter<T>` adds the token overloads to compile against 7.2.0; the overloads without a token come from the interface.

## 7.1.0 (2026-09-16)
**Summary** - Writers format each record into one reusable buffer and readers scan their input as a span, for half to two thirds less allocation on write and a third to two fifths less time on read with unchanged output; a fixed-length record longer than the schema can be rejected; the numeric columns share one public base; and unsubscribing from `IReader.RecordParsed` now works.

Until now each value written passed through several strings: the column formatted it to a string, the delimited writer quoted it into another and joined the record into a third, or the fixed-length writer padded it into another, and only then was anything handed to the `TextWriter`. Over 50,000 six-column records that came to 38.7 MB for the delimited writer and 36.3 MB for the fixed-length writer, almost all of it garbage before the record reached the stream.

Each writer now owns a single growable character buffer. Columns format straight into it - the numeric, date, `Guid`, `TimeSpan` and `char` columns through `ISpanFormattable.TryFormat`, so no string is ever created for the value - the delimited writer quotes a value in place once its text is known, the fixed-length writer pads or truncates in place, and the finished record goes to the `TextWriter` as one span. Once the buffer has grown to fit the widest record in the file it allocates nothing further. The measurements below are the best of fifteen runs, the two builds alternated on the same machine:

| 50,000 records, six columns | 7.0.0 | 7.1.0 |
|---|---|---|
| delimited | 27.9 ms, 38.7 MB | 24.7 ms, 14.4 MB |
| delimited, async | 31.6 ms, 38.7 MB | 27.1 ms, 14.4 MB |
| fixed-length | 28.9 ms, 36.3 MB | 23.3 ms, 19.4 MB |
| delimited through the type mapper | 60.6 ms, 50.9 MB | 49.1 ms, 26.7 MB |

The text written is identical in every case; a new set of tests formats every built-in column type both ways and asserts the two agree, including custom output formats and non-default cultures.

**New API.** `IColumnDefinition` gains `Format(IColumnContext?, object?, IBufferWriter<char>)` with a default implementation that writes the string the existing `Format` returns, so a class implementing the interface directly keeps compiling and working. `ColumnDefinition` exposes the same overload as a virtual method, `ColumnDefinition<T>` adds a matching `protected virtual OnFormat(IColumnContext?, T, IBufferWriter<char>)`, and a `protected static WriteFormatted<TValue>` helper formats any `ISpanFormattable` into a buffer, growing the request until it fits. A custom column that only overrides the string `OnFormat`, as every existing one does, keeps working unchanged through the fallback; override the buffer overload as well to format without a string.

Two things to know if you have written your own columns. If a `ColumnDefinition<T>` subclass overrides the public `Format(IColumnContext?, object?)` itself rather than `OnFormat`, writers no longer call it - they call the buffer overload, which goes to `OnFormat`. Override the buffer overload too, or move the logic into `OnFormat`. And a column with an `OnFormatted` hook is still formatted to a string first, because the hook takes the whole string; `OnFormatting` is unaffected.

What remains per record is the boxing of each value by the type mapper, one `ColumnContext` per column (which `IsColumnContextDisabled` already switches off) and the record context itself.

**The readers scan their input as a span.** The tokeniser that split delimited text into values walked the input one character at a time through a circular queue, appending each character to a `StringBuilder` for the current value and to another for the record text, with the whole state machine written twice - once for synchronous reading and once for asynchronous. It now buffers the input as one contiguous block, jumps from one candidate separator or quote to the next with a vectorised `SearchValues` search, and copies each value out once. A record that runs off the end of the buffer is scanned again from its start once more text has arrived, which is what lets one state machine serve both readers, and the buffer grows if a single record outgrows it. The fixed-length reader finds its record separators the same way.

| 50,000 records, six columns | 7.0.0 | 7.1.0 |
|---|---|---|
| delimited | 83 ms, 42.2 MB | 47 ms, 42.3 MB |
| delimited, record text kept | 85 ms, 61.5 MB | 50 ms, 51.7 MB |
| delimited, async | 96 ms, 56.7 MB | 54 ms, 56.8 MB |
| fixed-length | 81 ms, 73.7 MB | 55 ms, 63.0 MB |

Allocation barely moves for a plain delimited read because what remains is the value strings themselves; where the record text is kept, or for fixed-length files, the double copy through a `StringBuilder` is gone. The values and record text produced are identical, including how leading whitespace is dropped, doubled quotes inside a quoted value, a separator that is a prefix of the record separator, and a lone carriage return as a line break. A new set of tests drives the tokeniser with buffers as small as one character so that every record straddles a refill. The initial read buffer is 16,384 characters, up from 4,096.
**Fixed-length records longer than the schema can now be rejected.** A fixed-length record shorter than the total width of the schema's windows has always been refused with a `RecordProcessingException`, but a longer one was accepted in silence: each column was read from its declared offset and the excess discarded, so a layout declared one character too narrow shifted every later column and reported nothing - a quantity of 42 in a column declared four characters wide instead of five came back as 4. `FixedLengthOptions.IsLongRecordRejected` treats such a record as an error, raising the same exception with the same record context as a short record so the two are handled alike, and a `RecordError` handler can skip it as it can any other. It defaults to false, because a file whose records carry trailing content that was always ignored would otherwise stop reading; turn it on when the layout is meant to describe the whole record. Records read by width alone, with `HasRecordSeparator` off, cannot be too long and are unaffected, as is a header record, which is skipped rather than partitioned.

**The eleven numeric columns share one base.** `ByteColumn` through `UInt64Column`, `SingleColumn`, `DoubleColumn` and `DecimalColumn` were eleven copies of the same sixty lines, differing only in the type and the `NumberStyles` they accept by default. They now derive from `NumberColumn<T>`, which parses and formats through the generic maths interfaces (`INumber<T>` gives `T.Parse` with number styles and a provider, and span formatting), so each concrete column is a constructor that names its type and default styles. `FormatProvider`, `NumberStyles` and `OutputFormat` are unchanged in name, type and behaviour; they have simply moved to the base, which a caller compiled against 7.0.0 picks up on recompiling. The base is public, so a numeric type the library does not ship a column for - `BigInteger`, `Int128`, `Half` - is a constructor's worth of code away.

**`EnumColumn<TEnum>.Parser` and `Formatter` are declared non-nullable.** They never returned null - assigning null has always restored the default parser or formatter, and still does, which the properties now say with `[AllowNull]` rather than a nullable type. A caller reading either property no longer needs a null check; a caller assigning null is unaffected. Alongside it, a handful of small tidy-ups with no visible effect: the type mapper's column lookup is a `FrozenDictionary`, four internal classes nothing derives from are sealed, the remaining null guards use `ArgumentNullException.ThrowIfNull`, and the `IDataReader` implementation's nineteen typed getters share one cast.

**Fixed: unsubscribing from `IReader.RecordParsed` never worked.** The interface event on `DelimitedReader` and `FixedLengthReader` forwarded each handler to the typed `RecordParsed` event through a fresh lambda, so removing the handler removed a lambda that had never been added and the original kept firing. `EventHandler<T>` is contravariant, so the handler now subscribes to the typed event as it is and can be removed again. Found by a ReSharper inspection pass that also tidied the annotations it flagged: `FixedLengthSchemaSelector.WithDefault` and `FixedLengthSchemaInjector.WithDefault` declare their parameter nullable, since null has always meant "no default"; `ColumnDefinition.DefaultValue` and `NullFormatter` carry `[AllowNull]`, since assigning null has always restored the default; and the enumeration conversion in `DataRecordExtensions` catches only the conversion failures it means to swallow rather than every exception.

## 7.0.0 (2026-09-16)
**Summary** - Target .NET 10 exclusively and remove the deprecated compatibility packages, leaving the package with no dependencies of its own.

**The package is now published as `Cynicszm.FlatFiles`.** It was previously `FlatFiles`, which is the upstream project this is forked from and not ours to publish under. The assembly is still `FlatFiles.dll` and every namespace and type name is unchanged, so nothing in your code changes - only the `PackageReference`:

```xml
<PackageReference Include="Cynicszm.FlatFiles" Version="7.0.0" />
```

Note that `FlatFiles` on nuget.org stops at 5.0.4; 6.0.4 was never published there.

The previous release built for .NET 8 and .NET 9. Both targets are dropped in favour of a single `net10.0` target, and that is the breaking change behind the major version bump. Anyone who still needs to run on .NET 8 or .NET 9 should stay on 6.0.4.

Five .NET Framework-era compatibility packages were still being referenced, every one of which is part of the shared framework on modern .NET: `System.Net.Http`, `System.Text.RegularExpressions`, `System.ValueTuple`, `System.Threading.Tasks.Extensions` and, in the test project, `System.Data.DataSetExtensions`. None of them contributed anything to the build. `System.Net.Http` 4.3.4 is the one worth calling out, because it carries a published advisory that trips package audits in consuming solutions. With all five gone, the NuGet package now declares no dependencies at all.

The `LangVersion 9.0` pin was removed at the same time, so the library compiles at the C# 14 default that comes with `net10.0`.

**Breaking:** `IRecordContext.Record` is now an empty string for delimited files unless you ask for it. The reader was building a string of each record's original text so it could be handed to you through that property, and nothing in parsing needs it - the column values are tokenised separately, and a schema selector is given those values rather than the text. For a caller who never read it, that string was the single largest avoidable cost of reading a file: over 50,000 records it measured as 9.5 MB of the 49.2 MB the reader allocated, a fifth of the total. The new `DelimitedOptions.PreserveRecordText` defaults to `false` and skips it.

If you read `IRecordContext.Record` on a delimited file, set `PreserveRecordText = true` and you get the old behaviour back exactly. Nothing else changes: the parsed values are identical either way, and throughput is the same to within a millisecond or two on a 50,000 record read - what this buys is allocation and the GC pressure that comes with it, not wall-clock time.

One consequence worth knowing before you take the default: this applies to the error path too. A `RecordError` or `ColumnError` handler that logged `e.RecordContext.Record` to show which line failed to parse now logs an empty string, leaving the physical record number as the identifier. The exception messages themselves are unchanged, since they format the record number rather than the text. Set `PreserveRecordText = true` if your error handling reads it.

Fixed-length files are unaffected and have no such option. That reader takes its column values out of the record text with `Substring`, so it always has the text and always reports it.

## 6.0.4 (2024-12-11)
**Summary** - Replace the .NET Framework and .NET Standard targets with .NET 8 and .NET 9, and strip out the conditional compilation those older targets needed.

*Backfilled from commit 5905884, which shipped without a changelog entry at the time.*

The `net451`, `netstandard1.6`, `netstandard2.0`, `netstandard2.1` and `netcoreapp3.0` targets were all dropped in favour of `net8` and `net9`. That is the breaking change behind the major version bump. It is also why this release is numbered 6.0.4 rather than 6.0.0: the patch number was carried over from 5.0.4 rather than reset.

With the old targets gone, the `#if` guards written to work around them were removed. No public API changed as a result, but what gets compiled did shift in two places. The `IAsyncEnumerable` overloads on the type mappers and the typed reader and writer extensions had been guarded to exclude `net451`, `netstandard1.6` and `netstandard2.0`, and are now unconditional. The ADO.NET types - `FlatFileDataReader`, `FlatFileDataReaderOptions`, `IFlatFileDataRecord` and the `DataTable` and `IDataRecord` extensions - had been guarded the other way and were left out of the `netstandard1.6` and `netstandard2.1` builds entirely; they are now always compiled. The `net451`-only `Array.Copy` branches in `FlatFileDataReader.GetBytes` and `GetChars` gave way to the implementation every other target already used.

The per-target package references went with them, since `System.Reflection.Emit` and `System.Data.Common` were only ever needed by the .NET Standard and .NET Core App builds. `System.Threading.Tasks.Extensions` was bumped to 4.6.0, and `System.Net.Http` 4.3.4 and `System.Text.RegularExpressions` 4.3.1 were added. All four of those were removed again in 7.0.0.

The package release notes were not updated for this release, so NuGet still shows the 5.0.3 nullability summary against it.

## 5.0.4 (2022-12-04)
**Summary** - The delimited and fixed-length writers use a cached record context. The schema information can only be determined after writing a record. This doesn't work well with the type mappers, injectors, and custom mappings since the context is needed prior writing the values. When switching from writing one type to another, the writer's cached context was still referring to the previously written type's schema, leading to the wrong context being passed to the custom mappers. This manifested itself as an `ArgumentOutOfRangeException`, trying to find a column in the wrong schema.

## 5.0.3 (2022-10-02)
**Summary** - In order for DefaultValue to work, columns have be marked as `IsNullable=false`; however, there was no setter on the property mappers to specify nullable.

## 5.0.2 (2022-05-26)
**Summary** - An out of memory exception was being caused by me creating a `StringBuilder` for the `FixedLengthParser` class, while the `RetryReader` is also buffering strings using a `StringBuilder`. I never called `GetRecord()` on the `RetryRecord`, so the memory in the `RetryReader` never got freed.

## 5.0.1 (2022-02-24)
**Summary** - An exception was being thrown by the `ColumnContext` class because the schema was missing when using injectors. For this release, I found a workaround to get injectors working again, but I am not happy with the solution. I will revisit this when I have more time to investigate a more all-encompassing solution.

## 5.0.0 (2022-02-20)
**Summary** - Rename classes/methods/etc. from "separated value" to "delimited". Break out each class into its own file. Introduce nullable references compile setting and improve null safety.

For the past several years, my classes being named `SeparatedValueReader` (and the like) has been a thorn in my side. Pretty much universally other libraries, and people in general, use the term "delimited". Delimited is also a smaller word, which makes typing it over and over less of a pain. While it will cause long-time users of the library some discomfort, the rename shouldn't take too long (I just did it 10,000 times for this version so I feel your pain).

Coming back to this code base after years of minimal maintenance, I had no idea where to find half my classes. I think I was afraid of the sheer number of files I'd end up with should I break everything out; however, a little more scrolling is better than guessing the correct file. This is mostly noticeable when looking through source control, not in the IDE. Almost every public interface and class (and many internal classes) are in their own files now.

I wanted to enable nullable reference types for a while. Immediately after enabling it, I had thousands of compile warnings. In fixing the warnings, I actually improved the code for processing and filtering records in the parsers, fixed my overall event broadcasting mechanism, and fixed several subtle bugs I wouldn't have caught otherwise. Some of these changes resulted in minor interface changes, hence the 5.0.0.

Ultimately, this is not the 5.0.0 release I was hoping for. I was hoping to include several other major refactorings before letting this loose on the world. My long-term plan, assuming there's a continued interest, is to separate out my delimited parser into a separate, stupid set of classes. These classes would use the latest .NET features to streamline that parsing process and give you, the library consumer, direct access to the low-level performance benefits if you needed it. I want to then separate out the schema definitions and make it possible to parse values coming from anywhere. I want to revisit the code generation happening in the type mappers. I don't have much of an appetite for those things, yet. But... I want to make sure the enhancements I made several months back get out there, as well as make sure bug fixes aren't targeting some older version.

## 4.16.0 (2021-06-27)
**Summary** - Several years ago, I was looking into the use of `IAsyncEnumerable`. I actually had some code already that would work exclusively for .NET Core App 3.0+. Now that `IAsyncEnumerable` is part is .NET Standard 2.1, I can support for additional runtimes. I officially added a .NET Standard 2.1 build. I then added additional extension methods for the `TypedReader` and `TypedWriter` classes. I also added methods to the type mapper classes that use these extension methods internally. I found various other places where I could use the extension methods, so reimplemented them where I could. This sort of code duplication actually led to some bugs recently, so I think it benefits me to consolidate as much as I can.

## 4.15.0 (2021-05-10)
**Summary** - Recent changes to write out headers when writing multiple records only applied to the extension method `WriteAll`. However, the mapper class's `Write` methods have a similar semantic and behaved the same as before. This commit actually changes the mapper methods to call `WriteAll` under the hood, so now the same behavior will be exhibited.

I discovered a bug I introduced with the previous version where I wrote the schema out no matter what. I only want to do this if the writer is configured to write out the schema, which is `false` by default.

## 4.14.0 (2021-05-01)
**Summary** - The behavior of `TypedWriter.WriteAll` is somewhat unintuitive when called with no records. The expectation is that the header is written when performing this bulk operation; otherwise, the caller has to explicitly call `WriteSchema` beforehand. This slightly changes the behavior of the code, such that it might result in headers/schema being written in cases where the file was blank before. However, when `IsFirstRecordSchema` is `true`, it is extremely unlikely consumers would expect a blank file to be generated.

During my testing, I also discovered a bug where the schema was getting set, then unset, when the header/schema record was the only record in the file. You should be able to try to `Read` the first record of an empty file and get `false` back, then get the schema via `GetSchema`; however, my code was throwing an `InvalidOperationException` or, worse, a `NullReferenceException`.

## 4.13.0 (2020-12-03)
**Summary** - This change allows the original text making up a record to be viewed while parsing a file. The raw record contents will be accessible via the `IRecordContext` interface, which is available within the event args.

I originally had some concerns regarding memory usage or performance impacts, but after profiling and benchmarking, no significant performance issues were detected.

## 4.12.0 (2020-10-21)
**Summary** - `FlatFileDataReader` not correctly ignoring ignored columns in several places.

The ADO.NET classes didn't receive the same level of love that the rest of the library received when introducing ignored columns. When getting the column names, their ordinal positions, etc., the `FlatFileDataReader` was returning information for ignored columns. This caused the `DataTable` extensions to see too many column names and yet receive too few record values in the table (with the data shifted to the left for each missing column). Fixing the `FlatFileDataReader` fixed the `DataTable` problems, as well.

Technically this is a breaking change that might warrant a major version change; however, as the previous behavior could not possibly be desired and few people actually use the ADO.NET classes, I am going to include this in the next minor version, treating it as just a bug fix.

## 4.11.0 (2020-10-09)
**Summary** - Allow handling unrecognized rows when using schema selectors.

Previously, if a record was encountered that could be handled by any of the configured schemas, the selector would throw a generic `FlatFilesException`. Now, a `RecordProcessingException`is thrown instead, which can be ignored causing the record to be skipped.

## 4.10.0 (2020-10-06)
**Summary** - Add the ability to explicitly write the schema using typed writers.

I never added support for writing the schema using typed writers. I never added `WriteSchema` and `WriteSchemaAsync` to the `IWriter` interface either. I don't see why not, so I added them.

## 4.9.0 (2020-09-26)
**Summary** - Make OnParsing, OnParsed, OnFormatting, OnFormatted events available to type mappings.

When I introduced the `OnParsing`, `OnParsed`, `OnFormatting` and `OnFormatted` delegates, I marked `Preprocessing` as deprecated but then did not mark it deprecated on the `ColumnDefinition` class or in the column property mapping classes.  Furthermore, I did not add methods to the property mapping classes to allow you to utilize the new delegates. While working on this, I also realized that whenever the `NullFormatter` or `DefaultValue` classes were being used, I was not executing the `OnParsed` and `OnFormatted` delegates with the output of these classes. So, now, I have marked all references to `Preprocessing` as deprecated, added methods to register the delegates on property mappings and now call `OnParsed` and `OnFormatted` regardless of whether the value being parsed/formatted is considered `null`.

One of the primary motivations of these changes was to allow inspecting the values found in ignored columns. For example, you might want to ignore a block of text within a file, but also perform some sanity checks to ensure that the ignored value corresponds to your expectations. For example, you might be working on a fixed-width file and you want to ignore pipe (`|`) characters appearing between values; as a sanity check, you additionally want to ensure the extracted string is in fact a pipe. If you saw something else, you could then assume something was wrong with the input text, such as instead of truncating a string to fit within the fixed-width column, the record got shifted over and the values no longer fit within the expected windows. I updated the `Parse` and `Format` methods to call the `OnParsing`, `OnParsed`, `OnFormatting` and `OnFormatted` delegates just like the other column types and updated the property mappings as well.

An interesting side-effect of these changes is that for `IgnoredColumn`s, spitting out a placeholder value can be achieved either via a `NullFormatter` or using the `OnFormatted` delegate. This is different than the other column types because they are not equipped to handle `null`s.

## 4.8.0 (2020-09-17)
**Summary** - Avoid memory leaks by creating new dynamic assemblies each time.

I was not sure how much of a runtime impact creating a new dynamic assembly would be and so I was trying to optimize the code by storing the `AssemblyBuilder`/`ModuleBuilder` in a static variable. However, I had no mechanism in place to prevent generating duplicate types/methods each time a reader/writer was created, so over time the same emitted code was being added to the dynamic assembly over and over again; a.k.a., a memory leak. At first I tried to implement some sort of caching but then realize that it was almost impossible to uniquely identify a type/column mapping configuration without looking at every property on every mapping. I decided to try creating new assemblies each and every time and that I ran a benchmark. There was no discernable difference in performance, so I think eliminating the premature optimization is the right approach.

## 4.7.0 (2020-02-15)
**Summary** - Support capturing trailing text after last fixed-length window.

For fixed-length files, it may be desirable to capture any extra data trailing after the last configured window. For example, a fixed-length format may be extended in the future with the additional information appended to the end of each record. In that example, in order to be backward compatible, the parser needs to only expect the original set of values (windows) but can perform additional operations on the extra information, if found. Another common practice is to place arbitrary-length text columns, such as comments or descriptions, at the end of records so a specific max length is not required; otherwise, the file format must include that many characters on each record even if most records only use a small portion of the alloted amount. This results in unnecessarily large files consisting of mostly whitespace.

In order to support these types of files, a new `Window.Trailing` indicator was added, which can be passed to a `FixedLengthSchema` or `FixedLengthTypeMapper` where a `Window` is normally expected. FlatFiles will detect this special marker `Window` and configure the reader to grab any trailing text (after the last regular `Window`). When using the `FixedLengthTypeMapper`, you specify the property you want the extra information written to. When working directly with the `FixedLengthReader`, the trailing text appears at the end of the returned arrays. Typically, you want to use a `string` property or `StringColumn` for trailing text, but you can technically use any column type.

You can technically specify the trailing window at any time during configuration; it doesn't matter if you define other windows before or afterwards. If you try to configure multiple trailing windows, the latest configuration is used and is *not* considered an error.

## 4.6.0 (2019-06-03)
**Summary** - Support replacing `DBNull` with `null` when calling `GetValues` on an `IDataRecord`.

### New Features
When working with ADO.NET classes, you often have to deal with `null`s by checking for instances of `DBNull`. Most of the time, this involves calling `IDataRecord.IsDBNull()` or comparing values to `DBNull.Value`. The `DataRecordExtensions` class provides a lot of convenience methods, like `GetNullableString`, that save you from having to distinguish between `null` and `DBNull` all the time. The `FlatFileDataReader` and `DataTableExtensions` classes also provide a lot of features/options surrounding `null` handling. One area where this was overlooked was in the `DataRecordExtensions.GetValues` method.

It was decided that `object[] GetValues()` *(extension)* and `int GetValues(object[])` *(built-in)* should have similar semantics, so instead an optional parameter was added: `replaceDBNulls` that allows specifying whether `DBNull`s should be replaced in the destination array or not (defaults to keeping `DBNull`s). An additional extension: `int GetValues(object[], bool)` was added to provide similar semantics for the built-in method.

### Other
There were also some tests failing due to culture-specific date formatting. Those unit tests were updated to format dates in a culture-insensitive way.

## 4.5.0 (2019-05-29)
**Summary** - Disable wrapping delimited values in quotes.

### New Features
* The `QuoteBehavior` enum now supports a `Never` option, to disable quotes within delimited files. This is set on the `SeparatedValueOptions.QuoteBehavior` property.
    * *NOTE - This can allow the generation of invalid delimited files if they contain the separator token*

## 4.4.0 (2019-05-25)
**Summary** - Allow pre- and post- parsing and formatting on columns. Allow specifying a global `IFormatProvider` in `IOptions`.

### New Features
* The `IOptions` interface now has a `IFormatProvider FormatProvider` property. If provided, all columns will automatically use this `IFormatProvider` as their default.
    * The `IFormatProvider` specified on a `IColumnDefinition` will override what is specified in `IOptions`.
    * If no `IFormatProvider` is found at either level, FlatFiles defaults to `CultureInfo.CurrentInfo`, as before.
* The `IColumnDefinition` interface now exposes four new properties:
    * OnParsing - Fires before attempting to parse the `string` value; the logical successor to `Preprocessor`. 
    * OnParsed - Fires after parsing the column, providing access to the parsed `object`.
    * OnFormatting - Fires before formatting the column, providing access to the `object`.
    * OnFormatted - Fires after formatting the column, providing access to the generated `string`.

Unlike `Preprocessor`, each of the new properties have the type `Func<IColumnContext, T, T>`, providing access to the column context.

### Deprecated
Since `Preprocessor` and `OnParsing` are effectively redundant, `Preprocessor` has been marked deprecated. Please upgrade any existing code to use `OnParsing` instead. The `Preprocessor` property will be removed in the next major release (a.k.a., v5.0).

## 4.3.4 (2019-03-10)
**Summary** - This release allows directly writing arbitrary text to the underlying `TextWriter`, for those who need it.

### New Features
* The `IWriter` interface now exposes `WriteRaw` and `WriteRawAsync` methods.
    * This is implemented by the `SeparatedValueWriter` and `FixedLengthWriter`.
* The `TypedReader` and `TypedWriter` classes now allow directly accessing the underlying `IReader` and `IWriter` objects.

## 4.3.3 (2018-10-15)
**Summary** - The `FixedLengthReader` class would loop indefinitely when partitioning a record whenever there was a metadata column.

### Bug Fixes
The `FixedLengthReader` class would loop indefinitely when partitioning a record whenever there was a metadata column. In this situation, the same index was being used to iterate over the columns/windows and to store the partitioned values in the value array. However, metadata columns are skipped so the index was not being incremented. The solution was to introduce a separate index for the columns/windows and the value array.

## 4.3.2 (2018-09-25)
**Summary** - The `RecordErrorEventArgs.Exception` property is always `null`.

## 4.3.1 (2018-09-25)
**Summary** - Handle `DBNull.Value` when writing out a `DataTable` to a file, treating it as `null`.

## 4.3.0 (2018-07-16)
**Summary** - Further ADO.NET support.

### New Features
* Expose `sbyte`, `ushort`, `uint` and `ulong` accessors on `FlatFileDataReader`.
* Modify `DataTableExtensions.ReadFlatFile` to support merging into a table with an existing schema and data.
* Improve use cases and performance of `DataTableExtensions.WriteFlatFile`.

### Bug Fixes
* The `DataTableExtensions.WriteFlatFile` method threw an exception for schemas including ignored columns.

I wanted to make sure `FlatFileDataReader` can be used to retrieve data of any built-in .NET type.

The `ReadFlatFile` and `WriteFlatFile` extension methods were previously very limited. When reading, `Reset` would be called on the `DataTable`, wiping out any schema and data, which may not be desired/expected. Going forward, `ReadFlatFile` will attempt to reuse an existing schema and upsert the data. For that reason, additional overloads are now provided to match overloads of the [DataTable.Load](https://docs.microsoft.com/en-us/dotnet/api/system.data.datatable.load?view=netframework-4.7.2) method.

Old versions of `WriteFlatFile` were inefficient, repeatedly getting and setting row values using column names, rather than indexes. Furthermore, now the same underlying array is used to hold values between writes, so an entire file can be written using a single array allocation, improving performance dramatically. Most importantly, the previous version of this method threw errors when schemas included ignored columns.

## 4.2.0 (2018-07-15)
**Summary** - Expose `DateTimeOffset` and `TimeSpan` accessors on `FlatFileDataReader`.

### New Features
* Expose `DateTimeOffset` and `TimeSpan` accessors on `FlatFileDataReader`.

## 4.1.0 (2018-07-15)
**Summary** - Support for `TimeSpan` columns and properties.

### New Features
* Support for `TimeSpan` columns and properties. Ability to treat days, hours, minutes, seconds, milliseconds or ticks as `TimeSpan`.

## 4.0.0 (2018-07-15)
**Summary** - Column-level error handling and further support for nulls.

### New Features
* Errors can now be inspected at the column-level using the `ColumnError` events. 
* Columns can be marked at nullable or not using the `IsNullable` property.
* A default value can be returned when nulls are encountered when `IsNullable = false`.
* Support for `DateTimeOffset` columns and properties.

### Breaking Changes
* The `Error` event of the `IReader` and `ITypedReader` interfaces were renamed to `RecordError`.
* `INullHandler` renamed to `INullFormatter`. Related properties renamed, as well.

## 3.0.1 (2018-07-08)
**Summary** - The `DataRecordExtensions.GetNullableString` calling itself recursively.

### Bug Fixes
* The `DataRecordExtensions.GetNullableString` method was calling itself, causing a `StackOverflowException`.

## 3.0.0 (2018-06-29)
**Summary** - Introducing custom mapping support and more contextual information.

### New Features
* The new type mapper method, `CustomMapping`, grants full control over the way values are mapped between raw `object[]` values and entities. See the [readme](https://github.com/Cynicszm/FlatFiles/blob/master/README.md#custom-mapping).
* Automatic column-to-property mapping for delimited file formats, via `GetAutoMappedReader` and `GetAutoMappedWriter` methods. See the [readme](https://github.com/Cynicszm/FlatFiles/blob/master/README.md#automatic-mapping-for-delimited-files).

### Enhancements
* All exceptions, events and custom mapping features now provide access to column, record and/or execution context.
* Fewer restrictions on the number of environments FlatFiles can run.
* Support for ADO.NET utilities for .NET Standard 2.0 and above (`IDataReader`/`IDataRecord`).

### Breaking Changes
* The `CustomProperty` and `WriteOnlyProperty` methods have been superseded by the `CustomMapping` method, so they have been removed.
* The `IProcessMetadata` interface has been replaced by the `IRecordContext` interface.
* The `RecordNumber` property of `RecordProcessingException` has been replaced with the `RecordContext` property.
* The `ProcessingErrorEventArgs` class has been replaced by the `ExecutionErrorEventArgs` class.
* The `IncludeFilteredRecords` property of `RecordNumberColumn` has been renamed to `IncludeSkippedRecords`.
* The `IColumnDefinition` interface methods `Parse` and `Format` now accept `IColumnContext` objects.
* The `IMetadataColumn` interface no longer has the `GetValue` method. Use the `MetadataColumn` base class instead. See the updated [readme](https://github.com/Cynicszm/FlatFiles/blob/master/README.md#metadata).
* Rename `FlatFileReader` to `FlatFileDataReader`.

Most significantly of all, previous versions of FlatFiles used `DynamicMethod` to generate code at runtime. A `DynamicMethod` can be configured to allow the generated code to access non-public classes and members from other assemblies. However, this additional access requires the code to be running in a trusted environment, meaning FlatFiles could not be used in a sandboxed environment.

The new custom mapping functionality required the creation of types at runtime, so `DynamicMethod` was no longer an option. However, there is no means of granting dynamic types access to non-public classes/members in other assemblies. Going forward, FlatFiles will only be able to access public classes and members in your projects. If you need FlatFiles to access internal classes/members, you can add this line to you `Assembly.cs` file:

```csharp
[assembly: InternalsVisibleTo("FlatFiles.DynamicAssembly,PublicKey=00240000048000009400000006020000002400005253413100040000010001009b9e44f637b293021ec4d8625071e5fe1682eeb167c233b46314cca79bf2769606285d5d1225cba8ce1e75be9e8ab7251d17eaf2c3b00fde5eac50a0f7dc7fec2f70279ff71c72341ad2738661babfdc6792479f14fd64d841285644d5c09c2902e9467f574e0d369161caee632087c5d819c3c36f76622306b09a4f868230c1")]
```

Otherwise, you can disable runtime optimization by calling `OptimizeMapping(false)` on your mapping, which will cause FlatFiles to fallback on reflection which can access private members at the cost of runtime overhead. Another alternative is to pass a delegate that accesses the internal member to the `CustomMapping` method.

Forcing users to add the `[InternalsVisibleTo]` attribute is in-line with what other .NET libraries involving runtime generation of types are doing (e.g., Moq and Castle.DynamicProxy). While this is may be inconvenient to some users, it makes the library more portable. It also mean, FlatFiles no longer depends on the [System.Reflection.Emit.Lightweight](https://www.nuget.org/packages/System.Reflection.Emit.Lightweight) NuGet package which is now considered [obsolete](https://github.com/dotnet/source-build/issues/532). You can read more in the [readme](https://github.com/Cynicszm/FlatFiles/blob/master/README.md#accessing-non-public-classes-and-members).

## 2.1.3 (2018-06-16)
**Summary** - Use `ConfigureAwait(false)` for all async operations.

### Enhancements
* Using `ConfigureAwait(false)` consistently can improve async/await performance and avoid deadlocks in some environments. For more information, read [this article](https://msdn.microsoft.com/en-us/magazine/jj991977.aspx).

## 2.1.2 (2018-06-16)
**Summary** - Code modernization and bug fixes.

### Bug Fixes
* Skipping the last record in a fixed-length file causes an error.

### Enhancements
* Cleaned up the code to use latest C# 7.3 features
* Added enum generic-constraint for EnumColumn and type mapping methods.

## 2.1.1 (2018-06-11)
**Summary** - Remove unneeded references to .NET Standard projects when targeting .NET 4.5.1. Updated resource file to be recognized by project.

## 2.1.0 (2018-06-05)
**Summary** - Write files with multiple schemas.

### New Features
I wanted to make sure flat files consisting of multiple schemas could be generated similar to the way they are read. Parallel to the "selectors" used to read files, there are now injectors for writing files. This release introduces the `SeparatedValueSchemaInjector`, `FixedLengthSchemaInjector`, `SeparatedValueTypeMapperInjector` and the `FixedLengthTypeMapperInjector` classes.

### Enhancements
* Several minor performance enhancements for the `SeparatedValueWriter`  and `FixedLengthWriter` classes.

## 2.0.0 (2018-06-05)
**Summary** - Read files with multiple schemas.

### New Features
Several requests were made to support files containing multiple schemas. Especially in fixed-length files, the use of data blocks with header and footer records is normal. The introduction of the `SeparatedValueSchemaSelector`, `FixedLengthSchemaSelector`, `SeparatedValueTypeMapperSelector` and `FixedLengthTypeMapperSelector` make it possible take existing schemas and type mappers, respectively, and combine them so the appropriate schema is used to parse a record.

### Breaking Changes
In previous versions, records could be skipped using `Func` properties on the options object. A similar property existed for handling errors while processing files. However, the naming and usage was not obvious. Going forward, readers will expose events for registering callback methods which can be used to skip records. For example:

```csharp
var mapper = SeparatedValueTypeMapper.Define(() => new Person());
// ...configure the type mapper
var reader = mapper.GetReader(stringReader, options);
// Register a handler that fires any time a record is extracted
reader.RecordRead += (sender, e) =>
{
    e.IsSkipped = e.Values.Length > 0 && e.Values[0] == "#Comment";
};
// Read all the records, skipping records starting with '#Comment'.
var results = reader.ReadAll().ToArray();
```

The properties on the options object have been removed in favor of the new events. Read the [README](https://github.com/Cynicszm/FlatFiles/blob/master/README.md#skipping-records) for more details.
