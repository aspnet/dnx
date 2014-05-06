param(
  [parameter(Position=0)]
  [string] $command,
  [switch] $verbosity = $false,
  [alias("g")][switch] $global = $false,
  [alias("p")][switch] $persistent = $false,
  [switch] $x86 = $false,
  [switch] $x64 = $false,
  [switch] $svr50 = $false,
  [switch] $svrc50 = $false,
  [parameter(Position=1, ValueFromRemainingArguments=$true)]
  [string[]]$args=@()
)

$userKrePath = $env:USERPROFILE + "\.kre"
$userKrePackages = $userKrePath + "\packages"
$globalKrePath = $env:ProgramFiles + "\KRE"
$globalKrePackages = $globalKrePath + "\packages"

$scriptPath = $myInvocation.MyCommand.Definition

function Kvm-Help {
@"
kvm - K Runtime Environment Version Manager

kvm upgrade
  install latest KRE from feed
  set 'default' alias
  add KRE bin to path of current command line

kvm install <semver>|<alias> [-x86][-x64] [-svr50][-svrc50] [-g|-global]
  install requested KRE from feed

kvm list [-g|-global]
  list KRE versions installed 

kvm use <semver>|<alias> [-x86][-x64] [-svr50][-svrc50] [-g|-global]
  add KRE bin to path of current command line

kvm alias
  list KRE aliases which have been defined

kvm alias <alias>
  display value of named alias

kvm alias <alias> <semver> [-x86][-x64] [-svr50][-svrc50]
  set alias to specific version

kvm setup
  install kvm tool machine-wide and download latest KRE 

"@ | Write-Host
}

function Kvm-Global-Setup {
    $persistent = $true

    If (Needs-Elevation)
    {
        $arguments = "& '$scriptPath' setup $(Requested-Switches) -persistent"
        Start-Process "$psHome\powershell.exe" -Verb runAs -ArgumentList $arguments -Wait
        break
    }

    $scriptFolder = [System.IO.Path]::GetDirectoryName($scriptPath)

    Write-Host "Copying file $globalKrePath\bin\kvm.ps1"
    md "$globalKrePath\bin" -Force | Out-Null
    copy "$scriptFolder\kvm.ps1" "$globalKrePath\bin\kvm.ps1"

    Write-Host "Copying file $globalKrePath\bin\kvm.cmd"
    copy "$scriptFolder\kvm.cmd" "$globalKrePath\bin\kvm.cmd"

    Write-Host "Adding $globalKrePath\bin to machine PATH"
    $machinePath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)
    $machinePath = Change-Path $machinePath "$globalKrePath\bin" ($globalKrePath)
    [Environment]::SetEnvironmentVariable("Path", $machinePath, [System.EnvironmentVariableTarget]::Machine)

    Write-Host "Adding $globalKrePath\bin to process PATH"
    $envPath = $env:Path
    $envPath = Change-Path $envPath "$globalKrePath\bin" ($globalKrePath)
    Set-Path $envPath

    Write-Host "Adding $globalKrePath;%USERPROFILE%\.kre to machine KRE_HOME"
    $machineKreHome = [Environment]::GetEnvironmentVariable("KRE_HOME", [System.EnvironmentVariableTarget]::Machine)
    $machineKreHome = Change-Path $machineKreHome "%USERPROFILE%\.kre" ("%USERPROFILE%\.kre")
    $machineKreHome = Change-Path $machineKreHome $globalKrePath ($globalKrePath)

    Kvm-Global-Upgrade
}


function Kvm-Global-Upgrade {
    $version = Kvm-Find-Latest (Requested-Platform "svr50") (Requested-Architecture "x86")
    Kvm-Global-Install $version
    Kvm-Alias-Set "default" $version
}

function Kvm-Upgrade {
    $version = Kvm-Find-Latest (Requested-Platform "svr50") (Requested-Architecture "x86")
    Kvm-Install $version
    Kvm-Alias-Set "default" $version
}


function Kvm-Find-Latest {
param(
    [string] $platform,
    [string] $architecture
)
    Write-Host "Determining latest version"

    $url = "https://www.myget.org/F/aspnetvnext/api/v2/GetUpdates()?packageIds=%27KRE-$platform-$architecture%27&versions=%270.0%27&includePrerelease=true&includeAllVersions=false"

    $wc = New-Object System.Net.WebClient
    $wc.Credentials = new-object System.Net.NetworkCredential("aspnetreadonly", "4d8a2d9c-7b80-4162-9978-47e918c9658c")
    [xml]$xml = $wc.DownloadString($url)

    $version = Select-Xml "//d:Version" -Namespace @{d='http://schemas.microsoft.com/ado/2007/08/dataservices'} $xml 

    return $version
}

function Kvm-Install-Latest {
    Kvm-Install (Kvm-Find-Latest (Requested-Platform "svr50") (Requested-Architecture "x86"))
}

