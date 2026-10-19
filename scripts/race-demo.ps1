# Two guides take the same projector at the same moment - exactly one succeeds.
# Run while the API is running (Visual Studio, "http" profile), from the BudjetFlow folder:
#     powershell -ExecutionPolicy Bypass -File scripts\race-demo.ps1
# Uses the demo accounts from DbSeeder. Each round returns the projector afterwards.

param(
    [string]$BaseUrl = "http://localhost:5178",
    [int]$EquipmentId = 1,
    [int]$Rounds = 5
)

Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient

function Get-Token([string]$email, [string]$password) {
    $body = @{ email = $email; password = $password } | ConvertTo-Json
    (Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $body).token
}

function New-Request([string]$method, [string]$token) {
    $request = New-Object System.Net.Http.HttpRequestMessage $method, "$BaseUrl/api/equipment/$EquipmentId/holder"
    $request.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue "Bearer", $token
    $request
}

$rachel = Get-Token "rachel@budgetflow.local" "Guide!2026"
$shira = Get-Token "shira@budgetflow.local" "Guide!2026"
$sarah = Get-Token "sarah@budgetflow.local" "Coord!2026"

for ($round = 1; $round -le $Rounds; $round++) {
    # Both requests are sent before either answer arrives
    $rachelTask = $client.SendAsync((New-Request "PUT" $rachel))
    $shiraTask = $client.SendAsync((New-Request "PUT" $shira))
    [System.Threading.Tasks.Task]::WaitAll($rachelTask, $shiraTask)

    $rachelCode = [int]$rachelTask.Result.StatusCode
    $shiraCode = [int]$shiraTask.Result.StatusCode
    $loser = if ($rachelCode -eq 409) { $rachelTask } else { $shiraTask }
    $message = ($loser.Result.Content.ReadAsStringAsync().Result | ConvertFrom-Json).detail

    Write-Host "Round ${round}: Rachel=$rachelCode  Shira=$shiraCode  ->  the other one got: $message"

    # The coordinator returns the projector for the next round
    $client.SendAsync((New-Request "DELETE" $sarah)).Result | Out-Null
}
