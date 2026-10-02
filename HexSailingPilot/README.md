# $projectname$

A Valheim mod using BepInEx and Harmony.

## Development Setup

### Option 1: Environment Variable (Recommended)
Set a system environment variable:
```
VALHEIM_INSTALL=C:\Program Files (x86)\Steam\steamapps\common\Valheim
```

### Option 2: User Project File
Create `$safeprojectname$.csproj.user` with your Valheim installation path.

### Option 3: Auto-Detection
The project will automatically search common Steam library locations.

## Building

- **Debug Build**: Automatically deploys to `BepInEx\plugins\$safeprojectname$`
- **Release Build**: Creates package in `bin\Release`

## Plugin Information

- **GUID**: `com.$username$.$safeprojectname$`
- **Name**: `$projectname$`
- **Version**: `1.0.0`

## Debugging

1. Build in Debug configuration
2. Launch Valheim
3. Attach Visual Studio debugger to `valheim.exe`
4. Set breakpoints in your patches