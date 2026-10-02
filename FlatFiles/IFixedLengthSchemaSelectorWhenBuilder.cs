using System;

namespace FlatFiles
{

    /// <summary>
    /// Allows specifying which schema to use when a predicate is matched.
    /// </summary>
    public interface IFixedLengthSchemaSelectorWhenBuilder
    {
        /// <summary>
        /// Specifies which schema to use when the predicate is matched.
        /// </summary>
        /// <param name="schema">The schema to use.</param>
        /// <returns>The builder for further configuration.</returns>
        /// <exception cref="ArgumentNullException">Schema is null.</exception>
        IFixedLengthSchemaSelectorUseBuilder Use(FixedLengthSchema schema);

        /// <summary>
        /// Specifies that a record matching the predicate is passed over rather than read.
        /// </summary>
        /// <returns>The builder for further configuration.</returns>
        /// <exception cref="NotSupportedException">This builder does not support skipping.</exception>
        /// <remarks>
        /// A record nothing matches is an error; a record matched here is not. This is how a file carrying
        /// sections meant for somebody else is read without every line of them being reported, and it decides
        /// the same way in both readers - which a handler for the record being read cannot, because the two
        /// raise that event on opposite sides of choosing a schema.
        /// <para>
        /// The default throws. It is here so that adding this member breaks nobody: the selector's own builder,
        /// which is what <c>When</c> hands back and the only implementation there is, overrides it.
        /// </para>
        /// </remarks>
        IFixedLengthSchemaSelectorUseBuilder Skip()
        {
            throw new NotSupportedException(Properties.Resources.SkipNotSupported);
        }
    }
}
