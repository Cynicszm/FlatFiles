## 8.7.0 (planned)
**Not released, and not started.** What is written here is the order the remaining work will be done in, kept with the releases so that the reasoning sits beside what it produced. Nothing below breaks anything, so none of it is waiting for a major version.

### Next

In the order they will be built. None of these breaks anything, so none of them is waiting for a major version.

- **Header-driven column matching against a supplied schema, and what to do with a record that does not fit it.** The header row is read and thrown away: never checked against the schema, never used to order it. A file whose columns have been reordered since the schema was written is read straight into the wrong properties, silently, because position is all the reader has. Matching the header by name, and saying what happens when it disagrees - reorder, refuse, or ignore - is the largest gap left against CsvHelper, which maps by name and is the one competitor this repository benchmarks against. Reading by position stays the default, since a file with no header has nothing else to go on.

  The same feature settles what a record of the wrong length means, because today the two ends disagree and neither is configurable. A delimited record with **too few** fields is refused; one with **too many** is accepted, the first however many the schema declares kept and the rest discarded. So a record carrying a separator inside an unquoted field is read with every later value one position to the left, and nothing says so - whether anything notices depends on whether a shifted value reaches a column that will not parse it, which is a matter of where the separator fell rather than of the record being wrong. The fixed-length reader already has `IsRaggedRight` and `IsLongRecordRejected` for the same question and answers it differently again.

  What is wanted is one way of saying it on both readers: refuse a short record or pad it, refuse a long one or discard the surplus, with the current behaviour as the default so nothing changes for anyone who does not ask. Whatever is chosen has to be visible - a record silently read one column out of step is the worst of the available outcomes, and is what happens now.

- **A delimited selector should be able to match on the record's text.** `DelimitedTypeMapperSelector` and `DelimitedSchemaSelector` are given each record's values to choose a schema by, so the reader has to parse a record into an array of values before it can decide what the record is - which is the one thing that stops a selected delimited mapping being read straight onto an entity, as a fixed-length one now is. A predicate over the record's text, beside the one over its values, would leave the choice to the caller: match on text and keep the faster path, or match on values and pay for them.

**Considered and already covered.** Seven more ideas came out of the same review and turned out to need no work. They are recorded so that nobody spends an afternoon rediscovering it.

Already supported when the review looked:

- multi-character separators - `DelimitedOptions.Separator` is a string, not a character;
- quoting behaviour - `QuoteBehaviour` quotes only what needs it, or everything, or nothing;
- whitespace preservation - `DelimitedOptions.PreserveWhiteSpace`, alongside `Trim` on the string and character array columns;
- files holding more than one schema - the schema selectors and injectors, on both readers and writers;
- `IDataReader` - `FlatFileDataReader`, with `DataTable` support beside it.

Shipped since the review, by the releases named:

- cancellation tokens on every asynchronous read and write - 7.2.0;
- ragged-right fixed-length files - 7.3.0.

Nothing that review raised was declined outright.
## 8.6.0 (2026-09-26)
**Summary** - Three ways of saying what a file looks like that the library did not have. A mapping can be built from attributes on the type rather than written out in fluent calls; comments and blank lines can be passed over by an option rather than by a handler on every reader; and a reader can be handed a stream rather than a `TextReader`, with the byte order mark taken off. Nothing here breaks anything, and nothing here changes what an existing mapping costs.

**A reader can be given a stream rather than a `TextReader`.**

```csharp
using var stream = File.OpenRead( @"C:\path\to\file.csv" );
var reader = new DelimitedReader( stream, schema );
```

The same overload is on `FixedLengthReader`, on `Read` and `GetReader` for both type mappers - as default implementations, so anything implementing those interfaces keeps compiling - and on `GetAutoMappedReader` and its asynchronous twin. Each takes an optional `Encoding`, and UTF-8 is used when none is given.

**The reason to have it is the byte order mark**, which is honoured whatever encoding was asked for and always taken off the text. A mark left in becomes part of the first value - turning `Bob` into `\uFEFFBob`, or a header column into one that nothing matches - and it fails quietly, which is the worst way for it to fail. There are tests for it on a value, on a header, and through auto-mapping, and one that hands a UTF-16 stream to a reader told UTF-8 and gets UTF-16, because a file that says what it is should be read as what it says.

The stream is left open. A reader does not own what it was handed, and none of these readers is disposable, so closing it stays the job of whoever opened it.

**It is a convenience and not a saving, which is worth saying because this item was once ranked first for the opposite reason.** The claim was that it would take allocation below CsvHelper. Measured, decoding 1.36 MB of UTF-8 costs about a fifth of a millisecond and a byte a record, because a `StreamReader` reuses its buffers, and the same file read through one rather than through a `StringReader` allocates the same bytes a record either way. What it is worth having for is the file whose encoding has to be sniffed rather than assumed, and for not making every caller remember three arguments.

Writing still takes a `TextWriter`.

**Comments and blank lines can be passed over by an option.** Both option classes could express it only through a `RecordRead` handler on every reader, which is a lot of ceremony for something most files want:

```csharp
var options = new DelimitedOptions { CommentPrefix = "#", IsBlankRecordSkipped = true };
```

Both are on `FixedLengthOptions` too, and both are answered before anything is parsed, so a record passed over is not a record: it never reaches a schema selector, a handler, or the header. A file whose header sits under a banner of comments is read as though the banner were not there.

**The reason to prefer them over a handler is not taste.** Attaching a `RecordRead` handler to a delimited reader makes **every record in the file** copy its values out of the parser's buffer, whether the handler looks at them or not, because the handler is given an array it may write into. The options look at the record where it lies.

Two things worth recording, because both were found by the tests rather than by reasoning:

- **The two readers have different things to hand, so they are asked different questions.** A fixed-length reader has the record's text. A delimited one does not: it only builds that string where `PreserveRecordText` asks for it, and otherwise leaves it empty. Testing the text on both would have made the comment prefix never match on a delimited file - and made `IsBlankRecordSkipped` skip every record in it, since every record's text is empty. The delimited reader is asked about the values it has already split instead, where a comment carries the prefix on its first value whether or not it holds a separator.
- **A record passed over still counts physically.** The physical record number is where a record sits in the file, and an error that reports one is only useful if it agrees with what a text editor shows. The logical number, which counts the records a caller is given, does not move.

`CommentPrefix` is matched ordinally against the start of the record before any trimming a column would do, so an indented comment is a record. `IsBlankRecordSkipped` is off by default, because a blank line in a delimited file is a record of one empty value and some files mean it. Both reach `IOptions` with default implementations, so anything implementing that interface keeps compiling and keeps its behaviour.

**A mapping can be built from the attributes on the type.** The library had no attribute types at all: every mapping was written out in fluent calls, which is the right default for a file whose shape is not the class's business and the wrong one where a class exists to mirror a file.

```csharp
[IgnoredWindow( 2, 4 )]
public class Transaction
{
    [Column( Order = 0 ), Window( 12 )]
    public string Reference { get; set; }

    [Column( Order = 1, Format = "0.00" ), Window( 10, Alignment = FixedAlignment.RightAligned, FillCharacter = '0' )]
    public decimal Amount { get; set; }
}

var mapper = FixedLengthTypeMapper.DefineFromAttributes<Transaction>();
```

`DefineFromAttributes` is on both mappers. A member without `[Column]` is not mapped, and the column's type comes from the member's type, as it does for an auto-mapped file, so the attribute carries only what the member cannot say for itself: a name, an order and a format. A fixed-length mapping takes a `[Window]` as well, and `[IgnoredWindow]` on the class declares a stretch of the record no member maps to.

**Order is explicit on purpose.** Reflection does not promise the order it reports members in. The generator could read declaration order out of the source and deliberately does not, because generation can be turned off as of 8.5.0 and a mapping that changed shape depending on whether the generator ran would make that switch unsafe. A member that gives an order is placed by it; one that does not keeps the order reflection gave it, after all of them.

**Three decisions worth recording:**

- **The members are mapped through the dynamic configuration's typed property methods, not through `CustomMapping`.** Auto-mapping uses `CustomMapping` with a compiled setter, which is why an auto-mapped read cannot use the typed accessors. An attribute-built mapping takes the same path as a hand-written one, so it keeps the accessors that make a mapped read and write cost the same under Native AOT as anywhere else.
- **The generator was taught the new entry point in the same change**, in both places it needed teaching. It finds entities by the name of the call, so `DefineFromAttributes<T>()` would otherwise have been an entity it never saw - and an attribute-mapped type that silently lost its accessors and boxed every value under AOT would have undone 8.1.0 through 8.5.0 for exactly the callers most likely to adopt a new convenience. The cheap syntax filter needed the name and so did the symbol resolution; the first alone looked like it worked and wrote nothing. There is a test for it.
- **A format is applied by the column rather than through the mapping interface.** There are twenty-seven of those and no shared base, so `TrySetInputFormat` and `TrySetOutputFormat` are internal virtuals on `ColumnDefinition`, answered by the six date and time columns and by `NumberColumn` for writing. That follows `UsesFormatProvider` from 8.5.0, and avoids the reflection over property names that 8.3.0 removed from `Mapper` for being something a trimmer may delete.

**What it refuses rather than guesses:** a type with no `[Column]` members at all, two columns claiming the same order, a fixed-length member missing its order or its window, and a member of a type no column reads. Each throws a `FlatFileException` naming the type and the member.

One behaviour found rather than chosen: an attribute-mapped enum writes its numeric value, because that is `EnumColumn`'s default formatter. The attributes do not set one, so a caller who wants the name sets it fluently as they would on any hand-written mapping. The test that expected otherwise was wrong and was changed; the library was not.

The integration baseline does not move: nothing here changes what the existing mappings cost.

## 8.5.0 (2026-09-25)
**Summary** - The generator can be told to write nothing, and a format provider stops costing an allocation for every column of every record. The second is the larger of the two by a long way: setting a culture is an ordinary thing to do, and it cost 40 bytes a column on reading and on writing alike, which on the widest sample was 15 KB a record. A delimited write of that sample allocated 15,405 bytes a record when 8.4.0 shipped and now allocates 75. Nothing here breaks anything.

This release also corrects something 8.4.0's entry claimed, and fixes the measurement that led to it. Both are below.

**The source generator can be told to write nothing.** It writes accessor registrations for every entity it finds at a `Define<T>()` or `DefineDynamic( typeof( T ) )` call site, which is what makes it worth having and is also not everybody's preference. `ExcludeAssets="analyzers"` on the package reference does not turn it off - tried, and the registrations are still written - so until now a caller who wanted it off had no way to ask.

```xml
<PropertyGroup>
  <FlatFilesGenerateAccessors>false</FlatFilesGenerateAccessors>
</PropertyGroup>
```

The package carries a props file declaring the property to the compiler, in `build` for a project that references the package and `buildTransitive` for one that gets it through another. Absent means yes, and so does a value that is not a boolean: a caller who has not asked gets what the package is for, and one who has typed the value wrong is better served by the generator carrying on than by it silently doing nothing. Nothing breaks when it is off - a mapping builds its own accessors at run time, exactly as it did before the generator existed.

