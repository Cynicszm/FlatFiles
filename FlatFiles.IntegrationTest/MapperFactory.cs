using System;
using System.Collections.Generic;
using FlatFiles.TypeMapping;

namespace FlatFiles.IntegrationTest
{
    /// <summary>
    ///     Turns a profile into a type mapper, so that a run covers the path a caller takes when it wants entities
    ///     rather than values. The other scenarios read through a schema, which is a different path through the
    ///     library: it never builds an entity, never asks the code generator for anything, and never uses the
    ///     setters a mapper builds per column. A regression in any of that is invisible to them, and one shipped.
    /// </summary>
    /// <remarks>
    ///     A sample has up to 379 columns and no class to match, so the mapper takes the first column of each kind
    ///     the profile knows about - text, whole number, decimal and date - onto a property of that type, and
    ///     ignores the rest. Ignoring still costs the reader the column; what it saves is 379 properties nobody
    ///     would write. What is measured is therefore a mapped read of the whole file, with a handful of members
    ///     filled per record, which is the shape of most mappings rather than the extreme of one.
    /// </remarks>
    internal static class MapperFactory
    {
        public static IDelimitedTypeMapper<MappedRecord> Create( FileProfile profile )
        {
            var mapper = DelimitedTypeMapper.Define<MappedRecord>();
            var wanted = new Wanted();
            foreach (var column in profile.Columns)
            {
                switch (wanted.Take( column ))
                {
                    case Kind.Text:
                        mapper.Property( x => x.Text ).ColumnName( column.Name );
                        break;
                    case Kind.Whole:
                        mapper.Property( x => x.Whole ).ColumnName( column.Name );
                        break;
                    case Kind.Amount:
                        mapper.Property( x => x.Amount ).ColumnName( column.Name );
                        break;
                    case Kind.Moment:
                        mapper.Property( x => x.Moment ).ColumnName( column.Name ).InputFormat( SchemaFactory.DateFormatFor( column.ModeWidth ) );
                        break;
                    default:
                        mapper.Ignored();
                        break;
                }
            }
            return mapper;
        }

        /// <summary>
        ///     A mapper per record layout behind the same rules the schema selector uses, which is how a file of
        ///     several layouts is read onto types.
        /// </summary>
        public static FixedLengthTypeMapperSelector CreateSelector( FileProfile profile )
        {
            var selector = new FixedLengthTypeMapperSelector();
            foreach (var type in profile.RecordTypes)
            {
                selector.When( SchemaFactory.Predicate( type ) ).Use( CreateLayout( type ) );
            }
            return selector;
        }

        /// <summary>
        ///     The same mappers behind an injector rather than a selector, for writing a file of several layouts.
        /// </summary>
        /// <param name="profile">The file being written.</param>
        /// <param name="layout">
        ///     Which layout the record about to be written belongs to. A selector reads the record's text and
        ///     decides; an injector is given the entity, and every layout here maps onto the same type, so the
        ///     entity cannot say which one it came from. It was settled when the records were read, and this
        ///     hands the answer back in the order they are written.
        /// </param>
        public static FixedLengthTypeMapperInjector CreateInjector( FileProfile profile, Func<int> layout )
        {
            var injector = new FixedLengthTypeMapperInjector();
            for (var index = 0; index != profile.RecordTypes.Count; ++index)
            {
                var wanted = index;
                injector.When<MappedRecord>( _ => layout() == wanted ).Use( CreateLayout( profile.RecordTypes[index] ) );
            }
            return injector;
        }

        private static IFixedLengthTypeMapper<MappedRecord> CreateLayout( RecordTypeProfile type )
        {
            var mapper = FixedLengthTypeMapper.Define<MappedRecord>();
            var wanted = new Wanted();
            foreach (var column in type.Columns)
            {
                var window = new Window( column.Window );
                switch (wanted.Take( column ))
                {
                    case Kind.Text:
                        mapper.Property( x => x.Text, window );
                        break;
                    case Kind.Whole:
                        mapper.Property( x => x.Whole, window );
                        break;
                    case Kind.Amount:
                        mapper.Property( x => x.Amount, window );
                        break;
                    case Kind.Moment:
                        mapper.Property( x => x.Moment, window ).InputFormat( SchemaFactory.DateFormatFor( column.ModeWidth ) );
                        break;
                    default:
                        mapper.Ignored( window );
                        break;
                }
            }
            return mapper;
        }

        /// <summary>
        ///     Hands out each kind of member once, to the first column the profile gives that kind to.
        /// </summary>
        private sealed class Wanted
        {
            private readonly HashSet<Kind> taken = [];

            public Kind Take( ColumnProfile column )
            {
                var kind = KindOf( column );
                return kind != Kind.None && taken.Add( kind ) ? kind : Kind.None;
            }

            private static Kind KindOf( ColumnProfile column )
            {
                return SchemaFactory.ColumnFor( column ) switch
                {
                    DateTimeColumn => Kind.Moment,
                    DecimalColumn => Kind.Amount,
                    Int32Column => Kind.Whole,
                    StringColumn => Kind.Text,
                    _ => Kind.None
                };
            }
        }

        private enum Kind
        {
            None,
            Text,
            Whole,
            Amount,
            Moment
        }
    }

    /// <summary>
    ///     What a mapped read fills. Properties rather than fields, public setters, and types the columns parse to
    ///     exactly or as their nullable form, because those are the conditions on which a mapper sets a member without
    ///     boxing the value. Nullable because a typed column parses an empty field to null, and a member that
    ///     cannot hold one refuses the record - the samples have plenty of both, and a run that refused records
    ///     would be measuring error recovery. A mapper sets a nullable member without boxing either - a
    ///     run that missed any of them would measure the slower path without saying so. Public because emitted
    ///     code cannot reach an internal type's constructor, and an optimised mapping onto one throws where it
    ///     is built rather than falling back.
    /// </summary>
    public sealed class MappedRecord
    {
        public string? Text { get; set; }

        public int? Whole { get; set; }

        public decimal? Amount { get; set; }

        public DateTime? Moment { get; set; }
    }
}
