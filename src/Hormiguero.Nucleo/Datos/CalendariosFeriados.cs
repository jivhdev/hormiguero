using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed record CalendarioFeriados(
    long Id,
    string Nombre,
    string PaisCodigo,
    string? Region,
    bool Activo,
    bool Predeterminado,
    string Origen
);

public sealed record Feriado(
    long Id,
    long CalendarioId,
    DateOnly Fecha,
    string Nombre,
    bool Activo,
    DateTime? FechaAnulacion
);

public sealed record ResumenImportacionFeriados(int Agregados, int Actualizados, int Reactivados);

public sealed class RepositorioCalendariosFeriados
{
    private const string AppAuditoria = "Nucleo";
    private readonly SqliteConnection conexion;

    public RepositorioCalendariosFeriados(SqliteConnection conexion)
    {
        this.conexion = conexion;
        AsegurarPrecargaChile();
    }

    public IReadOnlyList<CalendarioFeriados> ListarCalendarios()
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,nombre,pais_codigo,region,activo,predeterminado,origen "
            + "FROM calendarios_feriados ORDER BY predeterminado DESC, nombre COLLATE NOCASE;";
        using var lector = comando.ExecuteReader();
        var lista = new List<CalendarioFeriados>();
        while (lector.Read())
        {
            lista.Add(
                new CalendarioFeriados(
                    lector.GetInt64(0),
                    lector.GetString(1),
                    lector.GetString(2),
                    lector.IsDBNull(3) ? null : lector.GetString(3),
                    lector.GetInt32(4) != 0,
                    lector.GetInt32(5) != 0,
                    lector.GetString(6)
                )
            );
        }
        return lista;
    }

    public IReadOnlyList<Feriado> ListarFeriados(long calendarioId, bool incluirAnulados = true)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT id,calendario_id,fecha,nombre,activo,fecha_anulacion FROM feriados "
            + "WHERE calendario_id=$calendario AND ($incluir=1 OR activo=1) ORDER BY fecha;";
        comando.Parameters.AddWithValue("$calendario", calendarioId);
        comando.Parameters.AddWithValue("$incluir", incluirAnulados ? 1 : 0);
        using var lector = comando.ExecuteReader();
        var lista = new List<Feriado>();
        while (lector.Read())
        {
            lista.Add(
                new Feriado(
                    lector.GetInt64(0),
                    lector.GetInt64(1),
                    DateOnly.ParseExact(
                        lector.GetString(2),
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture
                    ),
                    lector.GetString(3),
                    lector.GetInt32(4) != 0,
                    lector.IsDBNull(5)
                        ? null
                        : DateTime.Parse(lector.GetString(5), CultureInfo.InvariantCulture)
                )
            );
        }
        return lista;
    }

    public long CrearCalendario(
        string nombre,
        string paisCodigo,
        string? region = null,
        bool predeterminado = false
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(paisCodigo);
        string ahora = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
        using var transaccion = conexion.BeginTransaction();
        if (predeterminado)
        {
            LimpiarPredeterminado(transaccion);
        }
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            "INSERT INTO calendarios_feriados(nombre,pais_codigo,region,activo,predeterminado,origen,creada_en,actualizada_en) "
            + "VALUES($nombre,$pais,$region,1,$predeterminado,'usuario',$ahora,$ahora) RETURNING id;";
        comando.Parameters.AddWithValue("$nombre", nombre.Trim());
        comando.Parameters.AddWithValue("$pais", paisCodigo.Trim());
        comando.Parameters.AddWithValue("$region", (object?)Normalizar(region) ?? DBNull.Value);
        comando.Parameters.AddWithValue("$predeterminado", predeterminado ? 1 : 0);
        comando.Parameters.AddWithValue("$ahora", ahora);
        long id = Convert.ToInt64(comando.ExecuteScalar());
        AuditoriaDatos.Registrar(
            conexion,
            transaccion,
            "crear_calendario_feriados",
            $"calendario:{id}",
            app: AppAuditoria
        );
        transaccion.Commit();
        return id;
    }

    public void ElegirPredeterminado(long calendarioId)
    {
        using var transaccion = conexion.BeginTransaction();
        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = transaccion;
            consulta.CommandText = "SELECT activo FROM calendarios_feriados WHERE id=$id;";
            consulta.Parameters.AddWithValue("$id", calendarioId);
            object? activo = consulta.ExecuteScalar();
            if (activo is null)
                throw new InvalidOperationException($"No existe el calendario {calendarioId}.");
            if (Convert.ToInt32(activo) == 0)
                throw new InvalidOperationException(
                    "No se puede elegir como predeterminado un calendario inactivo."
                );
        }
        LimpiarPredeterminado(transaccion);
        using (var comando = conexion.CreateCommand())
        {
            comando.Transaction = transaccion;
            comando.CommandText =
                "UPDATE calendarios_feriados SET predeterminado=1,actualizada_en=$ahora WHERE id=$id;";
            comando.Parameters.AddWithValue("$id", calendarioId);
            comando.Parameters.AddWithValue(
                "$ahora",
                DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
            );
            comando.ExecuteNonQuery();
        }
        AuditoriaDatos.Registrar(
            conexion,
            transaccion,
            "elegir_calendario_predeterminado",
            $"calendario:{calendarioId}",
            app: AppAuditoria
        );
        transaccion.Commit();
    }

    public long AgregarFeriado(long calendarioId, DateOnly fecha, string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var transaccion = conexion.BeginTransaction();
        string ahora = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            "INSERT INTO feriados(calendario_id,fecha,nombre,activo,fecha_anulacion,creada_en,actualizada_en) "
            + "VALUES($calendario,$fecha,$nombre,1,NULL,$ahora,$ahora) RETURNING id;";
        comando.Parameters.AddWithValue("$calendario", calendarioId);
        comando.Parameters.AddWithValue(
            "$fecha",
            fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        );
        comando.Parameters.AddWithValue("$nombre", nombre.Trim());
        comando.Parameters.AddWithValue("$ahora", ahora);
        try
        {
            long id = Convert.ToInt64(comando.ExecuteScalar());
            AuditoriaDatos.Registrar(
                conexion,
                transaccion,
                "agregar_feriado",
                $"feriado:{id}",
                $"calendario:{calendarioId}",
                app: AppAuditoria
            );
            transaccion.Commit();
            return id;
        }
        catch (SqliteException error)
            when (error.SqliteExtendedErrorCode == 1555 || error.SqliteExtendedErrorCode == 2067)
        {
            throw new InvalidOperationException(
                $"Ya existe un feriado para {fecha:yyyy-MM-dd} en ese calendario.",
                error
            );
        }
    }

    public void EditarFeriado(long feriadoId, DateOnly fecha, string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        using var transaccion = conexion.BeginTransaction();
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            "UPDATE feriados SET fecha=$fecha,nombre=$nombre,actualizada_en=$ahora "
            + "WHERE id=$id AND activo=1 RETURNING calendario_id;";
        comando.Parameters.AddWithValue("$id", feriadoId);
        comando.Parameters.AddWithValue(
            "$fecha",
            fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        );
        comando.Parameters.AddWithValue("$nombre", nombre.Trim());
        comando.Parameters.AddWithValue(
            "$ahora",
            DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
        );
        object? calendario;
        try
        {
            calendario = comando.ExecuteScalar();
        }
        catch (SqliteException error)
            when (error.SqliteExtendedErrorCode == 1555 || error.SqliteExtendedErrorCode == 2067)
        {
            throw new InvalidOperationException(
                "Ya existe un feriado para esa fecha en el calendario.",
                error
            );
        }
        if (calendario is null)
            throw new InvalidOperationException(
                $"No existe un feriado activo con identificador {feriadoId}."
            );
        AuditoriaDatos.Registrar(
            conexion,
            transaccion,
            "editar_feriado",
            $"feriado:{feriadoId}",
            $"calendario:{calendario}",
            app: AppAuditoria
        );
        transaccion.Commit();
    }

    public bool AnularFeriado(long feriadoId)
    {
        using var transaccion = conexion.BeginTransaction();
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            "UPDATE feriados SET activo=0,fecha_anulacion=$ahora,actualizada_en=$ahora "
            + "WHERE id=$id AND activo=1 RETURNING calendario_id;";
        comando.Parameters.AddWithValue("$id", feriadoId);
        comando.Parameters.AddWithValue(
            "$ahora",
            DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
        );
        object? calendario = comando.ExecuteScalar();
        if (calendario is null)
        {
            transaccion.Rollback();
            return false;
        }
        AuditoriaDatos.Registrar(
            conexion,
            transaccion,
            "anular_feriado",
            $"feriado:{feriadoId}",
            $"calendario:{calendario}",
            app: AppAuditoria
        );
        transaccion.Commit();
        return true;
    }

    public IReadOnlySet<DateOnly> ObtenerFeriadosActivos(
        long calendarioId,
        int anioInicial,
        int anioFinal
    )
    {
        if (anioInicial is < 1 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(anioInicial));
        if (anioFinal is < 1 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(anioFinal));
        if (anioInicial > anioFinal)
            throw new ArgumentException("El año inicial debe ser menor o igual que el año final.");
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT fecha FROM feriados WHERE calendario_id=$calendario AND activo=1 "
            + "AND fecha >= $desde AND fecha <= $hasta ORDER BY fecha;";
        comando.Parameters.AddWithValue("$calendario", calendarioId);
        comando.Parameters.AddWithValue("$desde", $"{anioInicial:D4}-01-01");
        comando.Parameters.AddWithValue("$hasta", $"{anioFinal:D4}-12-31");
        using var lector = comando.ExecuteReader();
        var conjunto = new HashSet<DateOnly>();
        while (lector.Read())
            conjunto.Add(
                DateOnly.ParseExact(lector.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture)
            );
        return conjunto;
    }

    public ResumenImportacionFeriados ImportarCsv(long calendarioId, string ruta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        using var lector = new StreamReader(
            ruta,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: true
        );
        return ImportarCsv(calendarioId, lector);
    }

    public ResumenImportacionFeriados ImportarCsv(long calendarioId, TextReader lector)
    {
        ArgumentNullException.ThrowIfNull(lector);
        var filas = new List<(DateOnly Fecha, string Nombre, int Linea)>();
        var fechas = new HashSet<DateOnly>();
        string? linea;
        int numeroLinea = 0;
        while ((linea = lector.ReadLine()) is not null)
        {
            numeroLinea++;
            string contenido = linea.Trim();
            if (contenido.Length == 0 || contenido.StartsWith('#'))
                continue;
            int separador = contenido.IndexOf(';');
            if (separador < 0)
                throw new FormatException($"Línea {numeroLinea}: se esperaba fecha;nombre.");
            string valorFecha = contenido[..separador].Trim();
            string nombre = contenido[(separador + 1)..].Trim();
            if (
                !DateOnly.TryParseExact(
                    valorFecha,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateOnly fecha
                )
            )
                throw new FormatException(
                    $"Línea {numeroLinea}: fecha inválida '{valorFecha}'; use el formato AAAA-MM-DD."
                );
            if (nombre.Length == 0)
                throw new FormatException(
                    $"Línea {numeroLinea}: el nombre del feriado está vacío."
                );
            if (!fechas.Add(fecha))
                throw new FormatException(
                    $"Línea {numeroLinea}: la fecha {fecha:yyyy-MM-dd} está repetida en el archivo."
                );
            filas.Add((fecha, nombre, numeroLinea));
        }

        using var transaccion = conexion.BeginTransaction();
        ValidarCalendario(calendarioId, transaccion);
        int agregados = 0;
        int actualizados = 0;
        int reactivados = 0;
        foreach (var fila in filas)
        {
            long? feriadoExistenteId;
            string? nombreExistente;
            bool activoExistente;
            using (var consulta = conexion.CreateCommand())
            {
                consulta.Transaction = transaccion;
                consulta.CommandText =
                    "SELECT id,nombre,activo FROM feriados WHERE calendario_id=$calendario AND fecha=$fecha;";
                consulta.Parameters.AddWithValue("$calendario", calendarioId);
                consulta.Parameters.AddWithValue(
                    "$fecha",
                    fila.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                );
                using var lectorFila = consulta.ExecuteReader();
                if (lectorFila.Read())
                {
                    feriadoExistenteId = lectorFila.GetInt64(0);
                    nombreExistente = lectorFila.GetString(1);
                    activoExistente = lectorFila.GetInt32(2) != 0;
                }
                else
                {
                    feriadoExistenteId = null;
                    nombreExistente = null;
                    activoExistente = false;
                }
            }

            if (feriadoExistenteId is long idExistente)
            {
                if (activoExistente && nombreExistente == fila.Nombre)
                    continue;

                string accion = activoExistente
                    ? "actualizar_feriado_importado"
                    : "reactivar_feriado_importado";
                using var actualizar = conexion.CreateCommand();
                actualizar.Transaction = transaccion;
                actualizar.CommandText =
                    "UPDATE feriados SET nombre=$nombre,activo=1,fecha_anulacion=NULL,actualizada_en=$ahora WHERE id=$id;";
                actualizar.Parameters.AddWithValue("$nombre", fila.Nombre);
                actualizar.Parameters.AddWithValue(
                    "$ahora",
                    DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
                );
                actualizar.Parameters.AddWithValue("$id", idExistente);
                actualizar.ExecuteNonQuery();
                AuditoriaDatos.Registrar(
                    conexion,
                    transaccion,
                    accion,
                    $"feriado:{idExistente}",
                    $"calendario:{calendarioId}",
                    app: AppAuditoria
                );
                if (activoExistente)
                    actualizados++;
                else
                    reactivados++;
                continue;
            }

            using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText =
                "INSERT INTO feriados(calendario_id,fecha,nombre,activo,fecha_anulacion,creada_en,actualizada_en) "
                + "VALUES($calendario,$fecha,$nombre,1,NULL,$ahora,$ahora) RETURNING id;";
            comando.Parameters.AddWithValue("$calendario", calendarioId);
            comando.Parameters.AddWithValue(
                "$fecha",
                fila.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            );
            comando.Parameters.AddWithValue("$nombre", fila.Nombre);
            comando.Parameters.AddWithValue(
                "$ahora",
                DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
            );
            try
            {
                long id = Convert.ToInt64(comando.ExecuteScalar());
                AuditoriaDatos.Registrar(
                    conexion,
                    transaccion,
                    "importar_feriado",
                    $"feriado:{id}",
                    $"calendario:{calendarioId}",
                    app: AppAuditoria
                );
                agregados++;
            }
            catch (SqliteException error)
                when (error.SqliteExtendedErrorCode == 1555 || error.SqliteExtendedErrorCode == 2067
                )
            {
                throw new InvalidOperationException(
                    $"Línea {fila.Linea}: ya existe un feriado para {fila.Fecha:yyyy-MM-dd} en ese calendario.",
                    error
                );
            }
        }
        transaccion.Commit();
        return new ResumenImportacionFeriados(agregados, actualizados, reactivados);
    }

    public void ExportarCsv(long calendarioId, string ruta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        string? carpeta = Path.GetDirectoryName(Path.GetFullPath(ruta));
        if (!string.IsNullOrEmpty(carpeta))
            Directory.CreateDirectory(carpeta);
        using var escritor = new StreamWriter(ruta, append: false, new UTF8Encoding(false));
        ExportarCsv(calendarioId, escritor);
    }

    public void ExportarCsv(long calendarioId, TextWriter escritor)
    {
        ArgumentNullException.ThrowIfNull(escritor);
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT fecha,nombre FROM feriados WHERE calendario_id=$calendario AND activo=1 ORDER BY fecha;";
        comando.Parameters.AddWithValue("$calendario", calendarioId);
        using var lector = comando.ExecuteReader();
        while (lector.Read())
            escritor.WriteLine($"{lector.GetString(0)};{lector.GetString(1)}");
    }

    private void AsegurarPrecargaChile()
    {
        using var transaccion = conexion.BeginTransaction();
        using (var existe = conexion.CreateCommand())
        {
            existe.Transaction = transaccion;
            existe.CommandText =
                "SELECT 1 FROM calendarios_feriados WHERE nombre='Chile' COLLATE NOCASE LIMIT 1;";
            if (existe.ExecuteScalar() is not null)
            {
                transaccion.Commit();
                return;
            }
        }
        var ensamblado = Assembly.GetExecutingAssembly();
        string recurso = ensamblado
            .GetManifestResourceNames()
            .Single(n => n.EndsWith("feriados-chile.csv", StringComparison.Ordinal));
        using var flujo = ensamblado.GetManifestResourceStream(recurso)!;
        using var lector = new StreamReader(flujo, Encoding.UTF8);
        string ahora = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
        using (var calendario = conexion.CreateCommand())
        {
            calendario.Transaction = transaccion;
            calendario.CommandText =
                "INSERT INTO calendarios_feriados(nombre,pais_codigo,region,activo,predeterminado,origen,creada_en,actualizada_en) "
                + "VALUES('Chile','CL',NULL,1,1,'precarga', $ahora,$ahora) RETURNING id;";
            calendario.Parameters.AddWithValue("$ahora", ahora);
            long id = Convert.ToInt64(calendario.ExecuteScalar());
            while (lector.ReadLine() is { } linea)
            {
                string contenido = linea.Trim();
                if (contenido.Length == 0 || contenido.StartsWith('#'))
                    continue;
                int separador = contenido.IndexOf(';');
                if (
                    separador <= 0
                    || !DateOnly.TryParseExact(
                        contenido[..separador],
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateOnly fecha
                    )
                )
                    throw new InvalidDataException(
                        "El archivo de feriados precargados contiene una fecha inválida."
                    );
                string nombre = contenido[(separador + 1)..].Trim();
                using var feriado = conexion.CreateCommand();
                feriado.Transaction = transaccion;
                feriado.CommandText =
                    "INSERT INTO feriados(calendario_id,fecha,nombre,activo,fecha_anulacion,creada_en,actualizada_en) "
                    + "VALUES($id,$fecha,$nombre,1,NULL,$ahora,$ahora);";
                feriado.Parameters.AddWithValue("$id", id);
                feriado.Parameters.AddWithValue(
                    "$fecha",
                    fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                );
                feriado.Parameters.AddWithValue("$nombre", nombre);
                feriado.Parameters.AddWithValue("$ahora", ahora);
                feriado.ExecuteNonQuery();
            }
        }
        transaccion.Commit();
    }

    private void LimpiarPredeterminado(SqliteTransaction transaccion)
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText =
            "UPDATE calendarios_feriados SET predeterminado=0 WHERE predeterminado=1;";
        comando.ExecuteNonQuery();
    }

    private void ValidarCalendario(long calendarioId, SqliteTransaction transaccion)
    {
        using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = "SELECT 1 FROM calendarios_feriados WHERE id=$id AND activo=1;";
        comando.Parameters.AddWithValue("$id", calendarioId);
        if (comando.ExecuteScalar() is null)
            throw new InvalidOperationException(
                $"No existe un calendario activo con identificador {calendarioId}."
            );
    }

    private static string? Normalizar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
