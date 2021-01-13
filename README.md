AsyncFixer helps developers in finding and correcting common async/await misuses. AsyncFixer was tested with hundreds of C# apps and successfully handles many corner cases. 

AsyncFixer will work just in the IDE and work as an analyzer on every project you open in Visual Studio. It can also operate in batch mode to correct all misuses in the document, project, or solution. You can download the VSIX from here: https://visualstudiogallery.msdn.microsoft.com/03448836-db42-46b3-a5c7-5fc5d36a8308

If you want AsyncFixer to deploy as a NuGet package and work as a project-local analyzer that participates in builds, please also use the nuget package. Attaching an analyzer to a project means that the analyzer travels with the project to source control and so it's easy to apply the same rule for the team. You can download the nuget package from here: https://www.nuget.org/packages/AsyncFixer

