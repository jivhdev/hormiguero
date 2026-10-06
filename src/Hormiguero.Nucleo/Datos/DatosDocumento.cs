using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record VersionDocumento(
    long Id,
    long DocumentoId,
    string Huella,
    string RutaObservada,
    DateTime RegistradaEn,
    string Estado
);

public sealed record CampoDocumento(
    long Id,
    long IdentificacionId,
    string Nombre,
    string NombreEstable,
    string TipoDato,
    bool Activo,
    string OrigenLectura,
    string? DatoDiccionarioId = null
);

public sealed record ValorDocumento(
    long Id,
    long VersionId,
    long CampoId,
    string ValorOriginal,
    string ValorClave,
    string Origen,
    double? Confianza,
    string Estado,
    DateTime FechaCreacion,
    string? DatoDiccionarioId = null
);

public sealed record ValorDocumentoLeido(
    string Nombre,
    string NombreEstable,
    string ValorOriginal,
    string ValorClave,
    string Origen,
    string TipoDato = "texto",
    string OrigenLectura = "marca",
    double? Confianza = null
);

public sealed record MarcaVersion(
    long Id,
    long VersionId,
    string Tipo,
    int Pagina,
    double X,
    double Y,
    double Ancho,
    double Alto,
    string? Texto,
    DateTime CreadaEn,
    string Estado
);

internal static class AuditoriaDatos
{
    public static void Registrar(
        SqliteConnection conexion,
        SqliteTransaction transaccion,
        string accion,
        string origen,
        string? destino = null,
        string? huella = null,
        string app = "Nucleo"
    )
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            "INSERT INTO auditoria(fecha,app,accion,origen,destino,huella,resultado) VALUES($f,$app,$a,$o,$d,$h,'ok');";
        comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        comando.Parameters.AddWithValue("$a", accion);
        comando.Parameters.AddWithValue("$o", origen);
        comando.Parameters.AddWithValue("$d", (object?)destino ?? DBNull.Value);
        comando.Parameters.AddWithValue("$h", (object?)huella ?? DBNull.Value);
        comando.Parameters.AddWithValue("$app", app);
        comando.ExecuteNonQuery();
    }
}

public sealed class RepositorioDocumentosDatos(SqliteConnection conexion)
{
    public (long DocumentoId, VersionDocumento Version) AsegurarDocumentoYVersionVigente(
        string ruta
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        string rutaCompleta = Path.GetFullPath(ruta);
        var archivo = new FileInfo(rutaCompleta);
        if (!archivo.Exists)
            throw new FileNotFoundException("No existe el documento.", rutaCompleta);
        string huella = Huella.Calcular(rutaCompleta);
        using var tx = conexion.BeginTransaction();
        var resultado = AsegurarDocumentoYVersionVigente(
            tx,
            rutaCompleta,
            archivo.Length,
            archivo.LastWriteTimeUtc,
            huella,
            "Buscadero",
            false
        );
        tx.Commit();
        return resultado;
    }