It was checked through a real package reference rather than only in the generator's tests, because that is where this would fail: packed to a version of its own to dodge NuGet's cache, referenced from a local feed, and built three ways. By default the consumer gets `Customer.Accessors.g.cs`; with the property set on the command line or in the project file it gets nothing, and all three builds succeed.

**Setting a format provider used to cost an allocation per column per record, and now costs nothing.** A column reaches the options' provider only through a column context, so one was built for every column of every record wherever the options carried one - 40 bytes apiece, on reading and writing alike. Most columns never ask: text, a boolean, a byte array. On the widest sample that was 15 KB a record spent so that a handful of date columns could ask a question the others never ask.

Three changes, each measured on its own:

- **A column that never consults a provider is no longer given a context.** Six of the library's columns ask - the four date and time ones and `NumberColumn`, which covers eleven numeric columns - and they say so. A conversion column defers to the column it wraps, and anything implementing `IColumnDefinition` from outside the library is assumed to want one, as it already was for every other purpose.
- **A context built only so a column can ask is built once per column**, not once per value, and pointed at each record in turn. Nothing else ever sees it: it goes to a library column with no hook attached, which reads the provider off and lets go. Everything that reaches user code - a hook, a metadata or complex column, a column declared elsewhere, and **every error recovery** - is still given a context of its own, because a handler may keep hold of one.
- **An execution context is cached per schema rather than one at a time.** The cache held a single entry and its own comment admitted the gap: a schema selector "misses only when consecutive records take different schemas", which for a file of fourteen record layouts is nearly every record. So the context, its private options clone and now its column contexts were being rebuilt per record. The single entry is still the fast path and still a reference comparison; the rest sit in a map beside it, one per layout, which a selector or injector bounds.

The third of those improves reading a file of several layouts even where no provider is set at all: `read-typed` on the fourteen-layout sample goes from 3,574 bytes a record to 3,494, and `read-mapper` on the shortest from 805 to 778.

**What that change bought**, against the figures 8.4.0 recorded. They fall again below, for a different reason:

| Sample | `write-text` | `write-typed` | `write-mapper` |
| --- | ---: | ---: | ---: |
| S1/S1, 379 columns | 15,405 -> **245** | 15,405 -> **245** | 15,408 -> **248** |
| S1/S2, 344,352 records | 2,410 -> **90** | 2,410 -> **90** | 2,411 -> **91** |
| S1/S3, every field quoted | 8,074 -> **233** | 8,074 -> **234** | 8,076 -> **236** |
| S2/S1, fourteen layouts | 3,867 -> **367** | 3,867 -> **367** | 4,088 -> **508** |
| S2/S2, 235 columns | 13,136 -> **3,689** | 13,138 -> **3,696** | 13,458 -> **3,957** |
| S2/S3, shortest record | 1,668 -> **423** | 1,668 -> **423** | 1,842 -> **570** |

Giving a column its own type is now free on the way out: `write-typed` costs what `write-text` costs on every sample. Time fell with it - writing the 235-column sample went from 67 ms to 42, and the fourteen-layout one from 272 ms to 160.

**A correction to what 8.4.0's entry says.** That entry reported that "the delimited writer allocates about twice what the delimited reader does", and it is wrong. The writing scenarios set a format provider for the sake of a reproducible hash and the reading ones did not, so the two were not asked the same question, and the difference between them was the per-column context described above rather than anything about writing. Measured like for like, a delimited write of the widest sample allocates **245 bytes a record against that read's 5,685**: writing was already the cheaper direction by a wide margin, and is now cheaper still. The figures in that entry are what was measured; the conclusion drawn from them was not sound, and the 8.4.0 entry is left as it was released rather than rewritten after the fact.

**The integration harness had been measuring its own buffers.** `WriteRun` opened its output with a megabyte of `FileStream` buffer and a megabyte of `StreamWriter` buffer, inside the measured region, and the samples have between 1,790 and 344,352 records to divide that by. On the sample of 1,790 it was **3,601 of the 3,689 bytes a record** the table reported. The reading side carried a megabyte of its own.

The tell was in the totals rather than the figures: three fixed-length samples of 235, 34 and 172 columns and 1,790, 18,047 and 22,481 records reported 6.6, 7.6 and 8.3 MB - near enough the same from very different files, which is what a constant divided by the records looks like. A micro-benchmark reproduced 3,601 against the harness's 3,689 and then dropped to 95 bytes a record on a four-kilobyte buffer and 81 on no file at all.

Both sides now use the framework's own buffer sizes, because a buffer the harness picks is one the library is then judged on. What this fixes is not cosmetic: three megabytes of constant diluted the check to the point of blindness. A regression of fifty bytes a record on the shortest sample would have moved its figure by 1.4%, inside the two percent the check allows, and gone out unremarked. The same regression now moves it by 28%.

It follows that a claim on the 8.5.0 list was wrong as well - that a fixed-length write of a wide record still cost about fifteen bytes a column. It cost nothing of the sort: the 235-column sample writes at **179 bytes a record** and the 34-column one at 168, so writing a fixed-length record barely costs per column at all. That item is struck rather than carried forward.

The baseline moves, so the full table is here - six samples, seven scenarios apiece, from the runs the baseline was taken from.

**Delimited, read**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | `read-parse` | 1.52 s | 1.23 s - 1.80 s | 24.2 | 5,657 | 12.5 MB | 49.2 MB |
| S1/S1 | 379 | 37,031 | `read-typed` | 1.56 s | 1.43 s - 1.96 s | 23.6 | 5,601 | 12.6 MB | 50.4 MB |
| S1/S1 | 379 | 37,031 | `read-values` | 1.49 s | 1.45 s - 1.52 s | 24.7 | 8,657 | 12.6 MB | 50.5 MB |
| | | | | | | | | | |
| S1/S2 | 58 | 344,352 | `read-parse` | 1.98 s | 1.85 s - 2.16 s | 51.3 | 1,411 | 12.4 MB | 49.1 MB |
| S1/S2 | 58 | 344,352 | `read-typed` | 4.73 s | 3.12 s - 5.78 s | 21.5 | 1,335 | 12.5 MB | 50.3 MB |
| S1/S2 | 58 | 344,352 | `read-values` | 3.17 s | 2.67 s - 3.70 s | 32.0 | 1,823 | 12.5 MB | 50.2 MB |
| | | | | | | | | | |
| S1/S3 | 196 | 39,337 | `read-parse` | 877 ms | 818 ms - 978 ms | 51.0 | 3,501 | 12.5 MB | 49.2 MB |
| S1/S3 | 196 | 39,337 | `read-typed` | 1.15 s | 1.12 s - 1.18 s | 39.0 | 2,961 | 12.4 MB | 51.2 MB |
| S1/S3 | 196 | 39,337 | `read-values` | 1.09 s | 1.08 s - 1.11 s | 40.9 | 4,553 | 12.4 MB | 51.1 MB |

**Fixed-length, read**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | `read-parse` | 216 ms | 197 ms - 234 ms | 74.6 | 3,609 | 12.8 MB | 49.8 MB |
| S2/S1 | 172 | 22,481 | `read-typed` | 263 ms | 248 ms - 276 ms | 61.3 | 3,447 | 12.5 MB | 51.9 MB |
| S2/S1 | 172 | 22,481 | `read-values` | 272 ms | 261 ms - 278 ms | 59.3 | 4,155 | 12.8 MB | 52.0 MB |
| | | | | | | | | | |
| S2/S2 | 235 | 1,790 | `read-parse` | 74 ms | 71 ms - 80 ms | 80.4 | 11,429 | 11.9 MB | 48.8 MB |
| S2/S2 | 235 | 1,790 | `read-typed` | 114 ms | 103 ms - 122 ms | 52.2 | 10,605 | 11.1 MB | 50.8 MB |
| S2/S2 | 235 | 1,790 | `read-values` | 123 ms | 116 ms - 136 ms | 48.7 | 12,507 | 11.8 MB | 50.8 MB |
| | | | | | | | | | |
| S2/S3 | 34 | 18,047 | `read-parse` | 93 ms | 88 ms - 98 ms | 46.5 | 1,773 | 11.8 MB | 49.2 MB |
| S2/S3 | 34 | 18,047 | `read-typed` | 150 ms | 146 ms - 151 ms | 29.0 | 1,539 | 12.3 MB | 51.1 MB |
| S2/S3 | 34 | 18,047 | `read-values` | 166 ms | 144 ms - 239 ms | 26.1 | 1,807 | 11.9 MB | 51.1 MB |

**Delimited, written**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | `write-text` | 1.24 s | 1.16 s - 1.32 s | 28.9 | 75 | 309.5 MB | 354.8 MB |
| S1/S1 | 379 | 37,031 | `write-typed` | 1.65 s | 1.46 s - 1.76 s | 22.9 | 75 | 308.0 MB | 354.4 MB |
| | | | | | | | | | |
| S1/S2 | 58 | 344,352 | `write-text` | 3.86 s | 2.05 s - 5.62 s | 24.3 | 72 | 619.2 MB | 689.1 MB |
| S1/S2 | 58 | 344,352 | `write-typed` | 2.67 s | 2.57 s - 2.78 s | 37.9 | 72 | 594.3 MB | 659.8 MB |
| | | | | | | | | | |
| S1/S3 | 196 | 39,337 | `write-text` | 763 ms | 726 ms - 794 ms | 58.6 | 74 | 193.2 MB | 236.9 MB |
| S1/S3 | 196 | 39,337 | `write-typed` | 1.08 s | 1.06 s - 1.10 s | 42.1 | 74 | 173.0 MB | 218.0 MB |

**Fixed-length, written**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | `write-text` | 199 ms | 195 ms - 202 ms | 79.7 | 87 | 49.1 MB | 98.1 MB |
| S2/S1 | 172 | 22,481 | `write-typed` | 247 ms | 237 ms - 250 ms | 64.1 | 88 | 43.9 MB | 90.5 MB |
| | | | | | | | | | |
| S2/S2 | 235 | 1,790 | `write-text` | 60 ms | 55 ms - 67 ms | 99.4 | 179 | 12.2 MB | 64.4 MB |
| S2/S2 | 235 | 1,790 | `write-typed` | 74 ms | 72 ms - 78 ms | 80.8 | 186 | 17.0 MB | 61.6 MB |
| | | | | | | | | | |
| S2/S3 | 34 | 18,047 | `write-text` | 68 ms | 58 ms - 98 ms | 64.1 | 75 | 29.1 MB | 72.8 MB |
| S2/S3 | 34 | 18,047 | `write-typed` | 106 ms | 99 ms - 111 ms | 40.9 | 75 | 23.8 MB | 73.9 MB |

