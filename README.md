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

### Setting up a development environment for incremental compile

After successfully building the solution using the `build` script, you can create a dev symlink to the built K Runtime using the `build dev-install` command. The command creates symlinks from your user profile's KRE folder to the compiled binaries so you can do incremental builds without having to manually copy files around.

After running `build dev-install` and `kvm list` you will see a few new K Runtimes ending with `-dev`. Those are the symlinks created by the build script:

```
Active Version           Runtime Architecture Location                        Alias
------ -------           ------- ------------ --------                        -----
  *    1.0.0-beta2-10735 CLR     x86          C:\Users\victorhu\.kre\packages default
       1.0.0-dev         CLR     amd64        C:\Users\victorhu\.kre\packages CLR-amd64-dev
       1.0.0-dev         CLR     x86          C:\Users\victorhu\.kre\packages CLR-x86-dev
       1.0.0-dev         CoreCLR amd64        C:\Users\victorhu\.kre\packages CoreCLR-amd64-dev
       1.0.0-dev         CoreCLR x86          C:\Users\victorhu\.kre\packages CoreCLR-x86-dev
       1.0.0-dev         Mono                 C:\Users\victorhu\.kre\packages Mono-dev
```

If you want to remove the symlinks, simply delete them from your user profile's KRE folder.

This project is part of ASP.NET vNext. You can find samples, documentation and getting started instructions for ASP.NET vNext at the [Home](https://github.com/aspnet/home) repo.

