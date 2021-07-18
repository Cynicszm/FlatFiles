﻿using System;
using System.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    /// Tests the FixedLengthSchema class.
    /// </summary>
    [TestClass]
    public class FixedLengthSchemaTester
    {
        /// <summary>
        /// An exception should be thrown if we try to add a null column definition.
        /// </summary>
        [TestMethod]
        public void TestAddColumn_NullDefinition_Throws()
        {
            FixedLengthSchema schema = new FixedLengthSchema();
            Assert.ThrowsException<ArgumentNullException>(() => schema.AddColumn(null, new Window(1)));
        }

        /// <summary>
        /// An exception should be thrown if a column with the same name was already added.
        /// </summary>
        [TestMethod]
        public void TestAddColumn_DuplicateColumnName_Throws()
        {
            FixedLengthSchema schema = new FixedLengthSchema();
            schema.AddColumn(new StringColumn("Name"), new Window(1));
            Assert.ThrowsException<ArgumentException>(() => schema.AddColumn(new Int32Column("name"), new Window(1)));
        }

        /// <summary>
        /// Each of the values should be parsed and returned in the respective location.
        /// </summary>
        [TestMethod]
        public void TestParseValues_ParsesValues()
        {
            FixedLengthSchema schema = new FixedLengthSchema();
            schema.AddColumn(new StringColumn("first_name"), new Window(10))
                  .AddColumn(new StringColumn("last_name"), new Window(10))
                  .AddColumn(new DateTimeColumn("birth_date") { InputFormat = "yyyyMMdd" }, new Window(8))
                  .AddColumn(new Int32Column("weight"), new Window(5));
            string[] values = new string[] { "bob", "smith", "20120123", "185" };
            var executionContext = new FixedLengthExecutionContext(schema, new FixedLengthOptions());
            var recordContext = new FixedLengthRecordContext(executionContext);
            object[] parsed = schema.ParseValues(recordContext, values);
            object[] expected = new object[] { "bob", "smith", new DateTime(2012, 1, 23), 185 };
            CollectionAssert.AreEqual(expected, parsed);
        }

        /// <summary>
        /// If there are no columns in the schema, the ColumnCollection.Count
        /// should be zero.
        /// </summary>
        [TestMethod]
        public void TestColumnDefinitions_NoColumns_CountZero()
        {
            FixedLengthSchema schema = new FixedLengthSchema();
            ColumnCollection collection = schema.ColumnDefinitions;
            Assert.AreEqual(0, collection.Count);
        }

        /// <summary>
        /// If there columns in the schema, the ColumnCollection.Count
        /// should equal the number of columns.
        /// </summary>
        [TestMethod]
        public void TestColumnDefinitions_WithColumns_CountEqualsColumnCount()
        {
            FixedLengthSchema schema = new FixedLengthSchema();
            schema.AddColumn(new Int32Column("id"), new Window(10))
                  .AddColumn(new StringColumn("name"), new Window(25))
                  .AddColumn(new DateTimeColumn("created"), new Window(10));
            ColumnCollection collection = schema.ColumnDefinitions;
            Assert.AreEqual(3, collection.Count);
        }

        /// <summary>
        /// I should be able to get the column definitions by index.
        /// </summary>
        [TestMethod]
        public void TestColumnDefinitions_FindByIndex()
        {
            FixedLengthSchema schema = new FixedLengthSchema();
            IColumnDefinition id = new Int32Column("id");
            IColumnDefinition name = new StringColumn("name");
            IColumnDefinition created = new DateTimeColumn("created");
            schema.AddColumn(id, new Window(10))
                  .AddColumn(name, new Window(25))
                  .AddColumn(created, new Window(10));
            ColumnCollection collection = schema.ColumnDefinitions;
            Assert.AreSame(id, collection[0]);
            Assert.AreSame(name, collection[1]);
            Assert.AreSame(created, collection[2]);
        }

        /// <summary>
        /// Make sure we can get column definitions using the IEnumerable interface.
        /// </summary>
        [TestMethod]
        public void TestColumnDefinitions_GetEnumerable_Explicit()
        {
            FixedLengthSchema schema = new FixedLengthSchema();
            IColumnDefinition id = new Int32Column("id");
            IColumnDefinition name = new StringColumn("name");
            IColumnDefinition created = new DateTimeColumn("created");
            schema.AddColumn(id, new Window(10))
                .AddColumn(name, new Window(25))
                .AddColumn(created, new Window(10));
            IEnumerable collection = schema.ColumnDefinitions;
            IEnumerator enumerator = collection.GetEnumerator();
        }
    }
}
