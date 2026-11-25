# The Barbarian — arquitectura con VContainer

Este documento define cómo crear código para **The Barbarian**. VContainer es el contenedor de dependencias y el sistema de entrada/salida por ciclo de Unity. No transforma automáticamente clases en entidades ni sistemas ECS: **entidades y sistemas deben representarse mediante abstracciones de proyecto**.

## Objetivos

- Constructor limpio y dependencias explícitas.
- Entidades independientes de MonoBehaviour cuando no necesitan Unity.
- Sistemas registrados por interfaz y ejecutados por las interfaces de ciclo de VContainer.
- Separación entre datos, comportamiento, presentación y infraestructura.
- Alcance claro para servicios globales, de juego y de escena.

## Instalación actual del proyecto

VContainer ya está declarado en `Packages/manifest.json`:

```json
"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer"
```

La integración opcional con Unity ECS requiere el package `com.unity.entities` y la definición `VCONTAINER_ECS_INTEGRATION`. Actualmente el proyecto no declara Unity Entities en el manifiesto, por lo que el patrón recomendado para empezar es VContainer + GameObjects/MonoBehaviours + sistemas de código C#.

## Conceptos principales

### Entidad

Una entidad representa una unidad que tiene identidad y estado. Puede tener tres formas:

1. **Entidad de datos o dominio**: clase/struct C# sin MonoBehaviour.
2. **Entidad Unity**: un `GameObject` con componentes MonoBehaviour inyectados.
3. **Entidad ECS**: una entidad de Unity.Entities. Esta opción requiere instalar y activar la integración ECS.

No registres todo componente de una entidad como Singleton. El modelo recomendado es:

- Datos de entidad: clase o componentes pequeños.
- Vista/animación/audio: MonoBehaviour.
- Reglas: sistemas singleton o scoped.
- Fábricas: `Func<TParam, T>` o un servicio `IEntityFactory`.

Ejemplo de entidad de dominio:

```csharp
public sealed class BarbarianEntity
{
    public int Id { get; }
    public BarbarianStats Stats { get; }

    public BarbarianEntity(int id, BarbarianStats stats)
    {
        Id = id;
        Stats = stats;
    }
}

public sealed class BarbarianStats
{
    public float Health { get; set; } = 100f;
    public float MoveSpeed { get; set; } = 5f;
}
```

La entidad contiene datos y operaciones propias. Los sistemas contienen procesos que afectan a varias entidades o coordinan servicios.

### Sistema

Un sistema es una clase con una responsabilidad pequeña y registro automático en el ciclo de VContainer. Se recomienda registrar sistemas como entry points o interfaces implementadas por ellos.

```csharp
using VContainer.Unity;

public sealed class BarbarianMovementSystem : ITickable
{
    private readonly BarbarianRegistry registry;

    public BarbarianMovementSystem(BarbarianRegistry registry)
    {
        registry = registry;
    }

    public void Tick()
    {
        // Procesar entidades activas.
    }
}
```

Regístralo en un `LifetimeScope`:

```csharp
using VContainer;
using VContainer.Unity;

public sealed class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<BarbarianRegistry>(Lifetime.Singleton);

        builder.RegisterEntryPoint<BarbarianMovementSystem>(Lifetime.Singleton);

        builder.UseEntryPoints(entryPoints =>
        {
            entryPoints.Add<BarbarianSpawnSystem>();
            entryPoints.Add<BarbarianCombatSystem>();
        });
    }
}
```

`RegisterEntryPoint<T>` registra el sistema y su dispatcher de ciclo. `UseEntryPoints` permite agrupar varios sistemas.

## Registros y lifetimes

### Lifetime.Singleton

Una instancia para toda la jerarquía del contenedor. El objeto y sus dependencias deben vivir durante toda la sesión o mientras exista el scope raíz.

Usar para:

- Configuración global.
- Catálogos de datos.
- Servicios de audio o almacenamiento.
- Repositorios globales.
- Sistemas principales del juego.

### Lifetime.Scoped

Una instancia por scope. Las instancias no se comparten entre scopes hijos.

Usar para:

- Estado de una partida.
- Sistemas propios de una escena o nivel.
- Registry de entidades de la partida.
- Servicios que deben destruirse al cambiar de escena.

### Lifetime.Transient

Una nueva instancia por resolución.

Usar para:

- Objetos temporales.
- Comandos.
- Servicios ligeros sin estado compartido.

Ejemplo:

```csharp
builder.Register<ILevelRepository, LevelRepository>(Lifetime.Singleton);
builder.Register<GameState>(Lifetime.Scoped);
builder.Register<IEnemyFactory, EnemyFactory>(Lifetime.Transient);
```

Si una clase debe resolverse por su clase concreta y por interfaces, usa:

