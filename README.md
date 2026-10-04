# Ejemplo simple de BFF en .NET 10

Un **Backend for Frontend (BFF)** prepara una API para las necesidades de una interfaz concreta. En este ejemplo, la página web hace una sola petición para obtener nombre, precio y disponibilidad. El BFF consulta catálogo e inventario por HTTP y transforma las respuestas al modelo de la pantalla.

## Arquitectura

```mermaid
flowchart LR
    Web["Página web"] -->|"GET /bff/products/1"| BFF["Bff.Api · Facade"]
    BFF --> Catalog["CatalogClient · Adapter"]
    BFF --> Inventory["InventoryClient · Adapter"]
    Catalog -->|HTTP| CatalogApi["API de catálogo"]
    Inventory -->|HTTP| InventoryApi["API de inventario"]
    subgraph Demo.Backend
        CatalogApi
        InventoryApi
    end
```

Las dos APIs simuladas viven en `Demo.Backend` para ejecutar el ejemplo con solo dos procesos. Sus URLs se configuran por separado: puedes sustituirlas por servicios reales sin cambiar la fachada. Los dos proyectos no se referencian entre sí; se comunican por HTTP.

## Ejecutar

Requisito: SDK de .NET 10. Las aplicaciones no requieren base de datos. `Bff.Api` usa FluentValidation 12.1.1. Los proyectos de pruebas usan MSTest.Sdk, Moq y Microsoft.AspNetCore.Mvc.Testing; la suite de aserciones fluidas añade FluentAssertions 7.2.0. `NuGet.Config` configura nuget.org para restaurar estos paquetes. La primera restauración requiere conexión si no están en la caché local.

Desde la carpeta raíz, compila:

```powershell
dotnet restore BffExample.slnx --configfile NuGet.Config
dotnet build BffExample.slnx --no-restore
```

En una terminal, inicia las APIs simuladas:

```powershell
dotnet run --project src/Demo.Backend --launch-profile http --no-restore
```

En otra terminal, inicia el BFF:

```powershell
dotnet run --project src/Bff.Api --launch-profile http --no-restore
```

