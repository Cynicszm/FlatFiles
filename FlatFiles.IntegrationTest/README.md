# FlatFiles.IntegrationTest

Reads the six integration test files end to end, writes them back, and reports what each of those costs:
time, bytes allocated per record, and peak memory. The files are committed, compressed, and generated only
when somebody asks, from a profile that describes their shape - how many columns, how wide each runs, how
often it is empty, and what its values parse as.

The benchmark project measures small reads precisely. This one reads whole files, shaped after the ones
that cause trouble in practice - hundreds of columns, hundreds of thousands to millions of records,
every field quoted, several record layouts in one file - where the numbers are dominated by the work
rather than by the harness. It is also a release gate: `check` compares every figure against a
committed baseline and fails when one moves.

The largest sample here is 344,352 records. A profile is what a larger one would be built from, so
adding one is a matter of writing the profile rather than of finding a file.

## Running it

    dotnet run --project FlatFiles.IntegrationTest -c Release

That measures every scenario: six samples, seven scenarios each, five processes apiece, so two hundred
and ten cold runs.

    dotnet run --project FlatFiles.IntegrationTest -c Release -- run Set1Sample2
    dotnet run --project FlatFiles.IntegrationTest -c Release -- check
    dotnet run --project FlatFiles.IntegrationTest -c Release -- profile
    dotnet run --project FlatFiles.IntegrationTest -c Release -- baseline
    dotnet run --project FlatFiles.IntegrationTest -c Release -- generate

## Where the samples come from

The samples are **committed**, in `Files`, compressed. They are unpacked into
`bin/Release/net10.0/Files` the first time something reads them and left there afterwards. Nothing
rebuilds one on its own: `generate` is the only command that writes a sample, and it exists to be
typed deliberately.

That is the point. The samples are the input every figure is gated against, so an input that quietly
rebuilt itself would make the gate meaningless - a change in the generator would move the numbers and
look like a change in the library. Committing them fixes the bytes, and the baseline records a
SHA-256 of each file so a run can say whether it is reading what the baseline was taken from. It
refuses to compare if it is not.

They are compressed because one of them is 102 MB, past what a repository will accept as a single
file. The six come to 210 MB unpacked and 54 MB packed. Unpacking is deterministic, so the bytes read
are the bytes committed.

Regenerating is therefore a deliberate act with a consequence:

    dotnet run --project FlatFiles.IntegrationTest -c Release -- generate
    dotnet run --project FlatFiles.IntegrationTest -c Release -- baseline

The first rewrites the samples and says so; the second takes the figures again. Both belong in the
same pull request as the changelog entry explaining what moved and why.

## The six files

Set 1 is delimited, set 2 is fixed-length. Between them they cover the widest record, the most
records, the heaviest record, every field quoted, and a file holding twenty-six record layouts at
once.

| Profile | Format | Columns | Records | Size | What it stresses |
| --- | --- | --- | --- | --- | --- |
| `Set1Sample1` | delimited | 379 | 37,031 | 37 MB | The widest delimited record, with a right-anchored final column of four distinct widths. |
| `Set1Sample2` | delimited | 58 | 344,352 | 102 MB | The most records. Few columns, a final column empty on every record, so per-record overhead shows up where per-field work does not. |
| `Set1Sample3` | delimited | 196 | 39,337 | 45 MB | Every field quoted, so a third of the file is punctuation and every value goes through the quoted path. |
| `Set2Sample1` | fixed-length | up to 172 | 22,481 | 16 MB | Fourteen record layouts in one file, chosen by a single character, from 21 windows wide to 172. Several layouts stop short of the 750-character record, leaving the tail unread. |
| `Set2Sample2` | fixed-length | 235 | 1,790 | 6 MB | The longest record at 3,500 characters, and few of them, so the cost is all in the record rather than in finding it. |
| `Set2Sample3` | fixed-length | 34 | 18,047 | 4 MB | The shortest record at 250 characters, so per-record overhead shows against very little per-field work. |

A fixed-length file holds records of several layouts and a character in the record says which, so its
schema is chosen per record by a `FixedLengthSchemaSelector`. That is a different path through the
library from a single-schema read, and one the type mapper's newer shortcuts do not take.

## The scenarios

Seven, named for the direction they measure. The three reading ones are each a step further than the
last, so the difference between two of them is the cost of the step; the mapped ones are a different
path through the library rather than a further step.

Every sample is handled five times, each in a process that starts, reads or writes the file once and
exits, and the figures are the mean of those five with the quickest and slowest beside them. That is how
the library is mostly used - a job starts, loads a file as fast as it can, and finishes - so everything a
job pays for is inside the measurement: the runtime compiling the parse or format path on first use, the
schema being built, the file being opened. Nothing is amortised over work a real caller never performs.

