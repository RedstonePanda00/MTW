# NCL integrated projects

This folder contains the consolidated `NCL` C# project.

## Integrated

- `NCLSource/NCLvsTW/NCLvsTW/NCLvsTW.csproj`
  - Main source project for the new `NCL` project.
  - All compile items from this project were copied into `NCLSource/NCL/NCL`.
  - The project identity was changed to `RootNamespace=NCL` and `AssemblyName=NCL`.
  - RimWorld/Unity references now use `RimWorldManagedDir`, defaulting through `MSBuildProgramFiles32` to the Steam RimWorld install.

- `NCLSource/Mechanoid Upgradation/Mechanoid Upgradation/Mechanoid Upgradation.csproj`
  - This project also defines `namespace NCL`.
  - Its only source file, `Class1.cs`, defines the same public types already present in `NCLvsTW/Class1.cs`.
  - The newer and larger `NCLvsTW/Class1.cs` is the compiled copy in the consolidated project to avoid duplicate type definitions.

- `NCLSource/NCLWorm改改/NCLWorm改/NCLWorm.csproj`
  - Integrated under `NCLSource/NCL/NCL/Worm`.
  - Source namespace changed from `NCLWorm` to `NCL.Worm`.
  - This newer Worm copy is compiled to avoid duplicate type definitions with the older `NCLWorm改` project.

- `NCLSource/Projectile Redirection2/Projectile Redirection2/Projectile Redirection2.csproj`
  - Integrated under `NCLSource/NCL/NCL/Projectiles`.
  - Source namespace changed from `NCLProjectiles` to `NCL.Projectiles`.

## Not integrated

These projects were left untouched or skipped as duplicate sources:

- `NCLSource/NCLWorm改/NCLWorm.csproj` (`namespace NCLWorm`; older duplicate of the integrated Worm source)
