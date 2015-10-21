namespace Microsoft.Extensions.PlatformAbstractions
{
    public static class LibraryTypes
    {
        public static readonly string Package = nameof(Package);
        public static readonly string Project = nameof(Project);
        public static readonly string ReferenceAssembly = "Assembly"; //Can't use 'nameof(ReferenceAssembly)' because of WTE compat for now. Logged with tooling.
        public static readonly string GlobalAssemblyCache = nameof(GlobalAssemblyCache);
        public static readonly string Unresolved = nameof(Unresolved);
        public static readonly string Implicit = nameof(Implicit);
    }
}