**Delimited, read through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | 935 ms | 917 ms - 954 ms | 39.3 | 193 | 8.0 MB | 47.2 MB |
| S1/S2 | 58 | 344,352 | 1.74 s | 1.63 s - 1.93 s | 58.4 | 192 | 12.4 MB | 52.4 MB |
| S1/S3 | 196 | 39,337 | 719 ms | 665 ms - 765 ms | 62.2 | 174 | 7.7 MB | 46.9 MB |

**Fixed-length, read through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | 279 ms | 259 ms - 295 ms | 57.8 | 1,757 | 12.9 MB | 56.2 MB |
| S2/S2 | 235 | 1,790 | 157 ms | 149 ms - 169 ms | 38.0 | 7,505 | 11.9 MB | 53.7 MB |
| S2/S3 | 34 | 18,047 | 156 ms | 152 ms - 162 ms | 27.8 | 719 | 11.8 MB | 53.3 MB |

**Delimited, written through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | 934 ms | 930 ms - 948 ms | 15.2 | 78 | 12.7 MB | 54.0 MB |
| S1/S2 | 58 | 344,352 | 1.73 s | 1.67 s - 1.80 s | 17.8 | 72 | 55.8 MB | 108.4 MB |
| S1/S3 | 196 | 39,337 | 714 ms | 663 ms - 767 ms | 32.5 | 76 | 12.5 MB | 54.0 MB |

**Fixed-length, written through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | 256 ms | 244 ms - 272 ms | 61.9 | 228 | 9.9 MB | 63.1 MB |
| S2/S2 | 235 | 1,790 | 82 ms | 79 ms - 88 ms | 72.8 | 443 | 4.7 MB | 57.3 MB |
| S2/S3 | 34 | 18,047 | 108 ms | 98 ms - 115 ms | 40.1 | 222 | 14.1 MB | 59.3 MB |

Each row is one sample handled 5 times, each in a process of its own that starts, reads or writes the
file once and exits - which is how the library is mostly used - and the figures are the mean of those 5.
Everything a job pays for is inside them: the runtime compiling the parse or format path on first use, the
schema being built, the file being opened. The three reading scenarios are cumulative:
`read-parse` reads every column as text and asks for no value, `read-typed` gives each single-typed column
its own type, and `read-values` is `read-typed` with `GetValues` called on every record. The difference
between two of them is the cost of the step between.

`write-text` and `write-typed` are the same pair the other way round: the records read out of the sample
and written back, as text and with each single-typed column given its own type. The records are read into
memory first and that is **not** measured, so what these rows report is the write. It is also why their
peak figures are large: the whole sample is being held, which on the widest of them is several hundred
megabytes that the writer has nothing to do with.

The mapped scenarios have tables of their own because they are not a further step but a different path:
the file read onto entities, or written from them, through a type mapper - the only scenarios that build
an entity or use the accessors a mapper makes per column. They map the first column of each kind the
profile knows about and ignore the rest, which costs the reader the column but not the parse, so their
figures are far below the others and mean something different. Read each set against itself and against
the same set in an earlier release.

| Column | What it measures |
| --- | --- |
| Columns | Columns in the schema; for a fixed-length sample, in its widest record layout. |
| Records | Records the reader yielded. Gated exactly. No sample is built to have a record refused, so any refusal fails the check whatever else agrees. |
| Mean Total Time | Mean wall clock of 5 cold runs: opening the file, building the schema, constructing the reader or writer, taking or writing every record, and disposing, in a process that has done nothing else. Nothing is amortised over work a real caller never performs. **Reported, never gated.** |
| Range | The quickest and slowest of those 5 loads, so the spread behind the mean is visible rather than implied. |
| MB/s | File size divided by Mean Total Time, so it carries the same caveats. |
| Bytes/record | Mean bytes allocated across a load or a write, divided by records. Deterministic for given bytes on a given runtime, and repeats to within 0.1% here. **Gated at 2%.** |
| Peak heap | The largest the managed heap reached in any of the 5 loads, sampled every 5 ms. Reported. |
| Peak working set | The largest peak working set any of those processes reached. Each does one load and exits, so the figure is a whole job's footprint, most of it runtime start-up rather than the read. Reported. |


Each process runs with tiered compilation off, so every method is compiled optimised the first time it is
called. The run is still cold - it pays for compiling the path it takes - but what it allocates no longer
depends on how far the runtime got before promoting anything, which on the sample of twenty-six record
layouts moved a writing figure by a fifth from one process to the next. Both delimited samples measure
identically either way.

Averaging 5 whole processes takes most of the machine noise out, but a cold start is noisy by nature and
the range shows what is left. `FlatFiles.Benchmark` is the project that measures a warm steady state, with
statistics rather than a mean; these figures are the other question - what one job costs end to end - and
show the shape of the work rather than a number to compare release to release, which is why neither Mean
Total Time nor MB/s is gated.

Whether `write-text` wrote the file it read: S1/S1 `write-text` differs; S1/S2 `write-text` differs; S1/S3 `write-text` differs; S2/S1 `write-text` differs; S2/S2 `write-text` same; S2/S3 `write-text` same. A sample it cannot
reproduce is not a fault - a string column trims, so a field of spaces comes back empty; some
fixed-length layouts have windows that stop short of the record, so its tail is never read; and a
ragged final column's padding is not part of its value. What each write produced is pinned by hash in
the baseline either way, so a change in the bytes written fails the check whatever this says.

## 8.4.0 (2026-09-25)
**Summary** - Writing is measured now, in both of the places that measure. Twelve more benchmarks, three integration scenarios, and a gate that pins what a write produced by hash rather than trusting that it still looks right. Nothing the library does changes: the only edits to the library itself are internal names brought into line with the spelling rule the repository has carried since before 8.0.0, so an 8.4.0 assembly behaves exactly as an 8.3.0 one does. What the release buys is that the next change to the writing path cannot move without being seen - which is the thing 8.3.0 could not have said about itself.

**Writing is measured now, in both places that measure.** Reading has been measured from two directions for several releases: `FlatFiles.Benchmark` for a warm steady state and `FlatFiles.IntegrationTest` for what one cold job costs end to end. Writing had three benchmarks against nineteen for reading, and no integration scenario at all - so the release before last could move a write from 515 bytes a record to 79 with nothing in the repository able to say so afterwards, and a regression putting it back would have gone out unremarked.

**The benchmark suite has twelve more, one for every reading case that had no counterpart**: no schema, a schema with values, a type mapper, asynchronously, quoted fields, fields rather than properties, unoptimised, custom mapping both ways, auto-mapped, fixed-length through a schema, CsvHelper asynchronously, and a floor that builds the same text with a `StringBuilder`. Every benchmark's name now ends in the direction it measures, so `--filter *_Read` runs one half and `--filter *_Write` the other, and the command line is passed through to BenchmarkDotNet for that to be worth anything. The suite also reports allocation now, which it did not: the whole of the last release was about what the writing path allocates, against a suite that could not have shown it.

They were checked before being trusted. Every delimited case writing the same records agrees to the character - 1,140,119 bytes for the nine that write a header and 1,140,000 for the schemaless one that does not, the difference being exactly the header - which is what makes the floor a floor rather than a different measurement. Measured against a discarding writer, so that the output buffer does not swamp it, a mapped write costs **75 bytes a record** against **72** for handing the writer an array of values: the mapper adds three. `OptimiseMapping( false )` costs nothing on the writing path any more, which is worth knowing and was not.

**The integration gate has three writing scenarios**, `write-text`, `write-typed` and `write-mapper`, mirroring the reading ones - which are now `read-parse`, `read-typed`, `read-values` and `read-mapper`, named the same way and measuring exactly what they did before. Each reads its records out of the sample before the measurement starts, so what it reports is the write. A fixed-length sample of several layouts is written through a `FixedLengthSchemaInjector`, one schema per layout, which is what a caller with such a file writes.

**What each write produced is pinned by hash**, and that is the stronger half of the gate. Whether a write reproduces the file it read is reported rather than gated, because four of the six samples cannot reproduce it and are not meant to: a string column trims, so a field of eleven spaces comes back empty; some fixed-length layouts have windows that stop short of the record, so its tail is never read; and a ragged final column's padding is not part of its value. Pinning the bytes instead means a writer that quietly starts writing something else fails whether or not its output ever matched the sample. The writing scenarios format with the invariant culture so the hash means the same thing on the runner as it does on a desk.

**The measured processes now run with tiered compilation off**, which the first writing figures made necessary. Tier-0 code on the formatting path allocates about twice what optimised code does, and the sample of twenty-six record layouts warms each of them separately, so its writing scenarios measured anywhere between 6,355 and 7,752 bytes a record from one process to the next - a fifth of the figure, from nothing but how far the runtime had got before it promoted anything. A gate cannot live with that. With every method compiled optimised on its first call the same measurement repeats to two bytes. The run is still cold and still pays for compiling the path it takes; what it no longer reports is the runtime's warm-up. It changes almost nothing else - both delimited samples measure identically either way and the fixed-length reads move by under 1% - but it does mean the fixed-length writing figures below are half what they would have been, and the earlier ones were the JIT rather than the library.

**Three things the figures say**, none of which anything in the repository could have said a week ago:

- **The delimited writer allocates about twice what the delimited reader does; the fixed-length writer costs about what the fixed-length reader costs.** Delimited: 5,685 bytes a record read against 15,405 written, 1,414 against 2,410, 3,528 against 8,074. Fixed-length: 3,735 against 3,867, 12,068 against 13,136, and 1,859 against 1,668 - which is a write costing *less* than the read of the same file. Whatever is worth doing next to the writing path, it is in the delimited half of it.
- **A mapped write costs what a write through a schema costs.** Delimited, within three bytes a record - 15,408 against 15,405, 2,411 against 2,410, 8,076 against 8,074 - which is the typed getters of 8.3.0 seen at a scale the benchmarks cannot reach. Fixed-length costs about 5% more. A mapped *read* is far cheaper than a read through a schema, because the mapping ignores most columns and never parses them; a mapped write still writes every column, so it cannot be.
- **Giving a column its own type costs time on the way out but not allocation.** `write-typed` and `write-text` allocate the same bytes a record to the byte on every sample, and `write-typed` takes between a quarter and a third longer.

**Spelling, everywhere it was ours to choose.** `Conventions.md` has asked for British English in identifiers and comments since before 8.0.0, and the library's own internals had not been swept: `Serialize`, `Serializer`, `Deserializer` and `Initialize` on the type-mapper injectors, selectors, multiplexing readers and typed reader and writer, along with thirteen doc comments offering "further customizations". All of them are internal or private, so no published name moved and package validation has nothing to say. The test and benchmark projects had `Unoptimized` in six method names and a handful of locals. What the framework spells the American way is left alone, as the convention already says: `ModuleInitializer`, `IIncrementalGenerator.Initialize`, `AssemblyInitialize`, `JsonSerializer`, `OperationCanceledException`.

The baseline moves, so the full table is here - six samples, seven scenarios apiece, from the runs the baseline was taken from. The reading figures are unchanged from 8.3.0 within measurement noise, which is what a rename of the scenarios should do.

