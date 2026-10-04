namespace Archivero.Datos;

/// <summary>
/// Tipo de organización de subcarpetas dentro de la carpeta madre (Caso-3). El orden sigue la
/// lista completa del Paso 3b, por granularidad creciente. <see cref="Personalizado"/> es para
/// un patrón escrito a mano por el usuario ("Ninguna de estas — patrón personalizado").
/// </summary>
public enum FormatoCarpeta
{
    Directo,
    Anio,
    AnioSemestre,
    AnioTrimestre,
    AnioMes,
    AnioQuincena,
    AnioSemana,
    AnioMesDia,
    MesSinAnio,
    SemanaDelMes,
    Personalizado
}

public enum CampoMarca
{
    Emisor,
    Tipo,
    Fecha,
    NombreArchivo
}

/// <summary>Por qué un archivo terminó en "pendientes por reconocer" — determina qué pantalla abrir al revisarlo.</summary>
public enum MotivoPendiente
{
    /// <summary>No coincide con ninguna configuración: hace falta el asistente completo (REQ-003).</summary>
    NuevoDocumento,

    /// <summary>Coincidió con una configuración, pero un valor extraído no tiene forma válida.</summary>
    ValorInvalido,

    /// <summary>Coincidió con una configuración, pero ya existe un archivo con ese nombre en destino.</summary>
    Duplicado,

    /// <summary>Coincidió con una configuración, pero su carpeta de destino no está disponible ahora.</summary>
    CarpetaNoDisponible,

    /// <summary>Coincidió con una configuración, pero la carpeta del período actual todavía no existe (Caso-1, punto 2).</summary>
    PeriodoNuevo,

    /// <summary>El PDF no tiene texto extraíble: no se puede marcar por coordenadas (Caso-1, punto 1).</summary>
    SinTextoExtraible,

    /// <summary>Un texto que iba a formar parte del nombre/ruta tenía un carácter de control o un byte nulo (Caso-9, mejora 1a). Nunca se sanea, se rechaza.</summary>
    TextoConCaracteresInvalidos,

    /// <summary>El nombre resultante coincide con un nombre reservado de Windows (CON, PRN, COM1, etc. — Caso-9, mejora 1b).</summary>
    NombreReservadoPorWindows,

    /// <summary>La ruta final calculada, ya resuelta, no queda contenida en la carpeta configurada (Caso-9, mejora 1e — última línea de defensa contra path traversal).</summary>
    RutaFueraDeCarpetaConfigurada,

    /// <summary>El nombre de archivo o la ruta completa superan el largo máximo permitido (Caso-9, mejora 1f).</summary>
    NombreORutaDemasiadoLarga,

    /// <summary>El archivo no se pudo leer como PDF (dañado, vacío o truncado — Caso-11, punto 5). Va a "Pendientes de distribuir" para ruteo manual, igual que un PDF sin texto.</summary>
    ArchivoDanado,

    /// <summary>Coincidió con una configuración que pide confirmar el nombre cada vez (Caso-11, punto 1): la carpeta ya está resuelta, falta el nombre.</summary>
    NombrePorConfirmar
}

/// <summary>Texto legible para mostrarle al usuario por qué un documento quedó pendiente (Caso-9).</summary>
public static class MotivoPendienteExtensiones
{
    /// <summary>
    /// Va a "Pendientes de distribuir" (ruteo manual, Caso-4) en vez de "Pendientes por
    /// reconocer": PDF sin texto extraíble, o archivo que ni siquiera se puede leer (Caso-11, punto 5).
    /// </summary>
    public static bool EsPendienteDeDistribuir(this MotivoPendiente motivo) =>
        motivo is MotivoPendiente.SinTextoExtraible or MotivoPendiente.ArchivoDanado;