function Do-Kvm-Download {
param(
  [string] $kreFullName,
  [string] $kreFolder
)
    $parts = $kreFullName.Split(".", 2)

    $url = "https://www.myget.org/F/aspnetvnext/api/v2/package/" + $parts[0] + "/" + $parts[1]
    $kreFile = "$kreFolder\$kreFullName.nupkg"

    If (Test-Path $kreFolder) {
        Remove-Item $kreFolder -Force -Recurse
    }

    Write-Host "Downloading" $kreFullName "from https://www.myget.org/F/aspnetvnext/api/v2/"

    md $kreFolder -Force | Out-Null

    $wc = New-Object System.Net.WebClient
    $wc.Credentials = new-object System.Net.NetworkCredential("aspnetreadonly", "4d8a2d9c-7b80-4162-9978-47e918c9658c")
    $wc.DownloadFile($url, $kreFile)

    Do-Kvm-Unpack $kreFile $kreFolder
}

function Do-Kvm-Unpack {
param(
  [string] $kreFile,
  [string] $kreFolder
)
    Write-Host "Installing to" $kreFolder

    [System.Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem') | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($kreFile, $kreFolder)

    If (Test-Path ($kreFolder + "\[Content_Types].xml")) {
        Remove-Item ($kreFolder + "\[Content_Types].xml")
    }
    If (Test-Path ($kreFolder + "\_rels\")) {
        Remove-Item ($kreFolder + "\_rels\") -Force -Recurse
    }
    If (Test-Path ($kreFolder + "\package\")) {
        Remove-Item ($kreFolder + "\package\") -Force -Recurse
    }
}

function Kvm-Global-Install {
param(
  [string] $versionOrAlias
)
    If (Needs-Elevation) {
        $arguments = "& '$scriptPath' install -global $versionOrAlias $(Requested-Switches)"
        Start-Process "$psHome\powershell.exe" -Verb runAs -ArgumentList $arguments -Wait
        Kvm-Global-Use $versionOrAlias
        break
    }

    $kreFullName = Requested-VersionOrAlias $versionOrAlias
    $kreFolder = "$globalKrePackages\$kreFullName"
    Do-Kvm-Download $kreFullName $kreFolder
    Kvm-Global-Use $versionOrAlias
}

function Kvm-Install {
param(
  [string] $versionOrAlias
)
    if ($versionOrAlias.EndsWith(".nupkg"))
    {
        $kreFullName = [System.IO.Path]::GetFileNameWithoutExtension($versionOrAlias)
        $kreFolder = "$userKrePackages\$kreFullName"
        $kreFile = "$kreFolder\$kreFullName.nupkg"

        if (Test-Path($kreFolder)) {
          Write-Host "Target folder '$kreFolder' already exists"
        } else {
          md $kreFolder -Force | Out-Null
          copy $versionOrAlias $kreFile
          Do-Kvm-Unpack $kreFile $kreFolder
        }

        $kreBin = "$kreFolder\bin"
        Write-Host "Adding" $kreBin "to process PATH"
        Set-Path (Change-Path $env:Path $kreBin ($globalKrePackages, $userKrePackages))
    }
    else
    {
        $kreFullName = Requested-VersionOrAlias $versionOrAlias

        $kreFolder = "$userKrePackages\$kreFullName"

        Do-Kvm-Download $kreFullName $kreFolder
        Kvm-Use $versionOrAlias
    }
}

function Kvm-List {
    Get-ChildItem ($userKrePackages) | Select Name
}

function Kvm-Global-List {
    Get-ChildItem ($globalKrePackages) | Select Name
}

function Kvm-Global-Use {
param(
  [string] $versionOrAlias
)
    $kreFullName = Requested-VersionOrAlias $versionOrAlias

    $kreBin = "$globalKrePackages\$kreFullName\bin"

    Write-Host "Adding" $kreBin "to process PATH"
    Set-Path (Change-Path $env:Path $kreBin ($globalKrePackages, $userKrePackages))

    if ($persistent) {
        Write-Host "Adding $kreBin to machine PATH"
        $machinePath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)
        $machinePath = Change-Path $machinePath $kreBin ($globalKrePackages, $userKrePackages)
        [Environment]::SetEnvironmentVariable("Path", $machinePath, [System.EnvironmentVariableTarget]::Machine)
    }
}

function Kvm-Use {
param(
  [string] $versionOrAlias
)
    $kreFullName = Requested-VersionOrAlias $versionOrAlias

    $kreBin = "$userKrePackages\$kreFullName\bin"

    Write-Host "Adding" $kreBin "to process PATH"
    Set-Path (Change-Path $env:Path $kreBin ($globalKrePackages, $userKrePackages))

    if ($persistent) {
        Write-Host "Adding $kreBin to user PATH"
        $userPath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
        $userPath = Change-Path $userPath $kreBin ($globalKrePackages, $userKrePackages)
        [Environment]::SetEnvironmentVariable("Path", $userPath, [System.EnvironmentVariableTarget]::User)
    }
}

function Kvm-Alias-List {
    md ($userKrePath + "\alias\") -Force | Out-Null

    Get-ChildItem ($userKrePath + "\alias\") | Select @{label='Alias';expression={$_.BaseName}}, @{label='Name';expression={Get-Content $_.FullName }} | Format-Table -AutoSize
}

function Kvm-Alias-Get {
param(
  [string] $name
)
    md ($userKrePath + "\alias\") -Force | Out-Null
    Write-Host "Alias '$name' is set to" (Get-Content ($userKrePath + "\alias\" + $name + ".txt"))
}

function Kvm-Alias-Set {
param(
  [string] $name,
  [string] $value
)
    $kreFullName = "KRE-" + (Requested-Platform "svr50") + "-" + (Requested-Architecture "x86") + "." + $value

    Write-Host "Setting alias '$name' to '$kreFullName'"
    md ($userKrePath + "\alias\") -Force | Out-Null
    $kreFullName | Out-File ($userKrePath + "\alias\" + $name + ".txt") ascii
}

function Requested-VersionOrAlias() {
param(
  [string] $versionOrAlias
)
    If (Test-Path ($userKrePath + "\alias\" + $versionOrAlias + ".txt")) {
        $aliasValue = Get-Content ($userKrePath + "\alias\" + $versionOrAlias + ".txt")
        $parts = $aliasValue.Split('.', 2)
        $pkgVersion = $parts[1]
        $parts =$parts[0].Split('-', 3)
        $pkgPlatform = Requested-Platform $parts[1]
        $pkgArchitecture = Requested-Architecture $parts[2]
    } else {
        $pkgVersion = $versionOrAlias
        $pkgPlatform = Requested-Platform "svr50"
        $pkgArchitecture = Requested-Architecture "x86"
    }
    return "KRE-" + $pkgPlatform + "-" + $pkgArchitecture + "." + $pkgVersion
}

function Requested-Platform() {
param(
  [string] $default
)
    if ($svr50 -and $svrc50) {
        Throw "This command cannot accept both -svr50 and -svrc50"
    } 
    if ($svr50) {
        return "svr50"
    }
    if ($svrc50) {
        return "svrc50"
    }
    return $default
}

function Requested-Architecture() {
param(
  [string] $default
)
    if ($x86 -and $x64) {
        Throw "This command cannot accept both -x86 and -x64"
    } 
    if ($x86) {
        return "x86"
    }
    if ($x64) {
        return "x64"
    }
    return $default
}

function Change-Path() {
param(
  [string] $existingPaths,
  [string] $prependPath,
  [string[]] $removePaths
)
    $newPath = $prependPath
    foreach($portion in $existingPaths.Split(';')) {
      $skip = $portion -eq ""
      foreach($removePath in $removePaths) {
        if ($portion.StartsWith($removePath)) {
          $skip = $true
        }      
      }
      if (!$skip) {
        $newPath = $newPath + ";" + $portion
      }
    }
    return $newPath
}

function Set-Path() {
param(
  [string] $newPath
)
$env:Path = $newPath
@"
SET "PATH=$newPath"
"@ | Out-File ($userKrePath + "\run-once.cmd") ascii
}

function Needs-Elevation() {
    $user = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    $elevated = $user.IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
    return -NOT $elevated
}

function Requested-Switches() {
  $arguments = ""
  if ($x86) {$arguments = "$arguments -x86"}
  if ($x64) {$arguments = "$arguments -x64"}
  if ($svr50) {$arguments = "$arguments -svr50"}
  if ($svrc50) {$arguments = "$arguments -svrc50"}
  return $arguments
}

 try {
   if ($global) {
    switch -wildcard ($command + " " + $args.Count) {
#      "setup 0"            {Kvm-Global-Setup}
#      "upgrade 0"         {Kvm-Global-Upgrade}
#      "install 0"         {Kvm-Global-Install-Latest}
      "install 1"         {Kvm-Global-Install $args[0]}
      "list 0"            {Kvm-Global-List}
      "use 1"             {Kvm-Global-Use $args[0]}
      default             {Write-Host 'Unknown command, or global switch not supported'; Kvm-Help;}
    }
   } else {
    switch -wildcard ($command + " " + $args.Count) {
      "setup 0"           {Kvm-Global-Setup}
      "upgrade 0"         {Kvm-Upgrade}
      "install 0"         {Kvm-Install-Latest}
      "install 1"         {Kvm-Install $args[0]}
      "list 0"            {Kvm-List}
      "use 1"             {Kvm-Use $args[0]}
      "alias 0"           {Kvm-Alias-List}
      "alias 1"           {Kvm-Alias-Get $args[0]}
      "alias 2"           {Kvm-Alias-Set $args[0] $args[1]}
      "help"              {Kvm-Help}
      default             {Write-Host 'Unknown command'; Kvm-Help;}
    }
   }
  }
  catch {
    Write-Host $_ -ForegroundColor Red ;
  }
