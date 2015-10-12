DNX
===

Travis:   [![Travis](https://travis-ci.org/aspnet/dnx.svg?branch=dev)](https://travis-ci.org/aspnet/dnx)

The .NET Execution Environment contains the code required to bootstrap and run an application. This includes things like the compilation system, SDK tools, and the native CLR hosts.



### Goals
- Create a new development experience that enables a quick development workflow (change code and run)
- Have the ability to create a cached version of your application's dependencies ("compilation")
- Expose metadata about the runtime for others to query

### Principles
- NuGet all the things
- There is no such thing as "design time" (blur the lines between compilation and loading)
- Dependencies are always described as what not where (there's no such thing as a project/nuget/assembly reference)

### Setting up a development environment for incremental compile

After successfully building the solution using the `build` script, you can create a dev symlink to the built DNX using the `build dev-install` command. The command creates symlinks from your user profile's DNX folder to the compiled binaries so you can do incremental builds without having to manually copy files around.

After running `build dev-install` and `dnvm list` you will see a few new runtimes ending with `-dev`. Those are the symlinks created by the build script:

```
Active Version           Runtime Architecture Location                           Alias
------ -------           ------- ------------ --------                           -----
  *    1.0.0-beta2-10735 clr     x86          C:\Users\victorhu\.dnx\runtimes default
       1.0.0-dev         clr     x64          C:\Users\victorhu\.dnx\runtimes clr-x64-dev
       1.0.0-dev         clr     x86          C:\Users\victorhu\.dnx\runtimes clr-x86-dev
       1.0.0-dev         coreclr xd64         C:\Users\victorhu\.dnx\runtimes coreclr-x64-dev
       1.0.0-dev         coreclr x86          C:\Users\victorhu\.dnx\runtimes coreclr-x86-dev
       1.0.0-dev         mono                 C:\Users\victorhu\.dnx\runtimes mono-dev
```

If you want to remove the symlinks, simply delete them from your user profile's DNX folder.

### Initialize the submodules

This repository includes a few submodules. They need to be initialized before build.

To clone a repository and initialize all the submodules you can run:

```
git clone --recursive
```

If you have already cloned the repository without `--recursive` option, you can run following commands to initialize the submodules:

```
git submodule init
git submodule update
```

This project is part of ASP.NET 5. You can find samples, documentation and getting started instructions for ASP.NET 5 at the [Home](https://github.com/aspnet/home) repo.
