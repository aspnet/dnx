using System;
using System.IO;

namespace NuGet
{
    public class PackageInfo
    {
        private readonly IFileSystem _repositoryRoot;
        private readonly string _versionDir;
        private IPackage _package;

        public PackageInfo(IFileSystem repositoryRoot, string packageId, SemanticVersion version, string versionDir)
        {
            _repositoryRoot = repositoryRoot;
            Id = packageId;
            Version = version;
            _versionDir = versionDir;
        }

        public string Id { get; private set; }

        public SemanticVersion Version { get; private set; }

        public IPackage Package
        {
            get
            {
                if (_package == null)
                {
                    var nuspecPath = Path.Combine(_versionDir, string.Format("{0}.nuspec", Id));
                    _package = new UnzippedPackage(_repositoryRoot, nuspecPath);
                }

                return _package;
            }
        }

        public override bool Equals(object obj)
        {
            PackageInfo other = obj as PackageInfo;
            return !object.ReferenceEquals(null, other) && Equals(other);
        }

        public bool Equals(PackageInfo other)
        {
            return !object.ReferenceEquals(null, other) &&
                   string.Equals(Id, other.Id) &&
                   Version.Equals(other.Version);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Version.GetHashCode();
        }
    }
}