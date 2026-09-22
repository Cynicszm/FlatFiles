using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    [TestClass]
    public class FlatFileDataReaderTester
    {
        [TestMethod]
        public void ShouldGetDefaultSchemaForCsvFile()
        {
            var dataReader = GetFlatFileReaderWithDefaultSchema();
            var schema = dataReader.GetSchemaTable();

            string[] expectedNames = [ "Id", "Name", "CreatedOn", "IsActive", "VisitCount", "UniqueId", "FavouriteDay" ];
            string[] actualNames = [.. schema.Rows.Cast<DataRow>().Select( r => r.Field<string>( SchemaTableColumn.ColumnName ) )];
            CollectionAssert.AreEqual( expectedNames, actualNames );

            int[] expectedPositions = [.. Enumerable.Range( 0, 7 )];
            int[] actualPositions = [.. schema.Rows.Cast<DataRow>().Select( r => r.Field<int>( SchemaTableColumn.ColumnOrdinal ) )];
            CollectionAssert.AreEqual( expectedPositions, actualPositions );

            Type[] expectedTypes = [.. Enumerable.Repeat( typeof( string ), 7 )];
            Type[] actualTypes = [.. schema.Rows.Cast<DataRow>().Select( r => r.Field<Type>( SchemaTableColumn.DataType ) )];
            CollectionAssert.AreEqual( expectedTypes, actualTypes );
        }

        [TestMethod]
        public void ShouldGetDefaultSchemaForCSVFile_GetValue()
        {
            var dataReader = GetFlatFileReaderWithDefaultSchema();
            Assert.IsTrue( dataReader.Read(), "The first record could not be read." );
            Assert.AreEqual( 1, dataReader.GetValue<int>( "Id" ), "The wrong 'Id' was retrieved for 'Bob'." );
            Assert.AreEqual( "Bob", dataReader.GetValue<string>( "Name" ), "The wrong 'Name' was retrieved for 'Bob'." );
            Assert.AreEqual( new DateTime( 2018, 07, 03 ), dataReader.GetValue<DateTime>( "CreatedOn" ), "The wrong 'CreatedOn' was retrieved for 'Bob'." );
            Assert.IsTrue( dataReader.GetValue<bool>( "IsActive" ), "The wrong 'IsActive' was retrieved for 'Bob'" );
            Assert.AreEqual( 10, dataReader.GetValue<int?>( "VisitCount" ), "The wrong 'VisitCount' was retrieved for 'Bob'." );
            Assert.AreEqual( new Guid( "DC3A6AE3-00C8-4884-AC0F-F61EB769DFEB" ), dataReader.GetValue<Guid?>( "UniqueId" ), "The wrong 'UniqueId' was retrieved for 'Bob'." );
            Assert.AreEqual( DayOfWeek.Wednesday, dataReader.GetValue<DayOfWeek>( "FavouriteDay" ), "The wrong 'FavouriteDay' was retrieved for 'Bob'." );

            Assert.IsTrue( dataReader.Read(), "The second record could not be read." );
            Assert.AreEqual( 2, dataReader.GetValue<int>( "Id" ), "The wrong 'Id' was retrieved for 'Susan'." );
            Assert.AreEqual( "Susan", dataReader.GetValue<string>( "Name" ), "The wrong 'Name' was retrieved for 'Susan'." );
            Assert.AreEqual( new DateTime( 2018, 07, 04 ), dataReader.GetValue<DateTime>( "CreatedOn" ), "The wrong 'CreatedOn' was retrieved for 'Susan'." );
            Assert.IsFalse( dataReader.GetValue<bool>( "IsActive" ), "The wrong 'IsActive' was retrieved for 'Susan'" );
            Assert.AreEqual( null, dataReader.GetValue<int?>( "VisitCount" ), "The wrong 'VisitCount' was retrieved for 'Susan'." );
            Assert.AreEqual( new Guid( "{24C250EB-87C9-45DE-B01F-71A7754C6AAD}" ), dataReader.GetValue<Guid?>( "UniqueId" ), "The wrong 'UniqueId' was retrieved for 'Susan'." );
            Assert.AreEqual( DayOfWeek.Friday, dataReader.GetValue<DayOfWeek>( "FavouriteDay" ), "The wrong 'FavouriteDay' was retrieved for 'Susan'." );

            Assert.IsFalse( dataReader.Read(), "Too many records were read." );
        }

        private static FlatFileDataReader GetFlatFileReaderWithDefaultSchema()
        {
            const string data = @"Id,Name,CreatedOn,IsActive,VisitCount,UniqueId,FavouriteDay
1,Bob,2018-07-03,true,10,DC3A6AE3-00C8-4884-AC0F-F61EB769DFEB,Wednesday
2,Susan,2018-07-04,false,,{24C250EB-87C9-45DE-B01F-71A7754C6AAD},5
";
            var reader = new StringReader( data );
            var csvReader = new DelimitedReader( reader, new DelimitedOptions { IsFirstRecordSchema = true } );
            var dataReader = new FlatFileDataReader( csvReader );
            return dataReader;
        }

        [TestMethod]
        public void ShouldGetSpecifiedSchemaForCsvFile()
        {
            var dataReader = GetFlatFileReader();
            var schemaTable = dataReader.GetSchemaTable();

            string[] expectedNames = [ "Id", "Name", "CreatedOn", "IsActive", "VisitCount", "UniqueId" ];
            string[] actualNames = [.. schemaTable.Rows.Cast<DataRow>().Select( r => r.Field<string>( SchemaTableColumn.ColumnName ) )];
            CollectionAssert.AreEqual( expectedNames, actualNames );

            int[] expectedPositions = [.. Enumerable.Range( 0, 6 )];
            int[] actualPositions = [.. schemaTable.Rows.Cast<DataRow>().Select( r => r.Field<int>( SchemaTableColumn.ColumnOrdinal ) )];
            CollectionAssert.AreEqual( expectedPositions, actualPositions );

            Type[] expectedTypes = [ typeof( int ), typeof( string ), typeof( DateTime ), typeof( bool ), typeof( int ), typeof( Guid ) ];
            Type[] actualTypes = [.. schemaTable.Rows.Cast<DataRow>().Select( r => r.Field<Type>( SchemaTableColumn.DataType ) )];
            CollectionAssert.AreEqual( expectedTypes, actualTypes );
        }

        [TestMethod]
        public void ShouldGetRecordsFromReader()
        {
            var dataReader = GetFlatFileReader();
            Assert.IsTrue( dataReader.Read(), "The first record could not be read." );
            Assert.AreEqual( 1, dataReader.GetInt32( "Id" ), "The wrong 'Id' was retrieved for 'Bob'." );
            Assert.AreEqual( "Bob", dataReader.GetString( "Name" ), "The wrong 'Name' was retrieved for 'Bob'." );
            Assert.AreEqual( new DateTime( 2018, 07, 03 ), dataReader.GetDateTime( "CreatedOn" ), "The wrong 'CreatedOn' was retrieved for 'Bob'." );
            Assert.IsTrue( dataReader.GetBoolean( "IsActive" ), "The wrong 'IsActive' was retrieved for 'Bob'" );
            Assert.AreEqual( 10, dataReader.GetNullableInt32( "VisitCount" ), "The wrong 'VisitCount' was retrieved for 'Bob'." );

            Assert.IsTrue( dataReader.Read(), "The second record could not be read." );
            Assert.AreEqual( 2, dataReader.GetInt32( "Id" ), "The wrong 'Id' was retrieved for 'Susan'." );
            Assert.AreEqual( "Susan", dataReader.GetString( "Name" ), "The wrong 'Name' was retrieved for 'Susan'." );
            Assert.AreEqual( new DateTime( 2018, 07, 04 ), dataReader.GetDateTime( "CreatedOn" ), "The wrong 'CreatedOn' was retrieved for 'Susan'." );
            Assert.IsFalse( dataReader.GetBoolean( "IsActive" ), "The wrong 'IsActive' was retrieved for 'Susan'" );
            Assert.AreEqual( null, dataReader.GetNullableInt32( "VisitCount" ), "The wrong 'VisitCount' was retrieved for 'Susan'." );

            Assert.IsFalse( dataReader.Read(), "Too many records were read." );
        }

        [TestMethod]
        public void ShouldGetRecordsFromReader_GetValue()
        {
            var dataReader = GetFlatFileReader();
            Assert.IsTrue( dataReader.Read(), "The first record could not be read." );
            Assert.AreEqual( 1, dataReader.GetValue<int>( "Id" ), "The wrong 'Id' was retrieved for 'Bob'." );
            Assert.AreEqual( "Bob", dataReader.GetValue<string>( "Name" ), "The wrong 'Name' was retrieved for 'Bob'." );
            Assert.AreEqual( new DateTime( 2018, 07, 03 ), dataReader.GetValue<DateTime>( "CreatedOn" ), "The wrong 'CreatedOn' was retrieved for 'Bob'." );
            Assert.IsTrue( dataReader.GetValue<bool>( "IsActive" ), "The wrong 'IsActive' was retrieved for 'Bob'" );
            Assert.AreEqual( 10, dataReader.GetValue<int?>( "VisitCount" ), "The wrong 'VisitCount' was retrieved for 'Bob'." );
            Assert.AreEqual( new Guid( "DC3A6AE3-00C8-4884-AC0F-F61EB769DFEB" ), dataReader.GetValue<Guid?>( "UniqueId" ), "The wrong 'UniqueId' was retrieved for 'Bob'." );

            Assert.IsTrue( dataReader.Read(), "The second record could not be read." );
            Assert.AreEqual( 2, dataReader.GetValue<int>( "Id" ), "The wrong 'Id' was retrieved for 'Susan'." );
            Assert.AreEqual( "Susan", dataReader.GetValue<string>( "Name" ), "The wrong 'Name' was retrieved for 'Susan'." );
            Assert.AreEqual( new DateTime( 2018, 07, 04 ), dataReader.GetValue<DateTime>( "CreatedOn" ), "The wrong 'CreatedOn' was retrieved for 'Susan'." );
            Assert.IsFalse( dataReader.GetValue<bool>( "IsActive" ), "The wrong 'IsActive' was retrieved for 'Susan'" );
            Assert.AreEqual( null, dataReader.GetValue<int?>( "VisitCount" ), "The wrong 'VisitCount' was retrieved for 'Susan'." );
            Assert.AreEqual( new Guid( "{24C250EB-87C9-45DE-B01F-71A7754C6AAD}" ), dataReader.GetValue<Guid?>( "UniqueId" ), "The wrong 'UniqueId' was retrieved for 'Susan'." );

            Assert.IsFalse( dataReader.Read(), "Too many records were read." );
        }

        private static FlatFileDataReader GetFlatFileReader()
        {
            const string data = @"1,Bob,2018-07-03,true,10,DC3A6AE3-00C8-4884-AC0F-F61EB769DFEB
2,Susan,2018-07-04,false,,{24C250EB-87C9-45DE-B01F-71A7754C6AAD}
";
            var reader = new StringReader( data );
            var schema = new DelimitedSchema();
            schema.AddColumn( new Int32Column( "Id" ) );
            schema.AddColumn( new StringColumn( "Name" ) );
            schema.AddColumn( new DateTimeColumn( "CreatedOn" ) );
            schema.AddColumn( new BooleanColumn( "IsActive" ) );
            schema.AddColumn( new Int32Column( "VisitCount" ) );
            schema.AddColumn( new GuidColumn( "UniqueId" ) );
            var csvReader = new DelimitedReader( reader, schema );
            var dataReader = new FlatFileDataReader( csvReader );
            return dataReader;
        }

        [TestMethod]
        public void TestFlatFileReader_IgnoreIgnoredColumns()
        {
            const string data =
@"A,B,C
1,2,3
4,5,6";
            var schema = new DelimitedSchema();
            schema.AddColumn( new StringColumn( "A" ) );
            schema.AddColumn( new IgnoredColumn( "Ignored" ) );
            schema.AddColumn( new StringColumn( "C" ) );

            var options = new DelimitedOptions
            {
                IsFirstRecordSchema = true
            };

            var textReader = new StringReader( data );
            var csvReader = new DelimitedReader( textReader, schema, options );
            using var dataReader = new FlatFileDataReader( csvReader );
            Assert.AreEqual( "A", dataReader.GetName( 0 ) );
            Assert.AreEqual( "C", dataReader.GetName( 1 ) );
            Assert.AreEqual( 0, dataReader.GetOrdinal( "A" ) );
            Assert.AreEqual( -1, dataReader.GetOrdinal( "B" ) );
            Assert.AreEqual( 1, dataReader.GetOrdinal( "C" ) );

            var schemaTable = dataReader.GetSchemaTable();
            string[] columnNames = [.. schemaTable.Rows.OfType<DataRow>().Select( r => r.Field<string>( "ColumnName" ) )];
            CollectionAssert.AreEqual( new[] { "A", "C" }, columnNames );

            Assert.IsTrue( dataReader.Read() );
            var values1 = dataReader.GetValues();
            CollectionAssert.AreEqual( new[] { "1", "3" }, values1 );
            Assert.IsTrue( dataReader.Read() );
            var values2 = dataReader.GetValues();
            CollectionAssert.AreEqual( new[] { "4", "6" }, values2 );
            Assert.IsFalse( dataReader.Read() );
        }
    }
}
