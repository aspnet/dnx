function Web { 
    param(
    [string]$Path = $pwd,
    [string]$Url = "http://localhost:8080"
    )
    
    $watchDog = Join-Path $env:ProjectSystemDir "WatchDog"
    $webHost = Join-Path $env:ProjectSystemDir "WebHost"
    
    K $watchDog $webHost $Path $Url
}

function K {
    if($args.count -eq 0) {
        $args = @($pwd)
    }
    
    $bootstrapper = Join-Path $env:ProjectSystemDir "K\bin\Debug\K.exe"
    
    & $bootstrapper @args
}
