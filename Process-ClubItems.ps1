<#
.SYNOPSIS
    Reads club item submission emails, appends records to Excel, and generates DOCX batches of 10 cards.

.DESCRIPTION
    - Runs safely as an hourly scheduled task.
    - Reads unread Outlook emails whose subject matches a configurable regex.
    - Extracts a base64-encoded JSON payload from the email body.
    - Normalizes the submission into an Excel-backed queue/state machine.
    - When 10 queued records exist, creates a copy of the template DOCX and fills the 10 cards.
    - Cleans Excel records older than 6 months and generated DOCX files older than 4 weeks.

.NOTES
    You will almost certainly want to edit the configuration block at the top:
      * $Config.SubjectRegex
      * $Config.BodyBase64Regex
      * $Config.JsonFieldCandidates
      * folder paths

    This script uses COM automation for Outlook, Excel, and Word.
    It therefore expects Microsoft Office desktop apps to be installed on the machine that runs it.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# =========================
# Configuration
# =========================
$Config = [ordered]@{
    # EMAIL FILTERING
    MailboxName      = ''                        # Optional: mailbox display name. Leave blank for default Outlook store.
    FolderPath       = @('Inbox')               # Example: @('Inbox') or @('LARP','Items') for nested folders.

    # Make this the obvious/editable part for subject format changes.
    # Example expected groups:
    #   madeBy  = crafter / maker name from subject
    #   source  = optional source prefix such as MP
     SubjectRegex      = '^monster point item for\s+(?<madeBy>.+)$'
    # SubjectRegex     = '^(?<madeBy>.+?)\s*-\s*Item Submission(?:\s*\[(?<source>[A-Za-z]+)\])?$'

    # Make this the obvious/editable part for the encoded payload in the body.
    # This looks for a reasonably long base64 token.
    BodyBase64Regex  = '(?ms)Compressed token:\s*(?<payload>[A-Za-z0-9+/=\r\n]+)'

    # PATHS
    TemplateDocxPath = "C:\Users\brbar\Downloads\NewCardFront.docx"
    OutputDocxFolder = "C:\Users\brbar\Downloads\outputItems"
    ExcelPath        = "C:\Users\brbar\Downloads\ItemLogs.xlsx"
    LogPath          = "C:\Users\brbar\Downloads\logs.log"

    
    # PROCESSING
    # MaxBatchesPerRun = 1
    PendingBatchSize = 10
    DeleteExcelOlderThanMonths = 6
    DeleteDocxOlderThanDays    = 28

    # Allowed item types exactly as card text should show.
    AllowedItemTypes = @("'Mantic",'Magic','Spirit','Physical','Neuronic','Earthpower')
    ItemTypeAliases = @{
        earthpower = 'Earthpower'
        physical   = 'Physical'
        neuronic   = 'Neuronic'
        neuro      = 'Neuronic'
        spiritual  = 'Spirit'
        spirit     = 'Spirit'
        magical    = 'Magic'
        magic      = 'Magic'
        mantic     = "'Mantic"
        antimantic = "'Mantic"
        other      = 'Physical'
        none       = 'Physical'
    }

    # Which statuses exist in the Excel state machine.
    StatusQueued     = 'Queued'
    StatusRendered   = 'Rendered'
    StatusError      = 'Error'

    # JSON field mapping candidates.
    # Edit these arrays to match the schema emitted by your service.
    JsonFieldCandidates = [ordered]@{
        ItemType        = @('itemType','itemTypes','type','types','item.types','item.type')
        SourceNumber    = @('sourceNumber','number','itemNumber','source.number')
        SourcePrefix    = @('sourcePrefix','source','numberSource')
        Isp             = @('isp','totalIsp','ispTotal','value.isp')
        CharacterName   = @('characterName','character','recipient.characterName')
        ClassName       = @('className','class','recipient.className','recipient.characterClass')
        PlayerName      = @('playerName','realName','player.name','recipient.playerName')
        ItemName        = @('itemName','name','displayName','item.name','item.displayName')
        Abilities       = @('abilities','item.abilities','effects','powers','ispBreakdown','breakdown')
        SubmittedAt     = @('submittedAt','createdAt','createdDate','submittedDate')
    }
}

# =========================
# Helpers: logging and dates
# =========================
function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "[$timestamp] $Message"
    Write-Host $line
    $logDir = Split-Path -Path $Config.LogPath -Parent
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
    Add-Content -Path $Config.LogPath -Value $line
}

function Get-CutoffDate {
    param([int]$Months)
    return (Get-Date).AddMonths(-$Months)
}

