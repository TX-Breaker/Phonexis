# Script to rename all occurrences of "YouTubeLinkChooserCSharp" and "YouTubeLinkChooser" to "Phonexis - TXT to YT Multisearch" in all .cs and .xaml files

$files = Get-ChildItem -Path . -Recurse -Include *.cs,*.xaml,*.csproj,*.sln,*.resx | Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

foreach ($file in $files) {
    Write-Host "Processing $($file.FullName)"
    $content = Get-Content -Path $file.FullName -Raw
    $newContent = $content -replace "YouTubeLinkChooserCSharp", "Phonexis - TXT to YT Multisearch"
    $newContent = $newContent -replace "YouTubeLinkChooser", "Phonexis - TXT to YT Multisearch"
    
    if ($content -ne $newContent) {
        Write-Host "  Replacing content in $($file.FullName)"
        Set-Content -Path $file.FullName -Value $newContent
    }
}

Write-Host "Namespace replacement complete!"