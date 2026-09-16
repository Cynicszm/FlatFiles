using System;
using System.Data;
using System.Reflection;

namespace FlatFiles
{
    /// <summary>
    ///     Provides additional helper methods to the IDataRecord interface.
    /// </summary>
    public static class DataRecordExtensions
    {
#region GetBoolean

        extension( IDataRecord record )
        {
            /// <summary>
            ///     Gets the value of the specified column as a Boolean.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column.</returns>
            public bool GetBoolean( string name )
            {
                return Get( record, name, record.GetBoolean );
            }

            /// <summary>
            ///     Gets the value of the specified column as a Boolean -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public bool? GetNullableBoolean( int i )
            {
                return GetNullable( record, i, record.GetBoolean );
            }

            /// <summary>
            ///     Gets the value of the specified column as a Boolean -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public bool? GetNullableBoolean( string name )
            {
                return GetNullable( record, name, record.GetBoolean );
            }

#endregion

#region GetEnum

            /// <summary>
            ///     Maps the value of the column to the specified enumeration value.
            /// </summary>
            /// <typeparam name="TEnum">The type of the value to map to.</typeparam>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column mapped to the enumeration value.</returns>
            /// <remarks>This method attempts to generate the enumeration by its name (case-insensitive) or numeric value.</remarks>
            public TEnum GetEnum<TEnum>( int i )
                where TEnum : Enum
            {
                return GetValue<TEnum>( record, i )!;
            }

            /// <summary>
            ///     Maps the value of the column to the specified enumeration value.
            /// </summary>
            /// <typeparam name="TEnum">The type of the value to map to.</typeparam>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column mapped to the enumeration value.</returns>
            /// <remarks>This method attempts to generate the enumeration by its name (case-insensitive) or numeric value.</remarks>
            public TEnum GetEnum<TEnum>( string name )
                where TEnum : Enum
            {
                int ordinal = record.GetOrdinal( name );
                return GetValue<TEnum>( record, ordinal )!;
            }

            /// <summary>
            ///     Maps the value of the column to the specified enumeration value.
            /// </summary>
            /// <typeparam name="TEnum">The type of the value to map to.</typeparam>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column mapped to the enumeration value.</returns>
            /// <remarks>This method attempts to generate the enumeration by its name (case-insensitive) or numeric value.</remarks>
            public TEnum? GetNullableEnum<TEnum>( int i )
                where TEnum : struct, Enum
            {
                return GetValue<TEnum?>( record, i );
            }

            /// <summary>
            ///     Maps the value of the column to the specified enumeration value.
            /// </summary>
            /// <typeparam name="TEnum">The type of the value to map to.</typeparam>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column mapped to the enumeration value.</returns>
            /// <remarks>This method attempts to generate the enumeration by its name (case-insensitive) or numeric value.</remarks>
            public TEnum? GetNullableEnum<TEnum>( string name )
                where TEnum : struct, Enum
            {
                int ordinal = record.GetOrdinal( name );
                return GetValue<TEnum?>( record, ordinal );
            }

            /// <summary>
            ///     Maps the value of the column to an enumeration value.
            /// </summary>
            /// <typeparam name="T">The type of the column.</typeparam>
            /// <typeparam name="TEnum">The type of value to map to.</typeparam>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <param name="mapper">A method that maps from the column's type to the desired type.</param>
            /// <returns>The value of the column mapped to the enumeration value.</returns>
            public TEnum GetEnum<T, TEnum>( int i, Func<T?, TEnum> mapper )
                where TEnum : Enum
            {
                T? value = GetValue<T>( record, i );
                return mapper( value );
            }

