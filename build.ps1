# Restores nuget packages based on project.json file

if(!(Test-Path .nuget\nuget.exe))
{
    Write-Host "Downloading NuGet.exe"
    curl http://nuget.org/nuget.exe -outfile .nuget\nuget.exe
}

# Normal nuget restore
Write-Host "Downloading NuGet.exe"

& .nuget\nuget.exe restore

# List of reference names to exclude from nuget restore
$exclude = @{"Loader" = "";}

if(Test-Path lib)
{
    ls lib | %{ $exclude[$_.BaseName] = "" }
}

# Restore nuget from project.json

Write-Host "Restoring nuget packages from project.json files"

ls -r project.json | %{ $json = cat $_.FullName -raw | ConvertFrom-Json; 
    $exclude[$json.name] = ""
    for($i = 0; $i -le $json.dependencies.length; $i++) { 
        $o = $json.dependencies[$i]
        if($o) {
            $o | gm | Where-Object MemberType -eq NoteProperty | select Name
        }
    } 
} | %{ $_.Name } | unique | Where-Object { !$exclude.Contains($_) } | %{ 
    .nuget\nuget.exe install $_ -o packages -prerelease -configFile .nuget\nuget.config 
}

# Build the solution
msbuild ProjectSystem.sln