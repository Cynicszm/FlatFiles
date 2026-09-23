# FlatFiles.IntegrationTest

Reads six large files end to end and reports what each one costs: time, bytes allocated per record,
and peak memory. The files are committed, compressed, and generated only when somebody asks, from a
profile that describes their shape - how many columns, how wide each runs, how often it is empty, and
what its values parse as.

The benchmark project measures small reads precisely. This one measures large reads realistically, at
a scale where the numbers are dominated by the work rather than by the harness, and it is also a
release gate: `check` compares every figure against a committed baseline and fails when one moves.

## Running it

    dotnet run --project FlatFiles.IntegrationTest -c Release

That measures every scenario, one process per measurement.

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
| `Set1Sample1` | delimited | 379 | 37,031 | 37 MB | The widest delimited record. A right-anchored final column of four distinct widths, and 171 records carrying a separator inside an unquoted field. |
| `Set1Sample2` | delimited | 58 | 344,352 | 102 MB | The most records. Few columns, a final column empty on every record, so per-record overhead shows up where per-field work does not. |
| `Set1Sample3` | delimited | 196 | 39,337 | 45 MB | Every field quoted, so a third of the file is punctuation and every value goes through the quoted path. |
| `Set2Sample1` | fixed-length | up to 172 | 22,481 | 16 MB | Fourteen record layouts in one file, chosen by a single character, from 21 windows wide to 172. Several layouts stop short of the 750-character record, leaving the tail unread. |
| `Set2Sample2` | fixed-length | 235 | 1,790 | 6 MB | The longest record at 3,500 characters, and few of them, so the cost is all in the record rather than in finding it. |
| `Set2Sample3` | fixed-length | 34 | 18,047 | 4 MB | The shortest record at 250 characters, so per-record overhead shows against very little per-field work. |

A fixed-length file holds records of several layouts and a character in the record says which, so its
schema is chosen per record by a `FixedLengthSchemaSelector`. That is a different path through the
library from a single-schema read, and one the type mapper's newer shortcuts do not take.

## The scenarios

Each is a step further than the last, so the difference between two of them is the cost of the step.

- **`parse`** - every column read as `StringColumn`, no value asked for. The floor: find the records,
  find the fields, make a string of each.
- **`typed`** - each single-typed column given its own type. The difference from `parse` is what
  parsing a value into a `DateTime`, `decimal` or `int` costs over copying it out as text.
- **`values`** - `typed`, with `GetValues` called on every record. The difference is what a caller
  that keeps the values pays, which is an array per record and a copy into it.

A column the profile shows as mixed stays text in every scenario. Typing it would mean records failing
on values the file it was modelled on was perfectly happy with, and the run would be measuring error
recovery rather than parsing.

## The release gate

`check` measures everything and compares it against `Baseline.json`, which is committed beside this
file. It runs in one place: the publish workflow, before a release is packed, and a release fails if
anything moved. It is deliberately not on the pull request build - generating and reading 220 MB is
too slow to put on every push, and what it guards is a release going out with something changed.
Three figures, gated differently on purpose:

| Figure | Gate | Why |
| --- | --- | --- |
| The samples themselves | exact, by SHA-256 | A figure measured against different bytes says nothing, so the check verifies its input before measuring anything. |
| Records read, records skipped | exact | A change here is a change in what the library does with a real-shaped file, whether or not anybody meant it. |
| Bytes allocated per record | 2% | Deterministic to the byte for a given file and runtime. Repeated runs here agree to within 0.1%, so 2% absorbs a runtime's housekeeping and nothing else. |
| Time, peak memory | not gated | Reported only. The same unchanged code has measured 11 ms and 22 ms on the same machine within a minute, and peak memory is dominated by runtime start-up rather than by the read. Gating either would fail releases at random. |

When a change is intended, `baseline` rewrites the file and the change is described in the changelog
like any other. A baseline that moves without an entry beside it is the thing this is meant to catch.

The samples themselves are gated the same way and for the same reason. Until a new baseline is taken,
a regenerated sample fails the check rather than quietly shifting every figure under it.

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

## A note on records with too many fields

`Set1Sample1` models 171 records that carry a separator inside an unquoted field, so those records
present 380 fields against a 379-column schema. The reader rejects a record with too few fields and
accepts one with too many, keeping the first 379 and discarding the rest - so every field after the
stray separator is read one position to the left, silently.

In the `parse` scenario all 37,031 records are therefore read without complaint, 171 of them holding
shifted values. In `typed` only 148 fail, because a shifted value has to reach a column that will not
parse it before anything notices, and where the separator lands among text columns nothing does.
Whether a misaligned record is noticed depends on how the schema is typed and on where the stray
separator fell, not on the record being wrong.
