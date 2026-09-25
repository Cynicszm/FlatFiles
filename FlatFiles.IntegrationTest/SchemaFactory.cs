using System;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Turns a profile into a schema. Two of them, in fact: one that reads every column as text, and one that
    ///     gives a column its own type wherever the profile says every record agrees on what that type is. The
    ///     pair is the point - the difference between them is what parsing a value costs over copying it.
    /// </summary>
    internal static class SchemaFactory
    {
        public static DelimitedSchema Create( FileProfile profile, bool typed )
        {
            var schema = new DelimitedSchema();
            foreach (var column in profile.Columns)
            {
                schema.AddColumn( typed ? ColumnFor( column ) : new StringColumn( column.Name ) );
            }
            return schema;
        }

        /// <summary>
        ///     The column's own type where the profile shows one kind and nothing else, and text otherwise. A
        ///     column that is mixed stays text deliberately: typing it would mean a record failing for a value the
        ///     original file was perfectly happy with, and the run would be measuring error recovery instead.
        /// </summary>
        public static ColumnDefinition ColumnFor( ColumnProfile column )
        {
            var kinds = ( column.Date > 0 ? 1 : 0 ) + ( column.Numeric > 0 ? 1 : 0 ) + ( column.String > 0 ? 1 : 0 );
            if (kinds != 1)
            {
                return new StringColumn( column.Name );
            }
            if (column.Date > 0)
            {
                return new DateTimeColumn( column.Name ) { InputFormat = DateFormatFor( column.ModeWidth ) };
            }
            if (column.String > 0)
            {
                return new StringColumn( column.Name );
            }
            // Anything wide enough to carry a decimal point is written with one, and the widest whole numbers
            // here run past what an int holds.
            return column.MaxWidth >= 4
                ? new DecimalColumn( column.Name )
                : new Int32Column( column.Name );
        }

        public static string DateFormatFor( int width )
        {
            return width switch
            {
                6 => "yyMMdd",
                10 => "yyyy-MM-dd",
                14 => "yyyyMMddHHmmss",
                19 => "yyyy-MM-dd HH:mm:ss",
                23 => "yyyy-MM-dd HH:mm:ss.fff",
                _ => "yyyyMMdd"
            };
        }

        /// <summary>
        ///     A schema per record layout, and the rule that picks between them. The rules are tried in order and
        ///     the first that matches wins, which is how the file was read where the profile came from.
        /// </summary>
        public static FixedLengthSchemaSelector CreateSelector( FileProfile profile, bool typed )
        {
            var selector = new FixedLengthSchemaSelector();
            foreach (var type in profile.RecordTypes)
            {
                selector.When( Predicate( type ) ).Use( CreateLayout( type, typed ) );
            }
            return selector;
        }

        /// <summary>
        ///     One record layout's schema. Writing a file of several layouts needs them one at a time, behind an
        ///     injector rather than a selector, so the schema is built here and the choosing left to the caller.
        /// </summary>
        public static FixedLengthSchema CreateLayout( RecordTypeProfile type, bool typed )
        {
            var schema = new FixedLengthSchema();
            foreach (var column in type.Columns)
            {
                schema.AddColumn( typed ? ColumnFor( column ) : new StringColumn( column.Name ), new Window( column.Window ) );
            }
            return schema;
        }

        public static Func<string, bool> Predicate( RecordTypeProfile type )
        {
            if (type.IsPrefixed)
            {
                var prefix = type.Prefix;
                return record => record.StartsWith( prefix, StringComparison.Ordinal );
            }
            var position = type.Position;
            var key = type.Key[0];
            return record => record.Length > position && record[position] == key;
        }

        /// <summary>
        ///     How many columns the typed schema actually gives a type to, for the run to report.
        /// </summary>
        public static int TypedColumnCount( FileProfile profile )
        {
            var count = 0;
            foreach (var column in profile.AllColumns)
            {
                if (ColumnFor( column ) is not StringColumn)
                {
                    ++count;
                }
            }
            return count;
        }
    }
}
