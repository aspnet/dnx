using System;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Net.Runtime.Loader;
using Newtonsoft.Json.Linq;

namespace Microsoft.Net.Runtime.Roslyn
{
    public static class TargetFrameworkConfigurationExtensions
    {
        public static CompilationSettings GetCompilationSettings(this Project project, FrameworkName frameworkName)
        {
            // TODO: Don't parse stuff everytime

            var rootOptions = project.GetCompilationOptions();
            var rootDefines = ConvertValue<string[]>(rootOptions, "define") ?? new string[] { };

            var specificOptions = project.GetCompilationOptions(frameworkName);
            var specificDefines = ConvertValue<string[]>(specificOptions, "define") ?? new string[] { };

            var defaultOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            var options = GetCompilationOptions(specificOptions) ?? 
                          GetCompilationOptions(rootOptions) ?? 
                          defaultOptions;

            var settings = new CompilationSettings
            {
                Defines = rootDefines.Concat(specificDefines),
                CompilationOptions = options
            };

            return settings;
        }

        private static CSharpCompilationOptions GetCompilationOptions(JToken compilationOptions)
        {
            if (compilationOptions == null)
            {
                return null;
            }

            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                                .WithHighEntropyVirtualAddressSpace(true);

            bool allowUnsafe = GetValue<bool>(compilationOptions, "allowUnsafe");
            string platformValue = GetValue<string>(compilationOptions, "platform");
            bool warningsAsErrors = GetValue<bool>(compilationOptions, "warningsAsErrors");

            Platform platform;
            if (!Enum.TryParse<Platform>(platformValue, out platform))
            {
                platform = Platform.AnyCPU;
            }

            ReportWarning warningOption = warningsAsErrors ? ReportWarning.Error : ReportWarning.Default;

            return options.WithAllowUnsafe(allowUnsafe)
                          .WithPlatform(platform)
                          .WithGeneralWarningOption(warningOption);
        }

        private static T ConvertValue<T>(JToken token, string name)
        {
            if (token == null)
            {
                return default(T);
            }

            var obj = token[name];

            if (obj == null)
            {
                return default(T);
            }

            return obj.ToObject<T>();
        }

        private static T GetValue<T>(JToken token, string name)
        {
            if (token == null)
            {
                return default(T);
            }

            var obj = token[name];

            if (obj == null)
            {
                return default(T);
            }

            return obj.Value<T>();
        }
    }
}
