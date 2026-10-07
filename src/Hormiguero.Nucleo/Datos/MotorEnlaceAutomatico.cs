using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed class MotorEnlaceAutomatico(SqliteConnection conexion)
{
    public const string ClaveConfianzaMinima = "enlace.confianza_minima";

    public double ConfianzaMinima
    {
        get
        {
            var valor = new Configuracion(conexion).Leer(ClaveConfianzaMinima);
            return double.TryParse(
                valor,
                System.Globalization.CultureInfo.InvariantCulture,
                out var confianza
            )
                ? Math.Clamp(confianza, 0, 1)
                : 0.90;
        }
        set
        {
            if (value is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(value));
            new Configuracion(conexion).Guardar(
                ClaveConfianzaMinima,
                value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
        }
    }

    public void Ejecutar(long? versionPublicada = null)
    {
        var reglas = LeerReglas();
        var enlaces = new RepositorioReglasYEnlaces(conexion);
        foreach (var regla in reglas)
        {
            foreach (var cadena in LeerCadenas(regla.VagonModeloId))
            {
                long? vagonComparacion = BuscarVagonComparacion(
                    cadena.CadenaId,
                    regla.VagonComparacionId
                );
                if (vagonComparacion is null)
                    continue;
                var vagonDestino = BuscarVagonDestino(cadena.CadenaId, regla.VagonModeloId);
                if (vagonDestino is null)
                    continue;
                var valorReferencia = LeerValorVagon(
                    vagonComparacion.Value,
                    regla.CampoComparacionId
                );
                if (valorReferencia is null)
                {
                    if (
                        versionPublicada is not null
                        && !ExistePropuestaORechazo(
                            vagonDestino.Value,
                            versionPublicada.Value,
                            regla.Id
                        )
                    )
                        enlaces.CrearDudoso(
                            vagonDestino.Value,
                            versionPublicada.Value,
                            regla.Id,
                            "Falta el dato necesario para comparar."
                        );
                    continue;
                }
                if (
                    versionPublicada is not null
                    && EsVersionVigente(versionPublicada.Value)
                    && !TieneValor(versionPublicada.Value, regla.CampoOrigenId)
                )
                {
                    if (
                        !ExistePropuestaORechazo(
                            vagonDestino.Value,
                            versionPublicada.Value,
                            regla.Id
                        )
                    )
                        enlaces.CrearDudoso(
                            vagonDestino.Value,
                            versionPublicada.Value,
                            regla.Id,
                            "Falta el dato necesario para comparar."
                        );
                    continue;
                }
                var comparacion = NormalizarClave(valorReferencia.Value.ValorClave, regla);
                var candidatos = BuscarCandidatos(regla, versionPublicada)
                    .Where(v => NormalizarClave(v.ValorClave, regla) == comparacion)
                    .ToArray();
                foreach (var candidato in candidatos)
                {
                    if (ExistePropuestaORechazo(vagonDestino.Value, candidato.VersionId, regla.Id))
                        continue;
                    string? motivo = null;
                    if (!EsVersionVigente(candidato.VersionId))
                        motivo = "La versión del documento cambió.";
                    else if (!EsVersionVigente(valorReferencia.Value.VersionId))
                        motivo = "La versión del documento comparado cambió.";
                    else if (
                        candidato.Confianza is not null
                        && candidato.Confianza < ConfianzaMinima
                    )
                        motivo = "La lectura no es segura.";
                    else if (
                        NormalizarClave(candidato.ValorClave, regla).Length < regla.LargoMinimo
                    )
                        motivo = "El valor es demasiado corto para enlazarlo automáticamente.";
                    else if (
                        valorReferencia.Value.Confianza is not null
                        && valorReferencia.Value.Confianza < ConfianzaMinima
                    )
                        motivo = "La lectura no es segura.";
                    else if (
                        comparacion.Length < regla.LargoMinimo
                        || NormalizarClave(candidato.ValorClave, regla).Length < regla.LargoMinimo
                    )
                        motivo = "El valor es demasiado corto para enlazarlo automáticamente.";
                    else if (candidatos.Length > 1)
                        motivo = $"Hay {candidatos.Length} documentos con el mismo dato.";
                    else if (EstaOcupado(vagonDestino.Value))
                        motivo = "El vagón ya tiene un documento enlazado.";

                    if (motivo is not null)
                        enlaces.CrearDudoso(
                            vagonDestino.Value,
                            candidato.VersionId,
                            regla.Id,
                            motivo
                        );
                    else
                        enlaces.CrearEnlace(
                            vagonDestino.Value,
                            candidato.VersionId,
                            "automatico",
                            regla.Id
                        );
                }
            }
        }
    }

    public void RegistrarError(Exception error, long? versionId = null)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "INSERT INTO auditoria(fecha,app,accion,origen,destino,resultado) VALUES($f,'Buscadero','error_motor_enlace',$o,$d,$r);";
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$o", versionId is null ? "motor" : $"version:{versionId}");
        cmd.Parameters.AddWithValue("$d", error.Message);
        cmd.Parameters.AddWithValue("$r", "No se pudo revisar el enlace automático.");
        cmd.ExecuteNonQuery();
    }

    private IReadOnlyList<ReglaVagon> LeerReglas()
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,vagon_modelo_id,identificacion_id,campo_origen_id,vagon_comparacion_id,campo_comparacion_id,operacion,normalizar_espacios,ignorar_guiones,ignorar_ceros_iniciales,largo_minimo,estado FROM reglas_vagon WHERE estado='activa' ORDER BY id;";
        using var r = cmd.ExecuteReader();
        var resultado = new List<ReglaVagon>();
        while (r.Read())
            resultado.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetInt64(2),
                    r.GetInt64(3),
                    r.GetInt64(4),
                    r.GetInt64(5),
                    r.GetString(6),
                    r.GetInt32(7) != 0,
                    r.GetInt32(8) != 0,
                    r.GetInt32(9) != 0,
                    r.GetInt32(10),
                    r.GetString(11)
                )
            );
        return resultado;
    }

    private IReadOnlyList<(long CadenaId, long? MadreId)> LeerCadenas(long vagonModelo)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT DISTINCT c.id,c.cadena_madre_id FROM cadenas c JOIN vagones_cadena v ON v.cadena_id=c.id WHERE c.estado='activa' AND v.estado='activo' AND v.vagon_modelo_id=$v;";
        cmd.Parameters.AddWithValue("$v", vagonModelo);
        using var r = cmd.ExecuteReader();
        var resultado = new List<(long, long?)>();
        while (r.Read())
            resultado.Add((r.GetInt64(0), r.IsDBNull(1) ? null : r.GetInt64(1)));
        return resultado;
    }

    private long? BuscarVagonComparacion(long cadenaId, long modeloVagon)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "WITH RECURSIVE familia(id) AS (SELECT $c UNION SELECT c.cadena_madre_id FROM cadenas c JOIN familia f ON c.id=f.id WHERE c.cadena_madre_id IS NOT NULL UNION SELECT c.id FROM cadenas c JOIN familia f ON c.cadena_madre_id=f.id) SELECT v.id FROM vagones_cadena v JOIN familia f ON f.id=v.cadena_id WHERE v.vagon_modelo_id=$m AND v.estado='activo' ORDER BY CASE WHEN v.cadena_id=$c THEN 0 ELSE 1 END,v.id LIMIT 1;";
        cmd.Parameters.AddWithValue("$c", cadenaId);
        cmd.Parameters.AddWithValue("$m", modeloVagon);
        var valor = cmd.ExecuteScalar();
        return valor is null ? null : Convert.ToInt64(valor);
    }

    private long? BuscarVagonDestino(long cadenaId, long modeloVagon)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id FROM vagones_cadena WHERE cadena_id=$c AND vagon_modelo_id=$m AND estado='activo' ORDER BY id LIMIT 1;";
        cmd.Parameters.AddWithValue("$c", cadenaId);
        cmd.Parameters.AddWithValue("$m", modeloVagon);
        var valor = cmd.ExecuteScalar();
        return valor is null ? null : Convert.ToInt64(valor);
    }

    private (long VersionId, string ValorClave, double? Confianza)? LeerValorVagon(
        long vagon,
        long campo
    )
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT e.version_id,x.valor_clave,x.confianza FROM enlaces_cadena e JOIN valores_documento x ON x.version_id=e.version_id JOIN campos_documento c ON c.id=x.campo_id WHERE e.vagon_cadena_id=$v AND e.estado='activo' AND x.campo_id=$c AND x.estado='vigente' AND (NOT EXISTS(SELECT 1 FROM tipos_documento_datos legado WHERE legado.identificacion_id=c.identificacion_id) OR (c.dato_diccionario_id IS NOT NULL AND EXISTS(SELECT 1 FROM tipos_documento_datos t WHERE t.identificacion_id=c.identificacion_id AND t.dato_diccionario_id=c.dato_diccionario_id AND t.activo=1 AND t.enlazable=1))) ORDER BY e.id DESC,x.id DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$v", vagon);
        cmd.Parameters.AddWithValue("$c", campo);
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? (r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetDouble(2))
            : null;
    }

    private IReadOnlyList<(long VersionId, string ValorClave, double? Confianza)> BuscarCandidatos(
        ReglaVagon regla,
        long? versionPublicada
    )
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT DISTINCT x.version_id,x.valor_clave,x.confianza FROM valores_documento x JOIN versiones_documento v ON v.id=x.version_id JOIN campos_documento c ON c.id=x.campo_id WHERE x.campo_id=$campo AND c.identificacion_id=$identificacion AND ((x.estado='vigente' AND v.estado='vigente') OR x.version_id=$publicada) AND (NOT EXISTS(SELECT 1 FROM tipos_documento_datos legado WHERE legado.identificacion_id=c.identificacion_id) OR (c.dato_diccionario_id IS NOT NULL AND EXISTS(SELECT 1 FROM tipos_documento_datos t WHERE t.identificacion_id=c.identificacion_id AND t.dato_diccionario_id=c.dato_diccionario_id AND t.activo=1 AND t.enlazable=1))) ORDER BY x.version_id;";
        cmd.Parameters.AddWithValue("$publicada", (object?)versionPublicada ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$campo", regla.CampoOrigenId);
        cmd.Parameters.AddWithValue("$identificacion", regla.IdentificacionId);
        using var r = cmd.ExecuteReader();
        var resultado = new List<(long, string, double?)>();
        while (r.Read())
            resultado.Add((r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetDouble(2)));
        return resultado;
    }

    private bool ExistePropuestaORechazo(long vagon, long version, long regla)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT 1 FROM enlaces_cadena WHERE vagon_cadena_id=$v AND version_id=$d AND regla_id=$r AND estado IN ('activo','dudoso','rechazado') LIMIT 1;";
        cmd.Parameters.AddWithValue("$v", vagon);
        cmd.Parameters.AddWithValue("$d", version);
        cmd.Parameters.AddWithValue("$r", regla);
        return cmd.ExecuteScalar() is not null;
    }

    private bool EstaOcupado(long vagon)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT 1 FROM enlaces_cadena WHERE vagon_cadena_id=$v AND estado='activo' LIMIT 1;";
        cmd.Parameters.AddWithValue("$v", vagon);
        return cmd.ExecuteScalar() is not null;
    }

    private bool EsVersionVigente(long version)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM versiones_documento WHERE id=$v AND estado='vigente';";
        cmd.Parameters.AddWithValue("$v", version);
        return cmd.ExecuteScalar() is not null;
    }

    private bool TieneValor(long version, long campo)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT 1 FROM valores_documento WHERE version_id=$v AND campo_id=$c AND estado='vigente' AND valor_clave<>'' LIMIT 1;";
        cmd.Parameters.AddWithValue("$v", version);
        cmd.Parameters.AddWithValue("$c", campo);
        return cmd.ExecuteScalar() is not null;
    }

    public static string NormalizarClave(string valor, ReglaVagon regla)
    {
        if (regla.NormalizarEspacios)
            valor = string.Join(
                ' ',
                valor.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            );
        if (regla.IgnorarGuiones)
            valor = valor.Replace("-", string.Empty, StringComparison.Ordinal);
        if (regla.NormalizarEspacios)
            valor = string.Join(
                ' ',
                valor.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            );
        if (regla.IgnorarCerosIniciales)
        {
            int inicio = 0;
            while (inicio < valor.Length - 1 && valor[inicio] == '0')
                inicio++;
            valor = valor[inicio..];
        }
        return valor;
    }
}
