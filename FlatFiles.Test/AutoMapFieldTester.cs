using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FlatFiles.TypeMapping;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Tests auto-mapping onto an entity that exposes public fields rather than properties, which takes
    ///     the field-matching path the property-based tests never reach.
    /// </summary>
    [TestClass]
    public class AutoMapFieldTester
    {
        public sealed class FieldEntity
        {
            public int Id;

            public string Name;
        }

        [TestMethod]
        public void TestGetAutoMappedReader_MatchesPublicFieldsByName()
        {
            var matcher = AutoMapMatcher.For( ( column, member ) => string.Equals( column.ColumnName, member.Name, StringComparison.OrdinalIgnoreCase ) );
            var options = new DelimitedOptions { IsFirstRecordSchema = true };
            var reader = DelimitedTypeMapper.GetAutoMappedReader<FieldEntity>( new StringReader( "Id,Name\r\n1,Bob\r\n" ), options, matcher );

            List<FieldEntity> entities = [.. reader.ReadAll()];

            Assert.AreEqual( 1, entities.Count, "One record should have been mapped." );
            Assert.AreEqual( 1, entities[0].Id );
            Assert.AreEqual( "Bob", entities[0].Name );
        }
    }
}
