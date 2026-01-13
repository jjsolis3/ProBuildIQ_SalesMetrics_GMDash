# Tools Folder

This folder contains external tools and executables required by the application.

## Rotativa (PDF Generation)

The `/Tools/Rotativa/` folder should contain the `wkhtmltopdf.exe` executable for PDF generation.

### Setup Instructions

If the `wkhtmltopdf.exe` file is missing, download it from the Rotativa package:

**Option 1: NuGet Package (Recommended)**
```bash
# The executable is included in the Rotativa.AspNetCore NuGet package
# and will be automatically restored during build
```

**Option 2: Manual Download**
1. Download wkhtmltopdf from: https://wkhtmltopdf.org/downloads.html
2. Place `wkhtmltopdf.exe` in `/Tools/Rotativa/`
3. Ensure the file is approximately 40MB in size

### Why is this in Tools instead of wwwroot?

Previously, the 40MB `wkhtmltopdf.exe` was stored in `wwwroot/Rotativa/`, which caused several issues:
- **Memory bloat** during debugging (Visual Studio loads all wwwroot files)
- **Security risk** (executable was accessible via web URLs)
- **Performance impact** (served by static files middleware)
- **Source control bloat** (40MB binary file in Git)

By moving it to `/Tools/`, we:
- ✅ Reduce debugger memory usage
- ✅ Keep executables out of the web root
- ✅ Improve build and deploy performance
- ✅ Exclude from source control via .gitignore

### Configuration

The Rotativa path is configured in `Program.cs`:
```csharp
var rotativaPath = Path.Combine(app.Environment.ContentRootPath, "Tools", "Rotativa");
Rotativa.AspNetCore.RotativaConfiguration.Setup(rotativaPath);
```

---

## Adding New Tools

When adding new external tools or executables:
1. Create a subfolder under `/Tools/` (e.g., `/Tools/YourTool/`)
2. Add the folder to `.gitignore` if it contains large binaries
3. Create a README section explaining setup
4. Configure the path in your application code
