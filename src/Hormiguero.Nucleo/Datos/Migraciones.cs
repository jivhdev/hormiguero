using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public static class Migraciones
{
    public static readonly IReadOnlyList<(int Version, string Sql)> Todas =
    [
        (
            1,
            "CREATE TABLE IF NOT EXISTS configuracion(clave TEXT PRIMARY KEY, valor TEXT NOT NULL);"
        ),
        (
            2,
            "CREATE TABLE IF NOT EXISTS documentos("
                + "id INTEGER PRIMARY KEY, "
                + "ruta TEXT NOT NULL UNIQUE COLLATE NOCASE, "
                + "carpeta_raiz TEXT NOT NULL COLLATE NOCASE, "
                + "nombre TEXT NOT NULL, "
                + "tamano INTEGER NOT NULL, "
                + "modificado TEXT NOT NULL, "
                + "huella TEXT, "
                + "estado TEXT NOT NULL, "
                + "tiene_texto INTEGER NOT NULL, "
                + "indexado_en TEXT NOT NULL); "
                + "CREATE TABLE IF NOT EXISTS numeros_documento("
                + "documento_id INTEGER NOT NULL REFERENCES documentos(id) ON DELETE CASCADE, "
                + "numero TEXT NOT NULL, "
                + "prefijo TEXT NOT NULL, "
                + "sufijo TEXT NOT NULL, "
                + "origen TEXT NOT NULL); "
                + "CREATE INDEX IF NOT EXISTS idx_numeros_documento_numero ON numeros_documento(numero); "
                + "CREATE INDEX IF NOT EXISTS idx_documentos_huella ON documentos(huella); "
                + "CREATE INDEX IF NOT EXISTS idx_documentos_carpeta_raiz ON documentos(carpeta_raiz);"
        ),
        (
            3,
            // ADR-001 de Archivero: configuraciones de identificación y registro de auditoría.
            "CREATE TABLE IF NOT EXISTS identificaciones("
                + "id INTEGER PRIMARY KEY, "
                + "tipo TEXT NOT NULL COLLATE NOCASE, "
                + "emisor TEXT NOT NULL COLLATE NOCASE, "
                + "datos TEXT NOT NULL, "
                + "actualizada TEXT NOT NULL, "
                + "UNIQUE(tipo, emisor)); "
                + "CREATE TABLE IF NOT EXISTS auditoria("
                + "id INTEGER PRIMARY KEY, "
                + "fecha TEXT NOT NULL, "
                + "app TEXT NOT NULL, "
                + "accion TEXT NOT NULL, "
                + "origen TEXT NOT NULL, "
                + "destino TEXT, "
                + "huella TEXT, "
                + "resultado TEXT NOT NULL); "
                + "CREATE INDEX IF NOT EXISTS idx_auditoria_fecha ON auditoria(fecha);"
        ),
        (
            4,
            // Fase B-4 (D-66): aviso entre apps de cada documento guardado (Archivero lo
            // escribe; Buscadero lo lee para encontrarlo al instante). Solo se agregan filas.
            "CREATE TABLE IF NOT EXISTS documentos_guardados("
                + "id INTEGER PRIMARY KEY, "
                + "ruta TEXT NOT NULL, "
                + "app TEXT NOT NULL, "
                + "guardado_en TEXT NOT NULL);"
        ),
        (
            5,
            "ALTER TABLE documentos ADD COLUMN estado_baja TEXT NOT NULL DEFAULT 'activo'; "
                + "ALTER TABLE documentos ADD COLUMN fecha_baja TEXT NULL; "
                + "CREATE TABLE IF NOT EXISTS modelos_cadena("
                + "id INTEGER PRIMARY KEY, nombre TEXT NOT NULL, fecha_creacion TEXT NOT NULL, "
                + "es_modelo_hijo INTEGER NOT NULL DEFAULT 0, preferencia_nombre INTEGER NOT NULL DEFAULT 0, "
                + "vagon_nombre_id INTEGER NULL, FOREIGN KEY(id,vagon_nombre_id) REFERENCES vagones_modelo(modelo_id,id)); "
                + "CREATE TABLE IF NOT EXISTS vagones_modelo("
                + "id INTEGER PRIMARY KEY, modelo_id INTEGER NOT NULL REFERENCES modelos_cadena(id), "
                + "padre_id INTEGER NULL REFERENCES vagones_modelo(id), orden INTEGER NOT NULL, nombre TEXT NOT NULL, "
                + "es_multiple INTEGER NOT NULL DEFAULT 0, es_anexo INTEGER NOT NULL DEFAULT 0, "
                + "modelo_cadena_hija_id INTEGER NULL REFERENCES modelos_cadena(id), UNIQUE(modelo_id,id), "
                + "FOREIGN KEY(modelo_id,padre_id) REFERENCES vagones_modelo(modelo_id,id)); "
                + "CREATE INDEX IF NOT EXISTS idx_vagones_modelo_modelo ON vagones_modelo(modelo_id, padre_id, orden); "
                + "CREATE TABLE IF NOT EXISTS cadenas("
                + "id INTEGER PRIMARY KEY, modelo_id INTEGER NULL REFERENCES modelos_cadena(id), "
                + "nombre_modelo_origen TEXT NULL, nombre TEXT NOT NULL, fecha_creacion TEXT NOT NULL, "
                + "estructura_json TEXT NOT NULL, cadena_madre_id INTEGER NULL REFERENCES cadenas(id), "
                + "vagon_padre_id INTEGER NULL, estado TEXT NOT NULL DEFAULT 'activa', fecha_anulacion TEXT NULL, "
                + "CHECK((cadena_madre_id IS NULL) = (vagon_padre_id IS NULL)), "
                + "FOREIGN KEY(cadena_madre_id,vagon_padre_id) REFERENCES vagones_cadena(cadena_id,id)); "
                + "CREATE INDEX IF NOT EXISTS idx_cadenas_modelo ON cadenas(modelo_id); "
                + "CREATE INDEX IF NOT EXISTS idx_cadenas_madre ON cadenas(cadena_madre_id, vagon_padre_id); "
                + "CREATE TABLE IF NOT EXISTS versiones_documento("
                + "id INTEGER PRIMARY KEY, documento_id INTEGER NOT NULL REFERENCES documentos(id), huella TEXT NOT NULL, "
                + "ruta_observada TEXT NOT NULL, registrada_en TEXT NOT NULL, estado TEXT NOT NULL DEFAULT 'vigente', fecha_anulacion TEXT NULL); "
                + "CREATE INDEX IF NOT EXISTS idx_versiones_huella ON versiones_documento(huella); "
                + "CREATE UNIQUE INDEX IF NOT EXISTS idx_versiones_vigente_documento ON versiones_documento(documento_id) WHERE estado='vigente'; "
                + "CREATE TABLE IF NOT EXISTS vagones_cadena("
                + "id INTEGER PRIMARY KEY, cadena_id INTEGER NOT NULL REFERENCES cadenas(id), "
                + "padre_id INTEGER NULL REFERENCES vagones_cadena(id), vagon_modelo_id INTEGER NULL REFERENCES vagones_modelo(id), "
                + "orden INTEGER NOT NULL, nombre TEXT NOT NULL, es_multiple INTEGER NOT NULL DEFAULT 0, es_anexo INTEGER NOT NULL DEFAULT 0, "
                + "version_id INTEGER NULL REFERENCES versiones_documento(id), estado TEXT NOT NULL DEFAULT 'activo', fecha_anulacion TEXT NULL, "
                + "UNIQUE(cadena_id,id), FOREIGN KEY(cadena_id,padre_id) REFERENCES vagones_cadena(cadena_id,id)); "
                + "CREATE INDEX IF NOT EXISTS idx_vagones_cadena_cadena ON vagones_cadena(cadena_id, padre_id, orden); "
                + "CREATE TABLE IF NOT EXISTS campos_documento("
                + "id INTEGER PRIMARY KEY, identificacion_id INTEGER NOT NULL REFERENCES identificaciones(id), nombre TEXT NOT NULL, "
                + "nombre_estable TEXT NOT NULL, tipo_dato TEXT NOT NULL, activo INTEGER NOT NULL DEFAULT 1, origen_lectura TEXT NOT NULL, "
                + "UNIQUE(identificacion_id, nombre_estable)); "
                + "CREATE INDEX IF NOT EXISTS idx_campos_identificacion ON campos_documento(identificacion_id, activo); "
                + "CREATE TABLE IF NOT EXISTS valores_documento("
                + "id INTEGER PRIMARY KEY, version_id INTEGER NOT NULL REFERENCES versiones_documento(id), campo_id INTEGER NOT NULL REFERENCES campos_documento(id), "
                + "valor_original TEXT NOT NULL, valor_clave TEXT NOT NULL, origen TEXT NOT NULL, confianza REAL NULL, estado TEXT NOT NULL DEFAULT 'vigente', "
                + "fecha_creacion TEXT NOT NULL, fecha_anulacion TEXT NULL); "
                + "CREATE INDEX IF NOT EXISTS idx_valores_campo_clave_estado ON valores_documento(campo_id, valor_clave, estado); "
                + "CREATE INDEX IF NOT EXISTS idx_valores_version_campo ON valores_documento(version_id, campo_id); "
                + "CREATE TABLE IF NOT EXISTS reglas_vagon("
                + "id INTEGER PRIMARY KEY, vagon_modelo_id INTEGER NOT NULL REFERENCES vagones_modelo(id), identificacion_id INTEGER NOT NULL REFERENCES identificaciones(id), "
                + "campo_origen_id INTEGER NOT NULL REFERENCES campos_documento(id), vagon_comparacion_id INTEGER NOT NULL REFERENCES vagones_modelo(id), "
                + "campo_comparacion_id INTEGER NOT NULL REFERENCES campos_documento(id), operacion TEXT NOT NULL, normalizar_espacios INTEGER NOT NULL DEFAULT 1, "
                + "ignorar_guiones INTEGER NOT NULL DEFAULT 0, ignorar_ceros_iniciales INTEGER NOT NULL DEFAULT 0, largo_minimo INTEGER NOT NULL DEFAULT 6, "
                + "estado TEXT NOT NULL DEFAULT 'activa', fecha_anulacion TEXT NULL); "
                + "CREATE UNIQUE INDEX IF NOT EXISTS idx_reglas_vagon_activa ON reglas_vagon(vagon_modelo_id) WHERE estado='activa'; "
                + "CREATE TABLE IF NOT EXISTS enlaces_cadena("
                + "id INTEGER PRIMARY KEY, vagon_cadena_id INTEGER NOT NULL REFERENCES vagones_cadena(id), version_id INTEGER NOT NULL REFERENCES versiones_documento(id), "
                + "origen TEXT NOT NULL, regla_id INTEGER NULL REFERENCES reglas_vagon(id), estado TEXT NOT NULL, creada_en TEXT NOT NULL, cambiada_en TEXT NULL); "
                + "CREATE INDEX IF NOT EXISTS idx_enlaces_version_estado ON enlaces_cadena(version_id, estado); "
                + "CREATE INDEX IF NOT EXISTS idx_enlaces_vagon_estado ON enlaces_cadena(vagon_cadena_id, estado); "
                + "CREATE TABLE IF NOT EXISTS marcas_version("
                + "id INTEGER PRIMARY KEY, version_id INTEGER NOT NULL REFERENCES versiones_documento(id), tipo TEXT NOT NULL, pagina INTEGER NOT NULL, "
                + "x REAL NOT NULL, y REAL NOT NULL, ancho REAL NOT NULL, alto REAL NOT NULL, texto TEXT NULL, creada_en TEXT NOT NULL, "
                + "estado TEXT NOT NULL DEFAULT 'activa', fecha_anulacion TEXT NULL); "
                + "CREATE INDEX IF NOT EXISTS idx_marcas_version_estado ON marcas_version(version_id, estado);"
        ),
        (
            6,
            "ALTER TABLE enlaces_cadena ADD COLUMN motivo TEXT NULL; "
                + "CREATE TABLE IF NOT EXISTS reglas_vagon_anuladas(id INTEGER PRIMARY KEY, vagon_modelo_id INTEGER NOT NULL, identificacion_id INTEGER NOT NULL, campo_origen_id INTEGER NOT NULL, vagon_comparacion_id INTEGER NOT NULL, campo_comparacion_id INTEGER NOT NULL, operacion TEXT NOT NULL, normalizar_espacios INTEGER NOT NULL, ignorar_guiones INTEGER NOT NULL, ignorar_ceros_iniciales INTEGER NOT NULL, largo_minimo INTEGER NOT NULL, anulada_en TEXT NOT NULL);"
        ),
        (
            7,
            "CREATE TABLE IF NOT EXISTS calendarios_feriados("
                + "id INTEGER PRIMARY KEY, nombre TEXT NOT NULL, pais_codigo TEXT NOT NULL, "
                + "region TEXT NULL, activo INTEGER NOT NULL DEFAULT 1, predeterminado INTEGER NOT NULL DEFAULT 0, "
                + "origen TEXT NOT NULL DEFAULT 'usuario', creada_en TEXT NOT NULL, actualizada_en TEXT NOT NULL, "
                + "CHECK(activo IN (0,1)), CHECK(predeterminado IN (0,1))); "
                + "CREATE UNIQUE INDEX IF NOT EXISTS idx_calendarios_feriados_un_predeterminado "
                + "ON calendarios_feriados(predeterminado) WHERE predeterminado=1; "
                + "CREATE TABLE IF NOT EXISTS feriados("
                + "id INTEGER PRIMARY KEY, calendario_id INTEGER NOT NULL REFERENCES calendarios_feriados(id), "
                + "fecha TEXT NOT NULL, nombre TEXT NOT NULL, activo INTEGER NOT NULL DEFAULT 1, "
                + "fecha_anulacion TEXT NULL, creada_en TEXT NOT NULL, actualizada_en TEXT NOT NULL, "
                + "CHECK(activo IN (0,1)), UNIQUE(calendario_id, fecha)); "
                + "CREATE INDEX IF NOT EXISTS idx_feriados_calendario_fecha_activo "
                + "ON feriados(calendario_id, fecha, activo);"
        ),
        (
            8,
            "CREATE TABLE IF NOT EXISTS reglas_alerta("
                + "id INTEGER PRIMARY KEY, nombre TEXT NOT NULL, modelo_cadena_id INTEGER NOT NULL REFERENCES modelos_cadena(id), "
                + "vagon_origen_modelo_id INTEGER NOT NULL, vagon_destino_modelo_id INTEGER NOT NULL, evento TEXT NOT NULL, "
                + "dias INTEGER NOT NULL CHECK(dias BETWEEN 0 AND 3650), modo_dias TEXT NOT NULL CHECK(modo_dias IN ('corridos','habiles')), "
                + "calendario_id INTEGER NULL REFERENCES calendarios_feriados(id), texto_aviso TEXT NOT NULL, repetir INTEGER NOT NULL DEFAULT 0 CHECK(repetir IN (0,1)), "
                + "estado TEXT NOT NULL DEFAULT 'activa' CHECK(estado IN ('activa','anulada')), creada_en TEXT NOT NULL, actualizada_en TEXT NOT NULL, "
                + "fecha_anulacion TEXT NULL, FOREIGN KEY(modelo_cadena_id,vagon_origen_modelo_id) REFERENCES vagones_modelo(modelo_id,id), "
                + "FOREIGN KEY(modelo_cadena_id,vagon_destino_modelo_id) REFERENCES vagones_modelo(modelo_id,id)); "
                + "CREATE INDEX IF NOT EXISTS idx_reglas_alerta_modelo_estado ON reglas_alerta(modelo_cadena_id,estado); "
                + "CREATE TABLE IF NOT EXISTS alertas("
                + "id INTEGER PRIMARY KEY, regla_id INTEGER NULL REFERENCES reglas_alerta(id), cadena_id INTEGER NULL REFERENCES cadenas(id), "
                + "vagon_cadena_id INTEGER NULL, version_id INTEGER NULL REFERENCES versiones_documento(id), clave_evento TEXT NULL, "
                + "texto TEXT NOT NULL, estado TEXT NOT NULL CHECK(estado IN ('pendiente','vencida','resuelta','descartada')), motivo TEXT NULL, "
                + "fecha_base TEXT NULL, cantidad_dias INTEGER NULL, modo_dias TEXT NULL, calendario_id INTEGER NULL REFERENCES calendarios_feriados(id), "
                + "fecha_objetivo TEXT NOT NULL, creada_en TEXT NOT NULL, actualizada_en TEXT NOT NULL, resuelta_en TEXT NULL, descartada_en TEXT NULL, "
                + "FOREIGN KEY(cadena_id,vagon_cadena_id) REFERENCES vagones_cadena(cadena_id,id), "
                + "CHECK((regla_id IS NULL) OR (cadena_id IS NOT NULL AND vagon_cadena_id IS NOT NULL)), "
                + "CHECK((cadena_id IS NOT NULL) OR (version_id IS NOT NULL))); "
                + "CREATE UNIQUE INDEX IF NOT EXISTS idx_alertas_idempotencia_regla ON alertas(regla_id,cadena_id,vagon_cadena_id) WHERE regla_id IS NOT NULL; "
                + "CREATE INDEX IF NOT EXISTS idx_alertas_estado_fecha ON alertas(estado,fecha_objetivo); "
                + "CREATE INDEX IF NOT EXISTS idx_alertas_cadena_estado ON alertas(cadena_id,estado); "
                + "CREATE INDEX IF NOT EXISTS idx_alertas_version_estado ON alertas(version_id,estado); "
                + "CREATE TABLE IF NOT EXISTS historial_alertas("
                + "id INTEGER PRIMARY KEY, alerta_id INTEGER NOT NULL REFERENCES alertas(id), accion TEXT NOT NULL, estado_anterior TEXT NULL, "
                + "estado_nuevo TEXT NOT NULL, motivo TEXT NULL, datos_anteriores TEXT NULL, datos_nuevos TEXT NULL, fecha TEXT NOT NULL, app TEXT NOT NULL); "
                + "CREATE INDEX IF NOT EXISTS idx_historial_alertas_alerta_fecha ON historial_alertas(alerta_id,fecha,id); "
                + "CREATE TRIGGER IF NOT EXISTS trg_alertas_no_borrar BEFORE DELETE ON alertas BEGIN SELECT RAISE(ABORT,'Las alertas no se borran.'); END; "
                + "CREATE TRIGGER IF NOT EXISTS trg_historial_alertas_no_editar BEFORE UPDATE ON historial_alertas BEGIN SELECT RAISE(ABORT,'El historial de alertas es inmutable.'); END; "
                + "CREATE TRIGGER IF NOT EXISTS trg_historial_alertas_no_borrar BEFORE DELETE ON historial_alertas BEGIN SELECT RAISE(ABORT,'El historial de alertas es inmutable.'); END;"
        ),
    ];

    public static void Aplicar(
        SqliteConnection conexion,
        IReadOnlyList<(int Version, string Sql)> migraciones
    )
    {
        // ADR-001: la regla se revisa antes de tocar la base para que un rechazo
        // no deje nada aplicado a medias.
        foreach (var (_, sql) in migraciones)
        {
            if (
                sql.Contains("DROP ", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("RENAME", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    "La migración contiene DROP o RENAME; solo se permite agregar (ADR-001)."
                );
            }
        }

        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText =
                "CREATE TABLE IF NOT EXISTS migraciones(version INTEGER PRIMARY KEY, aplicada_en TEXT NOT NULL);";
            comando.ExecuteNonQuery();
        }

        var versionesAplicadas = new HashSet<int>();
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "SELECT version FROM migraciones;";
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                versionesAplicadas.Add(lector.GetInt32(0));
            }
        }

        foreach (var (version, sql) in migraciones)
        {
            if (versionesAplicadas.Contains(version))
            {
                continue;
            }

            using var transaccion = conexion.BeginTransaction();
            using (var comando = conexion.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText = sql;
                comando.ExecuteNonQuery();
            }

            using (var comando = conexion.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText =
                    "INSERT INTO migraciones(version, aplicada_en) VALUES ($version, $aplicada_en);";
                comando.Parameters.AddWithValue("$version", version);
                comando.Parameters.AddWithValue("$aplicada_en", DateTime.Now.ToString("o"));
                comando.ExecuteNonQuery();
            }

            transaccion.Commit();
        }
    }
}
