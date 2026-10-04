using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public record DocumentoIndexado(
    string Ruta,
    string CarpetaRaiz,
    string Nombre,
    long Tamano,
    DateTime Modificado,
    string? Huella,
    string Estado,
    bool TieneTexto,
    IReadOnlyList<(string Numero, string Prefijo, string Sufijo, string Origen)> Numeros
);

public class Documentos
{
    private readonly SqliteConnection _conexion;

    public Documentos(SqliteConnection conexion)
    {
        _conexion = conexion;
    }

    public void Guardar(DocumentoIndexado doc)
    {
        using var transaccion = _conexion.BeginTransaction();

        string? idDocumentoStr = ObtenerIdPorRuta(doc.Ruta);
        if (idDocumentoStr == null)
        {
            using (var comando = _conexion.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText =
                    "INSERT INTO documentos(ruta, carpeta_raiz, nombre, tamano, modificado, huella, estado, tiene_texto, indexado_en) "
                    + "VALUES ($ruta, $carpeta_raiz, $nombre, $tamano, $modificado, $huella, $estado, $tiene_texto, $indexado_en);";
                comando.Parameters.AddWithValue("$ruta", doc.Ruta);
                comando.Parameters.AddWithValue("$carpeta_raiz", doc.CarpetaRaiz);
                comando.Parameters.AddWithValue("$nombre", doc.Nombre);
                comando.Parameters.AddWithValue("$tamano", doc.Tamano);
                comando.Parameters.AddWithValue("$modificado", doc.Modificado.ToString("o"));
                if (doc.Huella == null)
                {
                    comando.Parameters.AddWithValue("$huella", DBNull.Value);
                }
                else
                {
                    comando.Parameters.AddWithValue("$huella", doc.Huella);
                }
                comando.Parameters.AddWithValue("$estado", doc.Estado);
                comando.Parameters.AddWithValue("$tiene_texto", doc.TieneTexto ? 1L : 0L);
                comando.Parameters.AddWithValue("$indexado_en", DateTime.Now.ToString("o"));
                comando.ExecuteNonQuery();
            }

            using (var comando = _conexion.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText = "SELECT last_insert_rowid();";
                idDocumentoStr = Convert.ToInt64(comando.ExecuteScalar()).ToString();
            }
        }
        else
        {
            using (var comando = _conexion.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText =
                    "UPDATE documentos SET carpeta_raiz=$carpeta_raiz, nombre=$nombre, tamano=$tamano, modificado=$modificado, "
                    + "huella=$huella, estado=$estado, tiene_texto=$tiene_texto, indexado_en=$indexado_en, estado_baja='activo', fecha_baja=NULL WHERE ruta=$ruta;";
                comando.Parameters.AddWithValue("$ruta", doc.Ruta);
                comando.Parameters.AddWithValue("$carpeta_raiz", doc.CarpetaRaiz);
                comando.Parameters.AddWithValue("$nombre", doc.Nombre);
                comando.Parameters.AddWithValue("$tamano", doc.Tamano);
                comando.Parameters.AddWithValue("$modificado", doc.Modificado.ToString("o"));
                if (doc.Huella == null)
                {
                    comando.Parameters.AddWithValue("$huella", DBNull.Value);
                }
                else
                {
                    comando.Parameters.AddWithValue("$huella", doc.Huella);
                }
                comando.Parameters.AddWithValue("$estado", doc.Estado);
                comando.Parameters.AddWithValue("$tiene_texto", doc.TieneTexto ? 1L : 0L);
                comando.Parameters.AddWithValue("$indexado_en", DateTime.Now.ToString("o"));
                comando.ExecuteNonQuery();
            }

            using (var comando = _conexion.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText =
                    "DELETE FROM numeros_documento WHERE documento_id=(SELECT id FROM documentos WHERE ruta=$ruta);";
                comando.Parameters.AddWithValue("$ruta", doc.Ruta);
                comando.ExecuteNonQuery();
            }
        }

        long idDocumento = long.Parse(idDocumentoStr);
        foreach (var numero in doc.Numeros)
        {
            using (var comando = _conexion.CreateCommand())
            {
                comando.Transaction = transaccion;
                comando.CommandText =
                    "INSERT INTO numeros_documento(documento_id, numero, prefijo, sufijo, origen) "
                    + "VALUES ($documento_id, $numero, $prefijo, $sufijo, $origen);";
                comando.Parameters.AddWithValue("$documento_id", idDocumento);
                comando.Parameters.AddWithValue("$numero", numero.Numero);
                comando.Parameters.AddWithValue("$prefijo", numero.Prefijo);
                comando.Parameters.AddWithValue("$sufijo", numero.Sufijo);
                comando.Parameters.AddWithValue("$origen", numero.Origen);
                comando.ExecuteNonQuery();
            }
        }

        transaccion.Commit();
    }

    private string? ObtenerIdPorRuta(string ruta)
    {
        using var comando = _conexion.CreateCommand();
        comando.CommandText = "SELECT id FROM documentos WHERE ruta=$ruta;";
        comando.Parameters.AddWithValue("$ruta", ruta);
        using var lector = comando.ExecuteReader();
        return lector.Read() ? lector.GetInt64(0).ToString() : null;
    }

