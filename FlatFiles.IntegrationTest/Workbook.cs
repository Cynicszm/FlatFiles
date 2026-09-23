using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Writes a small .xlsx. A workbook is a zip of XML parts, and the few this needs are short enough to
    ///     write by hand, which keeps the project free of a spreadsheet dependency for the one thing it produces.
    /// </summary>
    /// <remarks>
    ///     Strings go inline rather than through a shared table. A shared table is what a real writer does, and it
    ///     is smaller on a sheet that repeats itself, but inline strings need no second pass and these sheets are
    ///     mostly numbers.
    /// </remarks>
    internal sealed class Workbook
    {
        private readonly List<Sheet> sheets = [];

        public Sheet AddSheet( string name )
        {
            // Excel refuses these in a sheet name, and refuses one past 31 characters.
            var cleaned = name;
            foreach (var character in @"\/?*[]:")
            {
                cleaned = cleaned.Replace( character, '-' );
            }
            var sheet = new Sheet( cleaned.Length > 31 ? cleaned[..31] : cleaned );
            sheets.Add( sheet );
            return sheet;
        }

        public void Save( string path )
        {
            Directory.CreateDirectory( Path.GetDirectoryName( path )! );
            using var stream = new FileStream( path, FileMode.Create, FileAccess.Write, FileShare.None );
            using var archive = new ZipArchive( stream, ZipArchiveMode.Create );

            Write( archive, "[Content_Types].xml", ContentTypes() );
            Write( archive, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""" );
            Write( archive, "xl/workbook.xml", WorkbookXml() );
            Write( archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships() );
            for (var index = 0; index != sheets.Count; ++index)
            {
                Write( archive, string.Format( CultureInfo.InvariantCulture, "xl/worksheets/sheet{0}.xml", index + 1 ), sheets[index].ToXml() );
            }
        }

        private static void Write( ZipArchive archive, string name, string content )
        {
            using var entry = archive.CreateEntry( name, CompressionLevel.Optimal ).Open();
            var bytes = Encoding.UTF8.GetBytes( content );
            entry.Write( bytes, 0, bytes.Length );
        }

        private string ContentTypes()
        {
            var builder = new StringBuilder();
            builder.Append( """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""" );
            for (var index = 0; index != sheets.Count; ++index)
            {
                builder.Append( CultureInfo.InvariantCulture, $"""<Override PartName="/xl/worksheets/sheet{index + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""" );
            }
            builder.Append( "</Types>" );
            return builder.ToString();
        }

        private string WorkbookXml()
        {
            var builder = new StringBuilder();
            builder.Append( """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""" );
            for (var index = 0; index != sheets.Count; ++index)
            {
                builder.Append( CultureInfo.InvariantCulture, $"""<sheet name="{Sheet.Escape( sheets[index].Name )}" sheetId="{index + 1}" r:id="rId{index + 1}"/>""" );
            }
            builder.Append( "</sheets></workbook>" );
            return builder.ToString();
        }

        private string WorkbookRelationships()
        {
            var builder = new StringBuilder();
            builder.Append( """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""" );
            for (var index = 0; index != sheets.Count; ++index)
            {
                builder.Append( CultureInfo.InvariantCulture, $"""<Relationship Id="rId{index + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{index + 1}.xml"/>""" );
            }
            builder.Append( "</Relationships>" );
            return builder.ToString();
        }
    }

    /// <summary>
    ///     One sheet, built a row at a time. A cell holds a number or text; nothing here needs a date, a formula
    ///     or a format.
    /// </summary>
    internal sealed class Sheet( string name )
    {
        private readonly List<List<object?>> rows = [];

        public string Name { get; } = name;

        public void Add( params object?[] cells )
        {
            rows.Add( [.. cells] );
        }

        public void Blank()
        {
            rows.Add( [] );
        }

        public string ToXml()
        {
            var builder = new StringBuilder();
            builder.Append( """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""" );
            for (var rowIndex = 0; rowIndex != rows.Count; ++rowIndex)
            {
                var row = rows[rowIndex];
                builder.Append( CultureInfo.InvariantCulture, $"""<row r="{rowIndex + 1}">""" );
                for (var cellIndex = 0; cellIndex != row.Count; ++cellIndex)
                {
                    Append( builder, Reference( cellIndex, rowIndex ), row[cellIndex] );
                }
                builder.Append( "</row>" );
            }
            builder.Append( "</sheetData></worksheet>" );
            return builder.ToString();
        }

        private static void Append( StringBuilder builder, string reference, object? value )
        {
            if (value is null)
            {
                return;
            }
            if (value is string text)
            {
                builder.Append( CultureInfo.InvariantCulture, $"""<c r="{reference}" t="inlineStr"><is><t xml:space="preserve">{Escape( text )}</t></is></c>""" );
                return;
            }
            var number = Convert.ToDouble( value, CultureInfo.InvariantCulture );
            builder.Append( CultureInfo.InvariantCulture, $"""<c r="{reference}"><v>{number.ToString( "R", CultureInfo.InvariantCulture )}</v></c>""" );
        }

        private static string Reference( int column, int row )
        {
            Span<char> letters = stackalloc char[3];
            var length = 0;
            for (var index = column; length < 3; index = index / 26 - 1)
            {
                letters[length++] = (char) ( 'A' + index % 26 );
                if (index < 26)
                {
                    break;
                }
            }
            var builder = new StringBuilder( 6 );
            for (var index = length - 1; index >= 0; --index)
            {
                builder.Append( letters[index] );
            }
            return builder.Append( row + 1 ).ToString();
        }

        public static string Escape( string value )
        {
            return value
                .Replace( "&", "&amp;" )
                .Replace( "<", "&lt;" )
                .Replace( ">", "&gt;" )
                .Replace( "\"", "&quot;" );
        }
    }
}
