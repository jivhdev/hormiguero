# MQD v2 — Registro de decisiones

Documento de trabajo mientras se define MQD v2. Vive en el monorepo `C:\JV\hormiguero` (github.com/jivhdev/hormiguero).
Las citas entre comillas son textuales de Javier.

| # | Fecha | Tema | Decisión | Palabras de Javier |
|---|---|---|---|---|
| D-01 | 2026-10-03 | Repositorio | Un solo monorepo para todo el ecosistema, en `C:\JV` + GitHub | "monorepo, obvio" |
| D-02 | 2026-10-03 | Ecosistema antiguo | No se arrastra nada del ecosistema antiguo; se desarrolla uno nuevo | "no me importa nada del ecosistema antiguo, sino que vayamos desarrollando el nuevo en el orden que decidamos mas logico" |
| D-03 | 2026-10-03 | Criterio de herramientas | Solo herramientas nuevas y actualizadas, o viejas ya validadas | "con las herramientas nuevas actualizadas o viejas validadas, son las dos unicas opciones que acepto" |
| D-04 | 2026-10-03 | Stack | .NET 10 + WPF | ".NET 10 + WPF, dale." |
| D-05 | 2026-10-03 | Metodología | MQD v2 es la columna vertebral; de otros marcos se toman piezas puntuales | "ok, MQD v2 como columna vertebral" |
| D-06 | 2026-10-03 | Ubicación de MQD v2 | Dentro del monorepo, abierto como vault de Obsidian | (elegido: "Dentro del monorepo") |
| D-07 | 2026-10-03 | Ritmo de agentes | Se diseña desde cero; el RTA v0.1 queda solo como referencia | (elegido: "No, partir de cero") |
| D-08 | 2026-10-03 | Antigravity | Fuera por ahora (las apps son WPF, sin navegador) | (elegido: "Sacarlo por ahora") |
| D-09 | 2026-10-03 | Criterios de aceptación | Se construyen equilibrando tres fuentes: lo que ya funciona, lo que dice Javier y lo que funciona en otros programas (reusar o inspirarse) | "debemos encontrar el equilibrio entre lo que ya esta y lo que estara en base a lo que yo diga, sumando a lo que ya funciona en otros programas que podemos usar directamente o inspirarnos en ellos" |
| D-10 | 2026-10-03 | Documentos reales | Son solo referencia para pruebas; los programas deben funcionar con cualquier PDF | "si bien es como referencia el uso de los documentos reales, el programa debe servir con cualquier pdf" |
| D-11 | 2026-10-03 | Modelos de IA | Se pueden usar todos los modelos de OpenCode Go, incluidos los de origen chino (el código es público). Regla que se mantiene: no pegar documentos reales de clientes en ninguna IA | "si es asi" (confirma la interpretación) |
| D-12 | 2026-10-03 | Día de trabajo | 1) ver el plan de desarrollo; 2) abrir la semilla del proyecto que toca según el orden; 3) desarrollarlo hasta completarlo y pasar al siguiente | "1. ver el plan de desarrollo. 2.abir la semilla con el proyecto seleccionado que corresponda al orden establecido 3.desarrollarlo hasta completarlo y pasar al siguiente" |
| D-13 | 2026-10-03 | Intervención de Javier | Aprueba el plan del proyecto al inicio; las IAs avanzan por bloques; Javier prueba el programa real en cada hito usable y decide lo que no está en la especificación | (elegido: "Apruebo el plan y pruebo en hitos") |
| D-14 | 2026-10-03 | Reparto de IAs | OpenCode hace lo fácil; Claude Code hace lo difícil y revisa/aprueba lo que hizo OpenCode | "HAREMOS UN PLAN PARA USAR OPEN CODE PARA LO FACIL Y CLAUDE CODE PARA LO DIFICIL Y DE PASO QUE REVISE LO QUE HIZO Y LE APRUEBE" (LEE.txt) |
| D-15 | 2026-10-03 | Traspaso entre IAs | Claude Code dirige a OpenCode: le pasa los bloques por terminal (`opencode run -m <modelo> --dir <carpeta>`), revisa el resultado y lo aprueba o devuelve. Javier solo abre Claude Code. Prueba de concepto superada (B-000, validador de RUT: 84 s, alcance respetado, 12/12 tests + 16/16 pruebas del revisor) | "lo que me recomiendas me encanta, pero veamos si es posible" |
| D-16 | 2026-10-03 | Definición de "completo" | Un proyecto está completo cuando Javier lo usó en su trabajo real al menos 1 mes sin hacerle actualizaciones. Además debe considerar la integración con el ecosistema | "debo haberlo usado al menos 1 mes sin hacerle actualizaciones para que este completado... ademas de no olvidar del asunto de la integracion" |
| D-17 | 2026-10-03 | Mes de uso | Durante el mes de uso de A se desarrolla B. Si A necesita un arreglo, se interrumpe B, se arregla A y su mes vuelve a contar desde cero | (elegido: "Se avanza con el siguiente") |
| D-18 | 2026-10-03 | Integración | Toda app funciona sola primero, pero nace configurada o lista para compartir datos, plantillas o bases de datos con el resto (quizás una base de datos común). El mecanismo exacto se decide después, con base en los libros y las referencias | "primero obvio funcionan solos pero como el mqd estara pensado en eso, la ap quedara tambien configurada o al menos lista para compartir datos y plantillas o bases de datos, nose talvez todos deban compartir al menos una base de datos.... esto se decidira en base a los recursos, los libros, y las aplicaciones" |
| D-19 | 2026-10-03 | Nombres primero | Antes de cualquier otra cosa se definen el nombre del ecosistema y de cada app (para no confundir repos ni documentos). Fusiones, orden y Seguimiento se deciden después | "quiero crear antes de nada el nombre del ecosistema y el nombre de las aplicaciones, asi no hay confusiones en los repos y bueno en todos lados en realidad" |
| D-20 | 2026-10-03 | Respaldo | Todo se respalda en GitHub todo el tiempo; se trabaja con Obsidian | "recuerda que todo debe ir respaldado en github todo el tiempo y el tema de obsidian y todo eso recuerdalo" |
| D-21 | 2026-10-03 | Estructura | Tres niveles: ecosistema → ambientes → apps. Primer ambiente: oficina. Se fusiona la mayor cantidad posible de apps por función; una función nueva se agrega a la app que corresponda | "fusionemos la mayor cantidad de ap posibles con sus distintos funcionamientos y si encontramos otro lo agregamos [...] como que categorizamos este sub-ambiente de oficina dentro del eocisistema" |
| D-22 | 2026-10-03 | Apps de oficina | 3 apps por función: Archivero (entrada), Buscadero (consulta y control, incluye seguimiento), Mensajero (salida) | (elegido: "Sí, 3 apps"; "usemos archivero y buscadero"; "Mensajero") |
| D-23 | 2026-10-03 | Nombre del ecosistema | Hormiguero | (elegido: "Hormiguero") |
| D-24 | 2026-10-03 | Nombre del ambiente de oficina | Obrera. Jerarquía: Hormiguero (ecosistema) → Obrera (ambiente de oficina) → Archivero, Buscadero, Mensajero | (elegido: "Obrera") |
| D-25 | 2026-10-03 | Orden de desarrollo | No se decide a mano: MQD v2 debe definir el procedimiento con el que se decide el orden de desarrollo | "esque eso lo vamos a definir en mqd po el orden de desarrollo de las cosas" |
| D-26 | 2026-10-03 | Repositorio público | El monorepo `hormiguero` es público desde el primer commit | (elegido: "Sí, público desde ya") |
| D-28 | 2026-10-03 | Proceso de la idea a la especificación | Se adopta la propuesta completa de `ANALISIS-REQUISITOS-EMERGENTES.md` (sección 3) para todas las apps: se mantiene lo que funcionó de la v1 y se agregan prototipo de pantallas, descubrimiento con documentos reales, construcción por juegos (apertura/medio/final), criterios ejecutables y la pregunta "¿y si quiere cambiarlo después?" | "osea integra todo lo que corresponda po, lo que convenga para todos los casos que se desarrollaran, dale" |
| D-29 | 2026-10-03 | Punto de partida | Todo se construye desde cero, lo antes posible, en .NET 10 + WPF y siempre integrado. El entorno antiguo es lo único que existe de estas apps (no hay otros repos ni copias) | "esque pasa que borre todo, literal lo que hay aca es lo unico que hay en el mundo de estas aps. hay que hacer todo desde cero. lo antes posible. en el lenguaje que elejimos y siempre pensando que todo esta integrado" |
| D-30 | 2026-10-03 | Regla de orden | Se desarrolla primero lo que depende de menos piezas (o de ninguna), luego lo que depende de lo ya construido, y así sucesivamente (orden de abajo hacia arriba por dependencias) | "primero lo que alimente menos cosas, entiendes? onda la que dependa menos de otras, o de ninguna, y luego la de menos, y asi sucesivamente imagino, es lo mas logico" |
| D-31 | 2026-10-03 | Urgencia | Todas las funciones son urgentes (Javier dejó de usar todos los .exe antiguos); los correos de facturas se usan una vez por semana (lunes) | "todos menos correos de facturas, eso lo uso todos los lunes" |
| D-32 | 2026-10-03 | Puente | No se usan los .exe antiguos mientras se construyen los nuevos | (elegido: "No") |
| D-33 | 2026-10-03 | Orden de desarrollo de Obrera | Núcleo nivel 0 (diseño, utilidades, lectura de PDF, datos) → nivel 1 (visor, reconocimiento) → apertura de Buscadero → apertura de Archivero → apertura de Mensajero → Seguimiento (dentro de Buscadero). De cada pieza del núcleo se construye solo lo que pide la app de encima. Después de las aperturas, cada app se completa en el mismo orden y parte su mes de uso | (elegido: "Sí, apruebo el orden") |
| D-34 | 2026-10-03 | Registro del avance | El avance (bloques, estado, traspasos) vive como notas Markdown con propiedades (YAML) dentro del repo: Obsidian las muestra como tablero visual (Bases) y la IA las lee como texto estructurado. Prioridad: lo visual en Obsidian y un formato cómodo para la IA | "lo que me importa es la cosa visual de obsidian y que la ia logre leerlo en un formato amigable para ella eso es lo unico que me interesa" |
| D-35 | 2026-10-03 | Diseño visual | Estilo minimalista propio para toda la familia Hormiguero; modo claro u oscuro según Windows, con opción de cambiarlo dentro de la app; pantallas amplias y simples (pocas cosas por pantalla, botones grandes, textos que explican) | (elegido: "Minimalista propio", "Sigue a Windows", "Amplia y simple") |
| D-36 | 2026-10-03 | Colores de identidad | Dos colores: grafito azul y verde hoja. Modo claro: principal grafito azul (#2E5C8A) con acento verde hoja. Modo oscuro: principal verde hoja (#6CC08B) con acento grafito azul (#7FB0E0). Muestras de referencia: combinaciones A y B del 2026-10-03 | "quiero la grafito en blanco y la verde en oscuro [...] que en modo claro en el azul y en el modo oscuro el verde?" |
| D-37 | 2026-10-03 | Nombre del método | MQD | "MQD." |
| D-38 | 2026-10-03 | Carpeta JV | `C:\JV` contiene todas las cosas de Javier y es igual en todos sus equipos; todo lo demás (entorno antiguo, recursos) vive fuera de `C:\JV` | "TODO LO DEMAS, VIVIRA FUERA DE JV. DENTRO DE JV, EN TODOS LOS PC, ESTARAN TODAS MIS COSAS" |
| D-39 | 2026-10-03 | ficheri | Se integra a Hormiguero; su función se ubica en la app que corresponda | (elegido: "Sí, se integra") |
| D-40 | 2026-10-03 | Repositorio de MQD | MQD vive en su propio repositorio público nuevo, `jivhdev/mqd`, en `C:\JV\MQD`. `jivhdev/mqd-vault` (privado) queda como archivo de la v1. Reemplaza a D-06 (MQD ya no vive dentro del monorepo de Hormiguero) | (elegido: "Repo nuevo para la v2", "Público, como Hormiguero") |
| D-41 | 2026-10-03 | Función de ficheri | ficheri calcula vencimientos: fecha ingresada + N días hábiles (sin feriados) o corridos, enlazado a una alerta. Se reparte: el cálculo de fechas va al núcleo (Utilidades, nivel 0) y la alerta de vencimiento a Seguimiento (Buscadero) | "FICHERI ME DECIA LAS FECHA INGRESADA POR EL USUARIO Y RESTABA SEGUN DIAS FERIADOS IGUAL HABILES O DE CORRIDOS Y ESO QUE SE ENLACE A LA ALAERTA" |
| D-42 | 2026-10-03 | Cierre de MQD v1 | Se subieron a `mqd-vault` los últimos cambios locales (especificación de Buscadero, CONTEXTO-MQD.md, MQD MODIFICACIONES.txt), sin libros, y se marcó la etiqueta `v1.0` | "SI OBVIO SUBELOS PORF" |
| D-43 | 2026-10-03 | Licencia de MQD | CC BY-SA 4.0 (licencia libre para documentos, mismo espíritu que la GPL v3 de Hormiguero) | "La que convenga para lo que quiero [...] Yo creo que sea gratis para todo el mundo [...] que quien quiera usarlo, lo quiere usar" |
| D-27 | 2026-10-03 | Licencia | GPL v3 para todo Hormiguero. Consecuencia: toda dependencia debe ser compatible con GPL v3 (MIT, BSD y Apache 2.0 lo son) | "GPL v3" |

## Herramientas evaluadas

| Herramienta | Veredicto | Estado |
|---|---|---|
| uv 0.12.22 | Instalado (requisito de Spec Kit) | Instalado |
| Spec Kit (specify) 1.0.13 | Para evaluar como pieza de MQD v2 | Instalado, sin activar |
| OpenSpec 1.14.0 | Para evaluar como pieza de MQD v2 | Instalado, sin activar |
| .NET 10 SDK, csharp-lsp, MarkItDown, frontend-design, CSharpier, Context7 | Recomendados | Pendiente de aprobación |
| Grill Me (solo esa skill) | Conviene | Pendiente |
| Ponytail | Conviene | Pendiente |
| RTK | Conviene cuando haya código | Pendiente |
| Graphify | Más adelante | — |
| agent-skills (Osmani) | Solo como referencia | Clonado en `_recursos` |
| Caveman | No | — |
| OmniRoute | No (riesgo de cuenta, datos y seguridad) | — |
| Antigravity | No por ahora (D-08) | — |

## Hallazgos

- 2026-10-03: en OpenCode Go, DeepSeek exige activar "Global regions" en la configuración de privacidad de la cuenta; sin eso responde "This Go model requires Global regions". GLM 5.3 Flash funciona con la configuración por defecto. No se cambió la configuración de la cuenta.
- 2026-10-03: en B-000 la IA aceptó puntos mal ubicados ("1.2.3-6" pasa como válido) porque el bloque no definía el formato exacto de los puntos. Fue un hueco del bloque, no un error de la IA: los bloques deben definir los casos límite.

## Pendientes

- Vista visual del ecosistema en Obsidian (Canvas + Bases + Grafo): falta confirmar que es lo que Javier imagina.
- Nombre del método, modo ligero, plantilla de RNF (6 o 7 campos), ficheri.
