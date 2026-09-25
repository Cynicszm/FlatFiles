; Unshipped analyser release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FF1001 | FlatFiles.Mapping | Info | A member has no generated accessor in one direction, so a mapping that uses it that way builds one at run time.
