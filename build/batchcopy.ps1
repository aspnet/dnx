param([string] $src, [string] $dst)

$src = $src -replace "/", "\"
$dst = $dst -replace "/", "\"

Write-Host "run: xcopy /F /Y /I $src $dst"

if ((Test-Path $src) -and (ls $src))
{
    xcopy /F /Y /I $src $dst
}

exit $LASTEXITCODE
