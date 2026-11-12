param([string]$Executable = (Join-Path $PSScriptRoot '..\WinUI\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\Xplorer.WinUI.exe'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
$exe = $Executable
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build WinUI for x64 first.' }
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('Xplorer-ui-check-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testDirectory) | Out-Null
$proc = $null
try {
    [IO.File]::WriteAllText((Join-Path $testDirectory 'ui-test-file.txt'), 'test')
    $proc = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do { Start-Sleep -Milliseconds 250; $proc.Refresh() } until ($proc.HasExited -or $proc.MainWindowHandle -ne [IntPtr]::Zero -or [DateTime]::UtcNow -ge $deadline)
    if ($proc.HasExited -or $proc.MainWindowHandle -eq [IntPtr]::Zero) { throw 'WinUI window did not appear.' }
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    function Find-Element([string]$property, [string]$value) {
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::$property, $value)
        return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do { Start-Sleep -Milliseconds 250; $address = Find-Element 'AutomationIdProperty' 'Address' } until ($null -ne $address -or [DateTime]::UtcNow -ge $deadline)
    if ($null -eq $address) { throw 'Address control not found.' }
    $value = $address.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $value.SetValue($testDirectory)
    $open = Find-Element 'NameProperty' 'Open'
    if ($null -eq $open) { throw 'Open button not found.' }
    $open.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do { Start-Sleep -Milliseconds 250; $file = Find-Element 'NameProperty' 'ui-test-file.txt' } until ($null -ne $file -or [DateTime]::UtcNow -ge $deadline)
    if ($null -eq $file) { throw 'Test file did not appear in the folder listing.' }
    Write-Output 'PASS WinUI startup and navigation to a temporary folder'
    $nameSort = Find-Element 'AutomationIdProperty' 'SortName'
    $sizeSort = Find-Element 'AutomationIdProperty' 'SortSize'
    if ($null -eq $nameSort -or $null -eq $sizeSort -or $nameSort.Current.Name -ne 'Name, sorted ascending') { throw 'Initial sort indicator missing.' }
    $sizeSort.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    if ($sizeSort.Current.Name -ne 'Size, sorted ascending' -or $nameSort.Current.Name -ne 'Sort by Name') { throw 'Ascending sort indicator failed.' }
    $sizeSort.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    if ($sizeSort.Current.Name -ne 'Size, sorted descending') { throw 'Descending sort indicator failed.' }
    $nameSort.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    if ($nameSort.Current.Name -ne 'Name, sorted ascending') { throw 'Sort indicator did not return to Name.' }
    Write-Output 'PASS WinUI sort indicators'
    foreach ($label in @('New folder', 'New file', 'Copy', 'Cut', 'Paste', 'Rename', 'Delete')) {
        $button = Find-Element 'NameProperty' $label
        if ($null -eq $button -or $button.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) { throw "File action button not found: $label" }
    }
    Write-Output 'PASS WinUI file action buttons exposed'

    $query = Find-Element 'AutomationIdProperty' 'SearchQuery'
    $searchButton = Find-Element 'NameProperty' 'Search'
    if ($null -eq $query -or $null -eq $searchButton) { throw 'Search controls not found.' }
    $query.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('ui-test-file')
    $searchButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 250
        $status = Find-Element 'AutomationIdProperty' 'Status'
        $statusText = if ($null -ne $status) { $status.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } else { '' }
    } until ($statusText -like 'Local search in *' -or [DateTime]::UtcNow -ge $deadline)
    if ($statusText -notlike 'Local search in *1 result(s).*') { throw "Search did not finish with the expected result: $statusText" }
    if ($null -eq (Find-Element 'NameProperty' 'ui-test-file.txt')) { throw 'Search result missing.' }
    Write-Output 'PASS WinUI local search'

    $clear = Find-Element 'NameProperty' 'Clear'
    if ($null -eq $clear) { throw 'Clear search button not found.' }
    $clear.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 250
        $status = Find-Element 'AutomationIdProperty' 'Status'
        $statusText = if ($null -ne $status) { $status.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } else { '' }
    } until ($statusText -eq '1 items' -or [DateTime]::UtcNow -ge $deadline)
    if ($statusText -ne '1 items' -or $null -eq (Find-Element 'NameProperty' 'ui-test-file.txt')) { throw 'Returning from search to the folder failed.' }
    Write-Output 'PASS WinUI return from search'

    function Submit-NameDialog([string]$action, [string]$name) {
        $button = Find-Element 'NameProperty' $action
        $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        do { Start-Sleep -Milliseconds 250; $inputField = Find-Element 'AutomationIdProperty' 'FileNameInput' } until ($null -ne $inputField -or [DateTime]::UtcNow -ge $deadline)
        if ($null -eq $inputField) { throw "$action dialog input not found." }
        $inputField.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($name)
        $ok = Find-Element 'NameProperty' 'OK'
        if ($null -eq $ok) { throw "$action dialog confirmation not found." }
        $ok.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    }
    Submit-NameDialog 'New folder' 'created-folder'
    $createdFolder = Join-Path $testDirectory 'created-folder'
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do { Start-Sleep -Milliseconds 250 } until ((Test-Path -LiteralPath $createdFolder -PathType Container) -or [DateTime]::UtcNow -ge $deadline)
    if (-not (Test-Path -LiteralPath $createdFolder -PathType Container)) { throw 'New folder was not created.' }
    Write-Output 'PASS WinUI create folder'

    Submit-NameDialog 'New file' 'created-file.txt'
    $createdFile = Join-Path $testDirectory 'created-file.txt'
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do { Start-Sleep -Milliseconds 250; $createdItem = Find-Element 'NameProperty' 'created-file.txt' } until ((Test-Path -LiteralPath $createdFile -PathType Leaf) -and $null -ne $createdItem -or [DateTime]::UtcNow -ge $deadline)
    if ($null -eq $createdItem -or -not (Test-Path -LiteralPath $createdFile -PathType Leaf)) { throw 'New file was not created and displayed.' }
    Write-Output 'PASS WinUI create file'

    function Find-ListItem([string]$name) {
        $element = Find-Element 'NameProperty' $name
        while ($null -ne $element -and $element.Current.ControlType -ne [System.Windows.Automation.ControlType]::ListItem) { $element = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($element) }
        return $element
    }
    (Find-Element 'NameProperty' 'Select all').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $first = Find-ListItem 'ui-test-file.txt'
    $second = Find-ListItem 'created-file.txt'
    if ($null -eq $first -or $null -eq $second -or -not $first.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected -or -not $second.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected) { throw 'Select all did not select both files.' }
    if ($null -eq (Find-Element 'NameProperty' '3 selected')) { throw 'Selection count did not update.' }
    (Find-Element 'NameProperty' 'Clear selection').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    if ($first.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected -or $second.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected) { throw 'Clear selection did not clear both files.' }
    if ($null -eq (Find-Element 'NameProperty' '0 selected')) { throw 'Selection count did not reset.' }
    Write-Output 'PASS WinUI select all and clear selection'
    $selected = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 300
        try {
            $listItem = Find-Element 'NameProperty' 'created-file.txt'
            while ($null -ne $listItem -and $listItem.Current.ControlType -ne [System.Windows.Automation.ControlType]::ListItem) { $listItem = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($listItem) }
            if ($null -ne $listItem) { $listItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); $selected = $true }
        } catch [System.Windows.Automation.ElementNotAvailableException] { }
    } until ($selected -or [DateTime]::UtcNow -ge $deadline)
    if (-not $selected) { throw 'Created file list item could not be selected.' }
    Submit-NameDialog 'Rename' 'renamed-file.txt'
    $renamedFile = Join-Path $testDirectory 'renamed-file.txt'
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do { Start-Sleep -Milliseconds 250 } until ((Test-Path -LiteralPath $renamedFile -PathType Leaf) -or [DateTime]::UtcNow -ge $deadline)
    if (-not (Test-Path -LiteralPath $renamedFile -PathType Leaf) -or (Test-Path -LiteralPath $createdFile)) { throw 'Rename failed.' }
    Write-Output 'PASS WinUI rename file'

    $addTab = Find-Element 'AutomationIdProperty' 'AddButton'
    if ($null -eq $addTab) { throw 'Add tab button not found.' }
    $tabCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)
    $before = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition).Count
    $addTab.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do { Start-Sleep -Milliseconds 250; $after = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition).Count } until ($after -gt $before -or [DateTime]::UtcNow -ge $deadline)
    if ($after -ne $before + 1) { throw "Expected one new tab; before=$before after=$after." }
    Write-Output 'PASS WinUI add tab'

    $close = Find-Element 'AutomationIdProperty' 'CloseButton'
    if ($null -eq $close) { throw 'Close tab button not found.' }
    $close.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do { Start-Sleep -Milliseconds 250; $remaining = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition).Count } until ($remaining -eq $before -or [DateTime]::UtcNow -ge $deadline)
    if ($remaining -ne $before) { throw 'Closing a tab failed.' }
    if ($null -ne (Find-Element 'AutomationIdProperty' 'CloseButton')) { throw 'The last tab still exposes a close button.' }
    Write-Output 'PASS WinUI close tab and protect last tab'
} finally {
    if ($null -ne $proc) { $proc.Refresh(); if (-not $proc.HasExited) { Stop-Process -Id $proc.Id } }
    if (Test-Path -LiteralPath $testDirectory) { Remove-Item -LiteralPath $testDirectory -Recurse -Force }
}