Each of those processes runs with **tiered compilation off**, so every method is compiled optimised the
first time it is called. The run is still cold - it pays for compiling the path it takes, and nothing is
amortised - but what it allocates no longer depends on how far the runtime got before it promoted anything.
Tier-0 code on the formatting path allocates about twice what optimised code does, and `Set2Sample1` warms
twenty-six record layouts separately, so its writing scenarios measured anywhere between 6,355 and 7,752
bytes a record from one process to the next before this was set. They now repeat to two bytes. It changes
almost nothing else: both delimited samples measure identically either way, and the fixed-length reads move
by under 1%.

Measuring a warm steady state instead would flatter the library and answer a question few callers ask.
`FlatFiles.Benchmark` is the project that measures that, properly, with statistics.

- **`read-parse`** - every column read as `StringColumn`, no value asked for. The floor: find the
  records, find the fields, make a string of each.
- **`read-typed`** - each single-typed column given its own type. The difference from `read-parse` is
  what parsing a value into a `DateTime`, `decimal` or `int` costs over copying it out as text.
- **`read-values`** - `read-typed`, with `GetValues` called on every record. The difference is what a
  caller that keeps the values pays, which is an array per record and a copy into it.
- **`read-mapper`** - the same file read onto entities through a type mapper. Not a step further than
  `read-values` and not comparable with it: a different path, measured because nothing else here goes
  anywhere near it.
- **`write-text`** - every value written back through a schema of string columns. The floor for
  writing, and the one scenario that can be held against the file it came from.
- **`write-typed`** - the same records written with each single-typed column given its own type, so the
  difference from `write-text` is what formatting a value costs over copying it.
- **`write-mapper`** - entities written through a type mapper. The only scenario that reads a member off
  an entity, and the only one the getters a mapper builds per column can be seen in.

A column the profile shows as mixed stays text in every scenario. Typing it would mean records failing
on values the file it was modelled on was perfectly happy with, and the run would be measuring error
recovery rather than parsing.

### What the writing scenarios measure

The records are read out of the sample **before** the measurement starts, so what these rows report is
the write and nothing else. Holding a whole sample costs several hundred megabytes on the wider ones,
which is why a write scenario's peak figures are large and mean little: they are mostly the records
being held rather than anything the writer does. Allocation, which is what the gate compares, is counted
from the moment the write begins.

A fixed-length sample of several layouts is written through a `FixedLengthSchemaInjector`, one schema per
layout, which is what a caller with such a file writes. A selector reads a record's text to choose; an
injector is given the values, and every layout here maps onto the same shape, so which layout a record
belongs to is settled when it is read and handed back in the order the records are written.

**Whether a write reproduces the file it read** is reported, not gated. `write-text` does on two of the
six samples and cannot on the rest, for reasons that are properties of the samples rather than faults: a
string column trims, so a field of spaces comes back empty; some fixed-length layouts have windows that
stop short of the record, so its tail is never read at all; and a ragged final column's padding is not
part of its value. `write-typed` writes a value it parsed rather than the characters the sample spelled
it with, and `write-mapper` maps four members and ignores every other column, so neither is meant to
reproduce anything. What each of them wrote is pinned by hash in the baseline instead, which is the
stronger check: the bytes the library writes cannot move without somebody saying so, whether or not they
ever matched the sample. The write scenarios format with the invariant culture so that hash means the
same thing on another machine.

### Why the mapped scenarios are here

The first three scenarios read through a schema. That path never builds an entity, never asks the code
generator for anything and never uses the setters a mapper makes per column, so no figure taken from it
can move when any of that changes. 8.1.0 shipped a type mapper that built its entity factory once per
record - emitting a type per record - through a run of this check in which nothing moved, because
nothing here read through a mapper. This scenario is that hole closed: with the factory built per
record it reports 4,442 bytes a record against Set1Sample3 and takes 150 seconds, against 184 bytes and
a second with it built once.

A sample has up to 379 columns and no class to match, so the mapper takes the first column of each kind
the profile knows about - text, whole number, decimal and date - onto a property of that type, and
ignores the rest. Ignoring costs the reader the column but not the parse, so the figures are lower than
`typed` and are not a measure of what mapping 379 columns would cost. They are a measure of the mapped
path, which is what they are here for. The members are nullable because a typed column parses an empty
field to null and a member that cannot hold one refuses the record; the samples have plenty of both,
and a scenario that refused records would be measuring error recovery.

