param(
    [Parameter(Position=0, Mandatory=$true)]
    [string]$Event,

    [Parameter(Position=1)]
    [string]$Title = $null,

    [Parameter(Position=2)]
    [string]$Message = $null,

    [Parameter(Position=3)]
    [string]$ActionLabel = $null,

    [Parameter(Position=4)]
    [int]$TimeoutSeconds = 5
)

$payload = [PSCustomObject]@{
    event = $Event
}

if ($Title) { $payload | Add-Member -MemberType NoteProperty -Name "title" -Value $Title }
if ($Message) { $payload | Add-Member -MemberType NoteProperty -Name "message" -Value $Message }
if ($ActionLabel) { $payload | Add-Member -MemberType NoteProperty -Name "actionLabel" -Value $ActionLabel }
if ($TimeoutSeconds -ge 0) { $payload | Add-Member -MemberType NoteProperty -Name "timeoutSeconds" -Value $TimeoutSeconds }

$json = $payload | ConvertTo-Json -Compress

try {
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "DesktopPetIpc", [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(3000)
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $writer = New-Object System.IO.StreamWriter($pipe, $utf8NoBom)
    $writer.AutoFlush = $true
    $reader = New-Object System.IO.StreamReader($pipe, $utf8NoBom)

    $writer.WriteLine($json)
    $response = $reader.ReadLine()

    Write-Host "[DesktopPet IPC] Terkirim: $json" -ForegroundColor Cyan
    Write-Host "[DesktopPet IPC] Balasan: $response" -ForegroundColor Green

    $pipe.Dispose()
} catch {
    Write-Host "[DesktopPet IPC Error] Gagal terhubung ke pipe DesktopPetIpc: $_" -ForegroundColor Red
    exit 1
}
