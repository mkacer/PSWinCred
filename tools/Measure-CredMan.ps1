#Requires -Version 5.1
<#
.SYNOPSIS
    Measures Windows Credential Manager behavior that WinCred's design depends on.
.DESCRIPTION
    Calls the CredMan API directly, without WinCred, and prints one line per question:
      Q1  How CredEnumerateW applies a filter (prefix, case, wildcard characters).
      Q2  Whether CredReadW matches exactly or applies wildcards itself.
      Q3  What CRED_PRESERVE_CREDENTIAL_BLOB does on CredWriteW.
      Q4  Which CRED_FLAGS_* values CredWriteW accepts and returns.
      Q5  Whether attributes and the target alias survive a read and write-back.
      Q6  How domain targets with non-ASCII labels are counted.
      Q7  Whether CRED_PERSIST_NONE can be written.

    It writes only credentials named WinCred.Probe.* and *.wincredprobe.example.com, with Session
    persistence, and deletes them at the end. Probe secrets are fixed test strings.
.PARAMETER SurveyStore
    Also reports counts of credential types, flags, persistence, attributes and secret sizes in your
    store. It prints no names and no secrets, but it reads every credential, so Windows decrypts every
    secret into this process.
#>
[CmdletBinding()]
param(
    [switch]$SurveyStore
)

$ErrorActionPreference = 'Stop'

