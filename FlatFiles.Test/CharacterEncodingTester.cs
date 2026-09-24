using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests reading and writing through real byte streams in the encodings flat files arrive in: UTF-8 with and
    ///     without a byte order mark, Windows-1252 and ISO-8859-1. The library works on text and leaves the encoding to the
    ///     StreamReader and StreamWriter, so these tests pin what a caller has to get right at that boundary and what
    ///     happens when they get it wrong.
    /// </summary>
    [TestClass]
    public class CharacterEncodingTester
    {
        /// <summary>
        ///     Names with characters outside ASCII that every encoding under test can represent, so the same text can be
        ///     round-tripped through all of them. The characters only some encodings hold have their own tests.
        /// </summary>
        private static readonly string[] Names = [ "Zoë Müller", "François Ærø", "Ünal Çelik" ];

        private static readonly Encoding Utf8NoBom = new UTF8Encoding( encoderShouldEmitUTF8Identifier: false );
        private static readonly Encoding Utf8WithBom = new UTF8Encoding( encoderShouldEmitUTF8Identifier: true );
        private static readonly Encoding Windows1252 = GetWindows1252();
        private static readonly Encoding Latin1 = Encoding.Latin1;

        private static Encoding GetWindows1252()
        {
            Encoding.RegisterProvider( CodePagesEncodingProvider.Instance );
            return Encoding.GetEncoding( 1252 );
        }

        private static DelimitedSchema Schema()
        {
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "Id" ) );
            schema.AddColumn( new StringColumn( "Name" ) );
            return schema;
        }

        private static DelimitedOptions Options()
        {
            return new DelimitedOptions { IsFirstRecordSchema = true, RecordSeparator = "\r\n" };
        }

        private static byte[] WriteBytes( Encoding encoding, IEnumerable<string> names )
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter( stream, encoding, leaveOpen: true ))
            {
                var flat = new DelimitedWriter( writer, Schema(), Options() );
                var id = 1;
                foreach (var name in names)
                {
                    flat.Write( [ id++, name ] );
                }
            }
            return stream.ToArray();
        }

        private static List<string> ReadNames( byte[] bytes, Encoding encoding, bool detectBom = true )
        {
            using var reader = new StreamReader( new MemoryStream( bytes ), encoding, detectBom );
            var flat = new DelimitedReader( reader, Schema(), Options() );
            List<string> names = [];
            while (flat.Read())
            {
                names.Add( (string) flat.GetValues()[1]! );
            }
            return names;
        }

        private static string FirstColumnName( byte[] bytes, Encoding encoding, bool detectBom )
        {
            using var reader = new StreamReader( new MemoryStream( bytes ), encoding, detectBom );
            var flat = new DelimitedReader( reader, Options() );
            return flat.GetSchema()!.ColumnDefinitions[0].ColumnName!;
        }

        [TestMethod]
        public void TestUtf8WithoutBom_RoundTrips_AndWritesNoPreamble()
        {
            var bytes = WriteBytes( Utf8NoBom, Names );

            Assert.IsFalse( bytes.Take( 3 ).SequenceEqual( new byte[] { 0xEF, 0xBB, 0xBF } ), "No byte order mark should be written." );
            Assert.AreEqual( (byte) 'I', bytes[0], "The file should begin with the header's first character." );
            CollectionAssert.AreEqual( Names, ReadNames( bytes, Utf8NoBom ) );
        }

        [TestMethod]
        public void TestUtf8WithBom_RoundTrips_AndTheMarkIsNotPartOfTheFirstColumnName()
        {
            var bytes = WriteBytes( Utf8WithBom, Names );

            CollectionAssert.AreEqual( new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take( 3 ).ToArray(), "The byte order mark should be written." );
            CollectionAssert.AreEqual( Names, ReadNames( bytes, Utf8WithBom ) );
            Assert.AreEqual( "Id", FirstColumnName( bytes, Utf8NoBom, detectBom: true ), "A reader that detects the mark must not see it as text." );
        }

        [TestMethod]
        public void TestUtf8WithBom_ReaderToldNotToDetectIt_LeaksTheMarkIntoTheHeader()
        {
            // This is the mistake behind "the first column is never found": the mark is read as text and becomes part of the name.
            var bytes = WriteBytes( Utf8WithBom, Names );

            var firstColumn = FirstColumnName( bytes, Utf8NoBom, detectBom: false );

            Assert.AreEqual( "﻿Id", firstColumn );
        }

        [TestMethod]
        public void TestUtf8_MultiByteCharacters_CountAsOneCharacterInFixedLengthWindows()
        {
            // Window widths are in characters. In UTF-8 "ü" is two bytes, but the window still holds one of it.
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Name" ), new Window( 6 ) );
            schema.AddColumn( new Int32Column( "Id" ), new Window( 2 ) );
            var stream = new MemoryStream();
            using (var writer = new StreamWriter( stream, Utf8NoBom, leaveOpen: true ))
            {
                new FixedLengthWriter( writer, schema, new FixedLengthOptions { RecordSeparator = "\r\n" } ).Write( [ "Müller", 42 ] );
            }
            var bytes = stream.ToArray();
            Assert.AreEqual( 9 + 2, bytes.Length, "Six characters, one of them two bytes, then two digits and a two-byte line break." );

            using var reader = new StreamReader( new MemoryStream( bytes ), Utf8NoBom );
            var flat = new FixedLengthReader( reader, schema, new FixedLengthOptions { RecordSeparator = "\r\n" } );
            Assert.IsTrue( flat.Read() );
            CollectionAssert.AreEqual( new object[] { "Müller", 42 }, flat.GetValues() );
        }

        [TestMethod]
        public void TestWindows1252_RoundTrips_AndEncodesTheEuroSignAsOneByte()
        {
            string[] names = [ "Zoë Müller", "€100 “quoted”", "Ünal™" ];

            var bytes = WriteBytes( Windows1252, names );

            Assert.IsTrue( bytes.Contains( (byte) 0x80 ), "The euro sign is 0x80 in Windows-1252." );
            Assert.IsTrue( bytes.Contains( (byte) 0x93 ) && bytes.Contains( (byte) 0x94 ), "Curly quotes are 0x93 and 0x94 in Windows-1252." );
            CollectionAssert.AreEqual( names, ReadNames( bytes, Windows1252 ) );
        }

        [TestMethod]
        public void TestWindows1252_ReadAsLatin1_TurnsTheEuroSignIntoAControlCharacter()
        {
            // The classic mismatch: the two encodings agree everywhere except 0x80-0x9F, which is where the euro sign,
            // curly quotes and the trademark sign live in Windows-1252 and are unassigned control codes in ISO-8859-1.
            var bytes = WriteBytes( Windows1252, [ "€100" ] );

            var misread = ReadNames( bytes, Latin1 )[0];

            Assert.AreEqual( "100", misread );
            Assert.AreNotEqual( "€100", misread );
        }

        [TestMethod]
        public void TestWindows1252_ReadAsUtf8_ProducesReplacementCharacters()
        {
            var bytes = WriteBytes( Windows1252, [ "Zoë" ] );

            var misread = ReadNames( bytes, Utf8NoBom )[0];

            Assert.AreEqual( "Zo�", misread, "A lone 0xEB byte is not valid UTF-8, so the decoder substitutes the replacement character." );
        }

        [TestMethod]
        public void TestLatin1_RoundTrips_WithOneBytePerCharacter()
        {
            var bytes = WriteBytes( Latin1, Names );

            var text = Latin1.GetString( bytes );
            Assert.AreEqual( text.Length, bytes.Length, "ISO-8859-1 is one byte per character." );
            CollectionAssert.AreEqual( Names, ReadNames( bytes, Latin1 ) );
        }

        [TestMethod]
        public void TestLatin1_CharacterOutsideTheEncoding_IsWrittenAsAQuestionMark()
        {
            // The euro sign is not in ISO-8859-1. The encoder substitutes '?', so the value does not survive the round trip.
            var bytes = WriteBytes( Latin1, [ "€100" ] );

            Assert.AreEqual( "?100", ReadNames( bytes, Latin1 )[0] );
        }

        [TestMethod]
        public void TestLatin1_FixedLengthFile_PartitionsByCharacterWhichIsAlsoByByte()
        {
            var schema = new FixedLengthSchema();
            schema.AddColumn( new StringColumn( "Name" ), new Window( 8 ) );
            schema.AddColumn( new StringColumn( "City" ), new Window( 6 ) );
            var bytes = Latin1.GetBytes( "Ærø     Zürich\r\nÇelik   Ünye  \r\n" );

            using var reader = new StreamReader( new MemoryStream( bytes ), Latin1 );
            var flat = new FixedLengthReader( reader, schema );
            List<object?[]> records = [];
            while (flat.Read())
            {
                records.Add( flat.GetValues() );
            }

            Assert.AreEqual( 2, records.Count );
            CollectionAssert.AreEqual( new object[] { "Ærø", "Zürich" }, records[0] );
            CollectionAssert.AreEqual( new object[] { "Çelik", "Ünye" }, records[1] );
        }

        [TestMethod]
        public void TestEveryEncoding_TheSameTextReadBackWithTheSameEncoding_IsIdentical()
        {
            foreach (var encoding in new[] { Utf8NoBom, Utf8WithBom, Windows1252, Latin1 })
            {
                var bytes = WriteBytes( encoding, Names );

                CollectionAssert.AreEqual( Names, ReadNames( bytes, encoding ), encoding.WebName );
            }
        }
    }
}
