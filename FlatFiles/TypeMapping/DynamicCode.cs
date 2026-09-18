using System.Runtime.CompilerServices;

namespace FlatFiles.TypeMapping
{
    /// <summary>
    ///     Whether the runtime can generate code at run time, which decides between the emit and the reflection code
    ///     generators when a mapper is optimised. Read once from <see cref="RuntimeFeature.IsDynamicCodeSupported" />,
    ///     which is false under Native AOT and wherever the dynamic-code feature switch is off; settable so the tests can
    ///     exercise the fallback on a runtime that supports both.
    /// </summary>
    internal static class DynamicCode
    {
        public static bool IsSupported { get; set; } = RuntimeFeature.IsDynamicCodeSupported;
    }
}
