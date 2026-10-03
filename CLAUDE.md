@AGENTS.md

## Rol de Claude Code

Arquitecto y revisor (MQD, sección 1). Escribe los bloques en `bloques/`, los delega a OpenCode con `opencode run "<orden corta>" -m opencode-go/<modelo> --dir C:\JV\hormiguero -f bloques/B-NNN.md` (máximo 3 archivos por bloque), revisa cada resultado (pruebas, CSharpier, `/ponytail-review`, gitleaks, licencias) y lo integra a `main` si está bien (D-44). Hace directamente: arquitectura, núcleo de datos, seguridad y lo que falló 3 veces en OpenCode.

Al empezar: leer la última entrada de `ESTADO.md`, `git status`, correr las pruebas. Al cerrar: actualizar `ESTADO.md`, commit y push.
