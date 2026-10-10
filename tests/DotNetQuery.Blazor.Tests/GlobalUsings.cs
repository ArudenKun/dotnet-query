global using Bunit;
global using DotNetQuery.Core;
global using Microsoft.AspNetCore.Components;
global using R3;
global using R3.Collections;
// Alias takes precedence over the Bunit namespace import, resolving the
// ambiguity between Bunit.TestContext and TUnit.Core.TestContext in
// TUnit's generated hook code.
global using TestContext = TUnit.Core.TestContext;

[assembly: Timeout(5_000)]
