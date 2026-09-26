using System;

namespace FlatFiles
{
    /// <summary>
    ///     Whether a record is one the options say to pass over without reading: a comment, or a blank line.
    /// </summary>
    /// <remarks>
    ///     Both readers ask this as soon as they have the record and before anything is parsed, so a record passed
    ///     over never reaches a schema selector, a record handler, or the header. That is the whole point of it
    ///     being an option rather than a handler: a <c>RecordRead</c> handler on a delimited reader makes every
    ///     record in the file copy its values out of the parser's buffer, whether the handler wants them or not.
    ///     <para>
    ///         The two readers are asked differently because they have different things to hand. A fixed-length
    ///         reader has the record's text. A delimited one does not - it only builds that string where
    ///         <c>PreserveRecordText</c> asks for it, and otherwise leaves it empty - so it is asked about the
    ///         values it split the record into instead. A comment has no separator in it, or has one and begins
    ///         with the prefix regardless, so its first value carries the prefix either way.
    ///     </para>
    ///     <para>
    ///         A record passed over still counts physically. The physical record number is where a record sits in
    ///         the file, and an error that reports one is only useful if it agrees with what a text editor shows.
    ///     </para>
    /// </remarks>
    internal static class PassedOverRecord
    {
        /// <summary>
        ///     Whether the record whose text this is should be passed over.
        /// </summary>
        public static bool Matches( IOptions options, string record )
        {
            if (options.IsBlankRecordSkipped && string.IsNullOrWhiteSpace( record ))
            {
                return true;
            }
            var prefix = options.CommentPrefix;
            return prefix is { Length: > 0 } && record.StartsWith( prefix, StringComparison.Ordinal );
        }

        /// <summary>
        ///     Whether the record these are the values of should be passed over.
        /// </summary>
        /// <remarks>
        ///     A blank record is one empty value, because that is what a blank line splits into. The prefix is
        ///     matched against the first value as it lies in the buffer, before any trimming a column would do,
        ///     which is the same text the fixed-length reader matches against.
        /// </remarks>
        public static bool Matches( IOptions options, in RawRecord record )
        {
            if (record.Count == 0)
            {
                return options.IsBlankRecordSkipped;
            }
            if (options.IsBlankRecordSkipped && record.Count == 1 && record[0].IsWhiteSpace())
            {
                return true;
            }
            var prefix = options.CommentPrefix;
            return prefix is { Length: > 0 } && record[0].StartsWith( prefix.AsSpan(), StringComparison.Ordinal );
        }
    }
}
