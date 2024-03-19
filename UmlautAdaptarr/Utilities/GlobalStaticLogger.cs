namespace UmlautAdaptarr.Utilities
{
    /// <summary>
    /// Service for providing a static logger to log errors and information. 
    /// The GlobalStaticLogger is designed to provide a static logger that can be used to log errors and information. 
    /// It facilitates logging for both static classes and extension methods.
    /// </summary>
    public static class GlobalStaticLogger
    {

        public static ILogger Logger;

        /// <summary>
        /// Initializes the GlobalStaticLogger with the provided logger factory.
        /// </summary>
        /// <param name="loggerFactory">The ILoggerFactory instance used to create loggers.</param>
        public static void Initialize(ILoggerFactory loggerFactory) => Logger = loggerFactory.CreateLogger("GlobalStaticLogger");
    }
}
