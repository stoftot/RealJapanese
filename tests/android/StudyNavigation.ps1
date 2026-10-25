#requires -Version 7.0
param([Parameter(Mandatory)][string]$ContextPath,[hashtable]$TestParameters=@{})

# Managed suite: use the worker's session and allocated ports. Never create/delete
# a session here, or select/move vocabulary: this smoke test preserves study lists.
$ErrorActionPreference = 'Stop'
$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json
if ($context.SessionMode -ne 'Managed' -or !$context.SessionUrl) {
    throw 'StudyNavigation requires the shared runner Managed session.'
}
if ($context.Capabilities.'appium:appPackage' -ne 'com.realjapanese.mobile' -or
    $context.Capabilities.'appium:udid' -ne $context.Serial -or !$context.ReservationId) {
    throw 'Expected RealJapanese on the exact claimed serial and reservation.'
}

function Invoke-SessionPost([string]$Path, [hashtable]$Body) {
    Invoke-RestMethod "$($context.SessionUrl)/$Path" -Method Post `
        -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 6) -TimeoutSec 20
}

function Find-Element([string]$Using, [string]$Value) {
    try { $response = Invoke-SessionPost 'element' @{using=$Using; value=$Value} }
    catch { throw "Failed to find $Using '$Value': $($_.Exception.Message)" }
    $elementId = $response.value.'element-6066-11e4-a52e-4f735466cecf'
    if (!$elementId) { throw "Element was not found: $Value" }
    return $elementId
}

function Click-Element([string]$Using, [string]$Value) {
    $elementId = Find-Element $Using $Value
    Invoke-SessionPost "element/$elementId/click" @{} | Out-Null
}

function Wait-Source([string]$XPath, [string]$Name) {
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $source = [string](Invoke-RestMethod "$($context.SessionUrl)/source" -TimeoutSec 20).value
        $document = [xml]$source
        if ($document.SelectSingleNode($XPath)) {
            [IO.File]::WriteAllText((Join-Path $context.OutputDirectory "$Name.xml"), $source)
            return $document
        }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $deadline)
    [IO.File]::WriteAllText((Join-Path $context.OutputDirectory "$Name.xml"), $source)
    throw "Timed out waiting for $Name ($XPath)."
}

# Locators are from the actual API-36 accessibility tree. No Chromedriver or
# WEBVIEW switch is required; HTML controls are exposed in NATIVE_APP.
Invoke-SessionPost 'timeouts' @{implicit=5000} | Out-Null
$null = Wait-Source '//*[@content-desc="Home"]' 'startup'
Click-Element 'accessibility id' 'Home'
$null = Wait-Source '//*[@text="What would you like to practise?"]' 'home'
Click-Element 'accessibility id' 'Nouns'
$nouns = Wait-Source '//*[@text="Known nouns"]' 'nouns'
$counts = $nouns.SelectNodes('//*[@text]') | Where-Object {
    $_.GetAttribute('text') -match '^\d+ selected / [1-9]\d* total$'
}
if (!$counts) { throw 'The packaged noun catalog is empty or did not render its count.' }
$null = Wait-Source '//*[contains(@text,"college; university") and contains(@text,"大学") and contains(@text,"だいがく")]' 'catalog-item'

$searchId = Find-Element 'xpath' '//android.widget.EditText[starts-with(@hint,"Search Known nouns")]'
Invoke-SessionPost "element/$searchId/click" @{} | Out-Null
Invoke-SessionPost "element/$searchId/value" @{text='college; university'} | Out-Null
$null = Wait-Source '//*[starts-with(@text,"1 matches.")]' 'filtered-count'
# With the IME open the filtered row can be rendered but omitted from native
# accessibility. Preserve it for MOBILE-004 visual review; this is separate from
# the automated count and initial Japanese catalog-item assertions.
$image = (Invoke-RestMethod "$($context.SessionUrl)/screenshot" -TimeoutSec 20).value
[IO.File]::WriteAllBytes((Join-Path $context.OutputDirectory 'filtered.png'), [Convert]::FromBase64String($image))
Invoke-SessionPost "element/$searchId/clear" @{} | Out-Null
Invoke-SessionPost 'back' @{} | Out-Null # Dismiss the IME before using the bottom navigation.
Click-Element 'accessibility id' 'Home'
$null = Wait-Source '//*[@text="What would you like to practise?"]' 'returned-home'
[ordered]@{
    Case='MOBILE-004'; Passed=$true; Serial=$context.Serial
    Scope='Automated assertions; filtered screenshot review required'
    Assertions=@('Home rendered','Nonempty packaged noun catalog',
        'Initial college; university / 大学 / だいがく entry','Search reports one match','Returned Home')
    RequiresVisualReview=@('filtered.png: expected college; university / 大学 / だいがく row')
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $context.OutputDirectory 'study-navigation.json')
Write-Host "PASS automated MOBILE-004 assertions on $($context.Serial); review filtered.png to complete the case."
# The worker captures failure UI/logcat and closes its session. Queue release
# belongs to the caller after the runner exits, including if this suite throws.