Abre [la página de productos](http://localhost:5100). El backend escucha en `http://localhost:5101`. Detén cada proceso con `Ctrl+C`.

## Patrones utilizados

| Patrón o técnica | Archivo | Función |
| --- | --- | --- |
| BFF | `Bff.Api` | Ofrece un contrato específico para esta pantalla web. |
| Facade | `Services/ProductPageService.cs` | Oculta la coordinación entre catálogo e inventario detrás de `GetAsync`. |
| Adapter | `Clients/CatalogClient.cs`, `Clients/InventoryClient.cs` | Convierte las APIs HTTP externas en interfaces C# para la fachada. |
| Agregación | `Services/ProductPageService.cs` | Ejecuta dos consultas en paralelo y compone una sola respuesta. |
| Inyección de dependencias | `Program.cs` | Conecta interfaces y clientes tipados gestionados por `IHttpClientFactory`. |
| DTO | `Models/ProductPageDto.cs` | Separa el contrato de la pantalla de los modelos internos de los backends. |

Para seguir el código, empieza por `ProductEndpoints`, continúa con `ProductPageService` y revisa los dos clientes HTTP. Las interfaces permiten sustituir los clientes por dobles en pruebas.

Cada clase, interfaz y `record` está en un archivo con su mismo nombre. Los `using` explícitos se declaran después del `namespace`, incluidos los dos `Program.cs`. Los modelos del backend están en `Demo.Backend/Models`.

Los archivos se agrupan por función en carpetas comunes:

```text
src/
├── Bff.Api/
│   ├── Clients/           # Adaptadores HTTP
│   ├── Configuration/     # Opciones de conexión
│   ├── Endpoints/         # Rutas del BFF
│   ├── ExceptionHandlers/ # Tratamiento de errores
│   ├── Interfaces/        # Contratos de clientes y servicios
│   ├── Models/            # Records y DTO
│   ├── Services/          # Fachada y agregación
│   ├── Validators/        # Reglas de FluentValidation
│   ├── wwwroot/           # Página web
│   └── Program.cs
└── Demo.Backend/
    ├── Endpoints/         # APIs simuladas
    ├── Models/            # Records del backend
    └── Program.cs
```

Las pruebas están en `tests/Bff.Api.Tests` y `tests/Bff.Api.FluentTests`, agrupadas en `Services`, `Clients`, `Configuration`, `ExceptionHandlers`, `Integration`, `Validators` y `Mocks`.

La fachada adapta datos para la presentación; el catálogo conserva el precio y el inventario conserva las existencias. `CanBuy` indica disponibilidad visual en esta demo, no autoriza una compra ni reserva stock. `SupplierCost` es un dato interno simulado: llega desde catálogo, pero no se publica en el BFF.

## Validaciones con FluentValidation

`ProductIdValidator`, en `Bff.Api/Validators`, valida el identificador de entrada con la regla `id > 0`. Se registra como `IValidator<int>` en `Program.cs` y se comparte entre los tres componentes, que mantienen sus firmas actuales:

| Componente | Dato de entrada | Cuándo se valida |
| --- | --- | --- |
| `CatalogClient.GetProductAsync` | `id` | Antes de enviar la petición HTTP al catálogo. |
| `InventoryClient.GetStockAsync` | `productId` | Antes de enviar la petición HTTP al inventario. |
| `ProductPageService.GetAsync` | `id` | Antes de lanzar las consultas a los clientes. |

La validación se ejecuta de forma explícita y recibe el token de cancelación. Por ejemplo, al entrar en `CatalogClient.GetProductAsync`:

```csharp
await validator.ValidateAndThrowAsync(id, cancellationToken);
```

Un identificador cero o negativo lanza `ValidationException` sin consultar las dependencias. `BackendExceptionHandler` la convierte en **HTTP 400** con el título `El id debe ser mayor que cero.`. El endpoint delega esta comprobación en el servicio, por lo que la misma regla protege las llamadas HTTP y las invocaciones directas desde código.

FluentValidation se aplica únicamente a estos parámetros de entrada. `CatalogProduct`, `InventoryStock` y `ProductPageDto` son resultados y no tienen validadores. Se mantienen el tratamiento de errores HTTP, la deserialización JSON y los casos de respuesta nula o inventario ausente.

## Por qué ProductPageService consulta en paralelo

En este BFF se mantienen las consultas a catálogo e inventario **en paralelo** porque ambas son independientes: solo necesitan el `id` recibido en la petición, y la pantalla necesita los dos resultados. Así se reduce el tiempo de respuesta.

```csharp
var productTask = catalogClient.GetProductAsync(id, cancellationToken);
var stockTask = inventoryClient.GetStockAsync(id, cancellationToken);

await Task.WhenAll(productTask, stockTask);
```

Las dos llamadas comienzan antes de esperar sus resultados. Si catálogo tarda 200 ms e inventario 300 ms, la comparación aproximada es:

| Ejecución | Tiempo de respuesta |
| --- | --- |
| Secuencial: primero catálogo, después inventario | 500 ms; los tiempos se suman. |
| En paralelo | 300 ms; domina la consulta más lenta. |

Estos tiempos son ilustrativos y no incluyen el trabajo adicional de composición y envío de la respuesta.

La contrapartida es que se consulta inventario incluso si el producto no existe. Con llamadas secuenciales se podría comprobar primero el 404 del catálogo y evitar esa segunda llamada.

La ejecución secuencial sería más adecuada si inventario necesitara un dato obtenido del catálogo, o si fueran frecuentes las consultas a productos inexistentes y se quisiera reducir las llamadas innecesarias al inventario. Para el funcionamiento actual, orientado a mostrar fichas de productos existentes, se mantiene la ejecución en paralelo.

## Probar la API

```powershell
Invoke-RestMethod http://localhost:5100/bff/products/1
```

Respuesta:

```json
{
  "id": 1,
  "name": "Portátil",
  "description": "Portátil de 14 pulgadas para trabajar y estudiar.",
  "price": 899.90,
  "displayPrice": "899,90 EUR",
  "availability": "Disponible",
  "canBuy": true
}
```

| Petición o situación | Resultado |
| --- | --- |
| `/bff/products/1` | 200, disponible. |
| `/bff/products/2` | 200, agotado; `canBuy` es `false`. |
| `/bff/products/999` | 404, producto inexistente. |
| `/bff/products/0` | 400, identificador inválido. |
| Backend detenido o respuesta inválida | 502. |
| Backend tarda más de 3 segundos | 504. |

Los errores usan `ProblemDetails`. Los detalles técnicos se registran en el servidor. La cancelación de la petición se propaga a las llamadas HTTP. La respuesta requiere las dos fuentes: si alguna falla, no se devuelve una ficha parcial, incluso si la otra devuelve un 404.

`/health` confirma que cada proceso responde; no comprueba sus dependencias.

## Verificación automática

### Pruebas de Bff.Api

Se conservan dos suites con los mismos 65 casos para comparar las aserciones:

| Proyecto | Aserciones | Ejecutor |
| --- | --- | --- |
| `Bff.Api.Tests` | `Assert`, `CollectionAssert` y `StringAssert` de `Microsoft.VisualStudio.TestTools.UnitTesting`. | MSTest con Microsoft.Testing.Platform. |
| `Bff.Api.FluentTests` | FluentAssertions: `Should().Be()`, `BeEquivalentTo()`, `ThrowAsync()` y `ThrowExactlyAsync()`. | MSTest con Microsoft.Testing.Platform. |

Se mantiene una suite con UnitTesting y otra equivalente con FluentAssertions. MSTest sigue descubriendo y ejecutando las pruebas de ambas suites. Las dos mantienen `[TestMethod]`, los bloques Arrange/Act/Assert y los mocks estrictos.

Al comparar un objeto obtenido, las dos suites declaran `expectedResult` en Arrange, antes de ejecutar la operación. Los records se comparan completos con `Assert.AreEqual(expectedResult, actualResult)` o `actualResult.Should().BeEquivalentTo(expectedResult)`. Los resultados nulos también tienen un expected explícito. En respuestas HTTP se agrupan el estado, el tipo de contenido y los datos estables del cuerpo; los errores no comparan identificadores de petición variables. Las excepciones se verifican por su tipo y, cuando corresponde, con `expectedException` o `expectedStatusCode` definidos en Arrange.

Por ejemplo, en las pruebas del cliente de catálogo:

```csharp
// Arrange
var expectedResult = new CatalogProduct(4, "Teclado", "Compacto", 49.90m, "EUR");
// Configurar el cliente y el transporte HTTP con mocks estrictos.

// Act
var product = await client.GetProductAsync(4, CancellationToken.None);

// Assert (UnitTesting)
Assert.AreEqual(expectedResult, product);
// En la suite FluentAssertions: product.Should().BeEquivalentTo(expectedResult);
```

```powershell
dotnet restore BffExample.slnx --configfile NuGet.Config
dotnet test --project tests/Bff.Api.Tests/Bff.Api.Tests.csproj --no-restore
dotnet test --project tests/Bff.Api.FluentTests/Bff.Api.FluentTests.csproj --no-restore
```

Para ejecutar las dos suites en una llamada:

```powershell
dotnet test --solution BffExample.slnx --no-restore
```

Por ejemplo, una aserción con UnitTesting:

```csharp
Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
```

En la suite con FluentAssertions:

```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
```

Los dos proyectos usan **MSTest.Sdk con Microsoft.Testing.Platform**. `global.json` selecciona ese runner para `dotnet test` en .NET 10. Todas las pruebas llevan `[TestMethod]` y los bloques `// Arrange`, `// Act`, `// Assert`. `[DataRow]` permite ejecutar varios casos con el mismo método.

| Carpeta | Comportamientos comprobados |
| --- | --- |
| `Services` | Validación del identificador de entrada sin consultar clientes cuando es inválido, agregación, precio para la pantalla, disponibilidad, producto inexistente, inventario ausente, errores, consultas en paralelo y propagación de cancelación. |
| `Clients` | Validación del identificador de entrada sin enviar HTTP cuando es inválido, ruta HTTP, deserialización, 404, errores HTTP, JSON inválido, cuerpo JSON nulo y cancelación. |
| `Validators` | Identificadores positivos, cero, negativos y los límites `int.MinValue` e `int.MaxValue`; propiedad y mensaje de error esperados. |
| `Configuration` | URLs absolutas HTTP(S) y barra final obligatoria. |
| `ExceptionHandlers` | No escribir una respuesta de error cuando el cliente cancela la petición. |
| `Integration` | DTO público, exclusión de datos internos, 400 por entradas inválidas sin llamadas a backends, 404/502/504/500 con ProblemDetails, inventario ausente, health y página HTML. |

**Las llamadas a `Demo.Backend` se resuelven con mocks de Moq, siempre con `MockBehavior.Strict`.** La fachada se prueba con mocks de `ICatalogClient` e `IInventoryClient`. Los clientes HTTP y las pruebas de integración usan un mock de `HttpMessageHandler`, con setups para el envío y la liberación del handler. Las pruebas de integración levantan el BFF en memoria con `WebApplicationFactory` y ejecutan los clientes y la fachada reales, usando el transporte mockeado. No requieren iniciar el backend, abrir puertos ni usar una base de datos.

El validador se prueba con instancias reales, sin mockear sus reglas. Cada caso declara el resultado esperado en Arrange y compara `IsValid`, la propiedad y el mensaje de error cuando corresponde. También se comprueba que los clientes y la fachada ejecuten la validación antes de llamar a las dependencias, que la API traduzca una entrada inválida a HTTP 400 y que los resultados se devuelvan sin validación de salida.

El timeout 504 se simula mediante una cancelación del transporte. Se comprueba su traducción a ProblemDetails sin esperar los tres segundos del timeout real. La página HTML se verifica como recurso estático; no se ejecuta JavaScript en un navegador.

### Prueba de humo con los dos procesos

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/smoke-test.ps1
```

El script compila, inicia procesos temporales en los puertos 5180 y 5181, comprueba la agregación, la exclusión de datos internos, los errores 400/404, la página HTML y el 502 al detener su backend. Finalmente detiene los procesos que ha creado. Los puertos se pueden cambiar con `-BffPort` y `-BackendPort`. La prueba no cubre el timeout 504 ni ejecuta JavaScript en un navegador.

## Configuración y alcance

Las direcciones están en `src/Bff.Api/appsettings.json`, sección `Backends`, y deben terminar en `/`. Puedes sobrescribirlas mediante variables de entorno:

```powershell
$env:Backends__CatalogBaseUrl = "http://localhost:5101/"
$env:Backends__InventoryBaseUrl = "http://localhost:5101/"
```

Ejemplo didáctico con datos en memoria y HTTP local. No incluye autenticación, compras ni persistencia. Para un despliegue real habría que incorporar HTTPS y seguridad según el tipo de cliente. Se mantiene una sola API de BFF organizada por carpetas para que los patrones sean fáciles de seguir.

Referencias oficiales: [patrón BFF](https://learn.microsoft.com/es-es/azure/architecture/patterns/backends-for-frontends), [clientes HTTP con IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory), [validación asíncrona con FluentValidation](https://docs.fluentvalidation.net/en/latest/async.html) y [pruebas de validadores](https://docs.fluentvalidation.net/en/latest/testing.html).
