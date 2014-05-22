KRuntime
===

This is the code required to bootstrap and run an ASP.NET vNext application. This includes things like the compilation system, SDK tools, and the native CLR hosts.



### Goals
- Create a new development experience that enables a quick development workflow (change code and run)
- Have the ability to create a cached version of your application's dependencies ("compilation")
- Expose metadata about the runtime for others to query

### Principles
- There is no such thing as "design time" (blur the lines between compilation and loading)
- Dependencies are always described as what not where (there's no such thing as a project/nuget/assembly reference)


This project is part of ASP.NET vNext. You can find samples, documentation and getting started instructions for ASP.NET vNext at the [Home](https://github.com/aspnet/home) repo.


