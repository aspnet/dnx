@Echo OFF

SET _runner=%~dp0..\Runner

SET _path=%~f1
IF "%_path%"=="" SET _path=%CD%

Call %~dp0\K run %_runner% %_path% < Nul