```csharp
builder.Register<BarbarianMovementSystem>(Lifetime.Singleton)
    .AsSelf()
    .AsImplementedInterfaces();
```

## LifetimeScope

Los scopes representan el tiempo de vida de contenedores relacionados:

- Scope raíz: configuración global y persistencia.
- Scope de partida: estado, sistemas y entidades de una partida.
- Scope de escena/nivel: referencias locales a la escena.
- Scope dinámico: entidades o bloques creados temporalmente.

Ejemplo de scope de partida:

```csharp
public sealed class MatchLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<MatchState>(Lifetime.Scoped);
        builder.RegisterEntryPoint<MatchSystem>(Lifetime.Scoped);
    }
}
```

Para relacionar scopes, utiliza `ParentReference` en el Inspector o crea scopes hijos mediante la API de `LifetimeScope`.

## Inyección

### Constructor recomendado

La inyección por constructor es el método preferido:

```csharp
public sealed class HealthSystem
{
    private readonly IEventBus eventBus;

    public HealthSystem(IEventBus eventBus)
    {
        this.eventBus = eventBus;
    }
}
```

VContainer intentará inyectar una clase si tiene un constructor válido. Se puede marcar explícitamente con `[Inject]`:

```csharp
using VContainer;

public sealed class EntitySpawner
{
    private readonly IObjectResolver resolver;

    [Inject]
    public EntitySpawner(IObjectResolver resolver)
    {
        this.resolver = resolver;
    }
}
```

`[Inject]` también puede utilizarse en métodos, propiedades y campos. Se recomienda usarlo principalmente en MonoBehaviours o para declarar constructor único de forma explícita.

Ejemplo MonoBehaviour:

```csharp
using UnityEngine;
using VContainer;

public sealed class BarbarianView : MonoBehaviour
{
    [Inject]
    private BarbarianHealth health;

    [Inject]
    public void Construct(BarbarianHealth injectedHealth)
    {
        health = injectedHealth;
    }
}
```

No mezcles resolución manual y constructor dentro del mismo componente sin necesidad.

### Atributos disponibles

- `[Inject]`: marca constructor, método, campo o propiedad como candidato a inyección.
- `[InjectIgnore]`: evita que VContainer procese una clase/struct/interfaz como objeto inyectable.
- `[Preserve]`: impide que herramientas de linkado/compilación eliminen símbolos marcados.
- `[Key(objectKey)]`: identifica un parámetro de campo, propiedad o constructor registrado mediante `Keyed(...)`.

Ejemplo de clave:

```csharp
builder.RegisterInstance(myConfig).As<IEnemyConfig>().Keyed("orc");

public EnemyService(
    [Key("orc")] IEnemyConfig config)
{
}
```

El soporte concreto de claves debe añadirse solo cuando exista una necesidad real de varios registros del mismo tipo.

## API de registro y resolución

Clases y métodos importantes:

- `LifetimeScope`: scope basado en MonoBehaviour.
- `IContainerBuilder`: registra servicios y factories.
- `IObjectResolver`: obtiene o crea instancias.
- `IScopedObjectResolver`: añade el contenedor padre y raíz.
- `ContainerBuilderExtensions`: registro de tipos, instancias y factories.
- `RegistrationBuilder`: asigna interfaces, parámetros y claves.
- `Lifetime`: `Transient`, `Singleton` o `Scoped`.
- `IInstaller`: instalación reutilizable con `Install(IContainerBuilder builder)`.
- `ActionInstaller`: envoltorio para una configuración declarada como lambda.

Registros habituales:

```csharp
builder.Register<StatsService>(Lifetime.Singleton);
builder.Register<IStatsService, StatsService>(Lifetime.Singleton);
builder.RegisterInstance(config).As<IConfig>();
builder.Register(c => new MatchState(c.Resolve<IConfig>()), Lifetime.Scoped);
builder.RegisterFactory<EnemyType, EnemyEntity>(enemyType => CreateEnemy(enemyType));

builder.RegisterDisposeCallback(container =>
{
    // Limpieza al destruir el scope.
});
```

Resolución manual:

```csharp
IObjectResolver resolver = scope.Container;
BarbarianRegistry registry = resolver.Resolve<BarbarianRegistry>();
```

Evita `Resolve<T>()` dentro de constructor salvo para factories o técnicas explícitas. Pide dependencias por interfaz o tipo concreto.

## Creación de entidades Unity

No instancies prefabs con `Instantiate()` si necesitan dependencias del contenedor. Usa las extensiones de VContainer:

```csharp
using VContainer;
using VContainer.Unity;
using UnityEngine;

public sealed class BarbarianSpawner
{
    private readonly IObjectResolver resolver;
    private readonly BarbarianView prefab;

    public BarbarianSpawner(IObjectResolver resolver, [Inject] BarbarianView prefab)
    {
        this.resolver = resolver;
        this.prefab = prefab;
    }

    public BarbarianView Spawn(Vector3 position)
    {
        return resolver.Instantiate(prefab, position, Quaternion.identity);
    }
}
```

