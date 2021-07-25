﻿using System;

namespace FlatFiles
{
    /// <summary>
    /// Holds the information related to a partitioned, unparsed fixed length record.
    /// </summary>
    public sealed class FixedLengthRecordPartitionedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of a FixedLengthRecordPartitionedEventArgs.
        /// </summary>
        internal FixedLengthRecordPartitionedEventArgs(IRecordContext context, string[] values)
        {
            RecordContext = context;
            Values = values;
        }

        /// <summary>
        /// Gets any metadata associated with the current read process.
        /// </summary>
        public IRecordContext RecordContext { get; }

        /// <summary>
        /// Gets the partitioned, unparsed record values read from the source file.
        /// </summary>
        public string[] Values { get; }

        /// <summary>
        /// Gets or sets whether the record should be skipped.
        /// </summary>
        public bool IsSkipped { get; set; }
    }
}