            /// <summary>
            ///     Maps the value of the column to an enumeration value.
            /// </summary>
            /// <typeparam name="T">The type of the column.</typeparam>
            /// <typeparam name="TEnum">The type of value to map to.</typeparam>
            /// <param name="name">The name of the column to find.</param>
            /// <param name="mapper">A method that maps from the column's type to the desired type.</param>
            /// <returns>The value of the column mapped to the enumeration value.</returns>
            public TEnum GetEnum<T, TEnum>( string name, Func<T?, TEnum> mapper )
                where TEnum : Enum
            {
                int ordinal = record.GetOrdinal( name );
                return GetEnum( record, ordinal, mapper );
            }

            /// <summary>
            ///     Maps the value of the column to an enumeration value.
            /// </summary>
            /// <typeparam name="T">The type of the column.</typeparam>
            /// <typeparam name="TEnum">The type of value to map to.</typeparam>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <param name="mapper">A method that maps from the column's type to the desired type.</param>
            /// <returns>The value of the column mapped to the enumeration value.</returns>
            public TEnum? GetNullableEnum<T, TEnum>( int i, Func<T?, TEnum?> mapper )
                where TEnum : struct, Enum
            {
                T? value = GetValue<T>( record, i );
                return mapper( value );
            }

            /// <summary>
            ///     Maps the value of the column to an enumeration value.
            /// </summary>
            /// <typeparam name="T">The type of the column.</typeparam>
            /// <typeparam name="TEnum">The type of value to map to.</typeparam>
            /// <param name="name">The name of the column to find.</param>
            /// <param name="mapper">A method that maps from the column's type to the desired type.</param>
            /// <returns>The value of the column mapped to the enumeration value.</returns>
            public TEnum? GetNullableEnum<T, TEnum>( string name, Func<T?, TEnum?> mapper )
                where TEnum : struct, Enum
            {
                int ordinal = record.GetOrdinal( name );
                return GetNullableEnum( record, ordinal, mapper );
            }

#endregion

#region GetByte

            /// <summary>
            ///     Gets the 8-bit unsigned integer value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The 8-bit unsigned integer value of the specified column.</returns>
            public byte GetByte( string name )
            {
                return Get( record, name, record.GetByte );
            }

            /// <summary>
            ///     Gets the value of the specified column as a byte -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public byte? GetNullableByte( int i )
            {
                return GetNullable( record, i, record.GetByte );
            }

            /// <summary>
            ///     Gets the value of the specified column as a byte -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public byte? GetNullableByte( string name )
            {
                int ordinal = record.GetOrdinal( name );
                return GetNullable( record, ordinal, record.GetByte );
            }

#endregion

#region GetBytes

            /// <summary>
            ///     Reads a stream of bytes from the specified column offset into the buffer
            ///     as an array, starting at the given buffer offset.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <param name="fieldOffset">The index within the field from which to start the read operation.</param>
            /// <param name="buffer">The buffer into which to read the stream of bytes.</param>
            /// <param name="bufferoffset">The index for buffer to start the read operation.</param>
            /// <param name="length">The number of bytes to read.</param>
            /// <returns>The actual number of bytes read.</returns>
            public long GetBytes( string name, long fieldOffset, byte[] buffer, int bufferoffset, int length )
            {
                int ordinal = record.GetOrdinal( name );
                return record.GetBytes( ordinal, fieldOffset, buffer, bufferoffset, length );
            }

#endregion

#region GetChar

            /// <summary>
            ///     Gets the character value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The character value of the specified column.</returns>
            public char GetChar( string name )
            {
                return Get( record, name, record.GetChar );
            }

            /// <summary>
            ///     Gets the value of the specified column as a char -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public char? GetNullableChar( int i )
            {
                return GetNullable( record, i, record.GetChar );
            }

            /// <summary>
            ///     Gets the value of the specified column as a char -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public char? GetNullableChar( string name )
            {
                return GetNullable( record, name, record.GetChar );
            }

#endregion

#region GetChars

