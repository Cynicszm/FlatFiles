using System;
using FlatFiles.Properties;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    /// Represents the mapping from a type property to a Boolean column.
    /// </summary>
    public interface IIgnoredMapping
    {
        /// <summary>
        /// Sets the name of the column in the input or output file.
        /// </summary>
        /// <param name="name">The name of the column.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IIgnoredMapping ColumnName(string name);

        /// <summary>
        /// Sets what value(s) are treated as null.
        /// </summary>
        /// <param name="formatter">The formatter to use.</param>
        /// <returns>The property mapping for further configuration.</returns>
        /// <remarks>Passing null will cause the default formatter to be used.</remarks>
        IIgnoredMapping NullFormatter(INullFormatter formatter);

        /// <summary>
        /// Sets the function to run before the input is parsed.
        /// </summary>
        /// <param name="handler">A function to call before the textual value is parsed.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IIgnoredMapping OnParsing(Func<IColumnContext?, string, string?>? handler);

        /// <summary>
        /// Sets a hook that transforms a value before it is parsed, reading it where it lies rather
        /// than as a string. Unlike <see cref="OnParsing"/> it does not stop the column being read
        /// straight onto an entity.
        /// </summary>
        /// <param name="handler">The hook, or null to remove one.</param>
        /// <returns>The property mapping for further configuration.</returns>
        /// <remarks>
        /// Defaulted so that an implementation written before this existed keeps compiling. The
        /// mappings this library returns all override it.
        /// </remarks>
        IIgnoredMapping OnParsingSpan(SpanParsingHook? handler) =>
            throw new NotSupportedException(Resources.SpanHookNotSupported);

        /// <summary>
        /// Sets the function to run after the input is parsed.
        /// </summary>
        /// <param name="handler">A function to call after the value is parsed.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IIgnoredMapping OnParsed(Func<IColumnContext?, object?, object?>? handler);

        /// <summary>
        /// Sets the function to run before the output is formatted as a string.
        /// </summary>1
        /// <param name="handler">A function to call before the value is formatted as a string.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IIgnoredMapping OnFormatting(Func<IColumnContext?, object?, object?>? handler);

        /// <summary>
        /// Sets the function to run after the output is formatted as a string.
        /// </summary>
        /// <param name="handler">A function to call after the value is formatted as a string.</param>
        /// <returns>The property mapping for further configuration.</returns>
        IIgnoredMapping OnFormatted(Func<IColumnContext?, string?, string?>? handler);
    }
}
