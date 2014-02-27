# Getting started

### Goals
- Create a new development experience that enables a quick development workflow (change code and run)
- Have the ability to create a cached version of your applicaiton's dependencies ("compilation")
- Expose metadata about the runtime for others to query

### Principles
- There is no such thing as "design time" (blur the lines between compilation and loading)
- Dependencies are always described as what not where (there's no such thing as a project/nuget/assembly reference)

### Writing an application

### Running an application on CoreCLR (k10)
- Append tools directory to PATH. SET PATH=%PATH%;<your_directory>\KRuntime\artifacts\build\ProjectK\tools
- SET TARGET_FRAMEWORK=k10
- k run
