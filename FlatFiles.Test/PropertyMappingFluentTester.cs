using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FlatFiles.TypeMapping;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Drives every Property overload on both type mappers and every fluent method on the mapping each
    ///     one returns, so that all of the property mapping classes are exercised rather than only the few
    ///     the feature tests happen to reach.
    /// </summary>
    /// <remarks>
    ///     The mapping classes are near-identical: a dozen setters that each write to the column and return
    ///     the mapping for chaining. Reflection is used deliberately so that adding a mapping or a setter is
    ///     covered without anyone remembering to extend this test.
    /// </remarks>
    [TestClass]
    public class PropertyMappingFluentTester
    {
        public enum Colour
        {
            Red,
            Green
        }

        public sealed class Nested
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        /// <summary>
        ///     One property of every type the mappers accept.
        /// </summary>
        public sealed class Wide
        {
            public bool Bool { get; set; }

            public bool? NullableBool { get; set; }

            public byte[] Bytes { get; set; }

            public byte Byte { get; set; }

            public byte? NullableByte { get; set; }

            public sbyte SByte { get; set; }

            public sbyte? NullableSByte { get; set; }

            public char[] Chars { get; set; }

            public char Char { get; set; }

            public char? NullableChar { get; set; }

            public DateTime DateTime { get; set; }

            public DateTime? NullableDateTime { get; set; }

            public DateTimeOffset DateTimeOffset { get; set; }

            public DateTimeOffset? NullableDateTimeOffset { get; set; }

            public decimal Decimal { get; set; }

            public decimal? NullableDecimal { get; set; }

            public double Double { get; set; }

            public double? NullableDouble { get; set; }

            public Guid Guid { get; set; }

            public Guid? NullableGuid { get; set; }

            public short Int16 { get; set; }

            public short? NullableInt16 { get; set; }

            public TimeSpan TimeSpan { get; set; }

            public TimeSpan? NullableTimeSpan { get; set; }

            public ushort UInt16 { get; set; }

            public ushort? NullableUInt16 { get; set; }

            public int Int32 { get; set; }

            public int? NullableInt32 { get; set; }

            public uint UInt32 { get; set; }

            public uint? NullableUInt32 { get; set; }

            public long Int64 { get; set; }

            public long? NullableInt64 { get; set; }

            public ulong UInt64 { get; set; }

            public ulong? NullableUInt64 { get; set; }

            public float Single { get; set; }

            public float? NullableSingle { get; set; }

            public string Text { get; set; }

            public Colour Colour { get; set; }

            public Colour? NullableColour { get; set; }

            public Nested Nested { get; set; }

            public Nested Other { get; set; }
        }

        private static List<object> DelimitedMappings()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Wide() );
            return
            [
                mapper.Property( x => x.Bool ),
                mapper.Property( x => x.NullableBool ),
                mapper.Property( x => x.Bytes ),
                mapper.Property( x => x.Byte ),
                mapper.Property( x => x.NullableByte ),
                mapper.Property( x => x.SByte ),
                mapper.Property( x => x.NullableSByte ),
                mapper.Property( x => x.Chars ),
                mapper.Property( x => x.Char ),
                mapper.Property( x => x.NullableChar ),
                mapper.Property( x => x.DateTime ),
                mapper.Property( x => x.NullableDateTime ),
                mapper.Property( x => x.DateTimeOffset ),
                mapper.Property( x => x.NullableDateTimeOffset ),
                mapper.Property( x => x.Decimal ),
                mapper.Property( x => x.NullableDecimal ),
                mapper.Property( x => x.Double ),
                mapper.Property( x => x.NullableDouble ),
                mapper.Property( x => x.Guid ),
                mapper.Property( x => x.NullableGuid ),
                mapper.Property( x => x.Int16 ),
                mapper.Property( x => x.NullableInt16 ),
                mapper.Property( x => x.TimeSpan ),
                mapper.Property( x => x.NullableTimeSpan ),
                mapper.Property( x => x.UInt16 ),
                mapper.Property( x => x.NullableUInt16 ),
                mapper.Property( x => x.Int32 ),
                mapper.Property( x => x.NullableInt32 ),
                mapper.Property( x => x.UInt32 ),
                mapper.Property( x => x.NullableUInt32 ),
                mapper.Property( x => x.Int64 ),
                mapper.Property( x => x.NullableInt64 ),
                mapper.Property( x => x.UInt64 ),
                mapper.Property( x => x.NullableUInt64 ),
                mapper.Property( x => x.Single ),
                mapper.Property( x => x.NullableSingle ),
                mapper.Property( x => x.Text ),
                mapper.EnumProperty( x => x.Colour ),
                mapper.EnumProperty( x => x.NullableColour ),
                mapper.ComplexProperty( x => x.Nested, DelimitedTypeMapper.Define( () => new Nested() ) ),
                mapper.ComplexProperty( x => x.Other, FixedLengthTypeMapper.Define( () => new Nested() ) )
            ];
        }

        private static List<object> FixedLengthMappings()
        {
            var mapper = FixedLengthTypeMapper.Define( () => new Wide() );
            var window = new Window( 10 );
            return
            [
                mapper.Property( x => x.Bool, window ),
                mapper.Property( x => x.NullableBool, window ),
                mapper.Property( x => x.Bytes, window ),
                mapper.Property( x => x.Byte, window ),
                mapper.Property( x => x.NullableByte, window ),
                mapper.Property( x => x.SByte, window ),
                mapper.Property( x => x.NullableSByte, window ),
                mapper.Property( x => x.Chars, window ),
                mapper.Property( x => x.Char, window ),
                mapper.Property( x => x.NullableChar, window ),
                mapper.Property( x => x.DateTime, window ),
                mapper.Property( x => x.NullableDateTime, window ),
                mapper.Property( x => x.DateTimeOffset, window ),
                mapper.Property( x => x.NullableDateTimeOffset, window ),
                mapper.Property( x => x.Decimal, window ),
                mapper.Property( x => x.NullableDecimal, window ),
                mapper.Property( x => x.Double, window ),
                mapper.Property( x => x.NullableDouble, window ),
                mapper.Property( x => x.Guid, window ),
                mapper.Property( x => x.NullableGuid, window ),
                mapper.Property( x => x.Int16, window ),
                mapper.Property( x => x.NullableInt16, window ),
                mapper.Property( x => x.TimeSpan, window ),
                mapper.Property( x => x.NullableTimeSpan, window ),
                mapper.Property( x => x.UInt16, window ),
                mapper.Property( x => x.NullableUInt16, window ),
                mapper.Property( x => x.Int32, window ),
                mapper.Property( x => x.NullableInt32, window ),
                mapper.Property( x => x.UInt32, window ),
                mapper.Property( x => x.NullableUInt32, window ),
                mapper.Property( x => x.Int64, window ),
                mapper.Property( x => x.NullableInt64, window ),
                mapper.Property( x => x.UInt64, window ),
                mapper.Property( x => x.NullableUInt64, window ),
                mapper.Property( x => x.Single, window ),
                mapper.Property( x => x.NullableSingle, window ),
                mapper.Property( x => x.Text, window ),
                mapper.EnumProperty( x => x.Colour, window ),
                mapper.EnumProperty( x => x.NullableColour, window ),
                mapper.ComplexProperty( x => x.Nested, DelimitedTypeMapper.Define( () => new Nested() ), window ),
                mapper.ComplexProperty( x => x.Other, FixedLengthTypeMapper.Define( () => new Nested() ), window )
            ];
        }

        /// <summary>
        ///     Synthesises a valid argument for a fluent setter's parameter. Delegates are cleared rather than
        ///     supplied, which is itself a legitimate call, and enums take their first member.
        /// </summary>
        private static object Argument( ParameterInfo parameter )
        {
            var type = parameter.ParameterType;
            if (type == typeof( string ))
            {
                return "x";
            }
            if (type == typeof( bool ))
            {
                return true;
            }
            if (type == typeof( IFormatProvider ))
            {
                return CultureInfo.InvariantCulture;
            }
            if (type == typeof( INullFormatter ))
            {
                return FlatFiles.NullFormatter.Default;
            }
            if (type == typeof( IDefaultValue ))
            {
                return FlatFiles.DefaultValue.Disabled();
            }
            if (type == typeof( DelimitedOptions ))
            {
                return new DelimitedOptions();
            }
            if (type == typeof( FixedLengthOptions ))
            {
                return new FixedLengthOptions();
            }
            if (type.IsEnum)
            {
                return Enum.GetValues( type ).GetValue( 0 );
            }
            if (type.IsValueType)
            {
                return Activator.CreateInstance( type );
            }
            return null;
        }

        /// <summary>
        ///     Calls every public fluent method the mapping declares, checks each returns the same mapping so
        ///     that chaining works, and checks the mapping still has its column afterwards.
        /// </summary>
        private static int ExerciseFluentSurface( object mapping )
        {
            var type = mapping.GetType();
            int invoked = 0;
            foreach (var method in type.GetMethods( BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly ))
            {
                if (method.IsSpecialName || method.IsGenericMethodDefinition || !method.ReturnType.IsInstanceOfType( mapping ))
                {
                    continue;
                }
                object[] arguments = [.. method.GetParameters().Select( Argument )];
                var result = method.Invoke( mapping, arguments );
                Assert.IsTrue( ReferenceEquals( mapping, result ), $"{type.Name}.{method.Name} should return the mapping for chaining." );
                ++invoked;
            }
            Assert.IsNotNull( ((IMemberMapping) mapping).ColumnDefinition, $"{type.Name} lost its column definition." );
            return invoked;
        }

        [TestMethod]
        public void TestDelimitedMapper_EveryMappingChainsEveryFluentMethod()
        {
            var mappings = DelimitedMappings();

            Assert.AreEqual( 41, mappings.Count, "One mapping was expected per property overload, enum overload and complex overload." );
            int invoked = mappings.Sum( ExerciseFluentSurface );
            Assert.IsTrue( invoked >= mappings.Count * 8, $"Only {invoked} fluent methods were driven across {mappings.Count} mappings." );
        }

        [TestMethod]
        public void TestFixedLengthMapper_EveryMappingChainsEveryFluentMethod()
        {
            var mappings = FixedLengthMappings();

            Assert.AreEqual( 41, mappings.Count, "One mapping was expected per property overload, enum overload and complex overload." );
            int invoked = mappings.Sum( ExerciseFluentSurface );
            Assert.IsTrue( invoked >= mappings.Count * 8, $"Only {invoked} fluent methods were driven across {mappings.Count} mappings." );
        }

        /// <summary>
        ///     The reflective sweep proves the setters run; this proves a representative few actually take
        ///     effect on the column.
        /// </summary>
        [TestMethod]
        public void TestDelimitedMapper_FluentSettersConfigureTheColumn()
        {
            var mapper = DelimitedTypeMapper.Define( () => new Wide() );

            var mapping = mapper.Property( x => x.Int64 )
                .ColumnName( "id" )
                .NumberStyles( NumberStyles.AllowThousands )
                .OutputFormat( "N0" )
                .Nullable( false );

            var column = (Int64Column) ((IMemberMapping) mapping).ColumnDefinition;
            Assert.AreEqual( "id", column.ColumnName );
            Assert.AreEqual( NumberStyles.AllowThousands, column.NumberStyles );
            Assert.AreEqual( "N0", column.OutputFormat );
            Assert.IsFalse( column.IsNullable );
        }

        [TestMethod]
        public void TestFixedLengthMapper_FluentSettersConfigureTheColumn()
        {
            var mapper = FixedLengthTypeMapper.Define( () => new Wide() );

            var mapping = mapper.Property( x => x.TimeSpan, new Window( 12 ) )
                .ColumnName( "elapsed" )
                .Nullable( true );

            var column = (TimeSpanColumn) ((IMemberMapping) mapping).ColumnDefinition;
            Assert.AreEqual( "elapsed", column.ColumnName );
            Assert.IsTrue( column.IsNullable );
        }
    }
}