    public (long DocumentoId, VersionDocumento Version) PublicarDocumento(
        string ruta,
        long tamano,
        DateTime modificado,
        string huella,
        string emisor,
        string tipo,
        IReadOnlyList<ValorDocumentoLeido> valores,
        string procedencia = "marca"
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);
        ArgumentException.ThrowIfNullOrWhiteSpace(emisor);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);

        using var tx = conexion.BeginTransaction();
        long identificacionId = ObtenerOCrearIdentificacion(tx, emisor, tipo);
        string rutaCompleta = Path.GetFullPath(ruta);
        var (documentoId, version) = AsegurarDocumentoYVersionVigente(
            tx,
            rutaCompleta,
            tamano,
            modificado,
            huella,
            "Archivero",
            true
        );
        var campos = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var valor in valores)
        {
            if (!campos.TryGetValue(valor.NombreEstable, out long campoId))
            {
                campoId = ObtenerOCrearCampo(tx, identificacionId, valor);
                campos.Add(valor.NombreEstable, campoId);
            }
        }

        foreach (var valor in valores)
        {
            long campoId = campos[valor.NombreEstable];
            if (valor.ValorOriginal.Length == 0)
                continue;
            using var existente = conexion.CreateCommand();
            existente.Transaction = tx;
            existente.CommandText =
                "SELECT 1 FROM valores_documento WHERE version_id=$v AND campo_id=$c AND estado='vigente' LIMIT 1;";
            existente.Parameters.AddWithValue("$v", version.Id);
            existente.Parameters.AddWithValue("$c", campoId);
            if (existente.ExecuteScalar() is not null)
                continue;
            InsertarValor(
                tx,
                version.Id,
                campoId,
                valor.ValorOriginal,
                valor.ValorClave,
                valor.Origen,
                valor.Confianza
            );
        }
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "publicar_documento",
            procedencia,
            ruta,
            huella,
            "Archivero"
        );
        tx.Commit();
        return (documentoId, version);
    }

    public long CorregirValor(long versionId, long campoId, string original, string clave)
    {
        ArgumentNullException.ThrowIfNull(original);
        using var tx = conexion.BeginTransaction();
        using (var anular = conexion.CreateCommand())
        {
            anular.Transaction = tx;
            anular.CommandText =
                "UPDATE valores_documento SET estado='anulado',fecha_anulacion=$f WHERE version_id=$v AND campo_id=$c AND estado='vigente';";
            anular.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            anular.Parameters.AddWithValue("$v", versionId);
            anular.Parameters.AddWithValue("$c", campoId);
            anular.ExecuteNonQuery();
        }
        long id = InsertarValor(tx, versionId, campoId, original, clave, "manual", null);
        AuditoriaDatos.Registrar(conexion, tx, "corregir_valor", $"valor:{id}");
        tx.Commit();
        return id;
    }

    private long ObtenerOCrearIdentificacion(SqliteTransaction tx, string emisor, string tipo)
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO identificaciones(tipo,emisor,datos,actualizada) VALUES($t,$e,'{}',$f) ON CONFLICT(tipo,emisor) DO UPDATE SET actualizada=excluded.actualizada RETURNING id;";
        cmd.Parameters.AddWithValue("$t", tipo);
        cmd.Parameters.AddWithValue("$e", emisor);
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private (long DocumentoId, VersionDocumento Version) AsegurarDocumentoYVersionVigente(
        SqliteTransaction tx,
        string ruta,
        long tamano,
        DateTime modificado,
        string huella,
        string app,
        bool tieneTexto
    )
    {
        using var buscar = conexion.CreateCommand();
        buscar.Transaction = tx;
        buscar.CommandText = "SELECT id FROM documentos WHERE ruta=$r LIMIT 1;";
        buscar.Parameters.AddWithValue("$r", ruta);
        object? valorId = buscar.ExecuteScalar();
        if (valorId is null)
        {
            buscar.CommandText = "SELECT id FROM documentos WHERE huella=$h ORDER BY id LIMIT 1;";
            buscar.Parameters.Clear();
            buscar.Parameters.AddWithValue("$h", huella);
            valorId = buscar.ExecuteScalar();
        }
        long documentoId;
        bool nuevo = valorId is null;
        bool cambioRuta = false;
        if (valorId is not null)
        {
            documentoId = Convert.ToInt64(valorId);
            using var rutaAnterior = conexion.CreateCommand();
            rutaAnterior.Transaction = tx;
            rutaAnterior.CommandText = "SELECT ruta FROM documentos WHERE id=$id;";
            rutaAnterior.Parameters.AddWithValue("$id", documentoId);
            cambioRuta = !string.Equals(
                Convert.ToString(rutaAnterior.ExecuteScalar()),
                ruta,
                StringComparison.OrdinalIgnoreCase
            );
        }
        else
            documentoId = 0;

        if (nuevo)
        {
            using var insertar = conexion.CreateCommand();
            insertar.Transaction = tx;
            insertar.CommandText =
                "INSERT INTO documentos(ruta,carpeta_raiz,nombre,tamano,modificado,huella,estado,tiene_texto,indexado_en) VALUES($r,$c,$n,$z,$m,$h,'ok',$tt,$f) RETURNING id;";
            AgregarDatosDocumento(insertar, ruta, tamano, modificado, huella, tieneTexto);
            documentoId = Convert.ToInt64(insertar.ExecuteScalar());
        }
        else
        {
            using var actualizar = conexion.CreateCommand();
            actualizar.Transaction = tx;
            actualizar.CommandText =
                "UPDATE documentos SET ruta=$r,carpeta_raiz=$c,nombre=$n,tamano=$z,modificado=$m,huella=$h,estado='ok',tiene_texto=CASE WHEN $tt=1 THEN 1 ELSE tiene_texto END,indexado_en=$f,estado_baja='activo',fecha_baja=NULL WHERE id=$id;";
            AgregarDatosDocumento(actualizar, ruta, tamano, modificado, huella, tieneTexto);
            actualizar.Parameters.AddWithValue("$id", documentoId);
            actualizar.ExecuteNonQuery();
        }
        if (nuevo || cambioRuta)
            AuditoriaDatos.Registrar(
                conexion,
                tx,
                nuevo ? "registrar_documento" : "mover_documento",
                ruta,
                null,
                huella,
                app
            );
        VersionDocumento version = ObtenerOCrearVersion(tx, documentoId, huella, ruta, app);
        return (documentoId, version);
    }

    private static void AgregarDatosDocumento(
        SqliteCommand comando,
        string ruta,
        long tamano,
        DateTime modificado,
        string huella,
        bool tieneTexto
    )
    {
        comando.Parameters.AddWithValue("$r", ruta);
        comando.Parameters.AddWithValue("$c", Path.GetDirectoryName(ruta) ?? "");
        comando.Parameters.AddWithValue("$n", Path.GetFileName(ruta));
        comando.Parameters.AddWithValue("$z", tamano);
        comando.Parameters.AddWithValue("$m", modificado.ToString("o"));
        comando.Parameters.AddWithValue("$h", huella);
        comando.Parameters.AddWithValue("$tt", tieneTexto ? 1 : 0);
        comando.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
    }

    private long ObtenerOCrearCampo(
        SqliteTransaction tx,
        long identificacionId,
        ValorDocumentoLeido valor
    )
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO campos_documento(identificacion_id,nombre,nombre_estable,tipo_dato,activo,origen_lectura) VALUES($i,$n,$e,$t,1,$o) ON CONFLICT(identificacion_id,nombre_estable) DO UPDATE SET nombre=excluded.nombre,activo=1 RETURNING id;";
        cmd.Parameters.AddWithValue("$i", identificacionId);
        cmd.Parameters.AddWithValue("$n", valor.Nombre);
        cmd.Parameters.AddWithValue("$e", valor.NombreEstable);
        cmd.Parameters.AddWithValue("$t", valor.TipoDato);
        cmd.Parameters.AddWithValue("$o", valor.OrigenLectura);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private VersionDocumento ObtenerOCrearVersion(
        SqliteTransaction tx,
        long documentoId,
        string huella,
        string ruta,
        string app = "Nucleo"
    )
    {
        using var buscar = conexion.CreateCommand();
        buscar.Transaction = tx;
        buscar.CommandText =
            "SELECT id,ruta_observada,registrada_en,estado FROM versiones_documento WHERE documento_id=$d AND huella=$h AND estado='vigente' LIMIT 1;";
        buscar.Parameters.AddWithValue("$d", documentoId);
        buscar.Parameters.AddWithValue("$h", huella);
        using var reader = buscar.ExecuteReader();
        if (reader.Read())
        {
            var v = new VersionDocumento(
                reader.GetInt64(0),
                documentoId,
                huella,
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2)),
                reader.GetString(3)
            );
            reader.Close();
            string rutaCompleta = Path.GetFullPath(ruta);
            if (!string.Equals(v.RutaObservada, rutaCompleta, StringComparison.OrdinalIgnoreCase))
            {
                using var update = conexion.CreateCommand();
                update.Transaction = tx;
                update.CommandText =
                    "UPDATE versiones_documento SET ruta_observada=$r WHERE id=$id;";
                update.Parameters.AddWithValue("$r", rutaCompleta);
                update.Parameters.AddWithValue("$id", v.Id);
                update.ExecuteNonQuery();
                AuditoriaDatos.Registrar(
                    conexion,
                    tx,
                    "cambiar_ruta_version",
                    $"version:{v.Id}",
                    rutaCompleta,
                    huella,
                    app
                );
                return v with { RutaObservada = rutaCompleta };
            }
            return v;
        }
        reader.Close();
        using (var anterior = conexion.CreateCommand())
        {
            anterior.Transaction = tx;
            anterior.CommandText =
                "UPDATE versiones_documento SET estado='anulada',fecha_anulacion=$f WHERE documento_id=$d AND estado='vigente';";
            anterior.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            anterior.Parameters.AddWithValue("$d", documentoId);
            anterior.ExecuteNonQuery();
        }
        string fecha = DateTime.Now.ToString("o");
        using var insertar = conexion.CreateCommand();
        insertar.Transaction = tx;
        insertar.CommandText =
            "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES($d,$h,$r,$f) RETURNING id;";
        insertar.Parameters.AddWithValue("$d", documentoId);
        insertar.Parameters.AddWithValue("$h", huella);
        insertar.Parameters.AddWithValue("$r", Path.GetFullPath(ruta));
        insertar.Parameters.AddWithValue("$f", fecha);
        long id = Convert.ToInt64(insertar.ExecuteScalar());
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "crear_version",
            $"documento:{documentoId}",
            ruta,
            huella,
            app: app
        );
        return new(
            id,
            documentoId,
            huella,
            Path.GetFullPath(ruta),
            DateTime.Parse(fecha),
            "vigente"
        );
    }

    private long InsertarValor(
        SqliteTransaction tx,
        long versionId,
        long campoId,
        string original,
        string clave,
        string origen,
        double? confianza
    )
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO valores_documento(version_id,campo_id,valor_original,valor_clave,origen,confianza,fecha_creacion,dato_diccionario_id) VALUES($v,$c,$o,$k,$g,$f,$d,(SELECT dato_diccionario_id FROM campos_documento WHERE id=$c)) RETURNING id;";
        cmd.Parameters.AddWithValue("$v", versionId);
        cmd.Parameters.AddWithValue("$c", campoId);
        cmd.Parameters.AddWithValue("$o", original);
        cmd.Parameters.AddWithValue("$k", clave);
        cmd.Parameters.AddWithValue("$g", origen);
        cmd.Parameters.AddWithValue("$f", (object?)confianza ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$d", DateTime.Now.ToString("o"));
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public IReadOnlyList<VersionDocumento> BuscarVersiones(string huella)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,documento_id,huella,ruta_observada,registrada_en,estado FROM versiones_documento WHERE huella=$huella ORDER BY id;";
        comando.Parameters.AddWithValue("$huella", huella);
        using var lector = comando.ExecuteReader();
        var versiones = new List<VersionDocumento>();
        while (lector.Read())
            versiones.Add(
                new(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    lector.GetString(2),
                    lector.GetString(3),
                    DateTime.Parse(lector.GetString(4)),
                    lector.GetString(5)
                )
            );
        return versiones;
    }

    public long GuardarCampo(CampoDocumento campo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campo.Nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(campo.NombreEstable);
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            campo.Id == 0
                ? "INSERT INTO campos_documento(identificacion_id,nombre,nombre_estable,tipo_dato,activo,origen_lectura,dato_diccionario_id) VALUES($i,$n,$e,$t,$a,$o,$d) RETURNING id;"
                : "UPDATE campos_documento SET identificacion_id=$i,nombre=$n,nombre_estable=$e,tipo_dato=$t,activo=$a,origen_lectura=$o,dato_diccionario_id=$d WHERE id=$id RETURNING id;";
        cmd.Parameters.AddWithValue("$i", campo.IdentificacionId);
        cmd.Parameters.AddWithValue("$n", campo.Nombre.Trim());
        cmd.Parameters.AddWithValue("$e", campo.NombreEstable.Trim());
        cmd.Parameters.AddWithValue("$t", campo.TipoDato);
        cmd.Parameters.AddWithValue("$a", campo.Activo ? 1 : 0);
        cmd.Parameters.AddWithValue("$o", campo.OrigenLectura);
        cmd.Parameters.AddWithValue("$d", (object?)campo.DatoDiccionarioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", campo.Id);
        long id = Convert.ToInt64(
            cmd.ExecuteScalar()
                ?? throw new InvalidOperationException("No existe el campo de documento.")
        );
        AuditoriaDatos.Registrar(conexion, tx, "guardar_campo", $"campo:{id}");
        tx.Commit();
        return id;
    }

    public IReadOnlyList<CampoDocumento> ListarCampos(
        long identificacionId,
        bool incluirInactivos = false
    )
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,identificacion_id,nombre,nombre_estable,tipo_dato,activo,origen_lectura,dato_diccionario_id FROM campos_documento WHERE identificacion_id=$id"
            + (incluirInactivos ? "" : " AND activo=1")
            + " ORDER BY nombre;";
        cmd.Parameters.AddWithValue("$id", identificacionId);
        using var r = cmd.ExecuteReader();
        var l = new List<CampoDocumento>();
        while (r.Read())
            l.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetString(2),
                    r.GetString(3),
                    r.GetString(4),
                    r.GetInt32(5) != 0,
                    r.GetString(6),
                    r.IsDBNull(7) ? null : r.GetString(7)
                )
            );
        return l;
    }

    public VersionDocumento RegistrarVersion(long documentoId, string huella, string rutaObservada)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaObservada);
        using var tx = conexion.BeginTransaction();
        using (var existente = conexion.CreateCommand())
        {
            existente.Transaction = tx;
            existente.CommandText =
                "SELECT id,huella,ruta_observada,registrada_en,estado FROM versiones_documento WHERE documento_id=$d AND huella=$h AND estado='vigente' LIMIT 1;";
            existente.Parameters.AddWithValue("$d", documentoId);
            existente.Parameters.AddWithValue("$h", huella);
            using var r = existente.ExecuteReader();
            if (r.Read())
            {
                var v = new VersionDocumento(
                    r.GetInt64(0),
                    documentoId,
                    r.GetString(1),
                    r.GetString(2),
                    DateTime.Parse(r.GetString(3)),
                    r.GetString(4)
                );
                r.Close();
                if (v.RutaObservada != rutaObservada)
                {
                    using var cambiar = conexion.CreateCommand();
                    cambiar.Transaction = tx;
                    cambiar.CommandText =
                        "UPDATE versiones_documento SET ruta_observada=$r WHERE id=$id;";
                    cambiar.Parameters.AddWithValue("$r", rutaObservada);
                    cambiar.Parameters.AddWithValue("$id", v.Id);
                    cambiar.ExecuteNonQuery();
                    AuditoriaDatos.Registrar(
                        conexion,
                        tx,
                        "cambiar_ruta_version",
                        $"version:{v.Id}",
                        rutaObservada,
                        huella
                    );
                    v = v with { RutaObservada = rutaObservada };
                }
                tx.Commit();
                return v;
            }
        }
        using (var anterior = conexion.CreateCommand())
        {
            anterior.Transaction = tx;
            anterior.CommandText =
                "UPDATE versiones_documento SET estado='anulada',fecha_anulacion=$f WHERE documento_id=$d AND estado='vigente';";
            anterior.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
            anterior.Parameters.AddWithValue("$d", documentoId);
            anterior.ExecuteNonQuery();
        }
        string fecha = DateTime.Now.ToString("o");
        long id;
        using (var cmd = conexion.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO versiones_documento(documento_id,huella,ruta_observada,registrada_en) VALUES($d,$h,$r,$f) RETURNING id;";
            cmd.Parameters.AddWithValue("$d", documentoId);
            cmd.Parameters.AddWithValue("$h", huella);
            cmd.Parameters.AddWithValue("$r", rutaObservada);
            cmd.Parameters.AddWithValue("$f", fecha);
            id = Convert.ToInt64(cmd.ExecuteScalar());
        }
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "crear_version",
            $"documento:{documentoId}",
            rutaObservada,
            huella
        );
        tx.Commit();
        return new(id, documentoId, huella, rutaObservada, DateTime.Parse(fecha), "vigente");
    }

    public bool AnularVersion(long id)
    {
        using var tx = conexion.BeginTransaction();
        long documentoId;
        string huella;
        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = tx;
            consulta.CommandText =
                "SELECT documento_id, huella FROM versiones_documento WHERE id=$id AND estado='vigente';";
            consulta.Parameters.AddWithValue("$id", id);
            using var lector = consulta.ExecuteReader();
            if (!lector.Read())
            {
                tx.Commit();
                return false;
            }
            documentoId = lector.GetInt64(0);
            huella = lector.GetString(1);
        }
        using (var comando = conexion.CreateCommand())
        {
            comando.Transaction = tx;
            comando.CommandText =
                "UPDATE versiones_documento SET estado='anulada',fecha_anulacion=$fecha WHERE id=$id;";
            comando.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("o"));
            comando.Parameters.AddWithValue("$id", id);
            comando.ExecuteNonQuery();
        }
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "anular_version",
            $"documento:{documentoId}",
            null,
            huella
        );
        tx.Commit();
        return true;
    }

    public long GuardarValor(
        long versionId,
        long campoId,
        string original,
        string clave,
        string origen,
        double? confianza = null,
        string? datoDiccionarioId = null
    )
    {
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO valores_documento(version_id,campo_id,valor_original,valor_clave,origen,confianza,fecha_creacion,dato_diccionario_id) VALUES($v,$c,$o,$k,$g,$f,$d,COALESCE($dato,(SELECT dato_diccionario_id FROM campos_documento WHERE id=$c))) RETURNING id;";
        cmd.Parameters.AddWithValue("$v", versionId);
        cmd.Parameters.AddWithValue("$c", campoId);
        cmd.Parameters.AddWithValue("$o", original);
        cmd.Parameters.AddWithValue("$k", clave);
        cmd.Parameters.AddWithValue("$g", origen);
        cmd.Parameters.AddWithValue("$f", (object?)confianza ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$d", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$dato", (object?)datoDiccionarioId ?? DBNull.Value);
        long id = Convert.ToInt64(cmd.ExecuteScalar());
        AuditoriaDatos.Registrar(conexion, tx, "crear_valor", $"valor:{id}", null);
        tx.Commit();
        return id;
    }

    public bool AnularValor(long id)
    {
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE valores_documento SET estado='anulado',fecha_anulacion=$f WHERE id=$id AND estado='vigente';";
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$id", id);
        bool cambio = cmd.ExecuteNonQuery() > 0;
        if (cambio)
            AuditoriaDatos.Registrar(conexion, tx, "anular_valor", $"valor:{id}");
        tx.Commit();
        return cambio;
    }

    public IReadOnlyList<ValorDocumento> BuscarValores(long campoId, string valorClave)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,version_id,campo_id,valor_original,valor_clave,origen,confianza,estado,fecha_creacion,dato_diccionario_id FROM valores_documento WHERE campo_id=$c AND valor_clave=$v AND estado='vigente' ORDER BY id;";
        cmd.Parameters.AddWithValue("$c", campoId);
        cmd.Parameters.AddWithValue("$v", valorClave);
        using var r = cmd.ExecuteReader();
        var l = new List<ValorDocumento>();
        while (r.Read())
            l.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetInt64(2),
                    r.GetString(3),
                    r.GetString(4),
                    r.GetString(5),
                    r.IsDBNull(6) ? null : r.GetDouble(6),
                    r.GetString(7),
                    DateTime.Parse(r.GetString(8)),
                    r.IsDBNull(9) ? null : r.GetString(9)
                )
            );
        return l;
    }

    public long GuardarMarca(MarcaVersion marca)
    {
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO marcas_version(version_id,tipo,pagina,x,y,ancho,alto,texto,creada_en) VALUES($v,$t,$p,$x,$y,$a,$l,$z,$f) RETURNING id;";
        cmd.Parameters.AddWithValue("$v", marca.VersionId);
        cmd.Parameters.AddWithValue("$t", marca.Tipo);
        cmd.Parameters.AddWithValue("$p", marca.Pagina);
        cmd.Parameters.AddWithValue("$x", marca.X);
        cmd.Parameters.AddWithValue("$y", marca.Y);
        cmd.Parameters.AddWithValue("$a", marca.Ancho);
        cmd.Parameters.AddWithValue("$l", marca.Alto);
        cmd.Parameters.AddWithValue("$z", (object?)marca.Texto ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$f", marca.CreadaEn.ToString("o"));
        long id = Convert.ToInt64(cmd.ExecuteScalar());
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "guardar_marca",
            $"marca:{id}",
            $"version:{marca.VersionId}",
            app: "Buscadero"
        );
        tx.Commit();
        return id;
    }

    public bool AnularMarca(long id)
    {
        using var tx = conexion.BeginTransaction();
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE marcas_version SET estado='anulada',fecha_anulacion=$f WHERE id=$id AND estado='activa';";
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$id", id);
        bool cambio = cmd.ExecuteNonQuery() > 0;
        if (cambio)
            AuditoriaDatos.Registrar(conexion, tx, "anular_marca", $"marca:{id}", app: "Buscadero");
        tx.Commit();
        return cambio;
    }

    public IReadOnlyList<MarcaVersion> BuscarMarcas(long versionId, bool incluirAnuladas = false)
    {
        using var cmd = conexion.CreateCommand();
        cmd.CommandText =
            "SELECT id,version_id,tipo,pagina,x,y,ancho,alto,texto,creada_en,estado FROM marcas_version WHERE version_id=$v"
            + (incluirAnuladas ? "" : " AND estado='activa'")
            + " ORDER BY id;";
        cmd.Parameters.AddWithValue("$v", versionId);
        using var r = cmd.ExecuteReader();
        var marcas = new List<MarcaVersion>();
        while (r.Read())
            marcas.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetString(2),
                    r.GetInt32(3),
                    r.GetDouble(4),
                    r.GetDouble(5),
                    r.GetDouble(6),
                    r.GetDouble(7),
                    r.IsDBNull(8) ? null : r.GetString(8),
                    DateTime.Parse(r.GetString(9)),
                    r.GetString(10)
                )
            );
        return marcas;
    }

    public IReadOnlyList<MarcaVersion> SincronizarMarcas(
        long versionId,
        IReadOnlyList<MarcaVersion> recibidas
    )
    {
        using var tx = conexion.BeginTransaction();
        var actuales = BuscarMarcasEnTransaccion(tx, versionId).ToDictionary(m => m.Id);
        var incluidas = new HashSet<long>();
        var resultado = new List<MarcaVersion>(recibidas.Count);
        foreach (var marca in recibidas)
        {
            if (marca.Id > 0 && actuales.TryGetValue(marca.Id, out var anterior))
            {
                incluidas.Add(marca.Id);
                if (Coinciden(anterior, marca))
                {
                    resultado.Add(anterior);
                    continue;
                }
                AnularMarcaEnTransaccion(tx, marca.Id);
            }
            resultado.Add(
                InsertarMarcaEnTransaccion(tx, marca with { Id = 0, VersionId = versionId })
            );
        }
        foreach (var id in actuales.Keys.Except(incluidas))
            AnularMarcaEnTransaccion(tx, id);
        tx.Commit();
        return resultado;
    }

    private IReadOnlyList<MarcaVersion> BuscarMarcasEnTransaccion(
        SqliteTransaction tx,
        long versionId
    )
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "SELECT id,version_id,tipo,pagina,x,y,ancho,alto,texto,creada_en,estado FROM marcas_version WHERE version_id=$v AND estado='activa' ORDER BY id;";
        cmd.Parameters.AddWithValue("$v", versionId);
        using var r = cmd.ExecuteReader();
        var marcas = new List<MarcaVersion>();
        while (r.Read())
            marcas.Add(
                new(
                    r.GetInt64(0),
                    r.GetInt64(1),
                    r.GetString(2),
                    r.GetInt32(3),
                    r.GetDouble(4),
                    r.GetDouble(5),
                    r.GetDouble(6),
                    r.GetDouble(7),
                    r.IsDBNull(8) ? null : r.GetString(8),
                    DateTime.Parse(r.GetString(9)),
                    r.GetString(10)
                )
            );
        return marcas;
    }

    private MarcaVersion InsertarMarcaEnTransaccion(SqliteTransaction tx, MarcaVersion marca)
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "INSERT INTO marcas_version(version_id,tipo,pagina,x,y,ancho,alto,texto,creada_en) VALUES($v,$t,$p,$x,$y,$a,$l,$z,$f) RETURNING id;";
        cmd.Parameters.AddWithValue("$v", marca.VersionId);
        cmd.Parameters.AddWithValue("$t", marca.Tipo);
        cmd.Parameters.AddWithValue("$p", marca.Pagina);
        cmd.Parameters.AddWithValue("$x", marca.X);
        cmd.Parameters.AddWithValue("$y", marca.Y);
        cmd.Parameters.AddWithValue("$a", marca.Ancho);
        cmd.Parameters.AddWithValue("$l", marca.Alto);
        cmd.Parameters.AddWithValue("$z", (object?)marca.Texto ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$f", marca.CreadaEn.ToString("o"));
        long id = Convert.ToInt64(cmd.ExecuteScalar());
        AuditoriaDatos.Registrar(
            conexion,
            tx,
            "guardar_marca",
            $"marca:{id}",
            $"version:{marca.VersionId}",
            app: "Buscadero"
        );
        return marca with { Id = id, Estado = "activa" };
    }

    private void AnularMarcaEnTransaccion(SqliteTransaction tx, long id)
    {
        using var cmd = conexion.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            "UPDATE marcas_version SET estado='anulada',fecha_anulacion=$f WHERE id=$id AND estado='activa';";
        cmd.Parameters.AddWithValue("$f", DateTime.Now.ToString("o"));
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteNonQuery() > 0)
            AuditoriaDatos.Registrar(conexion, tx, "anular_marca", $"marca:{id}", app: "Buscadero");
    }

    private static bool Coinciden(MarcaVersion anterior, MarcaVersion nueva) =>
        anterior.Tipo == nueva.Tipo
        && anterior.Pagina == nueva.Pagina
        && anterior.X == nueva.X
        && anterior.Y == nueva.Y
        && anterior.Ancho == nueva.Ancho
        && anterior.Alto == nueva.Alto
        && anterior.Texto == nueva.Texto;
}