# C# 5 only: Windows PowerShell 5.1 compiles Add-Type code with the .NET Framework compiler
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WinCredProbe
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME { public uint Low; public uint High; }

    [StructLayout(LayoutKind.Sequential)]
    public struct CREDENTIALW
    {
        public uint Flags; public uint Type; public IntPtr TargetName; public IntPtr Comment; public FILETIME LastWritten;
        public uint CredentialBlobSize; public IntPtr CredentialBlob; public uint Persist; public uint AttributeCount;
        public IntPtr Attributes; public IntPtr TargetAlias; public IntPtr UserName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CREDENTIAL_ATTRIBUTEW { public IntPtr Keyword; public uint Flags; public uint ValueSize; public IntPtr Value; }

    public class Attr
    {
        public string Keyword; public uint Flags; public byte[] Value;
    }

    public class Cred
    {
        public string TargetName; public uint Type; public uint Flags; public uint Persist = 1;
        public string UserName; public string Comment; public string TargetAlias;
        public byte[] Blob; public uint BlobSize;
        public List<Attr> Attributes = new List<Attr>();
    }

    public static class Native
    {
        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWriteW(ref CREDENTIALW credential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredEnumerateW(string filter, uint flags, out uint count, out IntPtr credentials);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDeleteW(string target, uint type, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredFree")]
        private static extern void CredFree(IntPtr buffer);

        public static int Write(Cred c, uint writeFlags)
        {
            CREDENTIALW n = new CREDENTIALW();
            n.Type = c.Type; n.Flags = c.Flags; n.Persist = c.Persist;
            List<IntPtr> allocations = new List<IntPtr>();
            try
            {
                n.TargetName = Str(c.TargetName, allocations);
                n.UserName = Str(c.UserName, allocations);
                n.Comment = Str(c.Comment, allocations);
                n.TargetAlias = Str(c.TargetAlias, allocations);
                if (c.Blob != null)
                {
                    n.CredentialBlobSize = (uint)c.Blob.Length;
                    n.CredentialBlob = Bytes(c.Blob, allocations);
                }
                if (c.Attributes.Count > 0)
                {
                    int size = Marshal.SizeOf(typeof(CREDENTIAL_ATTRIBUTEW));
                    IntPtr array = Marshal.AllocHGlobal(size * c.Attributes.Count);
                    allocations.Add(array);
                    for (int i = 0; i < c.Attributes.Count; i++)
                    {
                        CREDENTIAL_ATTRIBUTEW a = new CREDENTIAL_ATTRIBUTEW();
                        a.Keyword = Str(c.Attributes[i].Keyword, allocations);
                        a.Flags = c.Attributes[i].Flags;
                        a.ValueSize = (uint)c.Attributes[i].Value.Length;
                        a.Value = Bytes(c.Attributes[i].Value, allocations);
                        Marshal.StructureToPtr(a, IntPtr.Add(array, i * size), false);
                    }
                    n.AttributeCount = (uint)c.Attributes.Count;
                    n.Attributes = array;
                }
                return CredWriteW(ref n, writeFlags) ? 0 : Marshal.GetLastWin32Error();
            }
            finally
            {
                foreach (IntPtr p in allocations) { Marshal.FreeHGlobal(p); }
            }
        }

        public static Cred Read(string target, uint type, out int error)
        {
            IntPtr p;
            error = 0;
            if (!CredReadW(target, type, 0, out p)) { error = Marshal.GetLastWin32Error(); return null; }
            try { return Convert(p, true); } finally { CredFree(p); }
        }

        public static List<Cred> Enumerate(string filter, bool includeBlob, out int error)
        {
            IntPtr p;
            uint count;
            error = 0;
            List<Cred> list = new List<Cred>();
            if (!CredEnumerateW(filter, 0, out count, out p)) { error = Marshal.GetLastWin32Error(); return list; }
            try
            {
                for (int i = 0; i < count; i++) { list.Add(Convert(Marshal.ReadIntPtr(p, i * IntPtr.Size), includeBlob)); }
            }
            finally { CredFree(p); }
            return list;
        }

        public static int Delete(string target, uint type)
        {
            return CredDeleteW(target, type, 0) ? 0 : Marshal.GetLastWin32Error();
        }

        private static Cred Convert(IntPtr p, bool includeBlob)
        {
            CREDENTIALW n = (CREDENTIALW)Marshal.PtrToStructure(p, typeof(CREDENTIALW));
            Cred c = new Cred();
            c.TargetName = Marshal.PtrToStringUni(n.TargetName);
            c.Type = n.Type; c.Flags = n.Flags; c.Persist = n.Persist;
            c.UserName = Marshal.PtrToStringUni(n.UserName);
            c.Comment = Marshal.PtrToStringUni(n.Comment);
            c.TargetAlias = Marshal.PtrToStringUni(n.TargetAlias);
            c.BlobSize = n.CredentialBlobSize;
            if (includeBlob)
            {
                c.Blob = new byte[n.CredentialBlobSize];
                if (n.CredentialBlobSize > 0 && n.CredentialBlob != IntPtr.Zero) { Marshal.Copy(n.CredentialBlob, c.Blob, 0, c.Blob.Length); }
            }
            int size = Marshal.SizeOf(typeof(CREDENTIAL_ATTRIBUTEW));
            for (int i = 0; i < n.AttributeCount; i++)
            {
                CREDENTIAL_ATTRIBUTEW a = (CREDENTIAL_ATTRIBUTEW)Marshal.PtrToStructure(IntPtr.Add(n.Attributes, i * size), typeof(CREDENTIAL_ATTRIBUTEW));
                Attr attr = new Attr();
                attr.Keyword = Marshal.PtrToStringUni(a.Keyword);
                attr.Flags = a.Flags;
                attr.Value = new byte[a.ValueSize];
                if (a.ValueSize > 0) { Marshal.Copy(a.Value, attr.Value, 0, attr.Value.Length); }
                c.Attributes.Add(attr);
            }
            return c;
        }

        private static IntPtr Str(string s, List<IntPtr> allocations)
        {
            if (s == null) { return IntPtr.Zero; }
            IntPtr p = Marshal.StringToHGlobalUni(s);
            allocations.Add(p);
            return p;
        }

        private static IntPtr Bytes(byte[] bytes, List<IntPtr> allocations)
        {
            IntPtr p = Marshal.AllocHGlobal(Math.Max(bytes.Length, 1));
            allocations.Add(p);
            Marshal.Copy(bytes, 0, p, bytes.Length);
            return p;
        }
    }
}
'@

$GENERIC = 1
$DOMAIN_PASSWORD = 2
$PERSIST_LOCAL_MACHINE = 2
$PRESERVE_BLOB = 1
$domainUser = "$env:COMPUTERNAME\wincred-probe"
$secret1 = [Text.Encoding]::Unicode.GetBytes('probe-secret-1')
$secret2 = [Text.Encoding]::Unicode.GetBytes('probe-secret-2')
$written = [System.Collections.Generic.List[object]]::new()
# PowerShell turns $null into "" for .NET string parameters; [NullString]::Value passes a real null
$noFilter = [NullString]::Value

function Get-ProbeCredential {
    param([string]$Target, [uint32]$Type = $GENERIC, [string]$UserName, [string]$Comment, [string]$Alias, [byte[]]$Blob = $secret1, [uint32]$Flags = 0)
    $c = [WinCredProbe.Cred]::new()
    $c.TargetName = $Target; $c.Type = $Type; $c.Flags = $Flags; $c.Blob = $Blob
    if ($UserName) { $c.UserName = $UserName }
    if ($Comment) { $c.Comment = $Comment }
    if ($Alias) { $c.TargetAlias = $Alias }
    $c
}

function Invoke-ProbeWrite {
    param([WinCredProbe.Cred]$Record, [uint32]$WriteFlags = 0)
    $code = [WinCredProbe.Native]::Write($Record, $WriteFlags)
    $written.Add(@($Record.TargetName, $Record.Type))
    $code
}

function Format-Code([int]$Code) {
    if ($Code -eq 0) { 'ok' } else { "error $Code ($([ComponentModel.Win32Exception]::new($Code).Message))" }
}

function Format-NameList($Records) {
    $probe = @($Records | Where-Object { $_.TargetName -match 'WinCred\.Probe|wincredprobe' } | ForEach-Object { $_.TargetName } | Sort-Object)
    "$(@($Records).Count) returned; probe names: [$($probe -join ', ')]"
}

function Get-ProbeRead([string]$Target, [uint32]$Type) {
    $code = 0
    $c = [WinCredProbe.Native]::Read($Target, $Type, [ref]$code)
    if ($c) { "found '$($c.TargetName)'" } else { Format-Code $code }
}

function Invoke-Question([string]$Name, [scriptblock]$Body) {
    try { & $Body }
    catch { "$Name failed: $($_.Exception.Message)" }
}

function Get-BlobText($Record) {
    if (-not $Record.Blob) { return '(none)' }
    [Text.Encoding]::Unicode.GetString($Record.Blob)
}

try {
    # Fixtures
    foreach ($name in 'WinCred.Probe.alpha', 'WinCred.Probe.Beta', 'WinCred.Probe.br[1]', 'WinCred.Probe.q?x', 'WinCred.ProbeOther.x') {
        $null = Invoke-ProbeWrite (Get-ProbeCredential -Target $name)
    }
    $wildcardDomain = '*.wincredprobe.example.com'
    "Fixture: domain credential '$wildcardDomain': $(Format-Code (Invoke-ProbeWrite (Get-ProbeCredential -Target $wildcardDomain -Type $DOMAIN_PASSWORD -UserName $domainUser)))"

    # Q1: CredEnumerateW filters
    Invoke-Question 'Q1' {
        $filters = [ordered]@{
            'Q1a prefix'                 = 'WinCred.Probe.*'
            'Q1b lower case'             = 'wincred.probe.*'
            'Q1c no asterisk'            = 'WinCred.Probe.alpha'
            'Q1d bracket in prefix'      = 'WinCred.Probe.br[*'
            'Q1e question mark'          = 'WinCred.Probe.q?*'
            'Q1f asterisk in middle'     = 'WinCred.*.alpha'
            'Q1g prefix without dot'     = 'WinCred.Probe*'
            'Q1h leading asterisk'       = '*.wincredprobe.example.com'
            'Q1i asterisk only'          = '*'
        }
        foreach ($entry in $filters.GetEnumerator()) {
            $code = 0
            $found = [WinCredProbe.Native]::Enumerate($entry.Value, $false, [ref]$code)
            "$($entry.Key) '$($entry.Value)': $(if ($code) { Format-Code $code } else { Format-NameList $found })"
        }
        $code = 0
        $all = [WinCredProbe.Native]::Enumerate($noFilter, $false, [ref]$code)
        "Q1j no filter: $(@($all).Count) returned"
    }

    # Q2: CredReadW matching
    Invoke-Question 'Q2' {
        "Q2a exact wildcard name '$wildcardDomain': $(Get-ProbeRead $wildcardDomain $DOMAIN_PASSWORD)"
        "Q2b host under wildcard 'host.wincredprobe.example.com': $(Get-ProbeRead 'host.wincredprobe.example.com' $DOMAIN_PASSWORD)"
        "Q2c upper case 'WINCRED.PROBE.ALPHA': $(Get-ProbeRead 'WINCRED.PROBE.ALPHA' $GENERIC)"
        "Q2d generic name as DomainPassword: $(Get-ProbeRead 'WinCred.Probe.alpha' $DOMAIN_PASSWORD)"
        "Q2e literal '[' and '?' names: $(Get-ProbeRead 'WinCred.Probe.br[1]' $GENERIC); $(Get-ProbeRead 'WinCred.Probe.q?x' $GENERIC)"
    }

    # Q3: CRED_PRESERVE_CREDENTIAL_BLOB
    Invoke-Question 'Q3' {
        $target = 'WinCred.Probe.preserve'
        $null = Invoke-ProbeWrite (Get-ProbeCredential -Target $target -UserName 'u1' -Comment 'c1')
        $update = Get-ProbeCredential -Target $target -UserName 'u2' -Comment 'c2' -Blob $null
        "Q3a preserve, no blob, new user+comment: $(Format-Code (Invoke-ProbeWrite $update $PRESERVE_BLOB))"
        $code = 0; $c = [WinCredProbe.Native]::Read($target, $GENERIC, [ref]$code)
        "Q3a read back: secret '$(Get-BlobText $c)', user '$($c.UserName)', comment '$($c.Comment)'"
        "Q3b preserve with a new blob: $(Format-Code (Invoke-ProbeWrite (Get-ProbeCredential -Target $target -Blob $secret2) $PRESERVE_BLOB))"
        $code = 0; $c = [WinCredProbe.Native]::Read($target, $GENERIC, [ref]$code)
        "Q3b read back: secret '$(Get-BlobText $c)', user '$($c.UserName)', comment '$($c.Comment)'"
        "Q3c no preserve, no blob: $(Format-Code (Invoke-ProbeWrite (Get-ProbeCredential -Target $target -Blob $null)))"
        $code = 0; $c = [WinCredProbe.Native]::Read($target, $GENERIC, [ref]$code)
        "Q3c read back: secret '$(Get-BlobText $c)' ($($c.BlobSize) bytes)"
        $null = Invoke-ProbeWrite (Get-ProbeCredential -Target $target)
        $persist = Get-ProbeCredential -Target $target -Blob $null
        $persist.Persist = $PERSIST_LOCAL_MACHINE
        "Q3d preserve, change persistence to LocalMachine: $(Format-Code (Invoke-ProbeWrite $persist $PRESERVE_BLOB))"
        $code = 0; $c = [WinCredProbe.Native]::Read($target, $GENERIC, [ref]$code)
        "Q3d read back: persist $($c.Persist), secret '$(Get-BlobText $c)'"
        $null = [WinCredProbe.Native]::Delete($target, $GENERIC)
        $missing = 'WinCred.Probe.preserve-missing'
        "Q3e preserve on a missing credential: $(Format-Code (Invoke-ProbeWrite (Get-ProbeCredential -Target $missing -Blob $null) $PRESERVE_BLOB))"
        "Q3e afterwards: $(Get-ProbeRead $missing $GENERIC)"
        $domainUpdate = Get-ProbeCredential -Target $wildcardDomain -Type $DOMAIN_PASSWORD -UserName $domainUser -Comment 'changed' -Blob $null
        "Q3f preserve on a DomainPassword credential: $(Format-Code (Invoke-ProbeWrite $domainUpdate $PRESERVE_BLOB))"
    }

    # Q4: CRED_FLAGS_* on write and read, one bit at a time, then written back unchanged
    Invoke-Question 'Q4' {
        foreach ($bit in 0..15) {
            $flag = [uint32][math]::Pow(2, $bit)
            $flagTarget = "WinCred.Probe.flag$bit"
            $code = Invoke-ProbeWrite (Get-ProbeCredential -Target $flagTarget -UserName 'probe-user' -Flags $flag)
            if ($code -eq 0) {
                $readCode = 0; $c = [WinCredProbe.Native]::Read($flagTarget, $GENERIC, [ref]$readCode)
                if (-not $c) {
                    "Q4 flag 0x$($flag.ToString('X4')): write ok, read back $(Format-Code $readCode)"
                    continue
                }
                $c.Blob = $null
                $back = [WinCredProbe.Native]::Write($c, $PRESERVE_BLOB)
                "Q4 flag 0x$($flag.ToString('X4')): write ok, read back 0x$($c.Flags.ToString('X4')), written back: $(Format-Code $back)"
            }
            else {
                "Q4 flag 0x$($flag.ToString('X4')): write $(Format-Code $code)"
            }
        }
    }

    # Q5: attributes and target alias, then a preserve write-back of exactly what was read
    Invoke-Question 'Q5' {
        $attrTarget = 'WinCred.Probe.attr'
        $c = Get-ProbeCredential -Target $attrTarget -Alias 'WinCred.Probe.alias'
        foreach ($a in @(@('WinCredProbe:k1', [byte[]](1, 2, 3)), @('WinCredProbe:k2', [byte[]]@()))) {
            $attr = [WinCredProbe.Attr]::new(); $attr.Keyword = $a[0]; $attr.Value = $a[1]; $c.Attributes.Add($attr)
        }
        "Q5a write with alias and 2 attributes (one empty): $(Format-Code (Invoke-ProbeWrite $c))"
        $code = 0; $read = [WinCredProbe.Native]::Read($attrTarget, $GENERIC, [ref]$code)
        if ($read) {
            $describe = { param($x) "alias '$($x.TargetAlias)', attributes [$((@($x.Attributes) | ForEach-Object { "$($_.Keyword)=$([BitConverter]::ToString($_.Value))" }) -join '; ')]" }
            "Q5a read back: $(& $describe $read)"
            $read.Blob = $null
            "Q5b preserve write-back of the read credential: $(Format-Code ([WinCredProbe.Native]::Write($read, $PRESERVE_BLOB)))"
            $code = 0; $again = [WinCredProbe.Native]::Read($attrTarget, $GENERIC, [ref]$code)
            "Q5b read back: $(& $describe $again), secret '$(Get-BlobText $again)'"
        }
    }

    # Q6: non-ASCII labels in domain targets: longest accepted label per character
    Invoke-Question 'Q6' {
        $samples = [ordered]@{
            'e-acute (UTF-8: 2 bytes)'            = [string][char]0xE9
            'CJK (UTF-8: 3 bytes)'                = [string][char]0x65E5
            'emoji (UTF-16: 2 units, UTF-8: 4)'   = [char]::ConvertFromUtf32(0x1F600)
        }
        foreach ($entry in $samples.GetEnumerator()) {
            $try = {
                param($n)
                $name = ($entry.Value * $n) + '.wincredprobe.example.com'
                $code = Invoke-ProbeWrite (Get-ProbeCredential -Target $name -Type $DOMAIN_PASSWORD -UserName $domainUser)
                if ($code -eq 0) { $null = [WinCredProbe.Native]::Delete($name, $DOMAIN_PASSWORD) }
                $code
            }
            $first = & $try 1
            if ($first -ne 0) {
                "Q6 $($entry.Key): rejected even once: $(Format-Code $first)"
                continue
            }
            $lo = 1; $hi = 63
            while ($lo -lt $hi) {
                $mid = [int][math]::Ceiling(($lo + $hi) / 2)
                if ((& $try $mid) -eq 0) { $lo = $mid } else { $hi = $mid - 1 }
            }
            "Q6 $($entry.Key): longest accepted label = $lo repetitions ($($entry.Value.Length * $lo) UTF-16 units, $([Text.Encoding]::UTF8.GetByteCount($entry.Value * $lo)) UTF-8 bytes)"
        }
        "Q6 punycode label 'xn--9ca': $(Format-Code (Invoke-ProbeWrite (Get-ProbeCredential -Target 'xn--9ca.wincredprobe.example.com' -Type $DOMAIN_PASSWORD -UserName $domainUser)))"
    }

    # Q7: CRED_PERSIST_NONE
    Invoke-Question 'Q7' {
        $none = Get-ProbeCredential -Target 'WinCred.Probe.persistnone'
        $none.Persist = 0
        "Q7 write with persist 0: $(Format-Code (Invoke-ProbeWrite $none))"
    }

    if ($SurveyStore) {
        Invoke-Question 'Survey' {
            $code = 0
            $store = @([WinCredProbe.Native]::Enumerate($noFilter, $false, [ref]$code) | Where-Object { $_.TargetName -notmatch 'WinCred\.Probe|wincredprobe' })
            "S1 credentials (excluding probes): $($store.Count)"
            "S2 by type: $(($store | Group-Object Type | Sort-Object Name | ForEach-Object { "type $($_.Name) x$($_.Count)" }) -join ', ')"
            "S3 by persistence: $(($store | Group-Object Persist | Sort-Object Name | ForEach-Object { "persist $($_.Name) x$($_.Count)" }) -join ', ')"
            $bits = foreach ($bit in 0..31) { $flag = [uint32][math]::Pow(2, $bit); $n = @($store | Where-Object { $_.Flags -band $flag }).Count; if ($n) { "0x$($flag.ToString('X'))x$n" } }
            "S4 flag bits in use: $($bits -join ', ')"
            "S5 with attributes: $(@($store | Where-Object { $_.Attributes.Count }).Count); most attributes on one credential: $(($store | ForEach-Object { $_.Attributes.Count } | Measure-Object -Maximum).Maximum)"
            "S6 with a target alias: $(@($store | Where-Object { $_.TargetAlias }).Count)"
            "S7 secret sizes: odd byte count (not UTF-16) x$(@($store | Where-Object { $_.BlobSize % 2 }).Count), empty x$(@($store | Where-Object { -not $_.BlobSize }).Count), largest $(($store | Measure-Object BlobSize -Maximum).Maximum) bytes"
        }
    }
}
finally {
    # Remove every probe credential, including any a failed step left behind
    foreach ($entry in $written) { $null = [WinCredProbe.Native]::Delete($entry[0], $entry[1]) }
    $code = 0
    $left = @([WinCredProbe.Native]::Enumerate($noFilter, $false, [ref]$code) | Where-Object { $_.TargetName -match 'WinCred\.Probe|wincredprobe' })
    foreach ($c in $left) { $null = [WinCredProbe.Native]::Delete($c.TargetName, $c.Type) }
    "Cleanup: $($left.Count) leftover probe credential(s) removed in the final sweep"
}