**Delimited, read**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | `read-parse` | 915 ms | 892 ms - 935 ms | 40.2 | 5,685 | 13.5 MB | 48.8 MB |
| S1/S1 | 379 | 37,031 | `read-typed` | 1.15 s | 1.14 s - 1.16 s | 32.1 | 5,629 | 13.6 MB | 50.0 MB |
| S1/S1 | 379 | 37,031 | `read-values` | 1.17 s | 1.15 s - 1.21 s | 31.4 | 8,685 | 13.5 MB | 50.0 MB |
| | | | | | | | | | |
| S1/S2 | 58 | 344,352 | `read-parse` | 1.49 s | 1.42 s - 1.57 s | 68.3 | 1,414 | 13.4 MB | 48.5 MB |
| S1/S2 | 58 | 344,352 | `read-typed` | 1.90 s | 1.88 s - 1.92 s | 53.5 | 1,338 | 13.4 MB | 49.9 MB |
| S1/S2 | 58 | 344,352 | `read-values` | 1.94 s | 1.90 s - 2.03 s | 52.4 | 1,826 | 13.5 MB | 49.8 MB |
| | | | | | | | | | |
| S1/S3 | 196 | 39,337 | `read-parse` | 776 ms | 725 ms - 866 ms | 57.6 | 3,528 | 13.4 MB | 48.8 MB |
| S1/S3 | 196 | 39,337 | `read-typed` | 1.34 s | 1.17 s - 1.75 s | 33.4 | 2,988 | 13.5 MB | 50.8 MB |
| S1/S3 | 196 | 39,337 | `read-values` | 1.31 s | 1.19 s - 1.53 s | 34.2 | 4,580 | 13.4 MB | 50.8 MB |

**Fixed-length, read**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | `read-parse` | 185 ms | 176 ms - 195 ms | 87.0 | 3,735 | 13.3 MB | 49.5 MB |
| S2/S1 | 172 | 22,481 | `read-typed` | 248 ms | 234 ms - 289 ms | 65.0 | 3,574 | 13.7 MB | 51.5 MB |
| S2/S1 | 172 | 22,481 | `read-values` | 264 ms | 237 ms - 307 ms | 61.0 | 4,282 | 13.5 MB | 51.4 MB |
| | | | | | | | | | |
| S2/S2 | 235 | 1,790 | `read-parse` | 73 ms | 67 ms - 85 ms | 81.7 | 12,068 | 12.1 MB | 48.2 MB |
| S2/S2 | 235 | 1,790 | `read-typed` | 112 ms | 104 ms - 124 ms | 53.4 | 11,245 | 12.5 MB | 50.5 MB |
| S2/S2 | 235 | 1,790 | `read-values` | 108 ms | 99 ms - 122 ms | 55.4 | 13,147 | 12.3 MB | 50.3 MB |
| | | | | | | | | | |
| S2/S3 | 34 | 18,047 | `read-parse` | 87 ms | 83 ms - 96 ms | 49.8 | 1,859 | 12.8 MB | 48.6 MB |
| S2/S3 | 34 | 18,047 | `read-typed` | 142 ms | 137 ms - 152 ms | 30.5 | 1,624 | 12.7 MB | 50.7 MB |
| S2/S3 | 34 | 18,047 | `read-values` | 144 ms | 136 ms - 154 ms | 30.2 | 1,892 | 12.9 MB | 50.7 MB |

**Delimited, written**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | `write-text` | 1.08 s | 1,000 ms - 1.17 s | 33.3 | 15,405 | 324.0 MB | 375.0 MB |
| S1/S1 | 379 | 37,031 | `write-typed` | 1.39 s | 1.32 s - 1.41 s | 27.2 | 15,405 | 322.0 MB | 374.9 MB |
| | | | | | | | | | |
| S1/S2 | 58 | 344,352 | `write-text` | 2.20 s | 1.66 s - 4.28 s | 42.7 | 2,410 | 624.2 MB | 680.4 MB |
| S1/S2 | 58 | 344,352 | `write-typed` | 2.13 s | 2.03 s - 2.33 s | 47.6 | 2,410 | 599.3 MB | 656.5 MB |
| | | | | | | | | | |
| S1/S3 | 196 | 39,337 | `write-text` | 825 ms | 782 ms - 871 ms | 54.2 | 8,074 | 207.1 MB | 257.5 MB |
| S1/S3 | 196 | 39,337 | `write-typed` | 1.09 s | 1.04 s - 1.14 s | 41.9 | 8,074 | 187.0 MB | 234.4 MB |

**Fixed-length, written**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | `write-text` | 272 ms | 244 ms - 332 ms | 58.4 | 3,867 | 59.1 MB | 103.1 MB |
| S2/S1 | 172 | 22,481 | `write-typed` | 274 ms | 247 ms - 297 ms | 57.7 | 3,867 | 56.8 MB | 99.9 MB |
| | | | | | | | | | |
| S2/S2 | 235 | 1,790 | `write-text` | 67 ms | 61 ms - 75 ms | 88.7 | 13,136 | 26.8 MB | 65.0 MB |
| S2/S2 | 235 | 1,790 | `write-typed` | 82 ms | 72 ms - 89 ms | 73.1 | 13,138 | 26.1 MB | 65.3 MB |
| | | | | | | | | | |
| S2/S3 | 34 | 18,047 | `write-text` | 95 ms | 91 ms - 100 ms | 45.8 | 1,668 | 36.8 MB | 78.9 MB |
| S2/S3 | 34 | 18,047 | `write-typed` | 129 ms | 114 ms - 142 ms | 33.7 | 1,668 | 35.9 MB | 77.6 MB |

**Delimited, read through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | 726 ms | 696 ms - 757 ms | 50.6 | 221 | 9.0 MB | 46.8 MB |
| S1/S2 | 58 | 344,352 | 2.18 s | 1.27 s - 3.01 s | 46.6 | 196 | 13.5 MB | 52.0 MB |
| S1/S3 | 196 | 39,337 | 675 ms | 618 ms - 734 ms | 66.2 | 200 | 8.7 MB | 46.4 MB |

**Fixed-length, read through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | 266 ms | 254 ms - 278 ms | 60.6 | 1,884 | 13.6 MB | 55.8 MB |
| S2/S2 | 235 | 1,790 | 150 ms | 143 ms - 156 ms | 39.8 | 8,146 | 10.8 MB | 53.2 MB |
| S2/S3 | 34 | 18,047 | 165 ms | 156 ms - 173 ms | 26.3 | 805 | 12.3 MB | 52.7 MB |

**Delimited, written through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | 897 ms | 867 ms - 917 ms | 15.8 | 15,408 | 21.9 MB | 65.9 MB |
| S1/S2 | 58 | 344,352 | 1.75 s | 1.55 s - 1.99 s | 17.6 | 2,411 | 61.9 MB | 107.4 MB |
| S1/S3 | 196 | 39,337 | 683 ms | 659 ms - 704 ms | 34.0 | 8,076 | 22.4 MB | 66.3 MB |

**Fixed-length, written through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | 264 ms | 231 ms - 298 ms | 60.2 | 4,088 | 20.2 MB | 66.3 MB |
| S2/S2 | 235 | 1,790 | 85 ms | 80 ms - 87 ms | 70.5 | 13,458 | 16.4 MB | 59.0 MB |
| S2/S3 | 34 | 18,047 | 106 ms | 99 ms - 125 ms | 40.8 | 1,842 | 19.1 MB | 61.6 MB |

Each row is one sample handled 5 times, each in a process of its own that starts, reads or writes the
file once and exits - which is how the library is mostly used - and the figures are the mean of those 5.
Everything a job pays for is inside them: the runtime compiling the parse or format path on first use, the
schema being built, the file being opened. The three reading scenarios are cumulative:
`read-parse` reads every column as text and asks for no value, `read-typed` gives each single-typed column
its own type, and `read-values` is `read-typed` with `GetValues` called on every record. The difference
between two of them is the cost of the step between.

`write-text` and `write-typed` are the same pair the other way round: the records read out of the sample
and written back, as text and with each single-typed column given its own type. The records are read into
memory first and that is **not** measured, so what these rows report is the write. It is also why their
peak figures are large: the whole sample is being held, which on the widest of them is several hundred
megabytes that the writer has nothing to do with.

The mapped scenarios have tables of their own because they are not a further step but a different path:
the file read onto entities, or written from them, through a type mapper - the only scenarios that build
an entity or use the accessors a mapper makes per column. They map the first column of each kind the
profile knows about and ignore the rest, which costs the reader the column but not the parse, so their
figures are far below the others and mean something different. Read each set against itself and against
the same set in an earlier release.

| Column | What it measures |
| --- | --- |
| Columns | Columns in the schema; for a fixed-length sample, in its widest record layout. |
| Records | Records the reader yielded. Gated exactly. No sample is built to have a record refused, so any refusal fails the check whatever else agrees. |
| Mean Total Time | Mean wall clock of 5 cold runs: opening the file, building the schema, constructing the reader or writer, taking or writing every record, and disposing, in a process that has done nothing else. Nothing is amortised over work a real caller never performs. **Reported, never gated.** |
| Range | The quickest and slowest of those 5 loads, so the spread behind the mean is visible rather than implied. |
| MB/s | File size divided by Mean Total Time, so it carries the same caveats. |
| Bytes/record | Mean bytes allocated across a load or a write, divided by records. Deterministic for given bytes on a given runtime, and repeats to within 0.1% here. **Gated at 2%.** |
| Peak heap | The largest the managed heap reached in any of the 5 loads, sampled every 5 ms. Reported. |
| Peak working set | The largest peak working set any of those processes reached. Each does one load and exits, so the figure is a whole job's footprint, most of it runtime start-up rather than the read. Reported. |


Each process runs with tiered compilation off, so every method is compiled optimised the first time it is
called. The run is still cold - it pays for compiling the path it takes - but what it allocates no longer
depends on how far the runtime got before promoting anything, which on the sample of twenty-six record
layouts moved a writing figure by a fifth from one process to the next. Both delimited samples measure
identically either way.

Averaging 5 whole processes takes most of the machine noise out, but a cold start is noisy by nature and
the range shows what is left. `FlatFiles.Benchmark` is the project that measures a warm steady state, with
statistics rather than a mean; these figures are the other question - what one job costs end to end - and
show the shape of the work rather than a number to compare release to release, which is why neither Mean
Total Time nor MB/s is gated.

Whether `write-text` wrote the file it read: S1/S1 `write-text` differs; S1/S2 `write-text` differs; S1/S3 `write-text` differs; S2/S1 `write-text` differs; S2/S2 `write-text` same; S2/S3 `write-text` same. A sample it cannot
reproduce is not a fault - a string column trims, so a field of spaces comes back empty; some
fixed-length layouts have windows that stop short of the record, so its tail is never read; and a
ragged final column's padding is not part of its value. What each write produced is pinned by hash in
the baseline either way, so a change in the bytes written fails the check whatever this says.

