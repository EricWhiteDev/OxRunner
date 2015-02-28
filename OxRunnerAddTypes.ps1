$assemblies = (
  "windowsbase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
  "DocumentFormat.OpenXml, Version=2.5.5631.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
  "System.Xml.Linq, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
  "System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
  "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
  "System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
  "System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
)

$sources = @(
    "$PSScriptRoot\Runner\Repo.cs",
    "$PSScriptRoot\Runner\FileUtils.cs"
)

Add-Type -ReferencedAssemblies $assemblies -Path $sources