    public void Quitar(string ruta)
    {
        using var transaccion = _conexion.BeginTransaction();
        string fechaBaja = DateTime.Now.ToString("o");
        using (var comando = _conexion.CreateCommand())
        {
            comando.Transaction = transaccion;
            comando.CommandText =
                "UPDATE documentos SET estado_baja='anulado', fecha_baja=$fecha WHERE ruta=$ruta AND estado_baja='activo';";
            comando.Parameters.AddWithValue("$fecha", fechaBaja);
            comando.Parameters.AddWithValue("$ruta", ruta);
            comando.ExecuteNonQuery();
        }
        using (var comando = _conexion.CreateCommand())
        {
            comando.Transaction = transaccion;
            comando.CommandText =
                "INSERT INTO auditoria(fecha, app, accion, origen, destino, huella, resultado) "
                + "SELECT $fecha, 'Nucleo', 'baja_documento', ruta, NULL, huella, 'ok' FROM documentos WHERE ruta=$ruta AND fecha_baja=$fecha;";
            comando.Parameters.AddWithValue("$fecha", fechaBaja);
            comando.Parameters.AddWithValue("$ruta", ruta);
            comando.ExecuteNonQuery();
        }
        transaccion.Commit();
    }

    public IReadOnlyDictionary<string, (long Tamano, DateTime Modificado)> Firmas(
        string carpetaRaiz
    )
    {
        var resultado = new Dictionary<string, (long Tamano, DateTime Modificado)>(
            StringComparer.OrdinalIgnoreCase
        );
        using (var comando = _conexion.CreateCommand())
        {
            comando.CommandText =
                "SELECT ruta, tamano, modificado FROM documentos WHERE carpeta_raiz=$carpeta_raiz AND estado_baja='activo';";
            comando.Parameters.AddWithValue("$carpeta_raiz", carpetaRaiz);
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                string ruta = lector.GetString(0);
                long tamano = lector.GetInt64(1);
                string modificadoStr = lector.GetString(2);
                DateTime modificado = DateTime.Parse(
                    modificadoStr,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind
                );
                resultado[ruta] = (tamano, modificado);
            }
        }
        return resultado;
    }

    public IReadOnlyList<DocumentoIndexado> BuscarPorNumero(
        string numero,
        string? carpetaRaiz = null
    )
    {
        var resultado = new List<DocumentoIndexado>();
        using (var comando = _conexion.CreateCommand())
        {
            string sql =
                "SELECT d.id, d.ruta, d.carpeta_raiz, d.nombre, d.tamano, d.modificado, d.huella, d.estado, d.tiene_texto "
                + "FROM documentos d "
                + "INNER JOIN numeros_documento n ON n.documento_id = d.id "
                + "WHERE n.numero = $numero AND d.estado_baja='activo'";
            if (carpetaRaiz != null)
            {
                sql += " AND d.carpeta_raiz = $carpeta_raiz";
            }
            comando.CommandText = sql;
            comando.Parameters.AddWithValue("$numero", numero);
            if (carpetaRaiz != null)
            {
                comando.Parameters.AddWithValue("$carpeta_raiz", carpetaRaiz);
            }
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                long idDoc = lector.GetInt64(0);
                string ruta = lector.GetString(1);
                string carpeta = lector.GetString(2);
                string nombre = lector.GetString(3);
                long tamano = lector.GetInt64(4);
                string modificadoStr = lector.GetString(5);
                string? huella = lector.IsDBNull(6) ? null : lector.GetString(6);
                string estado = lector.GetString(7);
                bool tieneTexto = lector.GetInt64(8) == 1;
                var numeros = ObtenerNumeros(idDoc);
                resultado.Add(
                    new DocumentoIndexado(
                        ruta,
                        carpeta,
                        nombre,
                        tamano,
                        DateTime.Parse(
                            modificadoStr,
                            null,
                            System.Globalization.DateTimeStyles.RoundtripKind
                        ),
                        huella,
                        estado,
                        tieneTexto,
                        numeros
                    )
                );
            }
        }
        return resultado;
    }

    private IReadOnlyList<(
        string Numero,
        string Prefijo,
        string Sufijo,
        string Origen
    )> ObtenerNumeros(long idDocumento)
    {
        var numeros = new List<(string Numero, string Prefijo, string Sufijo, string Origen)>();
        using (var comando = _conexion.CreateCommand())
        {
            comando.CommandText =
                "SELECT numero, prefijo, sufijo, origen FROM numeros_documento WHERE documento_id = $documento_id;";
            comando.Parameters.AddWithValue("$documento_id", idDocumento);
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                string numero = lector.GetString(0);
                string prefijo = lector.GetString(1);
                string sufijo = lector.GetString(2);
                string origen = lector.GetString(3);
                numeros.Add((numero, prefijo, sufijo, origen));
            }
        }
        return numeros;
    }
}
