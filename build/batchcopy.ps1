param([string] $src, [string] $dst)

$src = $src -replace "/", "\"
$dst = $dst -replace "/", "\"

Write-Host "run: xcopy /F /Y /I $src $dst"
xcopy /F /Y /I $src $dst