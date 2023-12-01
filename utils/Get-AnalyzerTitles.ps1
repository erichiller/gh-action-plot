#!/bin/pwsh



# Get All Analyzer IDs in BuildLogHistory.md
[regex]::Matches((Get-Content ./BuildLogHistory.md), '[^|]+\| *\[([^\]]+)\] *\|') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique | Sort-Object

# Get Analyzer IDs and their URLs
$analyzerURLs = (Get-Content -Path ./BuildLogHistory.md -Raw).Split(0x0a) | Select-String -CaseSensitive -Pattern "\[([A-Z]{2,}[^\]]+)\]: +(.*) *" | ForEach-Object { @{ "$( $_.Matches.Groups[1].Value )" = "$( $_.Matches.Groups[2].Value )" } };

$analyzerURLs | Format-Table;

$analyzerURLs | ForEach-Object {
    $analyzerID = $_.Keys[0];
    [String] $url = $_.Values[0];
    if ($url.EndsWith('.md') ){
        
    }
}
# Get Title
# From GitHub .md
((Invoke-WebRequest -Uri https://raw.githubusercontent.com/microsoft/vs-threading/main/doc/analyzers/VSTHRD200.md).Content.Split(0x0a) | Select-String -Pattern "^# +(.*) *$").Matches.Groups[1].Value

# From Microsoft learn docs
(( Invoke-WebRequest -Uri https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2234 ).Content.Split(0x0a) | Select-String -pattern '<title>(.*)</title>').Matches.Groups[1].Value


<#
CSxxxx Index:
https://github.com/dotnet/docs/tree/main/docs/csharp/language-reference/compiler-messages
Then parse JSON:
  <script type="application/json" data-target="react-app.embeddedData">{"payload":{"allShortcutsEnabled":false,"path":"docs/csharp/language-reference/compiler-messages","repo":{"id":35890081,"defaultBranch":"main","name":"docs","ownerLogin":"dotnet","currentUserCanPush":false,"isFork":false,"isEmpty":false,"createdAt":"2015-05-19T15:13:32.000Z","ownerAvatar":"https://avatars.githubusercontent.com/u/9141961?v=4","public":true,"private":false,"isOrgOwned":true},"currentUser":null,"refInfo":{"name":"main","listCacheKey":"v0:1701397321.0","canEdit":false,"refType":"branch","currentOid":"e3ef83bbe7a3283ad5a15350da182dbf365f3618"},"tree":{"items":[{"name":"snippets","path":"docs/csharp/language-reference/compiler-messages/snippets","contentType":"directory"},{"name":"array-declaration-errors.md","path":"docs/csharp/language-reference/compiler-messages/array-declaration-errors.md","contentType":"file"},{"name":"constructor-errors.md","path":"docs/csharp/language-reference/compiler-messages/constructor-errors.md","contentType":"file"},{"name":"cs0001.md","path":"docs/csharp/language-reference/compiler-messages/cs0001.md","contentType":"file"},{"name":"

# TODO: The same logic should be possible for GitHub hosted analyzer docs.


#>
