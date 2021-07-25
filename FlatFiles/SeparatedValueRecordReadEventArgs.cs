﻿using System;

namespace FlatFiles
{
    /// <inheritdoc />
    /// <summary>
    /// Holds the information related to an unparsed separated value record.
    /// </summary>
    public sealed class SeparatedValueRecordReadEventArgs : EventArgs
    {
        /// <inheritdoc />
        /// <summary>
        /// Creates a new instance of a SeparatedValueRecordReadEventArgs.
        /// </summary>
        internal SeparatedValueRecordReadEventArgs(IRecordContext context, string[] values)
        {
            RecordContext = context;
            Values = values;
        }

        /// <summary>
        /// Gets any metadata associated with the current read process.
        /// </summary>
        public IRecordContext RecordContext { get; }

        /// <summary>
        /// Gets the unparsed record values read from the source file.
        /// </summary>
        public string[] Values { get; }

        /// <summary>
        /// Gets or sets whether the record should be skipped.
        /// </summary>
        public bool IsSkipped { get; set; }
    }
}
