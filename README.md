# FlatFiles

[![Build and test](https://github.com/Cynicszm/FlatFiles/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/Cynicszm/FlatFiles/actions/workflows/build.yml) [![NuGet](https://img.shields.io/nuget/v/Cynicszm.FlatFiles.svg)](https://www.nuget.org/packages/Cynicszm.FlatFiles)

Reads and writes CSV, fixed-length and other flat file formats with a focus on schema definition, configuration and speed. Supports mapping directly between files and classes.

Download using NuGet: [Cynicszm.FlatFiles](https://nuget.org/packages/Cynicszm.FlatFiles)

This is a fork of [FlatFiles](https://github.com/jehugaleahsa/FlatFiles) by Travis Parks, continued for .NET 10 after the original was archived. The assembly is still `FlatFiles.dll` and the namespaces are unchanged. Through 7.6.0 every type and member name was unchanged too, so only the package reference differed; 8.0.0 renames a few of them, and the [CHANGELOG](https://github.com/Cynicszm/FlatFiles/blob/master/CHANGELOG.md) names each one.

You can check out all of the awesome enhancements and new features in the [CHANGELOG](https://github.com/Cynicszm/FlatFiles/blob/master/CHANGELOG.md). 

## Overview
Plain-text formats primarily come in two variations: delimited (CSV, TSV, etc.) and fixed-width. FlatFiles comes with support for working with both formats. Unlike most other libraries, FlatFiles puts a focus on schema definition. You build and pass a schema to a reader or writer and it will use the schema to extract or write out your values.

A schema is defined by specifying what data columns are in your file. A column has a name, a type and an ordinal position in the file. The order matches whatever order you add the columns to the schema, so you're left just specifying the name and the type. Beyond that, you have a lot of control over the parsing/formatting behaviour when reading and writing, respectively. Most of the time, the out-of-the-box options will *just work*, too. But when you need that level of extra control, you don't have to bend over backward to work around the API, like with many other libraries. FlatFiles was designed to make handling oddball edge cases easier.

If you are working with data classes, defining schemas is even easier. You can use the type mappers to map your properties directly. This saves you from having to specify column names or types, since both can be derived from the property. For those working with ADO.NET, there's even support for `DataTable`s and `IDataReader`. If you really want to, you can read and write values using raw `object[]`.

## Table of Contents
* [Overview](#overview)
* [Type Mappers](#type-mappers)
    * [Auto-mapping](#auto-mapping)
    * [Constructor mapping](#constructor-mapping)
* [Schemas](#schemas)
    * [Column types](#column-types)
    * [Creating your own columns](#creating-your-own-columns)
* [Delimited Files](#delimited-files)
* [Fixed Length Files](#fixed-length-files)
* [Character Encoding](#character-encoding)
* [Handling Nulls](#handling-nulls)
    * [Default Values](#default-values)
    * [Null Formatters](#null-formatters)
* [Ignored Fields](#ignored-fields)
* [Metadata](#metadata)
    * [Writing metadata](#writing-metadata)
    * [Create your own metadata](#creating-your-own-metadata-columns)
* [Skipping Records](#skipping-records)
* [Error Handling](#error-handling)
* [Files Containing Multiple Schemas](#files-containing-multiple-schemas)
* [Custom Mapping](#custom-mapping)
* [Runtime Mapping](#runtime-mapping)
* [Disabling Optimisation](#disabling-optimisation)
* [Non-Public Classes and Members](#non-public-classes-and-members)
* [ADO.NET DataTables](#adonet-datatables)
* [FlatFileDataReader](#flatfiledatareader)
* [How This Library Is Verified](#how-this-library-is-verified)
* [License](#license)

## Type Mappers
Using the type mappers, you can directly read file contents into your classes:

```csv
customer_id,name,created,avg_sales
1,bob,20120321,12.34
2,Susan,20130108,13.88
3,Tom,20180519,88.23
```

```csharp
var mapper = DelimitedTypeMapper.Define<Customer>();
mapper.Property(c => c.CustomerId).ColumnName("customer_id");
mapper.Property(c => c.Name).ColumnName("name");
mapper.Property(c => c.Created).ColumnName("created").InputFormat("yyyyMMdd");
mapper.Property(c => c.AverageSales).ColumnName("avg_sales");
using (var reader = new StreamReader(File.OpenRead(@"C:\path\to\file.csv")))
{
    var options = new DelimitedOptions() { IsFirstRecordSchema = true };
    var customers = mapper.Read(reader, options).ToList();
}
```

To define the schema when working with type mappers, call `Property` in the order that the fields appear in the file. The type of the column is determined by the type of the mapped property. Each property configuration provides options for controlling the way FlatFiles handles strings, numbers, date/times, GUIDs, enums and more. Once the properties are configured, you can call `Read` or `Write` on the type mapper.

*Note* The `Read` method only retrieves records from the underlying file on-demand. To bring the entire file into memory at once, just call `ToList` or `ToArray`, or loop over the records inside of a `foreach`. This is good news for people working with enormous files!

Writing to a file is just as easily:

```csharp
mapper.Property(c => c.Created).OutputFormat("yyyyMMdd");
mapper.Property(c => c.AverageSales).OutputFormat("N2");
using (var writer = new StreamWriter(File.OpenCreate(@"C:\path\to\file2.csv")))
{
    var options = new DelimitedOptions() { IsFirstRecordSchema = true };
    mapper.Write(writer, customers, options);
}
```

*Note* I was able to customise the `OutputFormat` of properties that were previously configured. The first time `Property` is called on a property, FlatFiles assumes it's the next column to appear in the flat file. However, subsequent configuration on the property doesn't change the order of the columns or reset any other settings.

### Auto-mapping
If your delimited file (CSV, TSV, etc.) has a schema with column names that match your class's property names, you can use the `GetAutoMappedReader` method as a shortcut. This method will read the schema from your file and map the columns to the properties automatically, returning a reader for retrieving the data. It's important to note that you cannot customise the parsing behaviour of any of the columns, at which point you are better off explicitly defining the schema. Fortunately, FlatFiles uses pretty liberal parsing out-of-the-box, so most common formats will work.

By default, columns and properties are matched by name (case-insensitive). If you need more control over how columns and properties are matched, you can pass in your own `IAutoMapMatcher`. Given an `IColumnDefinition` and a `MemberInfo`, a matcher must determine whether the two map to one another. For convenience, you can also use the `AutoMapMatcher.For` method to pass a `Func<IColumnDefinition, MemberInfo, bool>` delegate rather than implement the interface.

Similarly, use the `GetAutoMappedWriter` method to automatically write out a delimited file. Note that there's no way to control the column formatting. However, you can control the name and position of the columns by passing an `IAutoMapResolver`. The `IAutoMapResolver` interface provides the `GetPosition` and a `GetColumnName` methods, both accepting a `MemberInfo`. For convenience, you can also use the `AutoMapResolver.For` method to pass delegates for determining the names/positions, rather than implement the interface. 

### Constructor mapping
`Define<T>()` does not need your class to have a parameterless constructor. If it hasn't got one, FlatFiles builds it by handing its constructor the values it parsed, matching each parameter to a mapped member by name, ignoring case.

That means a positional record maps with nothing extra:

```csharp
public record Customer(int CustomerId, string Name, DateTime Created, decimal AverageSales);

var mapper = DelimitedTypeMapper.Define<Customer>();
mapper.Property(c => c.CustomerId).ColumnName("customer_id");
mapper.Property(c => c.Name).ColumnName("name");
mapper.Property(c => c.Created).ColumnName("created").InputFormat("yyyyMMdd");
mapper.Property(c => c.AverageSales).ColumnName("avg_sales");
```

The same applies to a class whose properties are get-only, which nothing could fill before, and to one that checks what it is given rather than being built empty and assigned afterwards:

```csharp
public class Customer
{
    public Customer(int customerId, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(customerId);
        CustomerId = customerId;
        Name = name;
    }

    public int CustomerId { get; }
    public string Name { get; }
}
```

The rules are worth knowing before you rely on them:

* If your class has a parameterless constructor, FlatFiles uses it and assigns the properties afterwards, exactly as it always has. Nothing about an existing mapping changes.
* If you passed a factory to `Define` or `UseFactory`, you have said how the class is built and FlatFiles does not look for a constructor.
* Otherwise the greediest constructor whose parameters all match mapped members wins, so a class offering both a full constructor and a partial one is built as completely as the record allows.
* A member the constructor takes is not assigned again afterwards. One it does not take is assigned as usual, so you can mix a constructor with settable properties.
* If nothing matches — a parameter named something no mapped member is called, or one whose type will not take what the member parses to — you get a `FlatFileException` naming the class, the constructors it tried and the members that were mapped.

One thing to be aware of if you validate in a constructor: when it rejects a record, its own exception reaches you. It is not wrapped in a `RecordProcessingException`, so a [`RecordError`](#error-handling) handler will not skip the record for you. That matches how FlatFiles treats any failure while building an entity, including a `CustomMapping` reader that throws.

## Schemas
Under the hood, type mapping internally defines a schema, giving each column a name, order and type in the flat file. You can get access to the schema by calling `GetSchema` on the mapper.

You can work directly with schemas if you don't plan on using the type mappers. For instance, this is how we would define a CSV file schema:

```csharp
var schema = new DelimitedSchema();
schema.AddColumn(new Int64Column("customer_id"))
      .AddColumn(new StringColumn("name"))
      .AddColumn(new DateTimeColumn("created") { InputFormat = "yyyyMMdd", OutputFormat = "yyyyMMdd" })
      .AddColumn(new DoubleColumn("avg_sales") { OutputFormat = "N2" });
```

Or, if the schema is for a fixed-length file:

```csharp
var schema = new FixedLengthSchema();
schema.AddColumn(new Int64Column("customer_id"), 10)
  .AddColumn(new StringColumn("name"), 255)
  .AddColumn(new DateTimeColumn("created") { InputFormat = "yyyyMMdd", OutputFormat = "yyyyMMdd" }, 8)
  .AddColumn(new DoubleColumn("avg_sales") { OutputFormat = "N2" }, 10);
```

The `FixedLengthSchema` class is the same as the `DelimitedSchema` class, except it associates a `Window` to each column. A `Window` records the `Width` of the column in the file. It also allows you to specify the `Alignment` (left or right) in cases where the value doesn't fill the entire width of the column (the default is left aligned). The `FillCharacter` property can be used to say what character is used as padding. You can also set the `TruncationPolicy` to say whether to chop off the front or the back of values that exceed their width.

*Note* Some fixed-length files may have columns that are not used. The fixed-length schema doesn't provide a way to specify a starting index for a column. Look at the [Ignored Fields](#ignored-fields) section below to learn about ways to to handle this.

### Column types
Every column below reads and writes one .NET type. The type mappers' `Property` method has an overload for each of these types, and a second for the nullable form of each value type, and the dynamic mappers have a matching `...Property` method, so you rarely name a column class yourself unless you are building a schema by hand.

| Column | .NET type | Settings of its own |
| --- | --- | --- |
| `BooleanColumn` | `bool` | `TrueString`, `FalseString` |
| `ByteColumn` | `byte` | the number settings below |
| `SByteColumn` | `sbyte` | the number settings below |
| `Int16Column` | `short` | the number settings below |
| `UInt16Column` | `ushort` | the number settings below |
| `Int32Column` | `int` | the number settings below |
| `UInt32Column` | `uint` | the number settings below |
| `Int64Column` | `long` | the number settings below |
| `UInt64Column` | `ulong` | the number settings below |
| `SingleColumn` | `float` | the number settings below |
| `DoubleColumn` | `double` | the number settings below |
| `DecimalColumn` | `decimal` | the number settings below |
| `StringColumn` | `string` | `Trim` |
| `CharColumn` | `char` | `AllowTrailing` |
| `CharArrayColumn` | `char[]` | `Trim` |
| `ByteArrayColumn` | `byte[]` | `Encoding` |
| `DateTimeColumn` | `DateTime` | `InputFormat`, `OutputFormat`, `FormatProvider` |
| `DateTimeOffsetColumn` | `DateTimeOffset` | `InputFormat`, `OutputFormat`, `FormatProvider` |
| `DateOnlyColumn` | `DateOnly` | `InputFormat`, `OutputFormat`, `FormatProvider` |
| `TimeOnlyColumn` | `TimeOnly` | `InputFormat`, `OutputFormat`, `FormatProvider` |
| `TimeSpanColumn` | `TimeSpan` | `InputFormat`, `OutputFormat`, `FormatProvider` |
| `GuidColumn` | `Guid` | `InputFormat`, `OutputFormat` |
| `EnumColumn<TEnum>` | `TEnum` | `Parser`, `Formatter` |

The numeric columns share `FormatProvider`, `NumberStyles` and `OutputFormat`. A date or time column with an `InputFormat` parses exactly, using `ParseExact`; without one it parses generically, which accepts more but is slower and culture-dependent. `EnumColumn<TEnum>` reads a member's name or its number without regard to case unless you give it a `Parser`, and writes its number unless you give it a `Formatter`.

Every column also carries the settings that belong to all of them: `ColumnName`, `IsNullable`, `DefaultValue` and `NullFormatter`, covered under [Handling Nulls](#handling-nulls), and the `OnParsing`, `OnParsed`, `OnFormatting` and `OnFormatted` hooks for stepping in either side of a value being converted.

`TimeSpanColumn` also has static methods for files that store a duration as a plain number: `TimeSpanColumn.FromSeconds(new DoubleColumn("elapsed"))` and its `FromDays`, `FromHours`, `FromMinutes`, `FromMilliseconds` and `FromTicks` counterparts wrap a numeric column and convert what it reads.

Four columns do not map to a value of their own:

| Column | What it does |
| --- | --- |
| `IgnoredColumn` | Occupies a position in the file whose value is thrown away. See [Ignored Fields](#ignored-fields). |
| `RecordNumberColumn` | Supplies the record's number rather than reading anything. See [Metadata](#metadata). |
| `DelimitedComplexColumn` | Reads a whole delimited record out of a single field, using its own `DelimitedOptions`. |
| `FixedLengthComplexColumn` | Reads a whole fixed-length record out of a single field, using its own `FixedLengthOptions`. |

### Creating your own columns
The column types that ship with FlatFiles cover the usual types, but you can add your own by deriving from `ColumnDefinition<T>` and implementing two methods:

```csharp
protected override T OnParse(IColumnContext? context, string value);

protected override string OnFormat(IColumnContext? context, T value);
```

That is all a column needs, and it is all any column needed before FlatFiles 7.1.0. Everything around them -- trimming, null handling, default values and the `OnParsing`/`OnParsed` hooks -- is applied for you.

Two further overloads are optional, and let a column avoid creating a string for its value:

```csharp
protected override T OnParse(IColumnContext? context, ReadOnlySpan<char> value);

protected override void OnFormat(IColumnContext? context, T value, IBufferWriter<char> destination);
```

The readers hand a column the characters of its value where they can, rather than a string cut out of the record, and the writers let a column format straight into the record buffer rather than return a string to be copied. Implement these and no string is created for the value in either direction. Leave either one out and FlatFiles falls back to the string overload, so a column written against an earlier version keeps working unchanged.

## Delimited Files
If you are working with delimited files, such as comma-separated (CSV) or tab-separated (TSV) files, you want to use the `DelimitedTypeMapper`. Internally, the mapper uses the `DelimitedReader` and `DelimitedWriter` classes, both of which work in terms of raw `object` arrays. In effect, all the mapper does is map the values in the array to the properties in your data objects. These classes read data from a `TextReader`, such as a `StreamReader` or a `StringReader`, and write data to a `TextWriter`, such as a `StreamWriter` or a `StringWriter`. Internally, the mapper will build a `DelimitedSchema` based on the property/column configuration; this is where you customise the schema to match your file format. For more global settings, there is also a `DelimitedOptions` object that allows you to customise the read/write behaviour to suit your needs.

Within delimited files, fields can be surrounded with double quotes (`"`). This way they can include the separator within the field. You can override the "quote" character in the `DelimitedOptions` class, if needed. The `DelimitedOptions` class supports a `Separator` property for specifying the string/character that separates your fields. A comma (`,`) is the default separator; you'd obviously want to change this to tab (`\t`) for a TSV file.

The `RecordSeparator` property specifies what string/character is used to separate records. By default, FlatFiles will look for `\r`, `\n` or `\r\n`, which are the default line separators for Mac, Linux and Windows, respectively. When writing files, `Environment.NewLine` is used by default; this means by default you'll get different output if you run the same code on different platforms. If you need to target a specific platform, be sure to set the `RecordSeparator` property explicitly.

When working directly with the `DelimitedReader` class, the `IsFirstRecordSchema` option tells the reader to treat the first record in the file as the schema. This is useful when you don't know the schema ahead of time or you are just dumping files into staging tables. If you provide a schema, this setting tells the reader to simply skip the first record. If you are using it to determine the schema from the file, the reader treats every column as a `string` - it doesn't try to interpret the type from the data. While occasionally useful, you will normally want to provide a schema to give you the most control over the parsing process - that's what FlatFiles is good at!

When working directly with the `DelimitedWriter` class, setting `IsFirstRecordSchema` to `true` option causes a header to be written to the file upon writing the first record.

## Fixed Length Files
If you have a file with fixed length columns, you will want to use the `FixedLengthTypeMapper` class. Internally, the mapper uses the `FixedLengthReader` and `FixedLengthWriter` classes, both of which work in terms of raw `object` arrays. In effect, all the mapper does is map the values in the array to the properties in your data objects. These classes read data from a `TextReader`, such as `StreamReader` or `StringReader`, and write data to a `TextWriter`, such as `StreamWriter` or `StringWriter`. Internally, the mapper will build a `FixedLengthSchema` based on the property/column configuration; this is where you customise the schema to match your file format. For more global settings, there is also a `FixedLengthOptions` object that allows you to customise the read/write behaviour to suit your needs.

Since each column has a fixed length, FlatFiles provides configuration options to specify how to handle values that are too short or too long: `FillCharacter`, `Alignment` and `TruncationPolicy`. `FillCharacter` specifies what character is used to pad values on the left or the right, using space (` `) by default. You can configure whether the padding should go to the left or the right using the `Alignment` property, putting padding to the right by default (`LeftAligned`). `TruncationPolicy` tells FlatFiles how to crop values that exceed the width of their column when writing out to a file, removing leading characters by default. These options can be specified globally in the `FixedLengthOptions` object or overridden at the column level using the `Window` object.

The `RecordSeparator` property specifies what string/character is used to separate records. By default, FlatFiles will look for `\r`, `\n` or `\r\n`, which are the default line separators for Mac, Linux and Windows, respectively. When writing files, `Environment.NewLine` is used by default; this means by default you'll get different output if you run the same code on different platforms. If you need to target a specific platform, be sure to set the `RecordSeparator` property explicitly.

By default, FlatFiles assumes there is a separator string/character between each record.  If you set the `HasRecordSeparator` to `false`, FlatFiles will read the next record immediately following the last character of the previous record. When writing, it will not insert a separator, writing immediately after the last character of the previous record.

If the `FixedLengthOptions`'s `IsFirstRecordHeader` property is set to `true`, the first record in the file will be skipped when reading. Unlike the `DelimitedReader`, you must *always provide a schema for fixed-length files*, since the width of the columns cannot be determined from the file format. When writing, a header will be written to the file upon writing the first record.

By default a record shorter than the total width of the schema's windows raises a `RecordProcessingException`, and a longer one is read from its declared offsets with the surplus ignored. Two options change that. `IsLongRecordRejected` raises the same exception for a record longer than the schema, so a layout declared too narrow is reported rather than silently misread. `IsRaggedRight` reads a ragged-right file, where every column but the last has a fixed width and the last runs from its offset to the end of the line, however long or short: the last column takes whatever follows the other windows, a record that ends before the last column is read as far as it goes, with a window it ends inside taking the characters that are there and a window it never reaches parsing as null. `IsLongRecordRejected` has no effect alongside it, since no ragged-right record is too long. When writing with `IsRaggedRight`, the last column is written as formatted, neither padded nor truncated to its window, so a file read ragged and written ragged keeps its shape.

```csharp
var options = new FixedLengthOptions { IsRaggedRight = true };
var reader = new FixedLengthReader(new StreamReader(path), schema, options);
```

## Character Encoding
FlatFiles reads from a `TextReader` and writes to a `TextWriter`, so it never sees bytes. The character encoding is decided by the `StreamReader` or `StreamWriter` you open, before any FlatFiles class is involved, and the same schema and options work with any encoding.

```csharp
using (var reader = new StreamReader(File.OpenRead(@"C:\path\to\file.csv"), Encoding.UTF8))
{
    var csvReader = new DelimitedReader(reader, schema);
    // ...
}
```

**UTF-8** is the default for both `StreamReader` and `StreamWriter`. A `StreamReader` also detects a byte order mark by default and consumes it, so a file that begins with `EF BB BF` reads the same as one that does not. Keep that detection on: if you construct a reader with `detectEncodingFromByteOrderMarks: false`, the mark is read as text and becomes part of the first column's name (`"\uFEFFId"` rather than `"Id"`), and that column is never matched. To write a file with a mark, pass `new UTF8Encoding(true)` to the writer; `Encoding.UTF8` writes one too, and `new UTF8Encoding(false)` writes none.

**Windows-1252** is what most files from Windows tools are in when they are not UTF-8. It is not available in .NET by name until its provider is registered, once, at start-up:

```csharp
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var windows1252 = Encoding.GetEncoding(1252);
```

**ISO-8859-1** (Latin-1) is available directly as `Encoding.Latin1`. It agrees with Windows-1252 everywhere except the bytes `0x80` to `0x9F`, which Windows-1252 uses for the euro sign, curly quotes, the trademark sign and similar, and which ISO-8859-1 leaves as control characters. A Windows-1252 file read as Latin-1 therefore looks right until the first euro sign, which comes back as `U+0080`. When a file is described as "Latin-1" and contains a `€`, it is Windows-1252.

Two consequences of the encoding sitting outside FlatFiles:

* A character the encoding cannot represent is replaced when writing, not rejected. A `€` written through `Encoding.Latin1` arrives as `?`. Choose the encoding to fit the data, or use UTF-8.
* Fixed length windows are measured in characters, not bytes. A name such as `Müller` fills a six-character window in every encoding, even though it is seven bytes in UTF-8, so widths taken from a byte-oriented specification are right only for single-byte encodings.

The tests in `CharacterEncodingTester` pin each of these behaviours, including what the misreads look like.

## Handling Nulls
Each column can be marked as "nullable", using the `IsNullable` property. By default, all columns are nullable, meaning `null` is considered a valid value. Setting `IsNullable` to `false` will cause FlatFiles to throw an exception whenever a `null` is encountered.

Type mappers will automatically configure columns to be nullable based on the type of the property. If you need to support nulls, make sure you use nullable types for your properties, for example `int?` instead of `int`.

### Default Values
When working with non-nullable columns, you can specify a default value to use whenever a `null` is encountered, rather than throw an exception. You simply set a custom `IDefaultValue` on the column. The `DefaultValue` class provides helper methods for generating default values. For example:

```csharp
column.DefaultValue = DefaultValue.Use(0);
```

Or, if you are using type mappings, you can simply use the `DefaultValue` method:

```csharp
mapper.Property(c => c.Amount)
    .ColumnName("amount")
    .DefaultValue(DefaultValue.Use(0));
```

### Null Formatters
By default, FlatFiles will treat blank or empty strings as `null`. If nulls are represented differently in your file, you can set a custom `INullFormatter` on the column. If it is a fixed value, you can use the `NullFormatter.ForValue` method.

```csharp
var dateColumn = new DateTimeColumn("created") 
{
    NullFormatter = NullFormatter.ForValue("NULL") 
};
```

Or, if you are using type mappers, you can simply use the `NullFormatter` method:

```csharp
mapper.Property(c => c.Created)
    .ColumnName("created")
    .NullFormatter(NullFormatter.ForValue("NULL"));
```

You can implement the `INullFormatter` interface if you need to support something more complex. It has an optional overload taking a `ReadOnlySpan<char>`, which a reader uses to decide whether a value is null without creating a string for it; the default copies the characters and calls the string overload, so implementing only the string overload is enough.



## Ignored Fields
Most of the time, we don't have control over the flat file we're working with. This usually leads to columns that our code just doesn't care about. Both type mappers and schemas expect columns to be listed in the order they appear in the document. For type mappers, this would mean defining properties in your classes that you'd never use (e.g., Ignored1, Ignored2, etc.). Another common problem with fixed-length files is that they will separate fields with pipes (`|`), or other characters, even though they are fixed-length.

Types mappers support the `Ignored` method that will tell FlatFiles to simply ignore the next column in the file. Unlike other mapper methods, it does not take a property, since the value is simply thrown away. You can optionally call `ColumnName` if you want to provide a column name when writing out schemas.

When working with the `IFixedLengthTypeMapper`, `Ignored` takes a `Window`. This is a great way to skip unused sections within the document. You can even set the `FillCharacter` property to insert pipes (`|`), or another character, between fields.

Under the hood, type mappers is adding an `IgnoredColumn` to the underlying schema. `IgnoredColumn` has a constructor to optionally specify a column name. `IgnoredColumn` affects the way you work with readers and writers. The readers will initially retrieve all columns from the document and then throw away any ignored values. You'll only see values for the columns that aren't ignored. Also, you do not need to provide values to the writers for ignored columns; the schema will automatically take care of writing out blanks for them. From a development perspective, it's as if those columns didn't exist in the underlying document.

## Metadata
It is often useful to incorporate metadata with the records you are reading from a file. The most common example is tracking a record's line number, so users can be informed where to look in their files when something goes wrong.

Currently, the only out-of-the-box metadata column is `RecordNumberColumn`; however, it's really easy to create your own custom metadata columns (more on that below).

The `RecordNumberColumn` class provides options for controlling how the record number is generated. The `IncludeSchema` property indicates whether the schema or header row should be included in the count. The `IncludeSkippedRecords` property specifies whether to count records that are [skipped](#skipping-records).

By default, `RecordNumberColumn` will only count records that are actually returned, starting from `1`, then `2`, `3`, `4`, `5` and so on. If you count the schema, records will always start at `2`. Including the schema and skipped records is what you probably want if you're trying to simulate line number. The only time the record # wouldn't be the same as the line # is if a record spanned multiple lines. Here's an example showing how to capture this *pseudo* line number:

```csharp
var mapper = DelimitedTypeMapper.Define(() => new Person());
mapper.Property(x => x.Name);
mapper.CustomMapping(new RecordNumberColumn("RecordNumber")
{
    IncludeSchema = true,
    IncludeSkippedRecords = true
}).WithReader(p => p.RecordNumber);

var options = new DelimitedOptions() { IsFirstRecordSchema = true };
var results = mapper.Read(reader, options).ToArray();
```

### Writing metadata
I've not yet come up with a reason why you'd want to write out metadata, but I provide support for it anyway (feel free to provide me an example!).

```csharp
var mapper = FixedLengthTypeMapper.Define(() => new Person());
mapper.Property(x => x.Name, 10);
mapper.Ignored(1);
// No need to define a reader or a writer - the underlying schema handles writing the metadata 
mapper.CustomMapping(new RecordNumberColumn("RecordNumber") { IncludeSchema = true }, 10);
mapper.Ignored(1);
mapper.Property(x => x.CreatedOn, 10).OutputFormat("MM/dd/yyyy");
```

### Creating your own metadata columns
FlatFiles provides the `MetadataColumn<T>` abstract base class to allow you to create your own metadata columns. To implement this interface, you must implement the methods:

```csharp
T OnParse(IColumnContext context);

string OnFormat(IColumnContext context);
```

Within `IColumnContext`, the following information is currently provided:

* `PhysicalIndex` - The index of the column in the file.
* `LogicalIndex` - The index of the column, excluding ignored columns.
* `RecordContext` - Details about the record this column pertains to.
    * `PhysicalRecordNumber` - The actual number of records read from the file.
    * `LogicalRecordNumber` - The number of records that have not been skipped. *This count does not yet include the current record.*
    * `Record` - The text of the record. Always available for a fixed-length file; for a delimited file, only when `PreserveRecordText` is set.
    * `Values` - The raw, unparsed values of the record. This is a copy, built the first time you ask for it, so writing to it does not change what the remaining columns parse. To substitute a value *before* the record is parsed, use the column's `OnParsing` hook, or the `RecordRead` and `RecordPartitioned` events described under [Skipping Records](#skipping-records).
    * `ExecutionContext` - Details about the current read/write operation.
        * `Schema` - The schema being used to parse the file.
        * `Options` - The options passed to the reader/writer.

## Skipping Records
If you work directly with `DelimitedReader` or `FixedLengthReader`, you can call `Skip` to arbitrarily skip records in the input file. However, you often need the ability to inspect the record to determine whether it needs skipped. But what if you are trying to skip records *because* they can't be parsed? If you need more control over what records to skip, FlatFiles provides events for inspecting records during the parsing process. These events can be wired up whether you use type mappers or are working directly with readers.

Parsing a record goes through the following life-cycle:
1) Read text until a record terminator (usually a newline) is found.
2) For fixed-length records, partition the text into string columns based on the configured windows.
3) Convert the string columns to the designated column types, as defined in the schema.

For CSV files, breaking a record into columns is automatically performed while searching for the record terminator. Prior to trying to convert the text to ints, date/times, etc., FlatFiles provides you the opportunity to inspect the raw string values and skip records.

The `DelimitedReader` class provides a `RecordRead` event, which allows you to skip unwanted records. For example, you could use the code below to find and skip CSV records missing the necessary number of columns:

```csharp
reader.RecordRead += (sender, e) =>
{
    e.IsSkipped = e.Values.Length < 10;
};
```

Fixed-length files come in two flavors: those with and without record terminators. If there is no record terminator, the assumption is *all records are the same length*. Otherwise, each record can be a different length. For that reason, FlatFiles provides an extra opportunity to filter out records prior to splitting the text into columns. This is useful for filtering out records not meeting a minimum length requirement or those using a character to indicate something like comments.

The `FixedLengthReader` class provides the `RecordRead` event, which allows you to skip unwanted records. For example, you could use the code below to find and skip records starting with a `#` symbol:

```csharp
reader.RecordRead += (sender, e) => 
{
    e.IsSkipped = e.Record.StartsWith("#");
};
```

Similar to CSV files, you can also filter out fixed-length records *after* they are broken into columns. However, it is important to note that the record is expected to fit the configured windows.

Again, the `FixedLengthReader` class provides the `RecordPartitioned` event, which allows you to skip unwanted records. For example, you could use the code below to find and skip records whose third column has a flag:

```csharp
reader.RecordPartitioned += (sender, e) => 
{
    e.IsSkipped = e.Values[2] == "ERROR";
};
```

## Error Handling
The reader and writer classes support two events for handling errors: `RecordError` and `ColumnError`. The `ColumnError` event is raised whenever an error occurs while reading/writing a column; for example, when a value can't be parsed. In that case, an instance of `ColumnErrorEventArgs` will be sent to the listener(s), which provides access to the context (`ColumnContext`), the value that caused the error (`ColumnValue`) and the exception that was thrown (`Exception`).

Furthermore, the `IsHandled` property can be set to `true` to inform FlatFiles that the exception should *not* be propagated. In that case, a value should be provided for the `Substitution` property. This property is an `object`, so it's important to make sure the substituted value is the same type as the column or a runtime error may occur.

The following code could be used to detect issues with a file so that errors can be reported back to a user:

```csharp
public class ErrorDetail
{
    public int RecordNumber { get; set; }
    public string ColumnName { get; set; }
    public string ErrorValue { get; set; }
    public string ErrorMessage { get; set; }
} 
//...
var details = new List<ErrorDetail>();
var csvReader = new DelimitedReader(textReader, schema);
csvReader.ColumnError += (sender, e) =>
{
    var columnContext = e.ColumnContext;
    var detail = new ErrorDetail()
    {
        RecordNumber = columnContext.RecordContext.PhysicalRecordNumber,
        ColumnName = columnContext.ColumnDefinition.ColumnName,
        ErrorValue = e.ColumnValue?.ToString(),
        ErrorMessage = e.Exception.InnerException?.Message ?? e.Exception.Message
    };
    details.Add(detail);
    e.IsHandled = true;
    e.Substitution = null;  // May not work for non-nullable value types
};
```

If a column-level exception is not handled, the exception propagates. This, along with other record-level errors, will cause the `RecordError` event to be raised. Listeners will be sent an instance of `RecordErrorEventArgs`, which provides access to the context (`RecordContext`) and the exception that was thrown (`Exception`). Similarly, it provides an `IsHandled` property, that when set to `true`, will prevent the exception from propagating. In the case of record-level errors, this causes the record to be skipped while reading or writing.

## Files Containing Multiple Schemas
Some flat file formats will contain multiple schemas. Often, data appears within "blocks" with a header and perhaps a footer. It can be extremely useful to parse each type of record using a different schema and return them in the same order they appear in the file.

FlatFiles provides support for these file formats using the `SchemaSelector` and `TypeMapperSelector` classes. Once you define the schemas or type mappers, you can register them with the selector.

```csharp
var selector = new DelimitedTypeMapperSelector();
var dataMapper = getDataTypeMapper();
selector.When(x => x.Length == 10).Use(dataMapper);
selector.When(x => x.Length == 2).Use(getHeaderTypeMapper());
selector.When(x => x.Length == 3).Use(getFooterTypeMapper());
selector.WithDefault(dataMapper);
```

Each type mapper is associated with a predicate, which accepts the preprocessed record. If the predicate succeeds, that type mapper will be used to parse the record. Each predicate is tested in the order it is registered, so be sure to make your predicates smart enough to correctly identify the record type.

*Note: You'll might have noticed `dataMapper` is registered twice, once up-front with a specific condition and then as the default. This isn't necessary; however, making sure the most common schema is tried first may help performance in some cases.*

Once your selector is configured, you can call `GetReader`, passing in the `TextReader` and the options object. From there you can register events and read the objects from the file. Since different types can be returned, the reader will return instances of `object`.

If you want to work directly with schemas and readers, you can build a `SchemaSelector` by registering schemas with predicates in a similar fashion. The `DelimitedReader` and `FixedLengthReader` classes provide a constructor accepting a `SchemaSelector`. *Note that whenever you work with selectors, calls to `GetSchema` will return `null`.*

```csharp
var selector = new DelimitedSchemaSelector();
var recordSchema = getDataSchema();
selector.When(values => values.Length == 10).Use(recordSchema);
selector.When(values => values.Length == 2).Use(getHeaderSchema());
selector.When(values => values.Length == 3).Use(getFooterSchema());
selector.WithDefault(recordSchema);

var reader = new DelimitedReader(fileStream, selector);
while (reader.Read())
{
    object[] values = reader.GetValues();
    processRecord(values);
}
```

If you want to *create* multi-schema files, there are "injector" equivalents for each "selector" class. For example:

```csharp
var selector = new FixedLengthTypeMapperInjector();
selector.WithDefault(getRecordTypeMapper());
selector.When<HeaderRecord>().Use(getHeaderTypeMapper());
selector.When<FooterRecord>().Use(getFooterTypeMapper());

var stringWriter = new StringWriter();
var writer = injector.GetWriter(stringWriter);
writer.Write(new HeaderRecord() { BatchName = "First Batch", RecordCount = 2 });
writer.Write(new DataRecord() { Id = 1, Name = "Bob Smith", CreatedOn = new DateTime(2018, 06, 04), TotalAmount = 12.34m });
writer.Write(new DataRecord() { Id = 2, Name = "Jane Doe", CreatedOn = new DateTime(2018, 06, 05), TotalAmount = 34.56m });
writer.Write(new FooterRecord() { TotalAmount = 46.9m, AverageAmount = 23.45m, IsCriteriaMet = true });
```

The `When` method accepts a predicate if you need more than just the record type to decide what type mapper/schema to use.

## Custom Mapping
The `Property` methods are best when your file's schema pretty much matches one-to-one with your classes. One column goes to one property. Frequently, though, your classes are more structured than your flat files. Starting with FlatFiles 3.0, you can provide your own mapping logic to control serialising and deserialising your objects.

First, let's see how to use the `CustomMapping` method to simulate the `Property` methods:

```csharp
var mapper = DelimitedTypeMapper.Define(() => new Person());
mapper.CustomMapping(new StringColumn("FirstName"))
    .WithReader(p => p.FirstName)
    .WithWriter(p => p.FirstName);
```

This mapping tells FlatFiles to use a `StringColumm` to read/write the values from the file. It then says to read the values into and write out of the `FirstName` property of your class.

It's important to note that the type returned by the `IColumnDefinition` must exactly match the type of the property. You must deal with `null`s and conversions yourself. There are several other overloads that provide more control -- this particular example would be better handled sticking to the `Property` methods.

Here is a more complicated example where two columns are used to build a `Geolocation` property.

```csharp
public class Geolocation { public decimal Longitude; public decimal Latitude; }
public class Person { public Geolocation Location { get; set; } }
//...
var mapper = DelimitedTypeMapper.Define(() => new Person() 
{ 
    Location = new Geolocation() 
});
mapper.CustomMapping(new DecimalColumn("Longitude"))
    .WithReader((p, v) => p.Location.Longitude = (decimal)v)  // long way
    .WithWriter(p => p.Location.Longitude);
mapper.CustomMapping(new DecimalColumn("Latitude"))
    .WithReader(p => p.Location.Latitude)  // short way
    .WithWriter(p => p.Location.Latitude);
```

Finally, here is an example configuration, where multiple columns are stored in a collection.

```csharp
public class Contact
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<string> PhoneNumbers { get; set; } = new List<string>();
}
//...
var mapper = FixedLengthTypeMapper.Define(() => new Contact());
mapper.CustomMapping(new Int32Column("Id"), 10)
    .WithReader(c => c.Id)
    .WithWriter(c => c.Id);
mapper.CustomMapping(new StringColumn("Name"), 10)
    .WithReader(c => c.Name)
    .WithWriter(c => c.Name);
mapper.CustomMapping(new StringColumn("Phone1"), 12)
    .WithReader(PhoneReader)
    .WithWriter(c => c.PhoneNumbers.Count > 0 ? c.PhoneNumbers[0] : null);
mapper.CustomMapping(new StringColumn("Phone2"), 12)
    .WithReader(PhoneReader)
    .WithWriter(c => c.PhoneNumbers.Count > 1 ? c.PhoneNumbers[1] : null);
//...
public void PhoneReader(Contact contact, string phoneNumber)
{
    if (phoneNumber != null)
    {
        contact.PhoneNumbers.Add(phoneNumber);
    }
}
```

In benchmarks, using `CustomMapping` is only slightly slower than using `Property`, making it a great option when you need a little extra control. 

There are versions of `WithReader` and `WithWriter` that provide contextual information (`IColumnContext`), so you can access metadata while reading and writing. The `WithWriter` method also provides an overload passing along the underlying array being written to, so you can write to multiple columns simultaneously or inspect previously serialised values.

## Runtime Mapping
Even if you don't know the type of a class at compile time, it can still be beneficial to use the type mappers to populate these objects from a file. Or, if you are working in a language without support for expression trees, you'll be glad to know FlatFiles provides an alternative way to configure type mappers.

The code below illustrates how you could define a mapper for a type that is only known at runtime:

```csharp
// Assume there is a runtime-generated type called "entityType" and a "TextReader".
var mapper = DelimitedMapper.DefineDynamic(entityType);
mapper.Int32Property("Id");
mapper.StringProperty("Name");
var entities = mapper.Read(reader).ToArray();
// Do something with the entities.
```

## Disabling Optimisation
FlatFile's type mappers can serialise and deserialise extremely quickly by generating code at runtime, using classes in the  `System.Reflection.Emit` namespace. For most of us, that's awesome news because it means mapping values to and from your entities is almost as fast as if you had done the mapping by hand. However, there are some environments, like Mono running on iOS, that do not support runtime JIT'ing, so FlatFiles would not work.

Where the runtime cannot generate code at all, as under Native AOT or with the `System.Runtime.IsDynamicCodeSupported` feature switch off, the mappers detect it through `RuntimeFeature.IsDynamicCodeSupported` and use reflection without being asked, so a published AOT application maps entities without throwing and without a code change.

Elsewhere, mappers support a method `OptimiseMapping` that can be used to switch to (A.K.A., slow) reflection. For example:

```csharp
var mapper = DelimitedTypeMapper.Define<Person>(() => new Person());
mapper.Property(x => x.Id);
mapper.Property(x => x.Name);
mapper.OptimiseMapping(false);  // Use normal reflection to get and set properties
```

## Non-Public Classes and Members
As of FlatFiles 3.0, you can no longer map to non-public classes and members (aka., `internal`, `protected` or `private`) without taking additional steps. The simplest solution is to make your classes and members `public`. Alternatively, you can [disable optimisations](#disabling-optimisation) which will cause FlatFiles to use normal reflection, which should be able to access anything, at the cost of some runtime overhead.

Another option is to grant FlatFiles access to your `internal` classes and members by adding the following line to you `Assembly.cs` file:

```csharp
[assembly: InternalsVisibleTo("FlatFiles.DynamicAssembly,PublicKey=00240000048000009400000006020000002400005253413100040000010001009b9e44f637b293021ec4d8625071e5fe1682eeb167c233b46314cca79bf2769606285d5d1225cba8ce1e75be9e8ab7251d17eaf2c3b00fde5eac50a0f7dc7fec2f70279ff71c72341ad2738661babfdc6792479f14fd64d841285644d5c09c2902e9467f574e0d369161caee632087c5d819c3c36f76622306b09a4f868230c1")]
```

Notice that this only grants access to `internal` types/members. You will still not be able to access `private` members.

As a final option, you can use the `CustomMapping` method, passing delegates to read/write your members. Since the delegates are part of your project, they can access non-public members without trouble. There is almost no runtime overhead using `CustomMapping` instead of `Property`.


## ADO.NET DataTables
If you are using `DataTable`s, you can read and write to a `DataTable` using the `ReadFlatFile` and `WriteFlatFile` extension methods. Just pass the corresponding reader or writer object.

```csharp
var customerTable = new DataTable("Customer");
using (var streamReader = new StreamReader(File.OpenRead(@"C:\path\to\file.csv")))
{
    var reader = new DelimitedReader(streamReader, schema);
    customerTable.ReadFlatFile(reader);
}
```

## FlatFileDataReader
For low-level ADO.NET file reading, you can use the `FlatFileDataReader` class. It provides an `IDataReader` interface to the records in the file, making it compatible with other ADO.NET interfaces.

```csharp
// The DataReader Approach
using (var fileReader = new StreamReader(File.OpenRead(@"C:\path\to\file.csv"))
{
    var csvReader = new DelimitedReader(fileReader, schema);
    var dataReader = new FlatFileDataReader(csvReader);
    var customers = new List<Customer>();
    while (dataReader.Read())
    {
        var customer = new Customer();
        customer.CustomerId = dataReader.GetInt32(0);
        customer.Name = dataReader.GetString(1);
        customer.Created = dataReader.GetDateTime(2);
        customer.AverageSales = dataReader.GetDouble(3);
        customers.Add(customer);
    }
    return customers;
}
```

Usually in cases like this, it is just easier to use the type mappers. However, this could be useful if you are swapping out an actual database call with CSV data inside of a unit test.

FlatFiles also provides helpful extension methods on the `IDataReader` interface to make it easier to extract data. It provides `GetNullable*` variants of the `IDataReader` methods, so you don't need to constantly call `IsDBNull`. There are also variants of each method accepting the column name rather than the ordinal position.

There are also generic `GetValue<T>` methods that can deal with type conversions automatically for you. For example, anytime you read in a CSV file without providing the schema, FlatFiles assumes each column is a `string`. When calling `GetValue<int>("Id")` or `GetValue<DateTime?>("CreatedOn")`, FlatFiles will try to convert the values for you. This is extremely helpful when you don't want to provide (*or can't provide*) a schema but can still determine the column types. For example, when you know the column names and their types but their order could be different between runs.

## How This Library Is Verified
Besides the unit tests, this repository reads six files end to end before every release and compares what that cost against figures committed alongside them. The files are generated from profiles describing the shape of real ones — 379 columns; 344,352 records; every field quoted; fourteen record layouts chosen by a single character; a 3,500-character record — and they are committed rather than built, so the bytes being read are fixed and a change in the numbers means a change in the library.

Each file is read four ways: every column as text, every single-typed column given its type, that again with the values asked for, and the file read onto entities through a type mapper. The last is there because the first three never build an entity, so nothing taken from them can move when the mapping path does.

Three figures are compared. How many records each file yields is exact, and so is the requirement that none be refused. What a read allocates per record is compared to within 2%, being deterministic for given bytes on a given runtime. How long a read takes is reported and never compared, because it is not stable enough to decide anything.

The check runs in the publish workflow only, so a release fails if a figure moved without somebody meaning it to. [`FlatFiles.IntegrationTest/README.md`](FlatFiles.IntegrationTest/README.md) has the detail: the six files and what each stresses, the tables of measurements, what every column means, and how to take a new baseline when a change moves one on purpose.

## License
This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <http://unlicense.org>