Para los parámetros configurados por Inspector, se puede registrar el prefab como componente:

```csharp
builder.RegisterComponentInNewPrefab(prefab, Lifetime.Scoped);
```

También existen:

- `resolver.InjectGameObject(gameObject)`.
- `resolver.Instantiate(prefab)`.
- `resolver.Instantiate(prefab, position, rotation)`.
- `resolver.Instantiate(prefab, parent, false)`.
- `builder.RegisterComponentInHierarchy<T>()`.
- `builder.RegisterComponentOnNewGameObject<T>(Lifetime, name)`.
- `builder.RegisterComponentInNewPrefab(prefab, Lifetime)`.
- `builder.RegisterComponent(componentInstance)`.

Cada componente MonoBehaviour debe funcionar como puente entre Unity y las clases de dominio. Evita que contenga todo el comportamiento de la entidad.

## Ciclos de ejecución de sistemas

Las interfaces siguientes se encuentran en `VContainer.Unity`:

| Interfaz | Método | Momento |
|---|---|---|
| `IInitializable` | `Initialize()` | Inicio de la construcción, antes de IStartable |
| `IPostInitializable` | `PostInitialize()` | Después de todos los IInitializable |
| `IStartable` | `Start()` | Fase Startup del PlayerLoop |
| `IPostStartable` | `PostStart()` | Después de IStartable |
| `IAsyncStartable` | `StartAsync(CancellationToken)` | Startup asincrónico |
| `IFixedTickable` | `FixedTick()` | FixedUpdate |
| `IPostFixedTickable` | `PostFixedTick()` | Después de FixedUpdate |
| `ITickable` | `Tick()` | Update |
| `IPostTickable` | `PostTick()` | Después de Update |
| `ILateTickable` | `LateTick()` | LateUpdate |
| `IPostLateTickable` | `PostLateTick()` | Después de LateUpdate |

No implementes varias fases salvo que sea necesario. Para lógica normal de juego, usa `ITickable` o `IFixedTickable`.

Orden recomendado:

1. `IInitializable`: construir referencias, suscripciones y estado inicial.
2. `IStartable`: comprobar estado ya construido.
3. `ITickable`: lógica por frame.
4. `IFixedTickable`: física.
5. `ILateTickable`: cámara o sincronización posterior.

Para tareas que deben cancelar al destruir el scope, recibe `CancellationToken` en `IAsyncStartable` y libera suscripciones en `IDisposable` si el sistema implementa esa interfaz.

## Registro de sistemas por responsabilidad

Ejemplo recomendado:

```csharp
public sealed class MatchLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<MatchState>(Lifetime.Scoped);
        builder.Register<BarbarianRegistry>(Lifetime.Scoped);

        builder.UseEntryPoints(entryPoints =>
        {
            entryPoints.Add<SpawnSystem>();
            entryPoints.Add<MovementSystem>();
            entryPoints.Add<CombatSystem>();
            entryPoints.Add<CleanupSystem>();
        });
    }
}

public sealed class SpawnSystem : IInitializable, IDisposable
{
    public void Initialize() { }

    public void Dispose() { }
}

public sealed class MovementSystem : IFixedTickable
{
    public void FixedTick() { }
}

public sealed class CombatSystem : ITickable
{
    public void Tick() { }
}
```

Los sistemas deben definir una única responsabilidad. Pueden resolver todas sus dependencias por constructor, pero no deben crear dependencias con `new` si son sustituibles o poseen estado compartido.

## Recomendación de carpetas

```text
Assets/TheBarbarian/
├── Runtime/
│   ├── Domain/                 Datos y reglas independientes de Unity
│   ├── Entities/               Clases, registries y fábricas
│   ├── Systems/                Entry points y sistemas por responsabilidad
│   ├── Presentation/           MonoBehaviours y vistas
│   ├── Services/               Interfaces y implementaciones técnicas
│   ├── Configuration/          ScriptableObjects y parámetros
│   ├── Events/                 Mensajes y event bus
│   └── Scopes/                 LifetimeScope e IInstaller
├── Prefabs/
├── ScriptableObjects/
└── Scenes/
```

Convención sugerida:

- Una clase pública por archivo.
- Nombres terminados por su responsabilidad: `*Entity`, `*System`, `*Service`, `*Factory`, `*Registry`, `*LifetimeScope`, `*Installer`, `*View`.
- Interfaces técnicas en `Services/Interfaces` o junto al consumidor.
- Implementación en una clase separada para facilitar pruebas.

## Workflow para crear una entidad

