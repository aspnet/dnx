REM csc /t:exe /out:kdepend.dll /noconfig /nostdlib @..\..\csc_CoreClr_runtime_assemblies.rsp contracts.cs NativeMethods.cs program.cs 
REM csc /t:exe /out:kdepend.dll /noconfig /nostdlib @..\..\csc_CoreClr_CoreCLR_Goliad.rsp contracts.cs NativeMethods.cs program.cs 

REM csc /t:exe /out:kdepend.dll /noconfig /nostdlib @..\..\csc_CoreClr_K_runtime_assemblies.rsp contracts.cs NativeMethods.cs program.cs 
msbuild