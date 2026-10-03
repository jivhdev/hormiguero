# AGENTS.md — Hormiguero

Reglas para cualquier agente de IA que trabaje en este repositorio. El método completo está en [MQD](https://github.com/jivhdev/mqd/blob/main/MQD.md).

## Qué es

Familia de apps de escritorio para Windows, gratuitas y de código abierto (GPL v3), para oficinistas. Hormiguero (ecosistema) → Obrera (ambiente de oficina) → Archivero, Buscadero, Mensajero.

## Regla rectora

La IA ejecuta, no decide. No asume. No completa por cuenta propia. Si falta información, se detiene y la pide. Ningún nombre visible para el usuario lo elige la IA.

## Stack y comandos

- .NET 10 + WPF, solo Windows. SQLite (Microsoft.Data.Sqlite) para datos. Versiones centralizadas en `Directory.Packages.props`.
- Compilar: `dotnet build`
- Probar: `dotnet test`
- Formato: `dotnet csharpier format .` (verificar: `dotnet csharpier check .`)
- Licencias de dependencias: `dotnet nuget-license -i Hormiguero.slnx -a .config/licencias-permitidas.json`

## Estructura

```
src/Hormiguero.Nucleo/   piezas compartidas (datos, PDF, reconocimiento, utilidades)
tests/                   pruebas automáticas
semillas/<App>/          especificación de cada app (IDEA, DESCUBRIMIENTO, PROTOTIPO, SPEC, decisiones, casos)
bloques/                 una nota por bloque de trabajo
definicion/              decisiones del ecosistema (DECISIONES.md, ADR)
```

## Cómo se trabaja un bloque

1. Lee la nota del bloque completa. Solo puedes tocar los `archivos_permitidos` y la sección "Reporte del agente" de la propia nota del bloque.
2. Implementa lo mínimo que cumple el objetivo y sus casos límite, con sus pruebas.
3. Corre `dotnet build` y `dotnet test`. No termines con errores ni advertencias nuevas.
4. Completa "Reporte del agente" en la nota del bloque. No hagas commit: lo hace Claude al revisar.

Detente y escribe el reporte si: terminaste; necesitas tocar un archivo fuera de tu alcance; fallaste 3 veces con el mismo error; aparece una decisión de arquitectura, de alcance o un nombre; las pruebas fallan por algo ajeno al bloque.

## Código mínimo (adaptado de Ponytail, MIT, DietrichGebert/ponytail)

Antes de escribir código, quédate en el primer escalón que sirva:
1. ¿Hace falta construirlo? 2. ¿Ya existe en este repo? 3. ¿Lo trae .NET? 4. ¿Lo trae Windows? 5. ¿Lo resuelve una dependencia ya instalada? 6. ¿Cabe en una línea? 7. Recién ahí, el mínimo código que funcione.

- Sin abstracciones, dependencias ni código repetitivo que nadie pidió. Menos archivos, mejor.
- Primero entiende el problema completo; un cambio chico en el lugar equivocado es otro error.
- Nunca recortes: validación de entradas, manejo de errores que evita pérdida de datos, seguridad, accesibilidad, ni lo pedido en el bloque.
- La especificación (`SPEC.md` y el bloque) manda sobre estas reglas.

## Reglas que nunca se rompen

- Español neutro en código visible, textos, comentarios y documentos (nada de voseo).
- Nunca pegues ni subas documentos reales (`C:\JV\pruebas`). Las pruebas usan PDF generados.
- Solo dependencias con licencia compatible con GPL v3 (MIT, BSD, Apache 2.0…). Nunca PyMuPDF ni nada AGPL sin aprobación.
- Solo el núcleo abre la base de datos (ADR-001). La base vive en el disco local, nunca en red.
- Mover un archivo = copiar, verificar que es idéntico y recién ahí borrar el original.
- Nunca modificar un documento original. Nunca vigilar carpetas de red con consultas agresivas.
- Comentarios solo para explicar el "por qué" no obvio.
