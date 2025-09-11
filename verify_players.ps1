# Verify player API calls work correctly
Write-Host "Testing Player API Calls..." -ForegroundColor Green

# Test Arsenal (should return real players)
Write-Host "`nTesting Arsenal by team name..." -ForegroundColor Yellow
try {
    $arsenalResp = Invoke-RestMethod -Uri "https://www.thesportsdb.com/api/v1/json/3/searchplayers.php?t=Arsenal" -Method Get
    if ($arsenalResp.player -and $arsenalResp.player.Count -gt 0) {
        Write-Host "✅ Arsenal by name: Found $($arsenalResp.player.Count) players" -ForegroundColor Green
        Write-Host "   First player: $($arsenalResp.player[0].strPlayer) (Team: $($arsenalResp.player[0].strTeam))" -ForegroundColor Cyan
    } else {
        Write-Host "❌ Arsenal by name: No players found" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Arsenal by name: API Error - $($_.Exception.Message)" -ForegroundColor Red
}

# Test Arsenal by ID (should return real players)
Write-Host "`nTesting Arsenal by ID (133604)..." -ForegroundColor Yellow
try {
    $arsenalIdResp = Invoke-RestMethod -Uri "https://www.thesportsdb.com/api/v1/json/3/lookup_all_players.php?id=133604" -Method Get
    if ($arsenalIdResp.player -and $arsenalIdResp.player.Count -gt 0) {
        Write-Host "✅ Arsenal by ID: Found $($arsenalIdResp.player.Count) players" -ForegroundColor Green
        Write-Host "   First player: $($arsenalIdResp.player[0].strPlayer) (Team: $($arsenalIdResp.player[0].strTeam))" -ForegroundColor Cyan
        
        # Check if they are actually Arsenal players
        $allArsenal = $true
        foreach ($player in $arsenalIdResp.player) {
            if ($player.strTeam -notlike "*Arsenal*") {
                $allArsenal = $false
                break
            }
        }
        Write-Host "   All players are Arsenal players: $allArsenal" -ForegroundColor $(if($allArsenal) {"Green"} else {"Red"})
    } else {
        Write-Host "❌ Arsenal by ID: No players found" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Arsenal by ID: API Error - $($_.Exception.Message)" -ForegroundColor Red
}

# Test Lakers by ID (should return Arsenal players due to API limitation)
Write-Host "`nTesting Lakers by ID (134859)..." -ForegroundColor Yellow
try {
    $lakersResp = Invoke-RestMethod -Uri "https://www.thesportsdb.com/api/v1/json/3/lookup_all_players.php?id=134859" -Method Get
    if ($lakersResp.player -and $lakersResp.player.Count -gt 0) {
        Write-Host "✅ Lakers by ID: Found $($lakersResp.player.Count) players" -ForegroundColor Green
        Write-Host "   First player: $($lakersResp.player[0].strPlayer) (Team: $($lakersResp.player[0].strTeam))" -ForegroundColor Cyan
        
        # Check if they returned Arsenal players (indicating API limitation)
        $gotArsenal = $lakersResp.player[0].strTeam -like "*Arsenal*"
        Write-Host "   Got Arsenal players for Lakers (API limitation): $gotArsenal" -ForegroundColor $(if($gotArsenal) {"Yellow"} else {"Green"})
    } else {
        Write-Host "❌ Lakers by ID: No players found" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Lakers by ID: API Error - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTest Complete!" -ForegroundColor Green
