function Add-OxRepo {

    <#
    .SYNOPSIS
    Adds a document and moniker to the given repo.
    .DESCRIPTION
    Adds a document and moniker to the given repo.
    .EXAMPLE
    # Simple use
    Add-OxRepo -Repo \\bigi5-8\c\TestFileRepo -FilePath .\Doc1.docx -Moniker "ConformanceTest\CommentEx\GeneratedDocument.docx"
    .EXAMPLE
    #>     
  
    [CmdletBinding(SupportsShouldProcess=$True,ConfirmImpact='Medium')]
    param
    (
        [Parameter(Mandatory=$True, Position=0)]
        [string]$Repo,

        [Parameter(Mandatory=$True, Position=1)]
        [string]$FilePath,

        [Parameter(Mandatory=$True, Position=2)]
        [string]$Moniker
    )

    write-verbose "Adding a document to the repo"     
  
    [OxRun.Repo]$repo = New-Object OxRun.Repo($Repo, $false)
    $repo.Store($FilePath, $Moniker)
}