## 8.3.0 (2026-09-25)
**Summary** - A mapped read and a mapped write now cost the same whether or not the runtime can generate code, and a write costs a fraction of what it did either way. A delimited write through a type mapper allocated 515 bytes a record and now allocates 79; without dynamic code, 513 and now 76. A delimited read without dynamic code allocated 622 and now allocates 359, which is what the same read costs with a JIT. The package carries a source generator that arranges all of it, and a caller who does nothing gets all of it. Nothing here breaks anything.

**What was wrong.** 8.1.0 gave a type mapper a path that reads a record onto an entity without boxing any value, by closing `ColumnSetter<TEntity, T>` over each column's type. Closing a generic over a type needs `MakeGenericType` and a delegate built from a `MethodInfo`, and a runtime without dynamic code can do neither for a value type it was not built with. So under Native AOT the mapper fell back to reflection and a box for every value, and what 8.1.0 bought never reached AOT at all. Writing was worse and in a different way: it boxed every value on every runtime, JIT or not.

**A caller can hand a mapping its accessors.** `FlatFiles.CodeGeneration.MappingAccessors` takes them: `AddFactory`, `AddSetter`, `AddNullableSetter`, `AddGetter` and `AddNullableGetter`. Each registration closes its own generic types where it is written, which is the whole mechanism - nothing is closed later, so nothing has to be closed at run time.

Registering nothing changes nothing, which is what makes it safe to adopt a type at a time. So does registering the wrong thing: a registration written for a column of another type is refused where it is looked up, and the mapping builds its own accessor as it would have anyway. A member belongs to the type that **declares** it, so a property hiding one of the same name on a base class is a registration of its own.

**And the package writes those registrations for you.** A source generator ships in the package, under `analyzers/dotnet/cs`. It looks for the calls that name an entity - `Define<T>()` and `DefineDynamic( typeof( T ) )` on either mapper - and writes a registration for each type it finds, from a module initialiser. A caller does nothing.

It writes for a public property whose type a column carries, and for the nullable form of one. The two directions are asked about separately, a property being free to allow one and not the other: a get-only property, or one whose setter is private or `init`, is written from and not read onto. Every direction it cannot write for is reported at information level, naming which way round it was and why - no setter, a setter that is not public, an init-only setter, a getter that is not public, or a type no column carries. Information rather than a warning, because a member that falls back is slower rather than wrong; said at all, because a member quietly reverting to the slower path is how an expensive mistake hides, which is exactly what happened between 8.1.0 and 8.2.0.

A mapping the generator does not find is unaffected, and `MappingAccessors` stays open for what the search cannot see - a type named only at run time, or one from an assembly that does not reference the generator.

**A record is written straight from the entity it comes from.** This one helps every caller, not only those without dynamic code. A typed write read each member into an `object?[]` and handed that to the schema, which formatted the values and threw the array away: an array per record, and a box for every value of a value type, each unboxed a few frames later by the column that formatted it. The writer now fills one array for the whole write where it still needs one, and where the mapping allows it asks a getter per column for its member as the record is formatted, so no array is needed at all.

Measured on 10,000 records of 13 columns, a delimited write through a type mapper allocated 515 bytes a record and now allocates 79; fixed-length, 1,139 and now 703. Writing the same values as a caller-supplied array of objects costs 72, so what a mapped write adds over a hand-written one has gone from 443 bytes a record to 7.

The conditions are the reading path's, read the other way round: a column carrying an `OnFormatting` or `OnFormatted` hook, or a derived column that replaces `Format`, all three being declared in terms of `object` or `string`; a custom mapping with its own writer; a nested entity; a member that is a field, has a non-public getter, or whose type is neither the column's nor its nullable form; an entity that is a value type; and a writer whose schema is chosen per record by an injector, there being no values to choose one by until they have been worked out. Any of those and the write goes through the array as before.

**A column is no longer asked by reflection whether it can be parsed into its own type.** `Mapper` held columns as the non-generic `ColumnDefinition` and read `SupportsTypedParse` off the generic subclass through `GetProperty( ..., NonPublic )`. A trimmer is free to remove that property, and a lookup that comes back empty reads as "no" - so the faster path would have switched itself off silently under trimming, which is the same configuration this release exists to reach. It is declared on the base class now and answered by the column that knows its type.

**One thing found by measuring, worth writing down.** The typed write path began by asking `if (value is null)` of a value of an unconstrained generic type, which the compiler answers **by boxing it**. Thirteen `int` members cost 387 bytes a record on a path whose whole purpose was to avoid boxes, against 77 for thirteen strings. Asking `!typeof( T ).IsValueType && value is null` instead folds to a constant where the type is a value type, so the test that boxes is never reached; the same thirteen members then cost 75. Without measuring by member type it would have shipped as a tenth of the improvement and looked like a success.

**The integration figures are unchanged**, and were last recorded at 8.2.0. Nothing here alters what those six files cost to read: the samples are read rather than written, and reading with a JIT - which is what the check runs on - was already on the faster path.

## 8.2.0 (2026-09-24)
**Summary** - A fix for what 8.1.0 got wrong, and the two things looking for it turned up. An optimised typed read - the default - built a new entity factory for every record, which meant emitting a type per record: a delimited read through a type mapper took 7.6 seconds and allocated 4,457 bytes a record, against 62 ms and 877 at 8.0.0, and now takes 44 ms and allocates 362. Beside it, a fixed-length file of several record layouts is read onto entities without boxing its values, and a type the generated code cannot see is mapped rather than refused. The checks that let the first one through are closed: the tests now count what a read prepares, and the integration files are read onto entities as well as into values. Nothing here breaks anything.

**A typed read built a new entity factory for every record.** 8.1.0 gave the reader a path that takes a record straight onto an entity without boxing its values, and that path asks the mapper for a fresh entity per record. The mapper answered by asking the code generator for a factory each time, and the emit code generator answers a request for a factory by defining a type in its dynamic module. So an optimised typed read - the default - emitted one type per record, and the release that was meant to allocate less allocated five times more than the release before it. The factory is now built once per mapper, as the deserialiser and the column setters already were.

Measured on 10,000 records of 13 columns, a delimited read through an optimised type mapper allocated 4,457 bytes a record and took 7.6 seconds; it now allocates 362 bytes a record and takes 44 ms. Fixed-length, 4,824 bytes and 7.3 seconds, now 730 bytes and 37 ms. For comparison the same reads at 8.0.0 allocated 877 and 1,317 bytes a record, at 62 and 56 ms.

Those corrected figures are the ones 8.1.0's entry claims - 388 bytes a record delimited and 732 fixed-length. What that entry describes is what the change does once the factory is built once, which is what a mapping with `OptimiseMapping( false )` did all along, and what the default does again now. A reader comparing the 8.1.0 entry against 8.1.0 itself would not have found those figures.

Two things had to miss it for it to ship, and both are now closed.

The unit tests check what a read produces rather than what it costs, so a read that produced the right entities an expensive way passed every one of them. They now also check what a read prepares. The emit code generator counts what it is asked for - a factory, a constructor, a deserialiser, a serialiser - and a test runs the same read over one record and over a thousand and fails if the count follows the records. That is exact and says where the cost went, rather than being a threshold: preparing once a read is right and preparing once a record is wrong, whatever either costs. Beside it, a bound on what a read allocates per record, which holds for the reflection code generator too, where preparing is cheap enough not to show in a count but doing it per record is still wrong. Three of the six fail on the code as released.

The integration check reads its six files through schemas rather than type mappers, so nothing it gates touches the mapped path; it reported six files whose cost had not moved, correctly, about reads that were never affected. It now reads each file a fourth way, `mapper`, onto entities through a type mapper, and the tables carry it. Against `Set1Sample3` that scenario reports 184 bytes a record and about a second; with the factory built per record it reports 4,442 bytes a record and takes 150 seconds.

Adding that scenario showed up two more things, both fixed below: a fixed-length file of several layouts was read onto entities through a path that boxed every value, and a type the generated code cannot see was not read onto at all.

**A fixed-length file of several record layouts is read onto entities without boxing its values.** A file whose records follow more than one layout is read through `FixedLengthTypeMapperSelector`, which picks a mapper per record by looking at the record and hands the work to a deserialiser reading an array of parsed values. That array is what 8.1.0 stopped a plain mapper needing, and a selector kept needing it: every value boxed on its way to a property, and the array copied once more for the selector to read. Since the reader now says which schema matched when it hands a record over, a selector can hold one setter list per mapper and use the one the matched schema belongs to. Measured against the integration samples, `Set2Sample1` went from 2,086 bytes a record to 1,908 and from 709 ms to 448, `Set2Sample3` from 1,045 to 829, and `Set2Sample2` from 8,339 to 8,172.

It is all of them or none. The reader takes that path for every record once it has been given an assembler, so a selector holding one mapping that cannot be read that way - a custom reader, a nested entity, anything the mapper already refuses - puts every mapping behind it back on the values path. Five tests read the same records both ways and check the entities match.

A delimited selector cannot do the same, and the reason is in its shape rather than in the work: its predicates are handed the record's values, so the reader has to parse them into an array before it can choose a schema at all, and once that array exists there is nothing to save. A predicate given the record's text instead would settle it, which is a change to the selector's surface rather than to a reader, and is on the list below.

**A type the generated code cannot see is mapped rather than refused.** The emit code generator checks whether a constructor is public before emitting a call to it, and did not ask the same of the type. Mapping onto an internal class with an ordinary public constructor therefore emitted a factory that could not reach it, and the first record threw `MethodAccessException` from generated code - nothing a caller could act on, and nothing that said what to do about it. A mapping onto a type out of the generated code's reach now uses the reflection code generator, which is slower per value and works, exactly as a private constructor already did.

An assembly can let the generated code in by naming `FlatFiles.DynamicAssembly` in an `InternalsVisibleTo`, and one that has is taken at its word: its internal types keep the faster path. That is not a footnote - this repository's own test assembly does it, which is why an internal entity worked in every test ever written here and failed the moment it was tried anywhere else. Five tests now map onto a private nested type, which no `InternalsVisibleTo` can open up.

The baseline moves with this release. The `parse`, `typed` and `values` figures are where 8.1.0 left them, as they read through a schema and nothing here touches that. `mapper` is new, and its fixed-length rows already carry a change: they are what a selector costs now that it reads onto entities directly.

