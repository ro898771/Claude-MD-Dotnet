---
name: Dino-Dev-Dotnet_Wpf_Boilerplate
description: Boilerplate for a .NET 9 C# WPF tool. Use when starting a WPF project or porting Python to C#.
---

# .NET 9 WPF boilerplate (Split-Spread style)

Reference source: `C:\Users\ro898771\Documents\Split-Spread-Compiler\Split-Spread` (released as `C:\Dinosaur-List\Software_Downloaded\Split Spread-V-4.0.0.0_A-RoeyYee`).
Worked small example (Python PySide6 -> WPF port): `C:\Users\ro898771\Documents\Claude-MD-DotNet`.
Ready-to-copy files: `~/.claude/skills/init-project-csharp-wpf/template/` (rename namespace `GuQc` and exe `GU-QC`). Also see skill `init-project-csharp-wpf` for charts, differential testing and speed tips.

## Project layout
```
<Name>/
  <Name>.csproj        net9.0-windows, UseWPF, Nullable, ImplicitUsings, NuGetAudit=false, no NuGet packages if avoidable
  App.xaml(.cs)        theme: brushes + Btn/BtnPrimary/BtnView styles, DataGrid look, icon geometries
  MainWindow.xaml(.cs) UI + code-behind (title shows version: Assembly version .ToString(3))
  Services/            no WPF types: AppPaths, stores, parsers, workers
  Views/               secondary windows built in code (e.g. TextViewWindow), custom controls
  icon.ico             build.bat refuses to build without it
  build.bat            one-file publish
  README.md            Overview / Objective / Feature List / Update List (skill write-readme)
  .gitignore           bin/ obj/ publish/ release/ .vs/ *.user
```

## csproj essentials
- `OutputType WinExe`, `TargetFramework net9.0-windows`, `ApplicationIcon icon.ico`, `<Resource Include="icon.ico" />` (window `Icon="icon.ico"`).
- Version block, raised on every release (+ a line in README Update List):
  `Version 1.0.0`, `AssemblyVersion/FileVersion/InformationalVersion 1.0.0.0`, `IncludeSourceRevisionInInformationalVersion false`, `Product`, `AssemblyTitle`, `Description`, `Authors`, `Copyright`.
- Embed big/static helpers with `<EmbeddedResource ... LogicalName="X/%(Filename)%(Extension)" />`; a file with the same name beside the exe may override it.

## Theme (App.xaml)
White cards on `#F4F7FB`, ink `#1F2937`, muted `#64748B`, lines `#E2E8F0`, single accent `#2563EB`; rounded 4-10 px controls, Segoe UI 12, thin scrollbars, StreamGeometry vector icons (24x24 outline). Coloured accents per row: `#E0E7FF #DCFCE7 #FEF3C7 #FCE7F3 #DBEAFE #EDE9FE`.

## Paths and settings
- `AppPaths.Base` = folder of `Environment.ProcessPath` (NOT `AppContext.BaseDirectory`, which is a temp dir for single-file exes); fall back to `AppContext.BaseDirectory` when running under `dotnet`.
- User settings go in `<exe folder>\setting files\*.json` (create the folder on first save, UTF-8 without BOM, `System.Text.Json`, `WriteIndented`). Never write beside the source.
- Work folders (e.g. `Storage`, `Output`) also live next to the exe.

## build.bat (copy from the template and replace the exe name)
`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish`. Renames a locked running exe aside first (retry 15x), deletes old `*.old*.exe`, prints the stamped version, `build.bat run` launches it. For automation run the `dotnet publish` line directly (build.bat ends with `pause`).

## Release folder (what is handed out)
Name: `<Product>-V-<x.x.x.x>_A-<Author>` e.g. `Split Spread-V-4.0.0.0_A-RoeyYee`, containing:
```
<Exe>.exe                single-file self-contained exe
icon.ico
setting files/*.json     default settings
<data folders>           e.g. Storage/, template.pptx, only if the app reads them beside the exe
```
Steps: bump version -> `build.bat` -> copy `publish/<Exe>.exe`, icon, settings, data into `release/<folder>/` -> launch the exe once (5 s, check it did not exit).

## Porting a Python/PySide tool
1. Read all source and list every UI action and file effect (feature checklist, "exactly the same").
2. Python `Path(__file__).parent` data/config -> `AppPaths` + `setting files`.
3. QTableWidget -> WPF `DataGrid` (ObservableCollection of row objects with INotifyPropertyChanged, `ICollectionView.Filter` for search); QDialog -> Window built in code; stylesheet -> App.xaml styles; `QFileDialog.getExistingDirectory` -> `OpenFolderDialog`.
4. Keep behaviours that look odd (e.g. appending `skills` to the sync target, rewriting only the `description:` frontmatter line, `\n` line endings, UTF-8 no BOM).
5. Build, smoke test, package the release folder.

## Gotchas
- `UniformGrid`, `DataGridColumnHeader` live in `System.Windows.Controls.Primitives`.
- `OpenFolderDialog` is `Microsoft.Win32` (.NET 8+).
- Keep secondary windows hidden-not-closed when they hold state.
- Do NOT set `<InvariantGlobalization>true</InvariantGlobalization>` in a WPF csproj: the exe starts with "Cannot find non-neutral culture related to 'en-us'". A running exe with a modal error dialog still reports `HasExited=False`, so check `MainWindowTitle` in the smoke test.