Both formats read through the path that sets each member without boxing the value, though they did not
when this scenario was written. A fixed-length file of several record layouts is read through
`FixedLengthTypeMapperSelector`, which multiplexed deserialisers over a shared values array and took no
part in that path - which is why the factory built per record moved the delimited figures twenty-four
fold and the fixed-length ones not at all, and how the gap was found. The selector now holds a setter
list per mapper, so both halves of the table measure the same thing.

A delimited selector still reads the older way, for a reason worth knowing: its predicates are given the
record's values, so the reader has to build them before it can choose a schema at all. None of the
samples here uses one.

## The release gate

`check` measures everything and compares it against `Baseline.json`, which is committed beside this
file. It runs in one place: the publish workflow, before a release is packed, and a release fails if
anything moved. It is deliberately not on the pull request build - what it guards is a release going
out with something changed, and the run costs about eight minutes, 210 MB of unpacked disk and a peak of
some 700 MB of memory on whatever performs it. The memory is the writing scenarios holding a whole
sample; nothing the library does needs it. Gated differently on purpose:

| Figure | Gate | Why |
| --- | --- | --- |
| The samples themselves | exact, by SHA-256 | A figure measured against different bytes says nothing, so the check verifies its input before measuring anything. |
| Records read | exact | A change here is a change in what the library does with a real-shaped file, whether or not anybody meant it. A record refused is a record not yielded, so this catches one without needing a count of its own. |
| Records refused | must be none | No sample is built to have a record refused. One that does means something changed, so it fails whatever the other figures say. |
| Bytes allocated per record | 2% | Deterministic to the byte for a given file and runtime. Repeated runs here agree to within 0.1%, so 2% absorbs a runtime's housekeeping and nothing else. |
| What a write produced | exact, by SHA-256 | The same records written the same way produce the same bytes. This pins them, so a writer that quietly starts writing something else fails even where every figure beside it holds. |
| Time, peak memory | not gated | Reported as the mean of five cold loads with the range beside it. Even averaged, the same unchanged code moves more than a gate could live with; peak memory is a whole job's footprint, most of it runtime start-up. Gating either would fail releases at random. |

When a change is intended, `baseline` rewrites the file and the change is described in the changelog
like any other. A baseline that moves without an entry beside it is the thing this is meant to catch.

The samples themselves are gated the same way and for the same reason. Until a new baseline is taken,
a regenerated sample fails the check rather than quietly shifting every figure under it.

A release that takes a new baseline carries the whole table in its changelog entry - six samples, seven
scenarios apiece - so what a version cost is recorded where it shipped rather than only in a baseline
the next release overwrites. `baseline` prints it: eight tables and the notes saying what each column
measures, from the very runs it took the baseline from, so the entry and the gate hold the same
figures. Four tables cover the scenarios that go through a schema - one per format and direction - and
four more cover the mapped ones, because those are a different path and a row of one among the others
invites a comparison that means nothing. `run --markdown` prints the same block from fresh runs, which is
useful for looking but is not what the baseline recorded.

A release that leaves the baseline alone says the figures are unchanged and names the release that
last recorded them, rather than reprinting a table whose ungated columns would differ anyway.

## What the profiles pin, and what they do not

A profile carries counts and widths, not data. Generation reproduces the record count, the separator
and record separator, whether fields are quoted, the record layouts of a fixed-length file and how
often each appears, and per column the share of records reading as a date, a number, text or empty
along with the smallest, largest, mean and most frequent width. For the delimited files it also
reproduces the ragged final column and the number of records carrying a separator inside a field.

The fixed-length files come out byte for byte the size of the files they were profiled from, and the
delimited files within 0.2% on average record bytes - the workbook above carries both figures. The
spread is a different matter: a profile records
each column's widths on its own, so the generator draws them independently and record totals bunch
around the mean more tightly than the original file's did, where a wide field in one column tended to
mean a wide field in the next. Two further simplifications: a fixed-length value is written to the
left of its window whatever the original did, and column names are `Col1`..`ColN` throughout.

Widths and values are drawn from a golden-ratio sequence and a hash of the record number rather than
from a random source, so a profile always yields the same file, and the shares it asks for are reached
in far fewer records than sampling would need.

## Reading the files back

`profile` reads every generated file back, measures it, and writes
`Profiles/Generated/GeneratedFileProfiles.xlsx`: a summary that sets what was measured beside what the
profile asked for, and a sheet per file giving every column's type mix, widths and how far its average
width fell from the profile's. That workbook is how the generated files are checked against the shapes
they were meant to have, and it is committed so the check can be read without running anything.

    dotnet run --project FlatFiles.IntegrationTest -c Release -- profile

It takes a few seconds over all six files. The workbook is written by hand as a zip of XML parts, so
the project needs no spreadsheet library for the one thing it produces.
