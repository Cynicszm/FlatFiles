using System;

namespace FlatFiles
{
    /// <summary>
    /// Holds the information related to an unpartitioned, unparsed fixed length record.
    /// </summary>
    public sealed class FixedLengthRecordReadEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of a FixedLengthRecordReadEventArgs.
        /// </summary>
        internal FixedLengthRecordReadEventArgs(IRecordContext context, string record)
        {
            RecordContext = context;
            Record = record;
        }

        /// <summary>
        /// Gets any metadata associated with the current read process.
        /// </summary>
        /// <remarks>
        /// This event is raised before a schema has been chosen for the record, so the context carries no schema.
        /// What it does carry is which record this is, which a handler deciding whether to skip it needs in order
        /// to say so.
        /// </remarks>
        public IRecordContext RecordContext { get; }

        /// <summary>
        /// Gets the unpartitioned, unparsed record values read from the source file.
        /// </summary>
        public string Record { get; }

        /// <summary>
        /// Gets or sets whether the record should be skipped.
        /// </summary>
        public bool IsSkipped { get; set; }
    }
}
