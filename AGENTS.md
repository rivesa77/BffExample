# Convenciones del proyecto

- Cada clase, interfaz y record tiene un archivo con su mismo nombre.
- Agrupar archivos por función y alinear namespaces con las carpetas.
- Los using explícitos van después del namespace.
- Las pruebas usan `[TestMethod]` y bloques `// Arrange`, `// Act`, `// Assert`.
- Mockear las llamadas a Demo.Backend con Moq; las pruebas no requieren servicios externos.

# Ejecutar pruebas

MSTest.Sdk usa Microsoft.Testing.Platform. `global.json` selecciona este runner para `dotnet test` en .NET 10. Mantener habilitados los analizadores de MSTest y no usar opciones exclusivas de VSTest.

```powershell
dotnet restore BffExample.slnx --configfile NuGet.Config
dotnet test --project tests/Bff.Api.Tests/Bff.Api.Tests.csproj --no-restore
```

Validar también la compilación de la solución cuando se cambien referencias o código de las aplicaciones:

```powershell
dotnet build BffExample.slnx --no-restore
```
