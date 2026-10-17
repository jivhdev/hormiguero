using Hormiguero.Nucleo.Utilidades;
using Microsoft.Data.Sqlite;

namespace Hormiguero.Nucleo.Datos;

public sealed class RegistroDocumentoEnCadena(SqliteConnection conexion)
{
    public long RegistrarDesdeArchivo(
        string ruta,
        long lugarId,
        EsquemaDiccionario esquema,
        long cadenaId,
        string? numeroManual = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        var lugar = esquema.Lugares.Single(l => l.Id == lugarId);
        string numero =
            numeroManual?.Trim()
            ?? string.Concat(Path.GetFileNameWithoutExtension(ruta).Where(char.IsAsciiDigit))
                .TrimStart('0');
        if (numero.Length == 0)
            throw new ArgumentException("Indique el número del documento.", nameof(numeroManual));

        var archivo = new FileInfo(Path.GetFullPath(ruta));
        if (!archivo.Exists)
            throw new FileNotFoundException(
                "No existe el documento seleccionado.",
                archivo.FullName
            );
        var tipo = RepositorioEsquemasDiccionario
            .ListarTiposDocumento(conexion)
            .SingleOrDefault(t => t.Id == lugar.DatoTipoId);
        if (tipo.Id is null)
            throw new InvalidOperationException("El tipo de documento ya no está disponible.");

        var identificaciones = new Identificaciones(conexion);
        var existente = identificaciones
            .Listar()
            .FirstOrDefault(i => i.Tipo == tipo.Nombre && i.Emisor == esquema.Proveedor);
        long identificacionId = identificaciones.Guardar(
            new(
                existente?.Id ?? 0,
                tipo.Nombre,
                esquema.Proveedor,
                existente?.Datos ?? "{}",
                "Recibido",
                $"{tipo.Nombre} · {esquema.Proveedor}"
            )
        );
        var campo =
            new RepositorioDocumentosDatos(conexion)
                .ListarCampos(identificacionId)
                .FirstOrDefault(c => c.NombreEstable == lugar.DatoTipoId)
            ?? new CampoDocumento(
                0,
                identificacionId,
                tipo.Nombre,
                lugar.DatoTipoId,
                "texto",
                true,
                "manual",
                lugar.DatoTipoId
            );
        long campoId = new RepositorioDocumentosDatos(conexion).GuardarCampo(campo);
        new RepositorioDatosEnlazantes(conexion).GuardarDatoTipo(
            new(
                0,
                identificacionId,
                lugar.DatoTipoId,
                "sin_texto",
                campoId,
                1,
                0,
                0,
                1,
                1,
                true,
                true
            )
        );

        string huella = Huella.Calcular(archivo.FullName);
        string nombre = Path.GetFileNameWithoutExtension(ruta);
        var valores = new[]
        {
            new ValorDocumentoLeido(
                "Emisor",
                "emisor",
                esquema.Proveedor,
                esquema.Proveedor,
                "manual"
            ),
            new ValorDocumentoLeido("Tipo", "tipo", tipo.Nombre, tipo.Nombre, "manual"),
            new ValorDocumentoLeido(
                tipo.Nombre,
                lugar.DatoTipoId,
                numero,
                DiccionarioDatosEnlazantes.ClaveDeEnlace(lugar.DatoTipoId, numero),
                "manual",
                DatoDiccionarioId: lugar.DatoTipoId
            ),
            new ValorDocumentoLeido(
                "Nombre de archivo",
                "nombre_archivo",
                nombre,
                nombre,
                "manual"
            ),
            new ValorDocumentoLeido(
                "Nombre o razón social del proveedor",
                "nombre_proveedor",
                esquema.Proveedor,
                DiccionarioDatosEnlazantes.ClaveDeEnlace("nombre_proveedor", esquema.Proveedor),
                "manual",
                DatoDiccionarioId: "nombre_proveedor"
            ),
        };
        var publicado = new RepositorioDocumentosDatos(conexion).PublicarDocumento(
            archivo.FullName,
            archivo.Length,
            archivo.LastWriteTimeUtc,
            huella,
            esquema.Proveedor,
            tipo.Nombre,
            valores,
            "busqueda_buscadero"
        );

        new MotorCadenas(conexion).ProcesarVersion(publicado.Version.Id);
        UbicarEnCadena(publicado.Version.Id, lugarId, cadenaId);
        return publicado.Version.Id;
    }

