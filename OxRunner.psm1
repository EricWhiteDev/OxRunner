$ver = $PSVersionTable.PSVersion
if ($ver.Major -lt 3) { throw "You must be running PowerShell 3.0 or later" }
if (Get-Module OxRunner) { return }

. "$PSScriptRoot\OxRunnerAddTypes.ps1"

## Applies to any file
. "$PSScriptRoot\OxRunnerCmdlets\Add-OxRepo.ps1"

Export-ModuleMember `
    -Function @(
        'Add-OxRepo'
    )
