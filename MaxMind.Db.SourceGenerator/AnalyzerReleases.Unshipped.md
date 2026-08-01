; This file lists analyzer rules that have not been released yet.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MMDBSG001 | MaxMind.Db.SourceGenerator | Warning | Model type is inaccessible to generated code
MMDBSG002 | MaxMind.Db.SourceGenerator | Warning | Model constructor is inaccessible to generated code
MMDBSG003 | MaxMind.Db.SourceGenerator | Warning | Model property cannot be assigned by generated code
MMDBSG004 | MaxMind.Db.SourceGenerator | Warning | Open generic models are unsupported
MMDBSG005 | MaxMind.Db.SourceGenerator | Warning | Model contains duplicate map keys
MMDBSG006 | MaxMind.Db.SourceGenerator | Warning | Property model lacks an accessible parameterless constructor
MMDBSG007 | MaxMind.Db.SourceGenerator | Warning | Model has multiple deserialization constructors
MMDBSG008 | MaxMind.Db.SourceGenerator | Warning | Collection type cannot be generated
MMDBSG009 | MaxMind.Db.SourceGenerator | Warning | Source generation requires C# 9 or later
MMDBSG010 | MaxMind.Db.SourceGenerator | Warning | Required members need a SetsRequiredMembers constructor
MMDBSG011 | MaxMind.Db.SourceGenerator | Warning | Derived MapKey attributes cannot be evaluated