    public long RegistrarSinClasificacion(
        string ruta,
        string tipoId,
        string proveedor,
        string numero
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(proveedor);
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        var archivo = new FileInfo(Path.GetFullPath(ruta));
        if (!archivo.Exists)
            throw new FileNotFoundException(
                "No existe el documento seleccionado.",
                archivo.FullName
            );
        var tipo = RepositorioEsquemasDiccionario
            .ListarTiposDocumento(conexion)
            .SingleOrDefault(t => t.Id == tipoId);
        if (tipo.Id is null)
            throw new InvalidOperationException("El tipo de documento ya no está disponible.");
        var identificaciones = new Identificaciones(conexion);
        var existente = identificaciones
            .Listar()
            .FirstOrDefault(i => i.Tipo == tipo.Nombre && i.Emisor == proveedor);
        long identificacionId = identificaciones.Guardar(
            new(
                existente?.Id ?? 0,
                tipo.Nombre,
                proveedor,
                existente?.Datos ?? "{}",
                "Recibido",
                $"{tipo.Nombre} · {proveedor}"
            )
        );
        var documentos = new RepositorioDocumentosDatos(conexion);
        var campo =
            documentos.ListarCampos(identificacionId).FirstOrDefault(c => c.NombreEstable == tipoId)
            ?? new CampoDocumento(
                0,
                identificacionId,
                tipo.Nombre,
                tipoId,
                "texto",
                true,
                "manual",
                tipoId
            );
        long campoId = documentos.GuardarCampo(campo);
        new RepositorioDatosEnlazantes(conexion).GuardarDatoTipo(
            new(0, identificacionId, tipoId, "sin_texto", campoId, 1, 0, 0, 1, 1, true, true)
        );
        string nombre = Path.GetFileNameWithoutExtension(archivo.Name);
        var valores = new[]
        {
            new ValorDocumentoLeido("Emisor", "emisor", proveedor, proveedor, "manual"),
            new ValorDocumentoLeido("Tipo", "tipo", tipo.Nombre, tipo.Nombre, "manual"),
            new ValorDocumentoLeido(
                tipo.Nombre,
                tipoId,
                numero.Trim(),
                DiccionarioDatosEnlazantes.ClaveDeEnlace(tipoId, numero),
                "manual",
                DatoDiccionarioId: tipoId
            ),
            new ValorDocumentoLeido(
                "Nombre de archivo",
                "nombre_archivo",
                nombre,
                nombre,
                "manual"
            ),
            new ValorDocumentoLeido(
                "Nombre o razón social del proveedor",
                "nombre_proveedor",
                proveedor,
                DiccionarioDatosEnlazantes.ClaveDeEnlace("nombre_proveedor", proveedor),
                "manual",
                DatoDiccionarioId: "nombre_proveedor"
            ),
        };
        var publicado = documentos.PublicarDocumento(
            archivo.FullName,
            archivo.Length,
            archivo.LastWriteTimeUtc,
            Huella.Calcular(archivo.FullName),
            proveedor,
            tipo.Nombre,
            valores,
            "busqueda_carpeta"
        );
        return publicado.Version.Id;
    }

    private void UbicarEnCadena(long versionId, long lugarId, long cadenaId)
    {
        using var existe = conexion.CreateCommand();
        existe.CommandText =
            "SELECT EXISTS(SELECT 1 FROM ubicaciones_diccionario_todas WHERE version_id=$v AND cadena_id=$c AND estado='activo');";
        existe.Parameters.AddWithValue("$v", versionId);
        existe.Parameters.AddWithValue("$c", cadenaId);
        if (Convert.ToInt64(existe.ExecuteScalar()) != 0)
            return;

        using var info = conexion.CreateCommand();
        info.CommandText =
            "SELECT e.cardinalidad,COALESCE((SELECT MAX(linea_id) FROM ubicaciones_diccionario_todas WHERE cadena_id=$c),0)+1 FROM enlaces_esquema_diccionario e JOIN lugares_esquema_diccionario l ON l.esquema_id=(SELECT esquema_id FROM cadenas_diccionario WHERE id=$c) AND (l.id=e.lugar_a OR l.id=e.lugar_b) WHERE (e.lugar_a=$l OR e.lugar_b=$l) LIMIT 1;";
        info.Parameters.AddWithValue("$c", cadenaId);
        info.Parameters.AddWithValue("$l", lugarId);
        string? cardinalidad = null;
        long linea = 1;
        using (var r = info.ExecuteReader())
            if (r.Read())
            {
                cardinalidad = r.GetString(0);
                linea = r.GetInt64(1);
            }
        using var ocupado = conexion.CreateCommand();
        ocupado.CommandText =
            "SELECT EXISTS(SELECT 1 FROM ubicaciones_diccionario_todas WHERE cadena_id=$c AND lugar_id=$l AND estado='activo' AND ($card='uno_a_varios' OR linea_id IS NULL));";
        ocupado.Parameters.AddWithValue("$c", cadenaId);
        ocupado.Parameters.AddWithValue("$l", lugarId);
        ocupado.Parameters.AddWithValue("$card", (object?)cardinalidad ?? DBNull.Value);
        bool dudoso = Convert.ToInt64(ocupado.ExecuteScalar()) != 0;
        using var insertar = conexion.CreateCommand();
        insertar.CommandText =
            "INSERT INTO ubicaciones_cadena_diccionario(cadena_id,version_id,lugar_id,padre_id,linea_id,estado,motivo,orden) VALUES($c,$v,$l,NULL,$linea,$estado,$motivo,(SELECT COALESCE(MAX(orden),-1)+1 FROM ubicaciones_cadena_diccionario WHERE cadena_id=$c));";
        insertar.Parameters.AddWithValue("$c", cadenaId);
        insertar.Parameters.AddWithValue("$v", versionId);
        insertar.Parameters.AddWithValue("$l", lugarId);
        insertar.Parameters.AddWithValue(
            "$linea",
            (object?)(dudoso ? null : linea) ?? DBNull.Value
        );
        insertar.Parameters.AddWithValue("$estado", dudoso ? "dudoso" : "activo");
        insertar.Parameters.AddWithValue(
            "$motivo",
            dudoso ? "El lugar ya está ocupado en esta línea de la cadena." : DBNull.Value
        );
        insertar.ExecuteNonQuery();
        if (!dudoso)
            new RepositorioSituacionCadenaDiccionario(conexion).Recalcular(cadenaId, DateTime.Now);
    }
}