            /// <summary>
            ///     Reads a stream of characters from the specified column offset into the buffer
            ///     as an array, starting at the given buffer offset.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <param name="fieldoffset">The index within the row from which to start the read operation.</param>
            /// <param name="buffer">The buffer into which to read the stream of bytes.</param>
            /// <param name="bufferoffset">The index for buffer to start the read operation.</param>
            /// <param name="length">The number of bytes to read.</param>
            /// <returns>The actual number of characters read.</returns>
            public long GetChars( string name, long fieldoffset, char[] buffer, int bufferoffset, int length )
            {
                int ordinal = record.GetOrdinal( name );
                return record.GetChars( ordinal, fieldoffset, buffer, bufferoffset, length );
            }

#endregion

#region GetData

            /// <summary>
            ///     Returns an System.Data.IDataReader for the specified column name.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>An System.Data.IDataReader.</returns>
            public IDataReader GetData( string name )
            {
                return Get( record, name, record.GetData );
            }

#endregion

#region GetDataTypeName

            /// <summary>
            ///     Gets the data type information for the specified field.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The data type information for the specified field.</returns>
            public string GetDataTypeName( string name )
            {
                return Get( record, name, record.GetDataTypeName );
            }

#endregion

#region GetDateTime

            /// <summary>
            ///     Gets the DateTime value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The DateTime value of the specified column.</returns>
            public DateTime GetDateTime( string name )
            {
                return Get( record, name, record.GetDateTime );
            }

            /// <summary>
            ///     Gets the value of the specified column as a DateTime -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public DateTime? GetNullableDateTime( int i )
            {
                return GetNullable( record, i, record.GetDateTime );
            }

            /// <summary>
            ///     Gets the value of the specified column as a DateTime -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public DateTime? GetNullableDateTime( string name )
            {
                return GetNullable( record, name, record.GetDateTime );
            }
        }

#endregion

#region GetDateTimeOffset

        extension( IFlatFileDataRecord record )
        {
            /// <summary>
            ///     Gets the DateTime value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The DateTime value of the specified column.</returns>
            public DateTimeOffset GetDateTimeOffset( string name )
            {
                return Get( record, name, record.GetDateTimeOffset );
            }

            /// <summary>
            ///     Gets the value of the specified column as a DateTime -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public DateTimeOffset? GetNullableDateTimeOffset( int i )
            {
                return GetNullable( record, i, record.GetDateTimeOffset );
            }

            /// <summary>
            ///     Gets the value of the specified column as a DateTime -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public DateTimeOffset? GetNullableDateTimeOffset( string name )
            {
                return GetNullable( record, name, record.GetDateTimeOffset );
            }
        }

#endregion

#region GetDecimal

        extension( IDataRecord record )
        {
            /// <summary>
            ///     Gets the decimal value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The decimal value of the specified column.</returns>
            public decimal GetDecimal( string name )
            {
                return Get( record, name, record.GetDecimal );
            }

            /// <summary>
            ///     Gets the value of the specified column as a decimal -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public decimal? GetNullableDecimal( int i )
            {
                return GetNullable( record, i, record.GetDecimal );
            }

            /// <summary>
            ///     Gets the value of the specified column as a decimal -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public decimal? GetNullableDecimal( string name )
            {
                return GetNullable( record, name, record.GetDecimal );
            }

#endregion

#region GetDouble

            /// <summary>
            ///     Gets the double value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The double value of the specified column.</returns>
            public double GetDouble( string name )
            {
                return Get( record, name, record.GetDouble );
            }

            /// <summary>
            ///     Gets the value of the specified column as a double -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public double? GetNullableDouble( int i )
            {
                return GetNullable( record, i, record.GetDouble );
            }

            /// <summary>
            ///     Gets the value of the specified column as a double -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public double? GetNullableDouble( string name )
            {
                return GetNullable( record, name, record.GetDouble );
            }

#endregion

#region GetFieldType

            /// <summary>
            ///     Gets the System.Type information corresponding to the type of System.Object
            ///     that would be returned from System.Data.IDataRecord.GetValue(System.Int32).
            /// </summary>
            /// <param name="name">The name of the field to find.</param>
            /// <returns>
            ///     The System.Type information corresponding to the type of System.Object that
            ///     would be returned from System.Data.IDataRecord.GetValue(System.Int32).
            /// </returns>
            public Type GetFieldType( string name )
            {
                return Get( record, name, record.GetFieldType );
            }

#endregion

#region GetFloat