**Delimited**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | `parse` | 1.37 s | 1.32 s - 1.39 s | 26.8 | 5,685 | 13.5 MB | 49.5 MB |
| S1/S1 | 379 | 37,031 | `typed` | 1.61 s | 1.58 s - 1.67 s | 22.8 | 5,629 | 13.6 MB | 51.1 MB |
| S1/S1 | 379 | 37,031 | `values` | 1.59 s | 1.55 s - 1.63 s | 23.1 | 8,685 | 13.5 MB | 51.1 MB |
| | | | | | | | | | |
| S1/S2 | 58 | 344,352 | `parse` | 1.53 s | 1.45 s - 1.57 s | 66.5 | 1,414 | 13.4 MB | 49.2 MB |
| S1/S2 | 58 | 344,352 | `typed` | 1.79 s | 1.74 s - 1.84 s | 56.8 | 1,338 | 13.4 MB | 50.9 MB |
| S1/S2 | 58 | 344,352 | `values` | 1.79 s | 1.76 s - 1.82 s | 56.7 | 1,826 | 13.4 MB | 51.1 MB |
| | | | | | | | | | |
| S1/S3 | 196 | 39,337 | `parse` | 1.24 s | 1.16 s - 1.27 s | 36.1 | 3,528 | 13.5 MB | 48.3 MB |
| S1/S3 | 196 | 39,337 | `typed` | 1.56 s | 1.50 s - 1.68 s | 28.6 | 2,988 | 13.5 MB | 51.7 MB |
| S1/S3 | 196 | 39,337 | `values` | 1.49 s | 1.44 s - 1.55 s | 30.0 | 4,580 | 13.5 MB | 51.9 MB |

**Fixed-length**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | `parse` | 494 ms | 453 ms - 598 ms | 32.6 | 3,759 | 13.8 MB | 48.0 MB |
| S2/S1 | 172 | 22,481 | `typed` | 601 ms | 552 ms - 697 ms | 26.8 | 3,598 | 13.7 MB | 50.8 MB |
| S2/S1 | 172 | 22,481 | `values` | 582 ms | 567 ms - 597 ms | 27.7 | 4,305 | 13.7 MB | 50.9 MB |
| | | | | | | | | | |
| S2/S2 | 235 | 1,790 | `parse` | 127 ms | 113 ms - 170 ms | 47.0 | 12,090 | 13.0 MB | 45.5 MB |
| S2/S2 | 235 | 1,790 | `typed` | 145 ms | 136 ms - 162 ms | 41.2 | 11,269 | 12.8 MB | 47.6 MB |
| S2/S2 | 235 | 1,790 | `values` | 150 ms | 136 ms - 175 ms | 39.9 | 13,169 | 13.0 MB | 47.6 MB |
| | | | | | | | | | |
| S2/S3 | 34 | 18,047 | `parse` | 173 ms | 157 ms - 184 ms | 25.1 | 1,882 | 13.0 MB | 46.4 MB |
| S2/S3 | 34 | 18,047 | `typed` | 303 ms | 260 ms - 356 ms | 14.3 | 1,648 | 13.2 MB | 48.4 MB |
| S2/S3 | 34 | 18,047 | `values` | 249 ms | 230 ms - 264 ms | 17.4 | 1,916 | 12.9 MB | 48.5 MB |

**Delimited, through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | 1.17 s | 1.12 s - 1.20 s | 31.4 | 221 | 9.0 MB | 47.6 MB |
| S1/S2 | 58 | 344,352 | 1.33 s | 1.29 s - 1.37 s | 76.1 | 196 | 13.5 MB | 52.7 MB |
| S1/S3 | 196 | 39,337 | 1.09 s | 1.08 s - 1.10 s | 41.1 | 200 | 8.7 MB | 47.4 MB |

**Fixed-length, through a type mapper**

| Sample | Columns | Records | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | 468 ms | 455 ms - 494 ms | 34.4 | 1,908 | 14.0 MB | 54.3 MB |
| S2/S2 | 235 | 1,790 | 150 ms | 144 ms - 152 ms | 39.9 | 8,172 | 12.5 MB | 50.1 MB |
| S2/S3 | 34 | 18,047 | 199 ms | 193 ms - 210 ms | 21.7 | 829 | 13.0 MB | 50.2 MB |

Each row is one sample loaded 5 times, each in a process of its own that starts, reads the file once and
exits - which is how the library is mostly used - and the figures are the mean of those 5. Everything a
job pays for is inside them: the runtime compiling the parse path on first use, the schema being built,
the file being opened. The first three scenarios are cumulative:
`parse` reads every column as text and asks for no value, `typed` gives each single-typed column its
own type, and `values` is `typed` with `GetValues` called on every record. The difference between two
of them is the cost of the step between.

`mapper` has tables of its own because it is not a fourth step but a different path: the file read onto
entities through a type mapper, which is the only scenario that builds an entity or uses the setters a
mapper makes per column. It maps the first column of each kind the profile knows about and ignores the
rest, which costs the reader the column but not the parse, so its figures are far below the others and
mean something different. Read each set against itself and against the same set in an earlier release.

| Column | What it measures |
| --- | --- |
| Columns | Columns in the schema; for a fixed-length sample, in its widest record layout. |
| Records | Records the reader yielded. Gated exactly. No sample is built to have a record refused, so any refusal fails the check whatever else agrees. |
| Mean Total Time | Mean wall clock of 5 cold loads: opening the file, building the schema, constructing the reader, reading every record, and disposing, in a process that has done nothing else. Nothing is amortised over reads a real caller never performs. **Reported, never gated.** |
| Range | The quickest and slowest of those 5 loads, so the spread behind the mean is visible rather than implied. |
| MB/s | File size divided by Mean Total Time, so it carries the same caveats. |
| Bytes/record | Mean bytes allocated across a load, divided by records read. Deterministic for given bytes on a given runtime, and repeats to within 0.1% here. **Gated at 2%.** |
| Peak heap | The largest the managed heap reached in any of the 5 loads, sampled every 5 ms. Reported. |
| Peak working set | The largest peak working set any of those processes reached. Each does one load and exits, so the figure is a whole job's footprint, most of it runtime start-up rather than the read. Reported. |

Averaging 5 whole processes takes most of the machine noise out, but a cold start is noisy by nature and
the range shows what is left. `FlatFiles.Benchmark` is the project that measures a warm steady state, with
statistics rather than a mean; these figures are the other question - what one job costs end to end - and
show the shape of the work rather than a number to compare release to release, which is why neither Mean
Total Time nor MB/s is gated.
## 8.1.0 (2026-09-23)
**Summary** - Three changes to what a read costs and one to what it can map. A delimited read through a type mapper allocated 879 bytes a record at 8.0.0 and now allocates 388; fixed-length, 1,295 and now 732. `Define<T>()` has lost its `new()` constraint, so a positional record, a type whose properties are get-only and a type that validates in its constructor all map without being given a second, looser shape to be deserialised into. A release is now checked against the six integration test files before it goes out. Nothing here breaks anything.

**A typed read no longer copies the values out of the reader.** `IReader.GetValues` hands a caller its own array, because a caller may keep it or write to it. A type mapper does neither: it reads each value once, builds an entity, and lets the array go. Copying it for that meant every record through a mapper allocated the values array twice - once where the record was parsed, once for a copy nothing ever looked at. The mapper now takes the reader's own array through an internal member that says as much. Measured on 10,000 records of 13 columns, a delimited read through a type mapper allocated 879 bytes a record and now allocates 751; fixed-length, 1,295 and now 1,167. Reads that do not go through a mapper are untouched, and `GetValues` still copies, because its callers are the ones the copy is for.

**The parsed values are put in one array for the whole read.** Every record was parsed into a new `object?[]`, and since the previous change nothing keeps it: `GetValues` copies for its caller, and a type mapper reads each value once and lets it go. The reader now fills one array per read, sized to the schema and resized when a selector picks a different one. The exception is a `RecordParsed` handler, which is given the values and may keep them, so it is handed an array of its own; a test reads three records with a handler that keeps all three and checks they still differ. Measured on 10,000 records of 13 columns, a delimited read allocated 741 bytes a record and now allocates 613, a fixed-length read 1,157 and now 1,029, and the same reads through a type mapper 751 and 623, 1,167 and 1,039. That is one thirteen-element array in every case, and unlike the last change it helps a plain read too.

**A type mapper parses each value into the member that holds it, without boxing it first.** A record was parsed into an `object?[]` and the generated deserialiser then unboxed each element onto a property, so a thirteen-column record cost thirteen boxes whatever the file said. A mapper now builds one setter per column, each closed over the column's own type, and the reader hands it the record to read directly: an `Int32Column` parses to `int` and that `int` goes on the property. Measured on 10,000 records of 13 columns, a delimited read through a type mapper allocated 623 bytes a record and now allocates 388; fixed-length, 1,039 and now 732. Reads that do not go through a mapper are untouched, and so is how long either takes - this buys allocation, not speed.

The mapper takes that path only where it can see that nothing needs a value as an object, and there is a good deal it will not take it for: a column carrying an `OnParsing` or `OnParsed` hook, or a derived column that replaces `Parse`, since both are declared in terms of `object`; a custom mapping with its own reader, which covers every metadata column and the auto-mapped readers; a nested entity; a member that is a field, has a non-public setter, or whose type is neither the column's nor its nullable form; an entity that is a value type; and any runtime without dynamic code, where the closed generic cannot be made at all. A `RecordParsed` handler, a `RecordRead` handler and a schema selector each take it back for the records they touch, because all three are handed the values. Everything else reads as it did.

Error handling is unchanged, including the one part of it that still boxes: a `ColumnError` handler's substitution arrives as an `object` and is set through the member, which is a box for each recovered value rather than for every value. Twenty-four tests cover the new path and each way out of it. Beside them, a check that is not part of the suite reads thirty-one scenarios - hooks, substitutions, default values, null formatters, ignored columns, metadata columns, every handler, selectors, nested types, headers, asynchronous reads, both readers - through this version and through 8.0.0, and compares the two character for character; they agree on all thirty-one, including which exception each failure raises.

**A release is now checked against the six integration test files before it goes out.** Nothing in the package changes for it. `FlatFiles.IntegrationTest` reads six committed samples end to end - three delimited, three fixed-length, shaped after real files at 379 columns, 344,352 records, every field quoted, fourteen record layouts chosen by a single character and a 3,500-character record - and compares records, records skipped and bytes allocated per record against a committed baseline. It runs in the publish workflow only, and a release fails if a figure moved without somebody meaning it to. The files are committed rather than built, compressed because one of them is 102 MB, and nothing regenerates them on its own - they are the input every figure is gated against, and an input that rebuilt itself would turn a change in the generator into what looks like a change in the library. The baseline pins a SHA-256 of each, so a run reading different bytes is refused rather than reported.

This release takes the first baseline, so it carries the figures. A release that changes the baseline records what the samples cost under it; one that does not says so and leaves them where they were last written, because repeating an unchanged table only invites the reader to look for a difference that is not there.

