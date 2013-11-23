namespace NuGet
{
#if LOADER
    public interface ILogger
#else
    public interface ILogger : IFileConflictResolver
#endif
    {
        void Log(MessageLevel level, string message, params object[] args);       
    }
}