1. Define el contrato, estado y datos mínimos de la entidad.
2. Crea una entidad o modelo de dominio sin MonoBehaviour si no necesita Unity.
3. Crea una vista MonoBehaviour solo para Transform, Animator, colliders y otros componentes Unity.
4. Añade una interfaz o clase de fábrica si la entidad se crea dinámicamente.
5. Registra dependencias compartidas en el scope adecuado.
6. Inyecta dependencias en la fábrica o en la vista.
7. Instancia prefabs mediante `IObjectResolver.Instantiate(...)` o la API de componentes.
8. Registra la entidad en un Registry solo si otros sistemas deben localizarla.
9. Elimina referencias y suscripciones al desactivar o destruir la entidad.
10. Añade pruebas unitarias antes de integrar nueva lógica compleja.

Ejemplo de diseño:

```text
BarbarianEntity       Estado y operaciones de dominio
BarbarianView         Transform, animación y colisiones
BarbarianRegistry     Colección compartida de entidades activas
BarbarianFactory      Creación e inicialización
BarbarianStatsSystem  Modificación de estadísticas
BarbarianMovementSystem Movimiento
BarbarianCombatSystem  Ataques y daño
```

## Workflow para crear un sistema

1. Escribe una interfaz solo si existen varias implementaciones o sustitutos de prueba.
2. Elige una única responsabilidad.
3. Declara las dependencias por constructor.
4. Implementa la interfaz de ciclo adecuada.
5. Registra el sistema con `Lifetime.Singleton` o `Lifetime.Scoped`.
6. Usa `IInitializable` para preparar suscripciones o estado inicial.
7. Evita suscripciones duplicadas.
8. Implementa `IDisposable` si crea suscripciones, tareas o recursos propios.
9. Mantén físico en `IFixedTickable` y cámara/UI en `ILateTickable`.
10. Comprueba el orden respecto a sistemas que leen o modifican los mismos datos.

## Manejo de errores y disposición

- Usa `EntryPointExceptionHandler` o `RegisterEntryPointExceptionHandler(...)` para interceptar excepciones de entry points.
- Los sistemas deben capturar errores locales solo si pueden recuperarse.
- Implementa `IDisposable` para suscripciones, objetos nativos y recursos administrados.
- Usa `RegisterDisposeCallback(...)` para limpieza asociada a la construcción del scope.
- El contenedor dispone instancias con `IDisposable` de acuerdo con su lifetime y scope.

## Unity ECS opcional

Si se añade Unity Entities, VContainer ofrece tipos condicionados por `VCONTAINER_ECS_INTEGRATION`:

- `WorldRegistrationBuilder`.
- `WorldConfigurationHelper`.
- `SystemRegistrationBuilder`.
- `UnmanagedSystemRegistrationBuilder`.
- `UnmanagedSystemReference`.
- Métodos de registro de Worlds y ComponentSystemBase.

No se deben mezclar ECS, datos tradicionales y MonoBehaviour sin una regla clara de propiedad. Una entidad ECS debe evitar guardar referencias a MonoBehaviours o contenedores dentro de componentes ECS cuando puede expresarse mediante identificadores.

Instalación de referencia:

1. Añadir el package `com.unity.entities`.
2. Verificar que el asmdef de VContainer puede definir `VCONTAINER_ECS_INTEGRATION` al detectar el paquete.
3. Registrar Worlds y sistemas solo después de confirmar que las clases ECS están disponibles.
4. Mantener la capa de fábrica y presentación fuera de los sistemas ECS puros.

## Reglas prácticas

- No usar el Service Locator como arquitectura principal.
- No guardar `IObjectResolver` en entidades si pueden recibir sus dependencias explícitamente.
- No registrar MonoBehaviours mediante constructor; usa registro de componentes.
- No usar Singleton para datos que pertenecen a una partida o escena.
- No usar `MonoBehaviour.Update()` para sistemas que ya están registrados como entry points.
- No modificar el contenedor después de construirlo.
- No crear referencias circulares entre sistemas.
- No resolver `IEnumerable<T>` si el sistema debe conocer exactamente una implementación.
- Preferir interfaces pequeñas y dependencias por constructor.
- Conservar la jerarquía explícita de scopes antes de aumentar la cantidad de factories.

## Lista de comprobación antes de crear código

- [ ] ¿Es entidad, sistema, servicio o presentación?
- [ ] ¿Qué lifetime necesita?
- [ ] ¿Qué interfaces de ciclo implementa?
- [ ] ¿Qué entidades o recursos registra?
- [ ] ¿Debe persistir entre escenas?
- [ ] ¿Qué scope es su propietario?
- [ ] ¿Implementa `IDisposable` o `IAsyncStartable`?
- [ ] ¿Tiene una responsabilidad pequeña y comprobable?
- [ ] ¿Puede instanciarse sin usar `Resolve<T>()` manualmente?
- [ ] ¿La creación de prefabs pasa por `IObjectResolver.Instantiate(...)`?