**Delimited**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S1/S1 | 379 | 37,031 | `parse` | 1.38 s | 1.35 s - 1.43 s | 26.7 | 5,685 | 13.5 MB | 50.6 MB |
| S1/S1 | 379 | 37,031 | `typed` | 1.61 s | 1.60 s - 1.61 s | 22.9 | 5,629 | 13.6 MB | 52.5 MB |
| S1/S1 | 379 | 37,031 | `values` | 1.60 s | 1.57 s - 1.62 s | 23.0 | 8,685 | 13.6 MB | 53.0 MB |
| | | | | | | | | | |
| S1/S2 | 58 | 344,352 | `parse` | 1.50 s | 1.46 s - 1.53 s | 67.8 | 1,414 | 13.4 MB | 50.7 MB |
| S1/S2 | 58 | 344,352 | `typed` | 1.80 s | 1.74 s - 1.85 s | 56.3 | 1,338 | 13.5 MB | 52.4 MB |
| S1/S2 | 58 | 344,352 | `values` | 1.83 s | 1.77 s - 1.90 s | 55.4 | 1,826 | 13.4 MB | 52.2 MB |
| | | | | | | | | | |
| S1/S3 | 196 | 39,337 | `parse` | 1.22 s | 1.14 s - 1.27 s | 36.5 | 3,528 | 13.5 MB | 49.7 MB |
| S1/S3 | 196 | 39,337 | `typed` | 1.55 s | 1.46 s - 1.58 s | 28.9 | 2,988 | 13.5 MB | 53.1 MB |
| S1/S3 | 196 | 39,337 | `values` | 1.51 s | 1.49 s - 1.53 s | 29.7 | 4,580 | 13.5 MB | 53.1 MB |

**Fixed-length**

| Sample | Columns | Records | Scenario | Mean Total Time | Range | MB/s | Bytes/record | Peak heap | Peak working set |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| S2/S1 | 172 | 22,481 | `parse` | 540 ms | 443 ms - 613 ms | 29.8 | 3,759 | 13.8 MB | 49.0 MB |
| S2/S1 | 172 | 22,481 | `typed` | 615 ms | 545 ms - 693 ms | 26.2 | 3,597 | 13.8 MB | 51.8 MB |
| S2/S1 | 172 | 22,481 | `values` | 746 ms | 718 ms - 760 ms | 21.6 | 4,305 | 13.8 MB | 52.1 MB |
| | | | | | | | | | |
| S2/S2 | 235 | 1,790 | `parse` | 116 ms | 112 ms - 120 ms | 51.7 | 12,089 | 12.6 MB | 47.0 MB |
| S2/S2 | 235 | 1,790 | `typed` | 148 ms | 141 ms - 160 ms | 40.4 | 11,266 | 12.8 MB | 49.0 MB |
| S2/S2 | 235 | 1,790 | `values` | 159 ms | 143 ms - 181 ms | 37.6 | 13,168 | 12.3 MB | 48.9 MB |
| | | | | | | | | | |
| S2/S3 | 34 | 18,047 | `parse` | 155 ms | 152 ms - 158 ms | 28.0 | 1,882 | 13.2 MB | 47.6 MB |
| S2/S3 | 34 | 18,047 | `typed` | 205 ms | 199 ms - 211 ms | 21.1 | 1,648 | 13.2 MB | 49.7 MB |
| S2/S3 | 34 | 18,047 | `values` | 208 ms | 198 ms - 217 ms | 20.8 | 1,915 | 13.3 MB | 49.7 MB |

Each row is one sample loaded 5 times, each in a process of its own that starts, reads the file once and
exits - which is how the library is mostly used - and the figures are the mean of those 5. Everything a
job pays for is inside them: the runtime compiling the parse path on first use, the schema being built,
the file being opened. The scenarios are cumulative:
`parse` reads every column as text and asks for no value, `typed` gives each single-typed column its
own type, and `values` is `typed` with `GetValues` called on every record. The difference between two
of them is the cost of the step between.

| Column | What it measures |
| --- | --- |
| Columns | Columns in the schema; for a fixed-length sample, in its widest record layout. |
| Records | Records the reader yielded. Gated exactly. No sample is built to have a record refused, so any refusal fails the check whatever else agrees. |
| Mean Total Time | Mean wall clock of 5 cold loads: opening the file, building the schema, constructing the reader, reading every record, and disposing, in a process that has done nothing else. Nothing is amortised over reads a real caller never performs. **Reported, never gated.** |
| Range | The quickest and slowest of those 5 loads, so the spread behind the mean is visible rather than implied. |
| MB/s | File size divided by Mean Total Time, so it carries the same caveats. |
| Bytes/record | Mean bytes allocated across a load, divided by records read. Deterministic for given bytes on a given runtime, and repeats to within 0.1% here. **Gated at 2%.** |
| Peak heap | The largest the managed heap reached in any of the 5 loads, sampled every 5 ms. Reported. |
| Peak working set | The largest peak working set any of those processes reached. Each does one load and exits, so the figure is a whole job's footprint, most of it runtime start-up rather than the read. Reported. |

Averaging 5 whole processes takes most of the machine noise out, but a cold start is noisy by nature and
the range shows what is left. `FlatFiles.Benchmark` is the project that measures a warm steady state, with
statistics rather than a mean; these figures are the other question - what one job costs end to end - and
show the shape of the work rather than a number to compare release to release, which is why neither Mean
Total Time nor MB/s is gated.

**A type is built by its constructor where it cannot be built any other way.** `Define<T>()` required a parameterless constructor, as a `new()` constraint, so a type that has to be built complete was a compile error rather than anything the library could explain. That constraint is gone. Where a type has no parameterless constructor and no factory was supplied, the constructor it does have is handed the record's parsed values, its parameters matched to mapped members by name, ignoring case. A positional record maps with `Define<T>()` and nothing else; so does a type whose properties are get-only, which nothing could fill before, and one that validates in its constructor, which had to be built with placeholders and then contradicted.

The greediest constructor that can be satisfied wins, so a type offering both a full constructor and a partial one is built as completely as the record allows. A member the constructor does not take is assigned afterwards, as it always was. A parameter whose name matches nothing mapped, or whose type will not take what the member parses to, is no match, and the failure says which type it was building, what it tried and what was mapped, rather than reporting a missing default constructor. Nothing about an existing mapping changes: a caller who supplied a factory has said how the type is built, and a type with a parameterless constructor is built with it exactly as before. The only mappings that behave differently are the ones that used to throw.

Two smaller things came with it. A non-public parameterless constructor now works on the optimised path as well as the reflected one - emitted code cannot call it, so that case falls back to invoking it reflectively rather than emitting a call that fails. And the entry above this one described a feature that turned out to be largely unnecessary: `init` accessors, `required` members and positional records were all mapped correctly already through `Define( () => new Thing( ... ) )`, on both paths, and the entry had claimed otherwise for a year. What was actually missing was the narrower thing this describes.

One behaviour is worth knowing before relying on it. When a constructor refuses a record - the whole point of letting it validate - its exception reaches the caller as it was thrown, not wrapped in a `RecordProcessingException` that a `RecordError` handler could skip. That is how the library already treats a failure while an entity is being assembled, a custom reader that throws included, so this follows it rather than inventing a second convention; whether that is the right convention is a question for the error handling rather than for this change.

**This library puts performance first.** Where a performance change and a feature want the same release, the performance change goes in and the feature waits. That is the whole reason the last several releases read as they do - buffer writers, a span tokeniser, a column context built only when something can read it, span parsing on both readers - and it is why the three changes above went in ahead of every feature below. What is left on that list is feature work: the values array and the boxes were the two large allocations a typed read made, and both are now gone.
## 8.0.0 (2026-09-22)
**Summary** - The first major version of this fork. Three breaking changes, each recorded here before it was made: the public API is spelled the way the rest of the library is, the obsolete `Preprocessor` members are gone, and a delimited record is parsed straight from the parser's buffer, which makes `IRecordContext.Values` good only while its record is. A delimited read allocates 741 bytes a record where 7.6.0 allocated 1,171.

Everything below changes a name, removes a member, or changes what one promises. Only the third needs reading carefully: it is the one that changes no signature, so neither the compiler nor package validation will tell you about it.

**`TimeSpanColumn.FromMillseconds` is now `FromMilliseconds`.** The name was misspelled the day the method was written and stayed that way through every release since, because correcting it breaks every caller and a rename cannot go into a minor version. Its five siblings - `FromDays`, `FromHours`, `FromMinutes`, `FromSeconds` and `FromTicks` - were always spelled correctly, which is part of why the odd one out went unnoticed for so long; it surfaced while writing the README's column table, where the table had to print the wrong spelling because that was the published name. The fix at a call site is one letter.

Six spellings in the documentation comments are corrected with it, since those ship in the package as the text a consumer's editor shows: "equivilent" on both of `BooleanColumn`'s parsing methods, "a new instance instance of" on `ByteArrayColumn` and `CharArrayColumn`, and "a string column that has contains multiple, nested values" on both complex columns.

**The three American spellings left in the public API are now British.** `QuoteBehavior` becomes `QuoteBehaviour`, both as the enumeration and as the `DelimitedOptions` property; `OptimizeMapping` becomes `OptimiseMapping` on all four configuration interfaces and both mappers; and its `isOptimized` parameter becomes `isOptimised`, which matters for anyone calling it with a named argument. Conventions.md said these kept their spelling "until there is a reason to break the API anyway", and 8.0.0 is that reason - left undone, the inconsistency would have outlived every 7.x release and waited years for the next major version. For a consumer it is a find and replace of three identifiers with no behaviour change at all; the known consumers of this package use none of them.

The implementations named that parameter `optimize` while the interfaces named it `isOptimized`, so a named argument meant different things depending on which type the call was made through. Both are now `isOptimised`, and the field behind them is renamed rather than the parameter, as Conventions.md directs when a parameter belongs to a public member.

Every public type, member and parameter name was then read for the same fault, by reflecting over the built assembly rather than searching the source. Nothing else in 556 public names is misspelled or American.

**The documentation comments follow.** Eighty-nine American spellings across fifty-six files are now British - sixty-one of them "Initializes a new instance of" - so the text a consumer's editor shows is spelled the way the rest of the library is. Nothing outside a `///` comment changed, and nothing inside a `cref`, `name` or `langword` attribute changed either, because those name identifiers rather than read as prose. The conversion used an explicit list of words rather than a rule about `-ize` endings, which would also have caught size, prize and seize.

The thirty-two error messages in `Resources.resx` needed no respelling - not one of them was American - but reading them turned up three that were simply wrong, and those are corrected: "The type provided is not a an enumeration type", "The wrong number of values were passed", and "The column and record separator are the same string". `Resources.Designer.cs` carries a copy of each in its documentation and is updated to match; it is generated, so the copies would otherwise go stale until somebody regenerated it.

Package validation reports the removal as CP0002, and `FlatFiles/CompatibilitySuppressions.xml` carries that single entry, declaring it as Conventions.md requires. The file is emptied again when the baseline moves to 8.0.0.

