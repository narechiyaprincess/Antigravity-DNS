# SyntheticWire.ps1 - Pure offline synthetic DNS packet generator and parser
# Conforms to RFC 1035, RFC 3596 (AAAA), RFC 8484 (DoH) wire formats.

$ErrorActionPreference = 'Stop'

function Write-U16([System.Collections.Generic.List[byte]]$buffer, [int]$value) {
    $buffer.Add([byte](($value -shr 8) -band 0xFF))
    $buffer.Add([byte]($value -band 0xFF))
}

function Write-U32([System.Collections.Generic.List[byte]]$buffer, [int64]$value) {
    $buffer.Add([byte](($value -shr 24) -band 0xFF))
    $buffer.Add([byte](($value -shr 16) -band 0xFF))
    $buffer.Add([byte](($value -shr 8) -band 0xFF))
    $buffer.Add([byte]($value -band 0xFF))
}

function Read-U16([byte[]]$buf, [int]$p) {
    if ($p -lt 0 -or $p + 2 -gt $buf.Length) { throw [System.FormatException]"DNS_INVALID_RESPONSE" }
    return (([int]$buf[$p] -shl 8) -bor [int]$buf[$p + 1])
}

function Append-Bytes([System.Collections.Generic.List[byte]]$list, $bytes) {
    if ($null -ne $bytes) {
        foreach ($item in $bytes) {
            $list.Add([byte]$item)
        }
    }
}

function Encode-DnsName([string]$name) {
    $bytes = [System.Collections.Generic.List[byte]]::new()
    if ([string]::IsNullOrEmpty($name)) {
        $bytes.Add(0)
        return $bytes.ToArray()
    }
    $labels = $name.Split('.')
    foreach ($label in $labels) {
        $lBytes = [System.Text.Encoding]::ASCII.GetBytes($label)
        $bytes.Add([byte]$lBytes.Length)
        Append-Bytes $bytes $lBytes
    }
    $bytes.Add(0)
    return $bytes.ToArray()
}

function New-SyntheticDnsResponse {
    param(
        [string]$QuestionDomain,
        [int]$QuestionType = 1,
        [int]$Id = 1234,
        [int]$RCode = 0,
        [bool]$Truncated = $false,
        [PSCustomObject[]]$AnswerRecords = @()
    )
    $b = [System.Collections.Generic.List[byte]]::new()
    Write-U16 $b $Id
    
    # Flags: QR=1 (response), AA=1, TC=(if $Truncated 1 else 0), RD=1, RA=1
    $byte2 = 0x81 # QR=1, RD=1
    if ($Truncated) { $byte2 = $byte2 -bor 0x02 } # TC flag
    $b.Add([byte]$byte2)
    
    $byte3 = 0x80 -bor ($RCode -band 0x0F) # RA=1, RCODE
    $b.Add([byte]$byte3)
    
    Write-U16 $b 1 # QDCOUNT = 1
    Write-U16 $b ($AnswerRecords.Count) # ANCOUNT
    Write-U16 $b 0 # NSCOUNT
    Write-U16 $b 0 # ARCOUNT
    
    # Question section
    Append-Bytes $b (Encode-DnsName $QuestionDomain)
    Write-U16 $b $QuestionType
    Write-U16 $b 1 # CLASS IN
    
    # Answer section
    foreach ($r in $AnswerRecords) {
        Append-Bytes $b (Encode-DnsName $r.Owner)
        Write-U16 $b $r.Type
        Write-U16 $b 1 # CLASS IN
        Write-U32 $b $(if ($r.TTL) { $r.TTL } else { 60 })
        
        if ($r.Type -eq 1) { # A
            $ipBytes = ([System.Net.IPAddress]::Parse($r.Data)).GetAddressBytes()
            Write-U16 $b $ipBytes.Length
            Append-Bytes $b $ipBytes
        } elseif ($r.Type -eq 28) { # AAAA
            $ipBytes = ([System.Net.IPAddress]::Parse($r.Data)).GetAddressBytes()
            Write-U16 $b $ipBytes.Length
            Append-Bytes $b $ipBytes
        } elseif ($r.Type -eq 5) { # CNAME
            $targetBytes = Encode-DnsName $r.Data
            Write-U16 $b $targetBytes.Length
            Append-Bytes $b $targetBytes
        } else {
            $raw = if ($r.RawBytes) { $r.RawBytes } else { @(0) }
            Write-U16 $b $raw.Length
            Append-Bytes $b $raw
        }
    }
    
    return $b.ToArray()
}

