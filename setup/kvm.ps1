param(
  [parameter(Position=0)]
  [string]$command,
  [switch] $verbosity = $false,
  [alias("g")][switch] $global = $false,
  [switch] $x86 = $false,
  [switch] $x64 = $false,
  [switch] $svr50 = $false,
  [switch] $svrc50 = $false,
  [parameter(Position=1, ValueFromRemainingArguments=$true)]
  [string[]]$args=@()
)

$userKrePath = $env:USERPROFILE + "\.kre"


function Kvm-Help {
@"
kvm - K Runtime Environment Version Manager

kvm upgrade

kvm install <version>|<alias> [-x86][-x64] [-svr50][-svrc50]

kvm use <version>|<alias> [-x86][-x64] [-svr50][-svrc50]

kvm alias
kvm alias <alias>
kvm alias <alias> <version> [-x86][-x64] [-svr50][-svrc50]

kvm setup


"@ | Write-Host
}


function Kvm-Find-Latest {
    Write-Host "Determining latest version"

    $url = "https://www.myget.org/F/aspnetvnext/api/v2/GetUpdates()?packageIds=%27ProjectK%27&versions=%270.0%27&includePrerelease=true&includeAllVersions=false"

    $wc = New-Object System.Net.WebClient
    $wc.Credentials = new-object System.Net.NetworkCredential("aspnetreadonly", "4d8a2d9c-7b80-4162-9978-47e918c9658c")
    [xml]$xml = $wc.DownloadString($url)

    $version = Select-Xml "//d:Version" -Namespace @{d='http://schemas.microsoft.com/ado/2007/08/dataservices'} $xml 

    return $version
}

function Kvm-Install-Latest {
    Kvm-Install Kvm-Find-Latest
}

function Kvm

function Kvm-Install {
param(
  [string] $version
)
    if ($version -eq "") {
        $version = Kvm-Find-Latest
    }

    $url = "https://www.myget.org/F/aspnetvnext/api/v2/package/ProjectK/" + $version
    $kreFolder = $userKrePath + "\packages\ProjectK." + $version
    $kreFile = $kreFolder + "\ProjectK." + $version + ".nupkg"

    If (Test-Path $kreFolder) {
        Remove-Item $kreFolder -Force -Recurse
    }

    Write-Host "Downloading" $version "from https://www.myget.org/F/aspnetvnext/api/v2/"

    md $kreFolder -Force | Out-Null

    $wc = New-Object System.Net.WebClient
    $wc.Credentials = new-object System.Net.NetworkCredential("aspnetreadonly", "4d8a2d9c-7b80-4162-9978-47e918c9658c")
    $wc.DownloadFile($url, $kreFile)

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

    Kvm-Use $version
}

function Kvm-Use {
param(
  [string] $version
)

    If (Test-Path ($userKrePath + "\alias\" + $version + ".txt")) {
        $version = Get-Content ($userKrePath + "\alias\" + $version + ".txt")
    }

    $kreBin = $userKrePath + "\packages\ProjectK." + $version + "\tools"

    Write-Host "Adding" $kreBin "to PATH"

    $newPath = $kreBin
    foreach($portion in $env:Path.Split(';')) {
      if (!$portion.StartsWith($userKrePath)) {
        $newPath = $newPath + ";" + $portion
      }
    }

@"
SET "KRE_VERSION=$version"
SET "PATH=$newPath"
"@ | Out-File ($userKrePath + "\run-once.cmd") ascii
}

function Kvm-Alias-List {
    md ($userKrePath + "\alias\") -Force | Out-Null

    Get-ChildItem ($userKrePath + "\alias\") | Select @{label='alias';expression={$_.BaseName}}, @{label='value';expression={Get-Content $_.FullName }} | Format-Table -AutoSize
}

function Kvm-Alias-Get {
param(
  [string] $name
)
    Write-Host "Alias '$name' is set to"
    md ($userKrePath + "\alias\") -Force | Out-Null
    Get-Content ($userKrePath + "\alias\" + $name + ".txt")
}

function Kvm-Upgrade {
    $version = Kvm-Find-Latest
    Kvm-Install $version
    Kvm-Alias-Set "default" $version
}


function Kvm-Alias-Set {
param(
  [string] $name,
  [string] $value
)
    Write-Host "Setting alias '$name' to '$value'"
    md ($userKrePath + "\alias\") -Force | Out-Null
    $value | Out-File ($userKrePath + "\alias\" + $name + ".txt") ascii
}

  try {
    switch -wildcard ($command + " " + $args.Count) {
      "setup 0"           {Kvm-Setup}
      "upgrade 0"         {Kvm-Upgrade}
      "install 0"         {Kvm-Install-Latest}
      "install 1"         {Kvm-Install $args[0]}
      "use 1"             {Kvm-Use $args[0]}
      "alias 0"           {Kvm-Alias-List}
      "alias 1"           {Kvm-Alias-Get $args[0]}
      "alias 2"           {Kvm-Alias-Set $args[0] $args[1]}
      "help"              {Kvm-Help}
      default             {Write-Host 'Unknown command'; Kvm-Help;}
    }
  }
  catch {
    Write-Host $_ -BackgroundColor "Red" -ForegroundColor White ;
  }