# =========================
# Helpers: JSON extraction
# =========================
function Convert-FromBase64Json {
    param([string]$Base64Text)

    $bytes = [Convert]::FromBase64String($Base64Text.Trim())

    # GZip magic header = 1F 8B. Your "Compressed token" uses this format.
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0x1F -and $bytes[1] -eq 0x8B) {
        $inputStream = New-Object IO.MemoryStream(,$bytes)
        $gzipStream = New-Object IO.Compression.GzipStream($inputStream, [IO.Compression.CompressionMode]::Decompress)
        $reader = New-Object IO.StreamReader($gzipStream, [Text.Encoding]::UTF8)
        try {
            $json = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
            $gzipStream.Dispose()
            $inputStream.Dispose()
        }
    }
    else {
        $json = [Text.Encoding]::UTF8.GetString($bytes)
    }

    return $json | ConvertFrom-Json
}


function Get-NestedValue {
    param(
        [Parameter(Mandatory)]$Object,
        [Parameter(Mandatory)][string]$Path
    )

    $current = $Object
    foreach ($part in ($Path -split '\.')) {
        if ($null -eq $current) { return $null }

        if ($current -is [System.Collections.IDictionary]) {
            if ($current.Contains($part)) {
                $current = $current[$part]
            }
            else {
                return $null
            }
        }
        else {
            $prop = $current.PSObject.Properties[$part]
            if ($null -eq $prop) { return $null }
            $current = $prop.Value
        }
    }

    return $current
}

function Resolve-JsonField {
    param(
        [Parameter(Mandatory)]$JsonObject,
        [Parameter(Mandatory)][string[]]$Candidates
    )

    foreach ($candidate in $Candidates) {
        $value = Get-NestedValue -Object $JsonObject -Path $candidate
        if ($null -ne $value -and "$value" -ne '') {
            return $value
        }
    }

    return $null
}

function ConvertTo-ValueArray {
    param($Value)

    if ($null -eq $Value) { return @() }
    if ($Value -is [string]) { return @($Value) }

    if ($Value -is [System.Collections.IEnumerable]) {
        $items = New-Object System.Collections.Generic.List[object]
        foreach ($item in $Value) { $items.Add($item) }
        return $items.ToArray()
    }

    return @($Value)
}

function Normalize-ItemTypeToken {
    param($Value)

    if ($null -eq $Value) { return '' }

    $raw = [string]$Value
    if ([string]::IsNullOrWhiteSpace($raw)) { return '' }

    $key = (($raw.Trim().Trim("'") -replace '[^A-Za-z]', '')).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($key)) { return '' }

    if ($Config.ItemTypeAliases.ContainsKey($key)) {
        return $Config.ItemTypeAliases[$key]
    }

    foreach ($allowed in $Config.AllowedItemTypes) {
        $allowedKey = (($allowed -replace '[^A-Za-z]', '')).ToLowerInvariant()
        if ($allowedKey -eq $key) { return $allowed }
    }

    throw "Unsupported item type '$Value'. Allowed values: $($Config.AllowedItemTypes -join ', ')"
}

function Add-UniqueItemType {
    param(
        [System.Collections.Generic.List[string]]$Types,
        [string]$Type
    )

    if ($null -eq $Types) { return }
    if ([string]::IsNullOrWhiteSpace($Type)) { return }

    foreach ($existing in $Types) {
        if ($existing -ieq $Type) { return }
    }

    $Types.Add($Type)
}