# Helper record factories
function New-ARecord($Owner, $IP, $TTL = 60) {
    return [PSCustomObject]@{ Owner = $Owner; Type = 1; Data = $IP; TTL = $TTL }
}

function New-AAAARecord($Owner, $IPv6, $TTL = 60) {
    return [PSCustomObject]@{ Owner = $Owner; Type = 28; Data = $IPv6; TTL = $TTL }
}

function New-CNameRecord($Owner, $Alias, $TTL = 60) {
    return [PSCustomObject]@{ Owner = $Owner; Type = 5; Data = $Alias; TTL = $TTL }
}

# Order-Independent Wire Parser Implementation (Specification Oracle for F07 & F05)
function Parse-SyntheticWirePacketOrderIndependent {
    param(
        [byte[]]$Buffer,
        [string]$ExpectedHost,
        [int]$ExpectedType,
        [int]$ExpectedId
    )
    if ($null -eq $Buffer -or $Buffer.Length -lt 12) { throw [System.FormatException]"DNS_INVALID_RESPONSE" }
    
    $id = Read-U16 $Buffer 0
    if ($id -ne $ExpectedId) { throw [System.FormatException]"transaction mismatch" }
    
    $isResponse = ($Buffer[2] -band 0x80) -ne 0
    $opcode = ($Buffer[2] -band 0x78)
    $tc = ($Buffer[2] -band 0x02) -ne 0
    if (-not $isResponse -or $opcode -ne 0 -or $tc) { throw [System.FormatException]"DNS_INVALID_RESPONSE" }
    
    $qdCount = Read-U16 $Buffer 4
    if ($qdCount -ne 1) { throw [System.FormatException]"DNS_INVALID_RESPONSE" }
    
    $anCount = Read-U16 $Buffer 6
    $rcode = $Buffer[3] -band 0x0F
    
    # Parse question
    $pos = 12
    $qName = Read-DnsNameFromBuffer $Buffer ([ref]$pos)
    if (-not [string]::Equals($qName, $ExpectedHost, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw [System.FormatException]"question mismatch"
    }
    $qType = Read-U16 $Buffer $pos
    $qClass = Read-U16 $Buffer ($pos + 2)
    if ($qType -ne $ExpectedType -or $qClass -ne 1) { throw [System.FormatException]"question" }
    $pos += 4
    
    # Collect all answer records
    $rawAnswers = [System.Collections.Generic.List[PSObject]]::new()
    for ($i = 0; $i -lt $anCount; $i++) {
        $owner = Read-DnsNameFromBuffer $Buffer ([ref]$pos)
        $t = Read-U16 $Buffer $pos
        $cl = Read-U16 $Buffer ($pos + 2)
        $len = Read-U16 $Buffer ($pos + 8)
        $pos += 10
        if ($pos + $len -gt $Buffer.Length) { throw [System.FormatException]"truncated rdata" }
        
        $rawAnswers.Add([PSCustomObject]@{
            Owner  = $owner
            Type   = $t
            Class  = $cl
            Length = $len
            Offset = $pos
        })
        $pos += $len
    }
    
    # Graph-based / Multi-pass CNAME & Address resolver (Order Independent)
    # Resolves alias chains regardless of whether CNAME precedes or follows address records
    $allowed = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $allowed.Add($ExpectedHost) | Out-Null
    
    $cnames = [System.Collections.Generic.List[string]]::new()
    $addresses = [System.Collections.Generic.List[string]]::new()
    
    # Pass 1: Resolve CNAME chain from ExpectedHost with loop detection
    $cnameMap = @{}
    foreach ($rec in $rawAnswers) {
        if ($rec.Class -eq 1 -and $rec.Type -eq 5) {
            $cpos = $rec.Offset
            $alias = Read-DnsNameFromBuffer $Buffer ([ref]$cpos)
            if ($cpos -ne $rec.Offset + $rec.Length) { throw [System.FormatException]"cname length" }
            $cnameMap[$rec.Owner.ToLowerInvariant()] = $alias
        }
    }
    
    $curr = $ExpectedHost.ToLowerInvariant()
    $chainVisited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $chainVisited.Add($curr) | Out-Null
    while ($cnameMap.ContainsKey($curr)) {
        $next = $cnameMap[$curr].ToLowerInvariant()
        if ($chainVisited.Contains($next)) {
            throw [System.FormatException]"DNS CNAME alias loop detected: $curr -> $next"
        }
        $chainVisited.Add($next) | Out-Null
        $allowed.Add($next) | Out-Null
        $cnames.Add($next)
        $curr = $next
    }
    
    # Pass 2: Collect all address records whose owner is in $allowed
    foreach ($rec in $rawAnswers) {
        if ($rec.Class -eq 1 -and $rec.Type -eq $ExpectedType -and $allowed.Contains($rec.Owner)) {
            if ($rec.Type -eq 1 -and $rec.Length -eq 4) {
                $raw = New-Object byte[] 4
                [Array]::Copy($Buffer, $rec.Offset, $raw, 0, 4)
                $ip = [System.Net.IPAddress]::new($raw).ToString()
                $addresses.Add($ip)
            } elseif ($rec.Type -eq 28 -and $rec.Length -eq 16) {
                $raw = New-Object byte[] 16
                [Array]::Copy($Buffer, $rec.Offset, $raw, 0, 16)
                $ip = [System.Net.IPAddress]::new($raw).ToString()
                $addresses.Add($ip)
            }
        }
    }
    
    $code = if ($rcode -eq 0) {
        if ($addresses.Count -gt 0) { "DNS_OK" } else { "DNS_NODATA" }
    } elseif ($rcode -eq 3) {
        "DNS_NXDOMAIN"
    } else {
        "DNS_RCODE_$rcode"
    }
    
    return [PSCustomObject]@{
        Code      = $code
        RCode     = $rcode
        Addresses = $addresses.ToArray()
        Cnames    = $cnames.ToArray()
    }
}

# Helper to read DNS name with RFC 1035 compression pointer support and loop guard
function Read-DnsNameFromBuffer([byte[]]$buf, [ref][int]$posRef) {
    $p = $posRef.Value
    $steps = 0
    $jumped = $false
    $labels = [System.Collections.Generic.List[string]]::new()
    $seen = [System.Collections.Generic.HashSet[int]]::new()
    
    while ($true) {
        $steps++
        if ($steps -gt 128 -or $p -ge $buf.Length -or -not $seen.Add($p)) {
            throw [System.FormatException]"compression pointer loop"
        }
        $n = [int]$buf[$p++]
        if ($n -eq 0) {
            if (-not $jumped) { $posRef.Value = $p }
            break
        }
        if (($n -band 192) -eq 192) {
            if ($p -ge $buf.Length) { throw [System.FormatException]"DNS_INVALID_RESPONSE" }
            $target = ((([int]$n -band 63) -shl 8) -bor [int]$buf[$p++])
            if (-not $jumped) { $posRef.Value = $p }
            $jumped = $true
            $p = $target
            continue
        }
        if ($n -gt 63 -or $p + $n -gt $buf.Length) { throw [System.FormatException]"DNS_INVALID_RESPONSE" }
        $label = [System.Text.Encoding]::ASCII.GetString($buf, $p, $n)
        $labels.Add($label)
        $p += $n
    }
    return [string]::Join(".", $labels.ToArray())
}
