function Web { 
    param(
    [string]$Path = $pwd,
    [string]$Url = "http://localhost:8080"
    )
    
    $watchDog = Join-Path $env:K1Dir "WatchDog"
    $webHost = Join-Path $env:K1Dir "WebHost"
    
    K $watchDog $webHost $Path $Url
}

function K {
    if($args.count -eq 0) {
        $args = @($pwd)
    }
    
    $bootstrapper = Join-Path $env:K1Dir "K\bin\Debug\K.exe"
    
    & $bootstrapper @args
}