function Resolve-ItemTypesFromAbilities {
    param($Abilities)

    $types = New-Object System.Collections.Generic.List[string]

    foreach ($ability in (ConvertTo-ValueArray -Value $Abilities)) {
        $tokens = New-Object System.Collections.Generic.List[string]

        if ($ability -is [string]) {
            $tokens.Add($ability)
        }
        else {
            foreach ($propName in @('id','type','abilityType','name','abilityName','title','text','summary')) {
                $prop = $ability.PSObject.Properties[$propName]
                if ($null -ne $prop -and -not [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
                    $tokens.Add([string]$prop.Value)
                }
            }
        }

        $joined = ($tokens.ToArray() -join ' ').ToLowerInvariant()
        if ($joined -match '(^|[^a-z])spell([^a-z]|$)') {
            Add-UniqueItemType -Types $types -Type 'Magic'
        }
        if ($joined -match '(^|[^a-z])miracle([^a-z]|$)') {
            Add-UniqueItemType -Types $types -Type 'Spirit'
        }
        if ($joined -match '(^|[^a-z])evocation([^a-z]|$)') {
            Add-UniqueItemType -Types $types -Type 'Earthpower'
        }
    }

    if ($types.Count -eq 0) {
        $types.Add('Physical')
    }

    return $types.ToArray()
}

function Normalize-ItemTypes {
    param(
        $Value,
        $FallbackAbilities
    )

    $types = New-Object System.Collections.Generic.List[string]
    foreach ($rawType in (ConvertTo-ValueArray -Value $Value)) {
        $normalized = Normalize-ItemTypeToken -Value $rawType
        Add-UniqueItemType -Types $types -Type $normalized
    }

    if ($types.Count -eq 0) {
        foreach ($derivedType in (Resolve-ItemTypesFromAbilities -Abilities $FallbackAbilities)) {
            Add-UniqueItemType -Types $types -Type $derivedType
        }
    }

    if ($types.Count -eq 0) { return 'Physical' }

    # The Word template field is a single dropdown. If the JSON contains multiple
    # types, use the first valid type in the payload.
    return $types[0]
}

function Normalize-Isp {
    param($Value)
    if ($null -eq $Value -or "$Value" -eq '') { throw 'ISP missing from payload.' }
    $n = [int][math]::Round([double]$Value)
    if ($n -lt 0) { $n = 0 }
    if ($n -gt 9999) { $n = 9999 }
    return $n
}

function Get-AbilitiesText {
    param($Abilities)

    if ($null -eq $Abilities) { return '' }

    if ($Abilities -is [string]) {
        return $Abilities.Trim()
    }

    $names = New-Object System.Collections.Generic.List[string]
    foreach ($ability in $Abilities) {
        if ($ability -is [string]) {
            if (-not [string]::IsNullOrWhiteSpace($ability)) { $names.Add($ability.Trim()) }
            continue
        }

        $candidateName = $null
        foreach ($propName in @('name','abilityName','title','text','summary')) {
            $prop = $ability.PSObject.Properties[$propName]
            if ($null -ne $prop -and -not [string]::IsNullOrWhiteSpace([string]$prop.Value)) {
                $candidateName = [string]$prop.Value
                break
            }
        }

        if (-not [string]::IsNullOrWhiteSpace($candidateName)) {
            $names.Add($candidateName.Trim())
        }
    }

    return ($names.ToArray() -join ', ')
}

function Build-DescriptionText {
    param(
        [string]$ItemName,
        [string]$AbilitiesText
    )

    $safeName = [string]$ItemName
    if ($null -eq $safeName) { $safeName = '' }
    $safeName = $safeName.Trim()

    $safeAbilities = [string]$AbilitiesText
    if ($null -eq $safeAbilities) { $safeAbilities = '' }
    $safeAbilities = $safeAbilities.Trim()

    if ($safeName -and $safeAbilities) { return "$safeName`: $safeAbilities" }
    if ($safeName) { return "$safeName`:" }
    return $safeAbilities
}

function Build-DisplayNumber {
    param(
        [string]$SourcePrefix,
        $SourceNumber
    )

    if ($null -eq $SourceNumber -or "$SourceNumber" -eq '') { return '' }
    $prefix = [string]$SourcePrefix
    if ($null -eq $prefix) { $prefix = '' }
    $prefix = $prefix.Trim()
    if ($prefix) {
        if ($prefix -ieq 'MP') { return "MP$SourceNumber" }
        return "$prefix$SourceNumber"
    }
    return [string]$SourceNumber
}

function Convert-SubmissionToRecord {
    param(
        [Parameter(Mandatory)]$JsonObject,
        [Parameter(Mandatory)]$MailItem,
        [Parameter(Mandatory)][string]$MadeBy,
        [Parameter(Mandatory)][int]$Id
    )

    $submittedAt = Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.SubmittedAt
    if ($submittedAt) {
        $submittedDate = [datetime]$submittedAt
    }
    else {
        $submittedDate = [datetime]$MailItem.ReceivedTime
    }

    $rawAbilities  = Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.Abilities
    $itemType      = Normalize-ItemTypes -Value (Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.ItemType) -FallbackAbilities $rawAbilities
    $sourceNumber  = Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.SourceNumber
    $sourcePrefix  = Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.SourcePrefix
    $isp           = Normalize-Isp (Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.Isp)
    $characterName = [string](Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.CharacterName)
    $className     = [string](Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.ClassName)
    $playerName    = [string](Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.PlayerName)
    $itemName      = [string](Resolve-JsonField -JsonObject $JsonObject -Candidates $Config.JsonFieldCandidates.ItemName)
    $abilitiesText = Get-AbilitiesText $rawAbilities

    [pscustomobject]@{
        Id                = $Id
        Status            = $Config.StatusQueued
        AddedToDocxFile   = ''
        AddedToDocxAt     = ''
        EmailSubject      = [string]$MailItem.Subject
        EmailReceivedAt   = [datetime]$MailItem.ReceivedTime
        EmailEntryId      = [string]$MailItem.EntryID
        MadeBy            = $MadeBy
        SubmittedAt       = $submittedDate
        BlowUpDate        = $submittedDate.AddYears(2)
        ItemType          = $itemType
        SourcePrefix      = [string]$sourcePrefix
        SourceNumber      = [string]$sourceNumber
        DisplayNumber     = Build-DisplayNumber -SourcePrefix ([string]$sourcePrefix) -SourceNumber $sourceNumber
        ISP               = $isp
        CharacterName     = $characterName.Trim()
        ClassName         = $className.Trim()
        RealName          = $playerName.Trim()
        ItemName          = $itemName.Trim()
        Abilities         = $abilitiesText
        ItemText          = (Build-DescriptionText -ItemName $itemName -AbilitiesText $abilitiesText)
        RawJson           = ($JsonObject | ConvertTo-Json -Depth 20 -Compress)
    }
}

# =========================
# Helpers: Excel
# =========================
function Ensure-ParentDirectory {
    param([string]$Path)
    $parent = Split-Path -Path $Path -Parent
    if ($parent -and -not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
}

function Get-ExcelHeaders {
    return @(
        'Id','Status','AddedToDocxFile','AddedToDocxAt','EmailSubject','EmailReceivedAt','EmailEntryId','MadeBy',
        'SubmittedAt','BlowUpDate','ItemType','SourcePrefix','SourceNumber','DisplayNumber','ISP',
        'CharacterName','ClassName','RealName','ItemName','Abilities','ItemText','RawJson'
    )
}

function Open-ExcelWorkbook {
    param([string]$Path)

    Ensure-ParentDirectory -Path $Path

    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false

    if (Test-Path $Path) {
        $workbook = $excel.Workbooks.Open($Path)
    }
    else {
        $workbook = $excel.Workbooks.Add()
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = 'Items'
        $headers = Get-ExcelHeaders
        for ($i = 0; $i -lt $headers.Count; $i++) {
            $sheet.Cells.Item(1, $i + 1).Value2 = $headers[$i]
        }
        $sheet.Rows.Item(1).Font.Bold = $true
        $sheet.Columns.AutoFit() | Out-Null
        $workbook.SaveAs($Path)
    }

    return [pscustomobject]@{
        Excel    = $excel
        Workbook = $workbook
        Sheet    = $workbook.Worksheets.Item('Items')
    }
}

function Close-ExcelWorkbook {
    param($ExcelState)
    if ($null -eq $ExcelState) { return }

    try { $ExcelState.Workbook.Save() } catch {}
    try { $ExcelState.Workbook.Close($true) } catch {}
    try { $ExcelState.Excel.Quit() } catch {}

    foreach ($obj in @($ExcelState.Sheet, $ExcelState.Workbook, $ExcelState.Excel)) {
        if ($null -ne $obj) {
            try { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($obj) } catch {}
        }
    }

    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

function Get-LastUsedRow {
    param($Sheet)
    $used = $Sheet.UsedRange
    $rows = [int]$used.Rows.Count
    if ($rows -lt 1) { return 1 }
    return $rows
}

function Get-ColumnMap {
    param($Sheet)
    $map = @{}
    $usedCols = [int]$Sheet.UsedRange.Columns.Count
    for ($col = 1; $col -le $usedCols; $col++) {
        $name = [string]$Sheet.Cells.Item(1, $col).Value2
        if (-not [string]::IsNullOrWhiteSpace($name)) {
            $map[$name] = $col
        }
    }
    return $map
}

function Get-NextId {
    param($Sheet)
    $lastRow = Get-LastUsedRow -Sheet $Sheet
    if ($lastRow -le 1) { return 1 }

    $idColumn = (Get-ColumnMap -Sheet $Sheet)['Id']
    $maxId = 0
    for ($row = 2; $row -le $lastRow; $row++) {
        $value = $Sheet.Cells.Item($row, $idColumn).Value2
        if ($null -ne $value -and "$value" -ne '') {
            $intVal = [int]$value
            if ($intVal -gt $maxId) { $maxId = $intVal }
        }
    }
    return ($maxId + 1)
}

function Add-RecordToExcel {
    param(
        $Sheet,
        [Parameter(Mandatory)]$Record
    )

    $headers = Get-ExcelHeaders
    $nextRow = (Get-LastUsedRow -Sheet $Sheet) + 1

    for ($i = 0; $i -lt $headers.Count; $i++) {
        $header = $headers[$i]
        $value = $Record.$header

        if ($value -is [datetime]) {
            $Sheet.Cells.Item($nextRow, $i + 1).Value2 = $value.ToOADate()
            $Sheet.Cells.Item($nextRow, $i + 1).NumberFormat = 'dd/mm/yyyy hh:mm'
        }
        else {
            $Sheet.Cells.Item($nextRow, $i + 1).Value2 = [string]$value
        }
    }

    return $nextRow
}

function Get-ExcelRecords {
    param($Sheet)

    $records = New-Object System.Collections.Generic.List[object]
    $colMap = Get-ColumnMap -Sheet $Sheet
    $lastRow = Get-LastUsedRow -Sheet $Sheet

    for ($row = 2; $row -le $lastRow; $row++) {
        $id = $Sheet.Cells.Item($row, $colMap['Id']).Value2
        if ($null -eq $id -or "$id" -eq '') { continue }

        $obj = [ordered]@{ _Row = $row }
        foreach ($name in $colMap.Keys) {
            $obj[$name] = $Sheet.Cells.Item($row, $colMap[$name]).Value2
        }
        $records.Add([pscustomobject]$obj)
    }

    return $records.ToArray()
}

function Update-ExcelRecordStatus {
    param(
        $Sheet,
        [int[]]$Rows,
        [string]$Status,
        [string]$DocxFile = ''
    )

    $colMap = Get-ColumnMap -Sheet $Sheet
    $now = Get-Date
    foreach ($row in $Rows) {
        $Sheet.Cells.Item($row, $colMap['Status']).Value2 = $Status
        if ($DocxFile) {
            $Sheet.Cells.Item($row, $colMap['AddedToDocxFile']).Value2 = $DocxFile
            $Sheet.Cells.Item($row, $colMap['AddedToDocxAt']).Value2 = $now.ToOADate()
            $Sheet.Cells.Item($row, $colMap['AddedToDocxAt']).NumberFormat = 'dd/mm/yyyy hh:mm'
        }
    }
}

function Remove-OldExcelRows {
    param($Sheet)

    $records = Get-ExcelRecords -Sheet $Sheet
    $cutoff  = Get-CutoffDate -Months $Config.DeleteExcelOlderThanMonths

    foreach ($record in ($records | Sort-Object -Property _Row -Descending)) {
        $submitted = $null
        if ($record.SubmittedAt) {
            try { $submitted = [datetime]$record.SubmittedAt } catch {}
        }
        if ($null -ne $submitted -and $submitted -lt $cutoff) {
            $Sheet.Rows.Item([int]$record._Row).Delete()
        }
    }
}

# =========================
# Helpers: Outlook
# =========================
function Open-OutlookFolder {
    $outlook = New-Object -ComObject Outlook.Application
    $namespace = $outlook.GetNamespace('MAPI')

    if ([string]::IsNullOrWhiteSpace($Config.MailboxName)) {
        $folder = $namespace.GetDefaultFolder(6) # Inbox
    }
    else {
        $root = $namespace.Folders.Item($Config.MailboxName)
        if ($null -eq $root) { throw "Mailbox '$($Config.MailboxName)' not found in Outlook." }
        $folder = $root
    }

    foreach ($part in $Config.FolderPath) {
        if ($part -ieq 'Inbox' -and [string]::IsNullOrWhiteSpace($Config.MailboxName)) {
            continue
        }
        $folder = $folder.Folders.Item($part)
        if ($null -eq $folder) { throw "Folder part '$part' not found." }
    }

    [pscustomobject]@{
        Outlook   = $outlook
        Namespace = $namespace
        Folder    = $folder
    }
}

function Close-OutlookFolder {
    param($OutlookState)
    if ($null -eq $OutlookState) { return }
    foreach ($obj in @($OutlookState.Folder, $OutlookState.Namespace, $OutlookState.Outlook)) {
        if ($null -ne $obj) {
            try { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($obj) } catch {}
        }
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

function Get-MatchingUnreadEmails {
    param($Folder)

    $items = $Folder.Items
    $items.Sort('[ReceivedTime]', $true)

    $result = New-Object System.Collections.Generic.List[object]
    for ($i = 1; $i -le $items.Count; $i++) {
        $mail = $items.Item($i)
        if ($null -eq $mail) { continue }

        try {
            if ($mail.Class -ne 43) { continue } # olMail
            if (-not $mail.UnRead) { continue }
            if (-not ([regex]::IsMatch([string]$mail.Subject, $Config.SubjectRegex))) { continue }
            $result.Add($mail)
        }
        catch {
            Write-Log "Skipping one Outlook item: $($_.Exception.Message)"
        }
    }

    return $result.ToArray()
}

function Convert-EmailToRecord {
    param(
        [Parameter(Mandatory)]$MailItem,
        [Parameter(Mandatory)][int]$Id
    )

    $subjectMatch = [regex]::Match([string]$MailItem.Subject, $Config.SubjectRegex)
    if (-not $subjectMatch.Success) {
        throw "Subject did not match regex: $($MailItem.Subject)"
    }

    $madeBy = $subjectMatch.Groups['madeBy'].Value.Trim()
    $subjectSource = $subjectMatch.Groups['source'].Value.Trim()

    $body = [string]$MailItem.Body
    $payloadMatch = [regex]::Match($body, $Config.BodyBase64Regex)
    if (-not $payloadMatch.Success) {
        throw 'No base64 payload matching BodyBase64Regex was found in email body.'
    }

    $payload = $payloadMatch.Groups['payload'].Value -replace '\s+', ''
    $jsonObject = Convert-FromBase64Json -Base64Text $payload
    $record = Convert-SubmissionToRecord -JsonObject $jsonObject -MailItem $MailItem -MadeBy $madeBy -Id $Id

    if ($subjectSource -and -not $record.SourcePrefix) {
        $record.SourcePrefix = $subjectSource
        $record.DisplayNumber = Build-DisplayNumber -SourcePrefix $subjectSource -SourceNumber $record.SourceNumber
    }

    return $record
}

# =========================
# Helpers: Word rendering
# =========================
function Open-WordDocument {
    param([string]$Path)
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $doc = $word.Documents.Open($Path)

    return [pscustomobject]@{
        Word = $word
        Doc  = $doc
    }
}

function Close-WordDocument {
    param($WordState, [bool]$Save = $true)
    if ($null -eq $WordState) { return }

    try {
        if ($Save) { $WordState.Doc.Save() }
        $WordState.Doc.Close($Save)
    } catch {}
    try { $WordState.Word.Quit() } catch {}

    foreach ($obj in @($WordState.Doc, $WordState.Word)) {
        if ($null -ne $obj) {
            try { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($obj) } catch {}
        }
    }

    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

function Get-ContentControlByTitle {
    param(
        [Parameter(Mandatory)]$Document,
        [Parameter(Mandatory)][string]$Title
    )

    $matches = $Document.SelectContentControlsByTitle($Title)
    if ($null -eq $matches -or $matches.Count -lt 1) {
        throw "Template content control titled '$Title' was not found."
    }

    return $matches.Item(1)
}

function Set-ContentControlText {
    param(
        [Parameter(Mandatory)]$Document,
        [Parameter(Mandatory)][string]$Title,
        [AllowNull()][string]$Text,
        [int]$BaseFontSize = 0,
        [int]$MinFontSize = 6,
        [int]$ShrinkAfterLength = 0,
        [int]$CharsPerPoint = 35
    )

    $cc = Get-ContentControlByTitle -Document $Document -Title $Title
    $safeText = [string]$Text
    if ($null -eq $safeText) { $safeText = '' }

    $cc.Range.Text = $safeText

    if ($BaseFontSize -gt 0) {
        $fontSize = $BaseFontSize
        if ($ShrinkAfterLength -gt 0 -and $safeText.Length -gt $ShrinkAfterLength) {
            $overBy = $safeText.Length - $ShrinkAfterLength
            $fontSize = [Math]::Max($MinFontSize, $BaseFontSize - [Math]::Ceiling($overBy / [double]$CharsPerPoint))
        }
        try { $cc.Range.Font.Size = $fontSize } catch {}
    }
}

function Set-ContentControlDropdownValue {
    param(
        [Parameter(Mandatory)]$Document,
        [Parameter(Mandatory)][string]$Title,
        [Parameter(Mandatory)][string]$Value
    )

    $cc = Get-ContentControlByTitle -Document $Document -Title $Title
    $wanted = ([string]$Value).Trim()

    # For dropdown/list content controls, select an existing list entry where possible.
    try {
        for ($i = 1; $i -le $cc.DropdownListEntries.Count; $i++) {
            $entry = $cc.DropdownListEntries.Item($i)
            if ([string]::Equals([string]$entry.Text, $wanted, [StringComparison]::OrdinalIgnoreCase) -or
                [string]::Equals([string]$entry.Value, $wanted, [StringComparison]::OrdinalIgnoreCase)) {
                $entry.Select()
                return
            }
        }
    }
    catch {
        # Not a dropdown content control, or Word did not expose entries. Fall through.
    }

    # Fallback for plain text controls or dropdowns where the entry lookup failed.
    $cc.Range.Text = $wanted
}

function Format-ItemTypeText {
    param([string]$ItemType)

    $normalized = Normalize-ItemTypeToken -Value $ItemType
    if ([string]::IsNullOrWhiteSpace($normalized)) { return 'Physical' }
    return $normalized
}

function Set-CardField {
    param(
        [Parameter(Mandatory)]$Document,
        [Parameter(Mandatory)][int]$CardNumber,
        [Parameter(Mandatory)][string]$FieldTitle,
        [AllowNull()][string]$Text,
        [int]$BaseFontSize = 0,
        [int]$MinFontSize = 6,
        [int]$ShrinkAfterLength = 0,
        [int]$CharsPerPoint = 35
    )

    $title = "$FieldTitle$CardNumber"
    Set-ContentControlText -Document $Document -Title $title -Text $Text -BaseFontSize $BaseFontSize -MinFontSize $MinFontSize -ShrinkAfterLength $ShrinkAfterLength -CharsPerPoint $CharsPerPoint
}

function Fill-CardByNumber {
    param(
        [Parameter(Mandatory)]$Document,
        [Parameter(Mandatory)][int]$CardNumber,
        [Parameter(Mandatory)]$Record
    )

    $itemType = Format-ItemTypeText -ItemType $Record.ItemType
    $descriptionText = [string]$Record.ItemText

    Set-ContentControlDropdownValue -Document $Document -Title "Item Type$CardNumber" -Value $itemType
    Set-CardField -Document $Document -CardNumber $CardNumber -FieldTitle 'ISP'            -Text "# $($Record.DisplayNumber) ISP: $($Record.ISP)" -BaseFontSize 10 -MinFontSize 7 -ShrinkAfterLength 20 -CharsPerPoint 12
    Set-CardField -Document $Document -CardNumber $CardNumber -FieldTitle 'CharacterName'  -Text ([string]$Record.CharacterName) -BaseFontSize 10 -MinFontSize 7 -ShrinkAfterLength 22 -CharsPerPoint 12
    Set-CardField -Document $Document -CardNumber $CardNumber -FieldTitle 'CharacterClass' -Text ([string]$Record.ClassName)     -BaseFontSize 10 -MinFontSize 7 -ShrinkAfterLength 22 -CharsPerPoint 12
    Set-CardField -Document $Document -CardNumber $CardNumber -FieldTitle 'PlayerName'     -Text ([string]$Record.RealName)      -BaseFontSize 10 -MinFontSize 7 -ShrinkAfterLength 22 -CharsPerPoint 12
    Set-CardField -Document $Document -CardNumber $CardNumber -FieldTitle 'BlowUpDate'     -Text ((Convert-ExcelDateToDateTime $Record.BlowUpDate).ToString('dd/MM/yyyy')) -BaseFontSize 10 -MinFontSize 7
    Set-CardField -Document $Document -CardNumber $CardNumber -FieldTitle 'Description'    -Text $descriptionText -BaseFontSize 9 -MinFontSize 5 -ShrinkAfterLength 120 -CharsPerPoint 45
}

function Convert-ExcelDateToDateTime {
    param($Value)

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return $null
    }

    if ($Value -is [datetime]) {
        return $Value
    }

    $number = 0.0
    if ([double]::TryParse([string]$Value, [ref]$number)) {
        return [datetime]::FromOADate($number)
    }

    return [datetime]$Value
}

function Render-BatchToDocx {
    param(
        [Parameter(Mandatory)][object[]]$Records,
        [Parameter(Mandatory)][string]$TemplatePath,
        [Parameter(Mandatory)][string]$OutputFolder
    )

    if ($Records.Count -ne $Config.PendingBatchSize) {
        throw "Render-BatchToDocx expects exactly $($Config.PendingBatchSize) records. Got $($Records.Count)."
    }

    Ensure-ParentDirectory -Path (Join-Path $OutputFolder 'x')
    if (-not (Test-Path $OutputFolder)) { New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null }

    $firstId = [int]($Records | Sort-Object Id | Select-Object -First 1).Id
    $lastId  = [int]($Records | Sort-Object Id | Select-Object -Last 1).Id
    $stamp   = Get-Date -Format 'yy-MM-dd'
    $outputName = "{0}-{1}-{2}.docx" -f $firstId, $lastId, $stamp
    $outputPath = Join-Path $OutputFolder $outputName

    Copy-Item -Path $TemplatePath -Destination $outputPath -Force
    $wordState = $null

    try {
        $wordState = Open-WordDocument -Path $outputPath

        for ($i = 0; $i -lt $Config.PendingBatchSize; $i++) {
            Fill-CardByNumber -Document $wordState.Doc -CardNumber ($i + 1) -Record $Records[$i]
        }

        $wordState.Doc.Save()
        return $outputPath
    }
    finally {
        Close-WordDocument -WordState $wordState -Save $true
    }
}

# =========================
# Cleanup
# =========================
function Remove-OldGeneratedDocxFiles {
    param([string]$Folder)
    if (-not (Test-Path $Folder)) { return }

    $cutoff = (Get-Date).AddDays(-$Config.DeleteDocxOlderThanDays)
    Get-ChildItem -Path $Folder -Filter '*.docx' -File |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        ForEach-Object {
            Write-Log "Deleting old DOCX: $($_.FullName)"
            Remove-Item -Path $_.FullName -Force
        }
}

# =========================
# Main
# =========================
$excelState = $null
$outlookState = $null

try {
    Write-Log '=== Run started ==='

    if (-not (Test-Path $Config.TemplateDocxPath)) {
        throw "Template DOCX not found: $($Config.TemplateDocxPath)"
    }

    Ensure-ParentDirectory -Path $Config.ExcelPath
    Ensure-ParentDirectory -Path $Config.LogPath
    if (-not (Test-Path $Config.OutputDocxFolder)) {
        New-Item -ItemType Directory -Path $Config.OutputDocxFolder -Force | Out-Null
    }

    $excelState = Open-ExcelWorkbook -Path $Config.ExcelPath

    # Maintenance first.
    Remove-OldExcelRows -Sheet $excelState.Sheet
    Remove-OldGeneratedDocxFiles -Folder $Config.OutputDocxFolder

    # Read matching unread emails.
    $outlookState = Open-OutlookFolder
    $emails = @(Get-MatchingUnreadEmails -Folder $outlookState.Folder)
    Write-Log "Found $($emails.Count) unread matching email(s)."

    foreach ($mail in $emails) {
        $nextId = Get-NextId -Sheet $excelState.Sheet
        try {
            $record = Convert-EmailToRecord -MailItem $mail -Id $nextId
            [void](Add-RecordToExcel -Sheet $excelState.Sheet -Record $record)
            $mail.UnRead = $false
            $mail.Save()
            Write-Log "Queued email '$($mail.Subject)' as record ID $nextId."
        }
        catch {
            Write-Log "Failed to process email '$($mail.Subject)': $($_.Exception.Message)"
        }
    }

    $excelState.Workbook.Save()

    # Create batches while 10 queued records exist.
    while ($true) {
        $records = @(Get-ExcelRecords -Sheet $excelState.Sheet)
        $queued = @(
            $records |
            Where-Object { $_.Status -eq $Config.StatusQueued } |
            Sort-Object {[int]$_.Id} |
            Select-Object -First $Config.PendingBatchSize
        )

        if ($queued.Count -lt $Config.PendingBatchSize) {
            Write-Log 'Fewer than 10 queued records remain; no further DOCX created this run.'
            break
        }

        Write-Log "Rendering batch IDs $((($queued | Select-Object -ExpandProperty Id) -join ', '))."
        $outputPath = Render-BatchToDocx -Records $queued -TemplatePath $Config.TemplateDocxPath -OutputFolder $Config.OutputDocxFolder
        Update-ExcelRecordStatus -Sheet $excelState.Sheet -Rows ($queued | ForEach-Object { [int]$($_._Row) }) -Status $Config.StatusRendered -DocxFile (Split-Path -Path $outputPath -Leaf)
        $excelState.Workbook.Save()
        Write-Log "Created DOCX batch: $outputPath"
    }

    $excelState.Workbook.Save()
    Write-Log '=== Run finished successfully ==='
}
catch {
    Write-Log "FATAL: $($_.Exception.Message)"
    throw
}
finally {
    Close-OutlookFolder -OutlookState $outlookState
    Close-ExcelWorkbook -ExcelState $excelState
}