    public static string DescripcionLegible(this MotivoPendiente motivo) => motivo switch
    {
        MotivoPendiente.NuevoDocumento => "No coincide con ninguna configuración guardada.",
        MotivoPendiente.ValorInvalido => "Un valor extraído del documento no tiene forma válida.",
        MotivoPendiente.Duplicado => "Ya existe un archivo con ese nombre en el destino.",
        MotivoPendiente.CarpetaNoDisponible => "La carpeta de destino no está disponible ahora mismo.",
        MotivoPendiente.PeriodoNuevo => "La carpeta del período actual todavía no existe.",
        MotivoPendiente.SinTextoExtraible => "El documento no tiene texto que se pueda leer.",
        MotivoPendiente.TextoConCaracteresInvalidos => "Texto con caracteres inválidos",
        MotivoPendiente.NombreReservadoPorWindows => "Nombre de archivo reservado por Windows",
        MotivoPendiente.RutaFueraDeCarpetaConfigurada => "La ubicación calculada no corresponde a la carpeta configurada",
        MotivoPendiente.NombreORutaDemasiadoLarga => "Nombre o ruta demasiado larga",
        MotivoPendiente.ArchivoDanado => "No se pudo leer este archivo (puede estar dañado).",
        MotivoPendiente.NombrePorConfirmar => "Falta confirmar el nombre con el que se guarda.",
        _ => motivo.ToString()
    };
}

/// <summary>
/// TextoReferencia es el texto que efectivamente se extrajo de esa coordenada en el momento
/// de crear la marca (para Emisor/Tipo). La coincidencia automática (REQ-002) compara contra
/// este valor, no contra el nombre de la entidad — porque el campo marcado no siempre es el
/// nombre visible (ej. puede ser un RUT o un código, si el nombre es un logo/imagen).
/// </summary>
public record Marca(CampoMarca Campo, int Pagina, double X, double Y, double Ancho, double Alto, string? TextoReferencia = null);

/// <summary>
/// Un conjunto completo de marcas para UN diseño de documento. Una Configuracion puede tener
/// varios (cuando se vincula un documento con un diseño distinto a una configuracion existente).
/// </summary>
public record PatronReconocimiento(int Id, List<Marca> Marcas);

public record ConfiguracionDocumento
{
    public int Id { get; init; }
    public required string Emisor { get; init; }
    public required string Tipo { get; init; }
    public required string CarpetaDestino { get; init; }
    public required FormatoCarpeta FormatoCarpeta { get; init; }
    public string? PatronCarpeta { get; init; }
    public required bool Renombrar { get; init; }
    public required List<PatronReconocimiento> Patrones { get; init; }

    /// <summary>Caso-1, punto 5: si está activo, abre el archivo en el visor de PDF del sistema apenas se guarda solo.</summary>
    public bool AbrirDespuesDeGuardar { get; init; }

    /// <summary>
    /// Caso-11, punto 1: la carpeta se resuelve sola como siempre, pero el nombre no se extrae ni
    /// se mantiene: antes de guardar, el documento queda esperando que el usuario lo escriba o
    /// confirme. Excluyente con <see cref="Renombrar"/> (que queda en false).
    /// </summary>
    public bool PreguntarNombre { get; init; }
}

/// <summary>
/// Una ubicación guardada desde el flujo de PDFs sin texto extraíble (Caso-4, punto 3b) —
/// deliberadamente separada de <see cref="ConfiguracionDocumento"/>: acá no hay Emisor/Tipo ni
/// reconocimiento automático, solo un lugar ya usado antes al que volver rápido sin repetir el
/// asistente ni navegar a mano por el explorador de Windows.
/// </summary>
public record UbicacionSinTexto(int Id, string CarpetaMadre, FormatoCarpeta Formato, string? Patron);

/// <summary>Un botón de limpieza de nombre del paso final del flujo sin texto (Caso-4 punto 4, Caso-11 punto 6).</summary>
public enum OperacionNombre
{
    Borrar,
    DejarSoloNumeros,
    QuitarCerosIzquierda
}

/// <summary>
/// Atajo de guardado rápido del flujo de "distribuir" (Caso-11, punto 4): la combinación
/// completa de una vez (carpeta madre, formato/patrón y regla de nombre) guardada con un nombre,
/// para aplicarla de un click. La regla de nombre es la secuencia de botones de limpieza que el
/// usuario usó esa vez, que se vuelve a aplicar sobre el nombre original de cada documento nuevo.
/// </summary>
public record AtajoGuardadoRapido(
    int Id,
    string Nombre,
    string CarpetaMadre,
    FormatoCarpeta Formato,
    string? Patron,
    IReadOnlyList<OperacionNombre> ReglaNombre);
