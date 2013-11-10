## Getting started

Run build.ps1

```
$env:ProjectSystemDir = PATH WHERE YOU CLONED THE REPOSITORY

function Web { 
    if($args.count -eq 0) {
        $args = @($pwd, "http://localhost:8081")
    }
    
    if($args.count -eq 1) {
        $args += "http://localhost:8081"
    }
    
    $watchDog = Join-Path $env:ProjectSystemDir "WatchDog"
    $webHost = Join-Path $env:ProjectSystemDir "WebHost"
    
    K $watchDog $webHost @args
}

function K {
    if($args.count -eq 0) {
        $args = @($pwd)
    }
    
    $bootstrapper = Join-Path $env:ProjectSystemDir "K\bin\Debug\K.exe"
    
    & $bootstrapper @args
}
```
