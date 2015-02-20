//using NuGet.Packaging.Core;
//using NuGet.Versioning;
//using System.Linq;

//namespace NuGet.Resolver
//{
//    public static class PackageExtensions
//    {
//        public static JArray GetDependencies(ResolverPackage package)
//        {
//            //TODO: Support DependencyGroup TargetFramework. For now, always take the first dependencyGroup
//            if (package[Properties.DependencyGroups] == null)
//                return new JArray();
//            return package[Properties.DependencyGroups][0][Properties.Dependencies] as JArray;
//        }

//        public static PackageIdentity AsPackageIdentity(this JObject package)
//        {
//            return new PackageIdentity(package.GetId(), new NuGetVersion(package.Value<string>(Properties.Version)));
//        }

//        public static string GetId(this JObject package)
//        {
//            return package.Value<string>(Properties.PackageId);
//        }

//        public static string GetVersionAsString(this JObject package)
//        {
//            return package.Value<string>(Properties.Version);
//        }
//        public static NuGetVersion GetVersion(this JObject package)
//        {
//            return NuGetVersion.Parse(package.GetVersionAsString());
//        }

//        /// <summary>
//        /// Extension method to extract the dependency for a given package id
//        /// </summary>
//        /// <param name="package">The package object to inspect.</param>
//        /// <param name="id">The dependency id to look for.</param>
//        /// <returns>The VersionRange for the dependency.</returns>
//        public static VersionRange FindDependencyRange(this JObject package, string id)
//        {
//            var dependencies = package.GetDependencies();
//            if (dependencies == null)
//            {
//                return null;
//            }

//            var dependency = dependencies.Cast<JObject>().FirstOrDefault(d => d.Value<string>(Properties.PackageId) == id);
//            if (dependency == null)
//            {
//                return null;
//            }

//            string rangeString = dependency.Value<string>(Properties.Range);
//            if (string.IsNullOrEmpty(rangeString))
//            {
//                return VersionRange.Parse("0.0"); //Any version allowed
//            }

//            VersionRange range = null;
//            return VersionRange.TryParse(rangeString, out range) ? range : null;
//        }
//    }
//}
