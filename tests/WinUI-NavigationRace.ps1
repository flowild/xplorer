param([string]$Executable = (Join-Path $PSScriptRoot '..\WinUI\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\Xplorer.WinUI.exe'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Windows.Forms
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw 'Build WinUI for x64 first.' }
$root = Join-Path ([IO.Path]::GetTempPath()) ('Xplorer-race-check-' + [guid]::NewGuid().ToString('N'))
$proc = $null
try {
    $slow = [IO.Directory]::CreateDirectory((Join-Path $root 'many-items')).FullName
    $target = [IO.Directory]::CreateDirectory((Join-Path $root 'target')).FullName
    for ($i = 0; $i -lt 3000; $i++) { [IO.File]::WriteAllText((Join-Path $slow ('old-{0:D5}.txt' -f $i)), '') }
    [IO.File]::WriteAllText((Join-Path $target 'target-marker.txt'), '')
    $proc = Start-Process -FilePath $Executable -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do { Start-Sleep -Milliseconds 200; $proc.Refresh() } until ($proc.HasExited -or $proc.MainWindowHandle -ne [IntPtr]::Zero -or [DateTime]::UtcNow -ge $deadline)
    if ($proc.HasExited -or $proc.MainWindowHandle -eq [IntPtr]::Zero) { throw 'WinUI window did not appear.' }
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    function Find-Element([string]$property, [string]$value) {
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::$property, $value)
        $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    }
    function Wait-Element([string]$property, [string]$value) {
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        do { Start-Sleep -Milliseconds 100; $element = Find-Element $property $value } until ($null -ne $element -or [DateTime]::UtcNow -ge $deadline)
        if ($null -eq $element) { throw "Control not found: $value" }
        return $element
    }
    function Open-Folder([string]$path) {
        (Wait-Element 'NameProperty' 'Edit address').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        $address = Wait-Element 'AutomationIdProperty' 'Address'
        $address.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($path)
        $address.SetFocus()
        [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    }
    function Assert-Target {
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 150
            $address = Find-Element 'NameProperty' 'target'
            $marker = Find-Element 'NameProperty' 'target-marker.txt'
        } until (($null -ne $address -and $null -ne $marker) -or [DateTime]::UtcNow -ge $deadline)
        if ($null -eq $address -or $null -eq $marker) { throw "Target folder not displayed: $address" }
        Start-Sleep -Seconds 2
        $address = Find-Element 'NameProperty' 'target'
        if ($null -eq $address -or $null -eq (Find-Element 'NameProperty' 'target-marker.txt') -or $null -ne (Find-Element 'NameProperty' 'old-00000.txt')) { throw 'A previous request replaced the target folder.' }
    }
    Open-Folder $slow
    Open-Folder $target
    Assert-Target
    Write-Output 'PASS rapid folder navigation retains the latest folder'

    Open-Folder $slow
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do { Start-Sleep -Milliseconds 150; $status = (Wait-Element 'AutomationIdProperty' 'Status').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } until ($status -eq '3000 items' -or [DateTime]::UtcNow -ge $deadline)
    if ($status -ne '3000 items') { throw "Large folder did not finish loading: $status" }
    (Wait-Element 'AutomationIdProperty' 'SearchQuery').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('old-')
    (Wait-Element 'NameProperty' 'Search').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Open-Folder $target
    Assert-Target
    $status = (Wait-Element 'AutomationIdProperty' 'Status').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    if ($status -ne '1 items') { throw "Unexpected status after navigation: $status" }
    Write-Output 'PASS search followed by navigation retains the latest folder'

    $tabCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)
    (Wait-Element 'AutomationIdProperty' 'AddButton').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do { Start-Sleep -Milliseconds 150; $tabs = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition) } until ($tabs.Count -eq 2 -or [DateTime]::UtcNow -ge $deadline)
    if ($tabs.Count -ne 2) { throw 'Second tab not found.' }
    Open-Folder $slow
    $tabs.Item(0).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Assert-Target
    Write-Output 'PASS tab switch retains its folder after another tab navigates'
} finally {
    if ($null -ne $proc) { $proc.Refresh(); if (-not $proc.HasExited) { Stop-Process -Id $proc.Id } }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
