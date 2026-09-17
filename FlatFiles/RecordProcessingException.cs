using System;
using System.Globalization;

namespace FlatFiles
{
    /// <summary>
    ///     Represents an error that was thrown while parsing a record.
    /// </summary>
    public sealed class RecordProcessingException : FlatFileException
    {
        internal RecordProcessingException( IRecordContext context, string message )
            : base( string.Format( CultureInfo.CurrentCulture, message, context.PhysicalRecordNumber ) )
        {
            RecordContext = context;
        }

        internal RecordProcessingException( IRecordContext context, string message, Exception innerException )
            : base( string.Format( CultureInfo.CurrentCulture, message, context.PhysicalRecordNumber ), innerException )
        {
            RecordContext = context;
        }

        /// <summary>
        ///     Gets the metadata for the record being processed when the error occurred.
        /// </summary>
        public IRecordContext RecordContext { get; }
    }
}