**The obsolete `Preprocessor` members are removed.** `IColumnDefinition.Preprocessor`, the implementation on `ColumnDefinition`, and the `Preprocessor` method on all twenty-six property mapping interfaces have carried `[Obsolete]` pointing at `OnParsing` since before this fork. `OnParsing` is a drop-in replacement that also receives the column context: `column.Preprocessor = v => v.Trim()` becomes `column.OnParsing = ( _, v ) => v.Trim()`, and `mapper.Property( x => x.Name ).Preprocessor( f )` becomes `.OnParsing( ( _, v ) => f( v ) )`. Where a column carried both, the preprocessor ran first and the hook second; a caller that used both now chains them in one hook.

Fifty-one members went, and with them every `#pragma warning disable CS0618` in the repository - sixty of them across twenty-eight files, each existing only so the library could compile against something it was telling everybody else to stop using. The parse path is shorter for it: `ColumnDefinition<T>.Parse` and `IgnoredColumn.Parse` each dropped a branch on both the string and the span overload.

Package validation reports thirty removals, which with the renames above brings the suppression file to forty-two entries, each one read before it was kept.

**A delimited record is parsed straight from the parser's buffer, and `IRecordContext.Values` is only good while its record is.** 7.6.0 copied each record out of the buffer once and parsed its values as slices of that copy. Nothing is copied now: a record is a set of ranges into the buffered text, and a column reads its value where it lies, so a record whose values nobody asks to see as strings costs nothing at all. A delimited read allocated 1,171 bytes a record and now allocates 741; through a type mapper, 1,309 and now 879.

The catch is what pays for it. Those characters are gone as soon as the next record is read, so `Values` reports the record's values while that record is being processed - which covers every hook, every handler, every error and anything reached through `GetMetadata` - and null afterwards to a context something kept. Asked for while its record is current, the values are copied out and that array belongs to the context for ever; it is only the never-asked case that becomes null. `IRecordContext.Record` is unaffected: the fixed-length reader's record is a string the context owns, and a delimited reader's is one too when `PreserveRecordText` is set.

**`PreserveRecordText` is worth setting deliberately again.** 7.6.0 said it cost nothing, and for 7.6.0 that was true: the record was copied out of the buffer whether or not anybody wanted its text, because the values were slices of that copy. Nothing is copied now, so the option is the only thing that makes the string, and it is the difference between 741 and 1,035 bytes a record on the measurement above. Leave it off unless something reads `IRecordContext.Record`.

Nothing about this reaches package validation. No signature changes, so the tool passes it in silence; it is a break in what a member promises rather than in what it looks like, which is exactly the kind 7.5.0 shipped without anybody noticing, and is why it is written down here rather than left to be discovered.

The fixed-length reader narrows to match, though it need not: it parses from the record's own string, so its values could have stayed good for ever. One rule that holds for both readers is worth more than a rule that holds for one, and narrowing pays for itself, because the reader can now reuse a single set of ranges across every record instead of allocating one per record. A fixed-length read allocated 1,269 bytes a record and now allocates 1,157; through a type mapper, 1,407 and now 1,319. Both readers drop what a context can report when they read the next record, and that single act is what the whole contract rests on.

Time is unchanged on both readers, within the noise this machine produces.

**The two option classes were read through while this was written, and three things in them were wrong.** Setting `DelimitedOptions.QuoteBehaviour` to a value outside the enumeration threw "Encountered an invalid fixed width column alignment", which is the message for a different setting on a different reader and sends anyone who hits it looking in the wrong place; it now has a message of its own. `FixedLengthOptions` misspelled "column" and "truncation" in two remarks, and both classes, along with their constructors, named types that have never existed - `DelimitedParser`, `DelimitedParserOptions`, `FixedLengthParser`, `FixedLengthParserOptions` - so every one of them now names the reader and writer it belongs to. `DelimitedOptions.Quote` said it quoted records rather than values, `IsColumnContextDisabled` said "Gets" of a property with a setter, and the record separator's remark had its slashes the wrong way round and a word missing.

`FixedLengthOptions.RecordSeparator` gains the remark its delimited twin already had, saying what null means and that it is ignored when `HasRecordSeparator` is false. `FillCharacter` said it buffered values rather than padding them, and the three remarks that pointed at "the Window class" now point at the property on it.

## 7.6.0 (2026-09-22)
**Summary** - A delimited record is copied out of the parser's buffer once, as one string, and its values are parsed as slices of it rather than copied into a string each: around 8% less allocation on a delimited read and eleven fewer objects per record, with unchanged results and unchanged time.

7.5.0 gave every column a way to parse its value from characters rather than from a string, and the fixed-length reader a way to hand it one, because a fixed-length record is already a string and a value is a slice of it. A delimited record had no such string: the parser cut each value out of its buffer into a string of its own as it tokenised, thirteen values meaning thirteen strings plus the array holding them, and the column was handed a string whatever it did with it.

The parser now copies the record out of its buffer once, as one string, and records where each value sits within it. A value is a slice of that string, so a number, date, boolean, character or enumeration column reads the characters and makes nothing, and a string column makes the one string that is its value. Both readers now work the same way, and `IRecordContext.Values` is built from the record the first time anything asks for it, exactly as the fixed-length reader has done since 7.5.0.

Two kinds of value are not slices of their record, and both are still rebuilt as they were: one carrying a doubled quote, whose characters are interrupted by the quote that is dropped, and a quoted value followed by whitespace that `PreserveWhiteSpace` keeps, which the closing quote separates from it. The parser writes those into a buffer of its own and the record's ranges say which of the two texts each value came from, so a record can mix them freely.

A schema selector is handed the values as strings, as its predicates take them, and so is a handler for `RecordRead`, which may replace one and have the replacement parsed. Either of those means the strings are built before the record is parsed, exactly as before. Everything else - the parsing hooks, the null formatters, `ColumnError` handlers, a stored record context - reads them through `IRecordContext.Values`, which builds them on demand from the record the context owns, so a context kept long after its record still reports the right values.

**A behaviour break, and one of the two halves of it shipped in 7.5.0 unrecorded.** The array reached through `IRecordContext.Values` is a copy of the record's values rather than the array the columns are parsed from, so writing to it from a parsing hook - `context.RecordContext.Values[1] = "99"` from an `OnParsed` on an earlier column - no longer changes what a later column parses. It fails quietly: the write succeeds, the array shows it afterwards, and the record is parsed as though it had never happened.

Nothing about the API changed, so this compiles as it always did and package validation reports nothing; a consumer that substituted a value this way gets different output with no warning of any kind. **The fixed-length reader broke it in 7.5.0**, as part of building `Values` on demand, and the entry for that release does not say so - it was found while writing this one, by running the case against both versions. The delimited reader breaks it here, for the same reason. Substituting a value before the record is parsed is what `OnParsing`, the delimited reader's `RecordRead` handler and the fixed-length reader's `RecordPartitioned` handler are for, and all three still do it; a test on each reader now pins the behaviour so a future change to the parse path has to mean it.

`PreserveRecordText` now costs nothing: the record's text is copied out of the buffer whether or not the option is set, because the values are slices of it. The option still decides whether `IRecordContext.Record` carries that text or is empty, so nothing about it changes except its price.

Measured on 10,000 records of 13 columns, with the two builds alternated on the same machine. Time was unchanged within the noise on this machine, so only allocation is quoted:

| 10,000 records, 13 columns | 7.5.0 | 7.6.0 |
|---|---|---|
| delimited read | 1,268 bytes per record | 1,171 |
| delimited through the type mapper | 1,406 | 1,309 |
| fixed-length read | 1,269 | 1,269 |
| fixed-length through the type mapper | 1,407 | 1,407 |

The remaining copy is the record itself, and removing it would mean parsing straight from the parser's buffer, where the characters live only until the next record is read. That is a change to what `IRecordContext.Values` promises rather than a change of implementation - a context kept past its record could no longer report them - so it is not made here. The larger prize is reading UTF-8 bytes without decoding them to characters at all, which would remove this copy along the way.

## 7.5.0 (2026-09-22)
**Summary** - A fixed-length record is parsed from the characters of the record itself rather than from a string per column, for around two fifths less allocation on a fixed-length read and a third less through a type mapper, with unchanged results.

> **Added after this release went out.** It says "with unchanged results", and that was not quite true. Writing to `IRecordContext.Values` from a parsing hook stopped changing what a later column parses, because the array became a copy built on demand rather than the one the columns are parsed from. Nothing else about the release is affected, and the rest of the entry stands. The 7.6.0 entry describes it in full.

Until now every value in a fixed-length record was copied out of it twice before anything looked at it: once to cut the window out, and again to trim the fill character off what that left. Thirteen columns meant twenty-six strings per record, most of which were handed to a number, date or boolean column that read them and threw them away.

The reader now partitions a record into ranges - where each value starts within the record and how long it is, the fill character already trimmed off the range rather than out of a string - and the column reads its value from the record itself. Nothing is copied for a number, date, time, `Guid`, `TimeSpan`, boolean, character, enumeration or ignored column; a `string`, `char[]` or `byte[]` column copies once, because the copy is the parsed value. The raw values are still there for anything that asks: a `RecordPartitioned` handler is given them as strings, and may replace one as before, in which case the record is parsed from the strings; a column error carries the value it failed on and the whole partitioned record; and `IRecordContext.Values` copies them out of the record the first time it is read and not at all if it never is.

`IColumnDefinition` gains `Parse( IColumnContext?, ReadOnlySpan<char> )` and `INullFormatter` gains `IsNullValue( IColumnContext?, ReadOnlySpan<char> )`. Both are default interface members that copy the text and call the string overload, so a column or a null formatter implemented outside the library keeps compiling and keeps behaving as it did; package validation reports no break. A column deriving from `ColumnDefinition<T>` has the same choice one step lower: overriding the new `OnParse( IColumnContext?, ReadOnlySpan<char> )` reads the characters, and leaving it alone means the value arrives at the existing `OnParse` as a string. A column that replaced `Parse( IColumnContext?, string )` itself rather than `OnParse` still receives every value through it, because the library asks the type once whether it did.

The obsolete `Preprocessor` and the `OnParsing` hook are handed the whole value as a string, so a column carrying either of them is parsed from a string as before; `OnParsed`, the null formatters, the default values, trimming and the ragged-right option are all unaffected.

Measured on 10,000 records of 13 columns, best of eight runs with the two builds alternated on the same machine:

| 10,000 records, 13 columns | 7.4.1 | 7.5.0 |
|---|---|---|
| fixed-length read | 13.2 ms, 2,076 bytes per record | 11.6 ms, 1,269 |
| fixed-length through the type mapper | 20.2 ms, 2,214 | 14.7 ms, 1,407 |
| delimited read | 12.3 ms, 1,268 | 11.9 ms, 1,268 |
| delimited through the type mapper | 14.0 ms, 1,406 | 13.9 ms, 1,406 |

The delimited reader is deliberately unchanged, and its figures are there to show it: its parser copies each value out of the buffered text as it tokenises, so a delimited column is already handed a string that exists whatever it does with it. Handing the parser's own buffer to the columns is a separate change, and a larger one, because a quoted value is unescaped into a second buffer and both have to stay put until the record is parsed.

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