            /// <summary>
            ///     Gets the float value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The float value of the specified column.</returns>
            public float GetFloat( string name )
            {
                return Get( record, name, record.GetFloat );
            }

            /// <summary>
            ///     Gets the value of the specified column as a float -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public float? GetNullableFloat( int i )
            {
                return GetNullable( record, i, record.GetFloat );
            }

            /// <summary>
            ///     Gets the value of the specified column as a float -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public float? GetNullableFloat( string name )
            {
                return GetNullable( record, name, record.GetFloat );
            }

#endregion

#region GetGuid

            /// <summary>
            ///     Gets the Guid value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The Guid value of the specified column.</returns>
            public Guid GetGuid( string name )
            {
                return Get( record, name, record.GetGuid );
            }

            /// <summary>
            ///     Gets the value of the specified column as a Guid -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public Guid? GetNullableGuid( int i )
            {
                return GetNullable( record, i, record.GetGuid );
            }

            /// <summary>
            ///     Gets the value of the specified column as a Guid -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public Guid? GetNullableGuid( string name )
            {
                return GetNullable( record, name, record.GetGuid );
            }

#endregion

#region GetInt16

            /// <summary>
            ///     Gets the short value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The short value of the specified column.</returns>
            public short GetInt16( string name )
            {
                return Get( record, name, record.GetInt16 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a short -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public short? GetNullableInt16( int i )
            {
                return GetNullable( record, i, record.GetInt16 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a short -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public short? GetNullableInt16( string name )
            {
                return GetNullable( record, name, record.GetInt16 );
            }

#endregion

#region GetInt32

            /// <summary>
            ///     Gets the int value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The int value of the specified column.</returns>
            public int GetInt32( string name )
            {
                return Get( record, name, record.GetInt32 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a int -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public int? GetNullableInt32( int i )
            {
                return GetNullable( record, i, record.GetInt32 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a int -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public int? GetNullableInt32( string name )
            {
                return GetNullable( record, name, record.GetInt32 );
            }

#endregion

#region GetInt64

            /// <summary>
            ///     Gets the long value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The long value of the specified column.</returns>
            public long GetInt64( string name )
            {
                return Get( record, name, record.GetInt64 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a long -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public long? GetNullableInt64( int i )
            {
                return GetNullable( record, i, record.GetInt64 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a long -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public long? GetNullableInt64( string name )
            {
                return GetNullable( record, name, record.GetInt64 );
            }
        }

#endregion

#region GetSByte

        extension( IFlatFileDataRecord record )
        {
            /// <summary>
            ///     Gets the sbyte value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The sbyte value of the specified column.</returns>
            public sbyte GetSByte( string name )
            {
                return Get( record, name, record.GetSByte );
            }

            /// <summary>
            ///     Gets the value of the specified column as a sbyte -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public sbyte? GetNullableSByte( int i )
            {
                return GetNullable( record, i, record.GetSByte );
            }

            /// <summary>
            ///     Gets the value of the specified column as a sbyte -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public sbyte? GetNullableSByte( string name )
            {
                return GetNullable( record, name, record.GetSByte );
            }
        }

#endregion

#region GetString

        extension( IDataRecord record )
        {
            /// <summary>
            ///     Gets the string value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The int value of the specified column.</returns>
            public string GetString( string name )
            {
                return Get( record, name, record.GetString );
            }

            /// <summary>
            ///     Gets the value of the specified column as a string -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public string? GetNullableString( int i )
            {
                return record.IsDBNull( i ) ? null : record.GetString( i );
            }

            /// <summary>
            ///     Gets the value of the specified column as a string -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public string? GetNullableString( string name )
            {
                int ordinal = record.GetOrdinal( name );
                return GetNullableString( record, ordinal );
            }
        }

#endregion

#region GetTimeSpan

        extension( IFlatFileDataRecord record )
        {
            /// <summary>
            ///     Gets the DateTime value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The DateTime value of the specified column.</returns>
            public TimeSpan GetTimeSpan( string name )
            {
                return Get( record, name, record.GetTimeSpan );
            }

            /// <summary>
            ///     Gets the value of the specified column as a DateTime -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public TimeSpan? GetNullableTimeSpan( int i )
            {
                return GetNullable( record, i, record.GetTimeSpan );
            }

            /// <summary>
            ///     Gets the value of the specified column as a DateTime -or- the specified default value if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- the default value if the column is null.</returns>
            public TimeSpan? GetNullableTimeSpan( string name )
            {
                return GetNullable( record, name, record.GetTimeSpan );
            }

#endregion

#region GetUInt16

            /// <summary>
            ///     Gets the ushort value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The ushort value of the specified column.</returns>
            public ushort GetUInt16( string name )
            {
                return Get( record, name, record.GetUInt16 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a ushort -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public ushort? GetNullableUInt16( int i )
            {
                return GetNullable( record, i, record.GetUInt16 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a ushort -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public ushort? GetNullableUInt16( string name )
            {
                return GetNullable( record, name, record.GetUInt16 );
            }

#endregion

#region GetUInt32

            /// <summary>
            ///     Gets the uint value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The uint value of the specified column.</returns>
            public uint GetUInt32( string name )
            {
                return Get( record, name, record.GetUInt32 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a uint -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public uint? GetNullableUInt32( int i )
            {
                return GetNullable( record, i, record.GetUInt32 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a uint -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public uint? GetNullableUInt32( string name )
            {
                return GetNullable( record, name, record.GetUInt32 );
            }

#endregion

#region GetUInt64

            /// <summary>
            ///     Gets the ulong value of the specified column.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The ulong value of the specified column.</returns>
            public ulong GetUInt64( string name )
            {
                return Get( record, name, record.GetUInt64 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a ulong -or- null if the column is null.
            /// </summary>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public ulong? GetNullableUInt64( int i )
            {
                return GetNullable( record, i, record.GetUInt64 );
            }

            /// <summary>
            ///     Gets the value of the specified column as a ulong -or- null if the column is null.
            /// </summary>
            /// <param name="name">The name of the column to find.</param>
            /// <returns>The value of the column -or- null if the column is null.</returns>
            public ulong? GetNullableUInt64( string name )
            {
                return GetNullable( record, name, record.GetUInt64 );
            }
        }

#endregion

#region GetValue

        extension( IDataRecord record )
        {
            /// <summary>
            ///     Return the value of the specified field.
            /// </summary>
            /// <param name="name">The name of the field to find.</param>
            /// <returns>The System.Object which will contain the field value upon return.</returns>
            public object GetValue( string name )
            {
                return Get( record, name, record.GetValue );
            }

            /// <summary>
            ///     Returns the value of the specified field.
            /// </summary>
            /// <typeparam name="T">The type of the field.</typeparam>
            /// <param name="name">The name of the field to find.</param>
            /// <param name="provider">A format provider for converting to the desired type.</param>
            /// <returns>The value.</returns>
            public T? GetValue<T>( string name, IFormatProvider? provider = null )
            {
                int ordinal = record.GetOrdinal( name );
                return GetValue<T>( record, ordinal, provider );
            }

            /// <summary>
            ///     Returns the value of the specified field.
            /// </summary>
            /// <typeparam name="T">The type of the field.</typeparam>
            /// <param name="i">The zero-based column ordinal.</param>
            /// <param name="provider">A format provider for converting to the desired type.</param>
            /// <returns>The value.</returns>
            public T? GetValue<T>( int i, IFormatProvider? provider = null )
            {
                Type? underlyingType = Nullable.GetUnderlyingType( typeof( T ) );
                Type type = underlyingType ?? typeof( T );
                if (record.IsDBNull( i ))
                {
                    if (type.IsValueType && underlyingType is null)
                    {
                        throw new InvalidCastException();
                    }
                    return default;
                }
                object value = record.GetValue( i );
                if (type != value.GetType())
                {
                    if (type == typeof( Guid ))
                    {
                        value = GetGuid( value );
                    }
                    else if (type.IsEnum)
                    {
                        value = GetEnum( type, value );
                    }
                    else if (value is IConvertible)
                    {
                        value = Convert.ChangeType( value, type, provider );
                    }
                }
                return (T) value;
            }
        }

        private static object GetGuid( object value )
        {
            if (value is string stringValue)
            {
                return Guid.Parse( stringValue );
            }
            if (value is byte[] byteArray)
            {
                return new Guid( byteArray );
            }
            return value;
        }

        private static object GetEnum( Type type, object value )
        {
            try
            {
                if (type.GetCustomAttribute<FlagsAttribute>() is not null)
                {
                    return ToEnum( type, value );
                }
                if (Enum.IsDefined( type, value ))
                {
                    return ToEnum( type, value );
                }
                value = Convert.ChangeType( value, Enum.GetUnderlyingType( type ) );
                if (Enum.IsDefined( type, value ))
                {
                    return Enum.ToObject( type, value );
                }
            }
            catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException or ArgumentException)
            {
                // The value cannot be made into the enumeration, so it is handed back as it was.
            }
            return value;
        }

        private static object ToEnum( Type type, object value )
        {
            return value switch
            {
                string stringValue => Enum.Parse( type, stringValue ),
                _ => Enum.ToObject( type, value )
            };
        }

#endregion

#region GetValues

        extension( IDataRecord record )
        {
            /// <summary>
            ///     Creates an array of objects with the column values of the current record.
            /// </summary>
            /// <param name="replaceDBNulls">Indicates whether DBNull instances should be replaced with nulls.</param>
            /// <returns>An array of objects with the column values of the current record.</returns>
            public object[] GetValues( bool replaceDBNulls = false )
            {
                var values = new object[record.FieldCount];
                record.GetValues( values, replaceDBNulls );
                return values;
            }

            /// <summary>
            ///     Populates an array of objects with the column values of the current record.
            /// </summary>
            /// <param name="values">The array to store the values in.</param>
            /// <param name="replaceDBNulls">Indicates whether DBNull instances should be replaced with nulls.</param>
            /// <returns>The number of objects copied to the array.</returns>
            public int GetValues( object?[] values, bool replaceDBNulls = false )
            {
                // GetValues only writes into the array, so the element nullability it declares is
                // not something it can observe.
                int result = record.GetValues( values! );
                if (replaceDBNulls)
                {
                    for (int index = 0; index != result; ++index)
                    {
                        if (values[index] == DBNull.Value)
                        {
                            values[index] = null;
                        }
                    }
                }
                return result;
            }

#endregion

#region IsDBNull

            /// <summary>
            ///     Return whether the specified field is set to null.
            /// </summary>
            /// <param name="name">The name of the field to find.</param>
            /// <returns>true if the specified field is set to null; otherwise, false.</returns>
            public bool IsDBNull( string name )
            {
                return Get( record, name, record.IsDBNull );
            }
        }

#endregion

        private static T Get<T>( IDataRecord record, string name, Func<int, T> getter )
        {
            int index = record.GetOrdinal( name );
            return getter( index );
        }

        private static T? GetNullable<T>( IDataRecord record, int index, Func<int, T> getter )
            where T : struct
        {
            return record.IsDBNull( index ) ? null : getter( index );
        }

        private static T? GetNullable<T>( IDataRecord record, string name, Func<int, T> getter )
            where T : struct
        {
            int index = record.GetOrdinal( name );
            return GetNullable( record, index, getter );
        }
    }
}
