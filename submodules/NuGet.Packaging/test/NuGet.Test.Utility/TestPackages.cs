using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NuGet.Test.Utility
{
    public static class TestPackages
    {
        public static ZipArchive GetZip(FileInfo file)
        {
            return new ZipArchive(file.OpenRead());
        }

        public static FileInfo GetLegacyTestPackage()
        {
            string file = Path.GetTempFileName() + ".nupkg";
            FileInfo result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40b.dll", new byte[] { 0 });
                zip.AddEntry("lib/net45/test45.dll", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies> 
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return result;
        }

        public static FileInfo GetLegacyTestPackageMinClient()
        {
            string file = Path.GetTempFileName() + ".nupkg";
            FileInfo result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40b.dll", new byte[] { 0 });
                zip.AddEntry("lib/net45/test45.dll", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata minClientVersion=""3.0.5-beta"">
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies> 
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return result;
        }

        public static FileInfo GetLibSubFolderPackage()
        {
            string file = Path.GetTempFileName() + ".nupkg";
            FileInfo result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/x86/testx86.dll", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies> 
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return result;
        }

        public static FileInfo GetLegacyTestPackageWithReferenceGroups()
        {
            string file = Path.GetTempFileName() + ".nupkg";
            FileInfo result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test40b.dll", new byte[] { 0 });
                zip.AddEntry("lib/net45/test45.dll", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies> 
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                                <references> 
                                  <group targetFramework=""net45""> 
                                      <reference file=""test45.dll"" />
                                  </group> 
                                  <group>
                                    <reference file=""test.dll"" />
                                  </group>
                                </references>
                                <frameworkAssemblies>
                                  <frameworkAssembly assemblyName=""wp80Assem"" targetFramework=""wp80"" />
                                  <frameworkAssembly assemblyName=""anyAssem""  />
                                </frameworkAssemblies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return result;
        }

        public static FileInfo GetLegacyTestPackageWithPre25References()
        {
            string file = Path.GetTempFileName() + ".nupkg";
            FileInfo result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("lib/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/testa.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/test.dll", new byte[] { 0 });
                zip.AddEntry("lib/net40/testb.dll", new byte[] { 0 });
                zip.AddEntry("lib/net45/test45.dll", new byte[] { 0 });
                zip.AddEntry("lib/net451/test.dll", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>packageA</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                                <dependencies> 
                                   <group>
                                      <dependency id=""RouteMagic"" version=""1.1.0"" />
                                   </group>
                                   <group targetFramework=""net40"">
                                      <dependency id=""jQuery"" />
                                      <dependency id=""WebActivator"" />
                                   </group>
                                   <group targetFramework=""sl30"">
                                   </group>
                                </dependencies>
                                <references> 
                                  <reference file=""test.dll"" />
                                </references>
                                <frameworkAssemblies>
                                  <frameworkAssembly assemblyName=""wp80Assem"" targetFramework=""wp80"" />
                                  <frameworkAssembly assemblyName=""anyAssem""  />
                                </frameworkAssemblies>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return result;
        }

        public static FileInfo GetLegacyContentPackage()
        {
            string file = Path.GetTempFileName() + ".nupkg";
            FileInfo result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("content/Scripts/test.js", new byte[] { 0 });
                zip.AddEntry("content/Scripts/test2.js", new byte[] { 0 });
                zip.AddEntry("content/Scripts/test3.js", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>contentPackage</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                              </metadata>
                            </package>", Encoding.UTF8);

            }

            return result;
        }

        public static FileInfo GetLegacyContentPackageWithFrameworks()
        {
            string file = Path.GetTempFileName() + ".nupkg";
            FileInfo result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("content/net40/Scripts/test.js", new byte[] { 0 });
                zip.AddEntry("content/net45/Scripts/test2.js", new byte[] { 0 });
                zip.AddEntry("content/net451/Scripts/test3.js", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>contentPackage</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return result;
        }

        public static FileInfo GetLegacyContentPackageMixed()
        {
            string file = Path.GetTempFileName() + ".nupkg";
            FileInfo result = new FileInfo(file);

            using (var zip = new ZipArchive(File.Create(result.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("content/Scripts/test.js", new byte[] { 0 });
                zip.AddEntry("content/net40/Scripts/test2.js", new byte[] { 0 });
                zip.AddEntry("content/net40/Scripts/testb.js", new byte[] { 0 });
                zip.AddEntry("content/net45/Scripts/test3.js", new byte[] { 0 });

                zip.AddEntry("packageA.nuspec", @"<?xml version=""1.0"" encoding=""utf-8""?>
                            <package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
                              <metadata>
                                <id>contentPackage</id>
                                <version>2.0.3</version>
                                <authors>Author1, author2</authors>
                                <description>Sample description</description>
                                <language>en-US</language>
                                <projectUrl>http://www.nuget.org/</projectUrl>
                                <licenseUrl>http://www.nuget.org/license</licenseUrl>
                              </metadata>
                            </package>", Encoding.UTF8);
            }

            return result;
        }
    }
}
