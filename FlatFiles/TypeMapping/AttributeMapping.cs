using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using FlatFiles.Properties;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Builds a mapping from the attributes on an entity type, for the callers whose class exists to mirror a
    ///     file and who would rather say so beside the members than in fluent calls somewhere else.
    /// </summary>
    /// <remarks>
    ///     The column's type comes from the member's type, as it does for an auto-mapped file, so the attributes
    ///     carry only a name, an order, a format and - for a fixed-length file - a window. Everything else is left
    ///     to the mapper this returns, which is an ordinary one and can be configured fluently afterwards.
    ///     <para>
    ///         The members are mapped through the dynamic configuration's typed property methods, not through
    ///         <c>CustomMapping</c>, so an attribute-built mapping takes the same path as a hand-written one: typed
    ///         accessors, and the registrations the source generator writes.
    ///     </para>
    /// </remarks>
    internal static class AttributeMapping
    {
        public static void ApplyDelimited( IDynamicDelimitedTypeConfiguration configuration, Type entityType )
        {
            foreach (var column in Discover( entityType, isFixedLength: false ))
            {
                // A delimited record has no fixed positions, so a gap in one means nothing.
                if (column.Member is null)
                {
                    continue;
                }
                var mapping = MapDelimited( configuration, column, entityType );
                Finish( mapping, column );
            }
        }

        public static void ApplyFixedLength( IDynamicFixedLengthTypeConfiguration configuration, Type entityType )
        {
            foreach (var column in Discover( entityType, isFixedLength: true ))
            {
                if (column.Member is null)
                {
                    configuration.Ignored( new Window( column.Width ) );
                    continue;
                }
                var mapping = MapFixedLength( configuration, column, entityType );
                Finish( mapping, column );
            }
        }

        // ------------------------------------------------------------------ discovery

        /// <summary>
        ///     The columns the type declares, in the order the record has them.
        /// </summary>
        /// <remarks>
        ///     Reflection does not promise the order it reports members in, so a member that gave an order is placed
        ///     by it and one that did not keeps the order reflection gave it, after all of them. A fixed-length
        ///     record is its order, so there every column has to say where it goes.
        /// </remarks>
        private static List<MappedColumn> Discover( Type entityType, bool isFixedLength )
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            List<MappedColumn> columns = [];
            var found = 0;
            foreach (var member in entityType.GetProperties( flags ).Cast<MemberInfo>().Concat( entityType.GetFields( flags ) ))
            {
                var attribute = member.GetCustomAttribute<ColumnAttribute>();
                if (attribute is null)
                {
                    continue;
                }
                ++found;
                var window = member.GetCustomAttribute<WindowAttribute>();
                if (isFixedLength && window is null)
                {
                    throw new FlatFileException( string.Format( CultureInfo.CurrentCulture, Resources.AttributeMappingMissingWindow, entityType.Name, member.Name ) );
                }
                if (isFixedLength && attribute.Order == ColumnAttribute.Unordered)
                {
                    throw new FlatFileException( string.Format( CultureInfo.CurrentCulture, Resources.AttributeMappingMissingOrder, entityType.Name, member.Name ) );
                }
                columns.Add( new MappedColumn( member, attribute, window, attribute.Order, window?.Width ?? 0 ) );
            }
            if (found == 0)
            {
                throw new FlatFileException( string.Format( CultureInfo.CurrentCulture, Resources.AttributeMappingNoColumns, entityType.Name ) );
            }
            if (isFixedLength)
            {
                foreach (var gap in entityType.GetCustomAttributes<IgnoredWindowAttribute>())
                {
                    columns.Add( new MappedColumn( null, null, null, gap.Order, gap.Width ) );
                }
            }
            var ordered = columns.Where( x => x.Order != ColumnAttribute.Unordered ).ToList();
            var duplicate = ordered.GroupBy( x => x.Order ).FirstOrDefault( x => x.Count() > 1 );
            if (duplicate is not null)
            {
                throw new FlatFileException( string.Format( CultureInfo.CurrentCulture, Resources.AttributeMappingDuplicateOrder, entityType.Name, duplicate.Key ) );
            }
            // A stable sort: the ones that said where they go, in that order, then the ones that did not, as found.
            return [.. ordered.OrderBy( x => x.Order ), .. columns.Where( x => x.Order == ColumnAttribute.Unordered )];
        }

        // ------------------------------------------------------------------ the column each member takes

        private static object MapDelimited( IDynamicDelimitedTypeConfiguration configuration, MappedColumn column, Type entityType )
        {
            var name = column.Member!.Name;
            return Kind( column.Member, entityType ) switch
            {
                ColumnKind.Boolean => configuration.BooleanProperty( name ),
                ColumnKind.Byte => configuration.ByteProperty( name ),
                ColumnKind.ByteArray => configuration.ByteArrayProperty( name ),
                ColumnKind.Char => configuration.CharProperty( name ),
                ColumnKind.CharArray => configuration.CharArrayProperty( name ),
                ColumnKind.DateOnly => configuration.DateOnlyProperty( name ),
                ColumnKind.DateTime => configuration.DateTimeProperty( name ),
                ColumnKind.DateTimeOffset => configuration.DateTimeOffsetProperty( name ),
                ColumnKind.Decimal => configuration.DecimalProperty( name ),
                ColumnKind.Double => configuration.DoubleProperty( name ),
                ColumnKind.Guid => configuration.GuidProperty( name ),
                ColumnKind.Int16 => configuration.Int16Property( name ),
                ColumnKind.Int32 => configuration.Int32Property( name ),
                ColumnKind.Int64 => configuration.Int64Property( name ),
                ColumnKind.SByte => configuration.SByteProperty( name ),
                ColumnKind.Single => configuration.SingleProperty( name ),
                ColumnKind.String => configuration.StringProperty( name ),
                ColumnKind.TimeOnly => configuration.TimeOnlyProperty( name ),
                ColumnKind.TimeSpan => configuration.TimeSpanProperty( name ),
                ColumnKind.UInt16 => configuration.UInt16Property( name ),
                ColumnKind.UInt32 => configuration.UInt32Property( name ),
                ColumnKind.UInt64 => configuration.UInt64Property( name ),
                _ => Enum( configuration, column.Member, name, null )
            };
        }

        private static object MapFixedLength( IDynamicFixedLengthTypeConfiguration configuration, MappedColumn column, Type entityType )
        {
            var name = column.Member!.Name;
            var window = column.Window!.ToWindow();
            return Kind( column.Member, entityType ) switch
            {
                ColumnKind.Boolean => configuration.BooleanProperty( name, window ),
                ColumnKind.Byte => configuration.ByteProperty( name, window ),
                ColumnKind.ByteArray => configuration.ByteArrayProperty( name, window ),
                ColumnKind.Char => configuration.CharProperty( name, window ),
                ColumnKind.CharArray => configuration.CharArrayProperty( name, window ),
                ColumnKind.DateOnly => configuration.DateOnlyProperty( name, window ),
                ColumnKind.DateTime => configuration.DateTimeProperty( name, window ),
                ColumnKind.DateTimeOffset => configuration.DateTimeOffsetProperty( name, window ),
                ColumnKind.Decimal => configuration.DecimalProperty( name, window ),
                ColumnKind.Double => configuration.DoubleProperty( name, window ),
                ColumnKind.Guid => configuration.GuidProperty( name, window ),
                ColumnKind.Int16 => configuration.Int16Property( name, window ),
                ColumnKind.Int32 => configuration.Int32Property( name, window ),
                ColumnKind.Int64 => configuration.Int64Property( name, window ),
                ColumnKind.SByte => configuration.SByteProperty( name, window ),
                ColumnKind.Single => configuration.SingleProperty( name, window ),
                ColumnKind.String => configuration.StringProperty( name, window ),
                ColumnKind.TimeOnly => configuration.TimeOnlyProperty( name, window ),
                ColumnKind.TimeSpan => configuration.TimeSpanProperty( name, window ),
                ColumnKind.UInt16 => configuration.UInt16Property( name, window ),
                ColumnKind.UInt32 => configuration.UInt32Property( name, window ),
                ColumnKind.UInt64 => configuration.UInt64Property( name, window ),
                _ => Enum( configuration, column.Member, name, window )
            };
        }

        /// <summary>
        ///     Maps an enum member, which is the one kind whose mapping method is itself generic.
        /// </summary>
        /// <remarks>
        ///     Closing it needs <c>MakeGenericMethod</c>, which a runtime without dynamic code cannot always do for
        ///     a value type it was not built with. It happens once, where the mapper is built, so a runtime that
        ///     cannot do it says so there rather than reading a whole file the slow way - and the fluent
        ///     <c>EnumProperty</c> closes its own generic and always works.
        /// </remarks>
        private static object Enum( object configuration, MemberInfo member, string name, Window? window )
        {
            var memberType = MemberType( member );
            var enumType = Nullable.GetUnderlyingType( memberType ) ?? memberType;
            var declaring = window is null ? typeof( IDynamicDelimitedTypeConfiguration ) : typeof( IDynamicFixedLengthTypeConfiguration );
            var method = declaring.GetMethod( "EnumProperty" )!.MakeGenericMethod( enumType );
            var arguments = window is null ? new object[] { name } : [name, window];
            return method.Invoke( configuration, arguments )!;
        }

        // ------------------------------------------------------------------ what the attribute asked for

        /// <summary>
        ///     Applies the name and the format, which are the only things the attribute says that the member cannot.
        /// </summary>
        private static void Finish( object mapping, MappedColumn column )
        {
            if (mapping is not IMemberMapping member || member.ColumnDefinition is not ColumnDefinition definition)
            {
                return;
            }
            var attribute = column.Column!;
            if (!string.IsNullOrEmpty( attribute.Name ))
            {
                definition.ColumnName = attribute.Name;
            }
            if (attribute.Format is not null)
            {
                // A column that has neither is left alone rather than refused: a format on a text column says
                // nothing, and refusing it would make a shared attribute harder to write than it is worth.
                definition.TrySetInputFormat( attribute.Format );
                definition.TrySetOutputFormat( attribute.Format );
            }
        }

        private static ColumnKind Kind( MemberInfo member, Type entityType )
        {
            var memberType = MemberType( member );
            var type = Nullable.GetUnderlyingType( memberType ) ?? memberType;
            if (type.IsEnum)
            {
                return ColumnKind.Enum;
            }
            if (type == typeof( bool )) return ColumnKind.Boolean;
            if (type == typeof( byte )) return ColumnKind.Byte;
            if (type == typeof( byte[] )) return ColumnKind.ByteArray;
            if (type == typeof( char )) return ColumnKind.Char;
            if (type == typeof( char[] )) return ColumnKind.CharArray;
            if (type == typeof( DateOnly )) return ColumnKind.DateOnly;
            if (type == typeof( DateTime )) return ColumnKind.DateTime;
            if (type == typeof( DateTimeOffset )) return ColumnKind.DateTimeOffset;
            if (type == typeof( decimal )) return ColumnKind.Decimal;
            if (type == typeof( double )) return ColumnKind.Double;
            if (type == typeof( Guid )) return ColumnKind.Guid;
            if (type == typeof( short )) return ColumnKind.Int16;
            if (type == typeof( int )) return ColumnKind.Int32;
            if (type == typeof( long )) return ColumnKind.Int64;
            if (type == typeof( sbyte )) return ColumnKind.SByte;
            if (type == typeof( float )) return ColumnKind.Single;
            if (type == typeof( string )) return ColumnKind.String;
            if (type == typeof( TimeOnly )) return ColumnKind.TimeOnly;
            if (type == typeof( TimeSpan )) return ColumnKind.TimeSpan;
            if (type == typeof( ushort )) return ColumnKind.UInt16;
            if (type == typeof( uint )) return ColumnKind.UInt32;
            if (type == typeof( ulong )) return ColumnKind.UInt64;
            throw new FlatFileException( string.Format( CultureInfo.CurrentCulture, Resources.AttributeMappingUnsupportedType, entityType.Name, member.Name, memberType.Name ) );
        }

        private static Type MemberType( MemberInfo member )
        {
            return member is PropertyInfo property ? property.PropertyType : ( (FieldInfo) member ).FieldType;
        }

        private sealed class MappedColumn( MemberInfo? member, ColumnAttribute? column, WindowAttribute? window, int order, int width )
        {
            public MemberInfo? Member { get; } = member;

            public ColumnAttribute? Column { get; } = column;

            public WindowAttribute? Window { get; } = window;

            public int Order { get; } = order;

            public int Width { get; } = width;
        }

        private enum ColumnKind
        {
            Boolean, Byte, ByteArray, Char, CharArray, DateOnly, DateTime, DateTimeOffset, Decimal, Double, Guid,
            Int16, Int32, Int64, SByte, Single, String, TimeOnly, TimeSpan, UInt16, UInt32, UInt64, Enum
        }
    }
}
