using System;

namespace FlatFiles
{
    /// <summary>
    ///     Decides whether a record belongs to a schema or a mapping, from the record's own characters.
    /// </summary>
    /// <param name="record">The characters of the record, without the separator that ended it.</param>
    /// <returns>True if the record belongs to the schema or mapping the predicate was registered against.</returns>
    /// <remarks>
    ///     The characters are where they lie in the reader's buffer and are only valid for the length of the call,
    ///     which is why this is a delegate of its own rather than a <see cref="Func{T, TResult}" /> over a string:
    ///     nothing is copied, so recognising a record costs nothing. Do not keep hold of the span. Where the
    ///     decision really needs the record's values, register the predicate that takes them instead and the
    ///     reader will split every record as it did before.
    /// </remarks>
    public delegate bool RecordTextPredicate( ReadOnlySpan<char> record );
}
