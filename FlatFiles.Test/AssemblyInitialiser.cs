using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlatFiles.Test
{
    /// <summary>
    ///     Pins the culture used by every test in the assembly.
    /// </summary>
    /// <remarks>
    ///     A lot of these tests round-trip dates, decimals and currency through the current culture, so they
    ///     only hold under en-US. Setting that per test class made the outcome depend on which class happened
    ///     to be constructed first on a given thread: FixedLengthReaderMetadataTester never set it at all and
    ///     passed only while a sibling class leaked en-US onto the same thread, which stopped being true when
    ///     the test adapter changed its ordering. Setting the thread default once, before any test runs,
    ///     removes the ordering dependency - so test classes should not set the culture themselves.
    /// </remarks>
    [TestClass]
    public class AssemblyInitialiser
    {
        /// <summary>
        ///     Applies the test culture before the first test in the assembly runs.
        /// </summary>
        /// <param name="context">Holds information about the current test run.</param>
        [AssemblyInitialize]
        public static void Initialise( TestContext context )
        {
            var culture = new CultureInfo( "en-US" );
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}
