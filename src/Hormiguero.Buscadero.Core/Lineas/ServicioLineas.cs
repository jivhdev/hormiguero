using System.Text.Json;

namespace Buscadero.Core.Lineas;

public sealed class ServicioLineas : IDisposable
{
    private readonly RepositorioLineas _repositorio;

    public ServicioLineas(RepositorioLineas repositorio)
    {
        _repositorio = repositorio;
    }

    public void Dispose() => _repositorio.Dispose();

    // ----- Modelos de cadena -----

    public PlantillaLinea CrearPlantilla(string nombre, bool esModeloHijo = false)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException(
                "El nombre del modelo no puede estar vacío.",
                nameof(nombre)
            );
        }

        return _repositorio.AgregarPlantilla(nombre.Trim(), DateTime.Now, esModeloHijo);
    }

    public IReadOnlyList<PlantillaLinea> ObtenerPlantillas() => _repositorio.ObtenerPlantillas();

    public IReadOnlyList<OpcionReglaVagon> ListarOpcionesRegla() =>
        _repositorio
            .ListarOpcionesRegla()
            .Select(o => new OpcionReglaVagon(o.Id, o.Nombre, o.Campos))
            .ToList();

    public long GuardarRegla(ReglaVagonConfigurada regla)
    {
        if (regla.LargoMinimo < 0)
            throw new ArgumentOutOfRangeException(
                nameof(regla),
                "El largo mínimo no puede ser negativo."
            );
        if (_repositorio.ObtenerVagon(regla.VagonModeloId) is null)
            throw new ArgumentException("El documento que se completará no existe.", nameof(regla));
        if (_repositorio.ObtenerVagon(regla.VagonComparacionId) is null)
            throw new ArgumentException("El documento de comparación no existe.", nameof(regla));
        return _repositorio.GuardarRegla(regla);
    }

    public IReadOnlyList<DudosoVagon> ListarDudosos() => _repositorio.ListarDudosos();

    public int ContarDudosos() => ListarDudosos().Count;

    public bool AceptarDudoso(long id) => _repositorio.AceptarDudoso(id);

    public bool RechazarDudoso(long id) => _repositorio.RechazarDudoso(id);

    public IReadOnlyList<Hormiguero.Nucleo.Datos.EnlaceCadena> HistorialEnlaces(
        long vagonCadenaId
    ) => _repositorio.HistorialEnlaces(vagonCadenaId);

    public IReadOnlyList<PlantillaLinea> ObtenerModelosIndependientes() =>
        _repositorio.ObtenerPlantillas().Where(m => !m.EsModeloHijo).ToList();

    public void RenombrarPlantilla(long id, string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException(
                "El nombre del modelo no puede estar vacío.",
                nameof(nombre)
            );
        }

        _repositorio.RenombrarPlantilla(id, nombre.Trim());
    }

    public void BorrarPlantilla(long id) => _repositorio.BorrarPlantilla(id);

    /// <summary>
    /// Caso-12: define, una sola vez por modelo, como se van a nombrar las cadenas que
    /// nazcan de el. "PorDocumento" requiere el Id de un vagon (documento) del propio
    /// modelo; "Personalizado" se guarda pero se comporta como "Generico" (ver Caso-12).
    /// </summary>
    public void ConfigurarPreferenciaNombre(
        long plantillaId,
        PreferenciaNombreCadena preferencia,
        long? vagonNombreId
    )
    {
        if (preferencia == PreferenciaNombreCadena.PorDocumento && vagonNombreId is null)
        {
            throw new InvalidOperationException(
                "Elija qué documento va a servir como nombre de la cadena."
            );
        }

        _repositorio.ActualizarPreferenciaNombre(plantillaId, preferencia, vagonNombreId);
    }

    /// <summary>
    /// Nombre a mostrar de una cadena, segun la preferencia de su modelo (Caso-12).
    /// Si el modelo fue borrado, o la preferencia es Generico/Personalizado, o el
    /// documento elegido todavia no tiene archivo, se usa el nombre generico de respaldo.
    /// </summary>
    public string ObtenerNombreVisible(InstanciaLinea cadena)
    {
        if (cadena.PlantillaIdOrigen is not long plantillaId)
        {
            return cadena.Nombre;
        }

        var plantilla = _repositorio.ObtenerPlantilla(plantillaId);
        if (
            plantilla is null
            || plantilla.PreferenciaNombre != PreferenciaNombreCadena.PorDocumento
            || plantilla.VagonNombreId is null
        )
        {
            return cadena.Nombre;
        }

        var documento = _repositorio
            .ObtenerVagonesInstancia(cadena.Id)
            .FirstOrDefault(v => v.PlantillaVagonId == plantilla.VagonNombreId);

        return documento?.NombreDocumento ?? cadena.Nombre;
    }

    public PlantillaVagon AgregarVagon(
        long plantillaId,
        long? padreId,
        string nombre,
        bool esMultiple,
        bool esAnexo,
        long? modeloCadenaHijaId
    )
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException(
                "El nombre del documento no puede estar vacío.",
                nameof(nombre)
            );
        }

        if (esAnexo && padreId is null)
        {
            throw new InvalidOperationException(
                "Un documento anexo debe colgar de un documento principal."
            );
        }

        if (!esAnexo && padreId is not null)
        {
            throw new InvalidOperationException(
                "Los documentos de la cadena van en orden; solo los anexos cuelgan de otro documento."
            );
        }

        if (padreId is not null && _repositorio.ObtenerVagon(padreId.Value) is null)
        {
            throw new InvalidOperationException("El documento principal no existe.");
        }

        if (esMultiple)
        {
            if (modeloCadenaHijaId is null)
            {
                throw new InvalidOperationException(
                    "Un documento ramificado debe apuntar a un modelo de cadena hija."
                );
            }

            if (modeloCadenaHijaId.Value == plantillaId)
            {
                throw new InvalidOperationException(
                    "Un documento ramificado no puede apuntar a su propio modelo."
                );
            }

            if (_repositorio.ObtenerPlantilla(modeloCadenaHijaId.Value) is null)
            {
                throw new InvalidOperationException("El modelo de cadena hija no existe.");
            }
        }
        else
        {
            modeloCadenaHijaId = null;
        }

        return _repositorio.AgregarVagon(
            plantillaId,
            padreId,
            nombre.Trim(),
            esMultiple,
            esAnexo,
            modeloCadenaHijaId
        );
    }

    public void ActualizarVagon(
        long id,
        string nombre,
        bool esMultiple,
        bool esAnexo,
        long? modeloCadenaHijaId
    )
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException(
                "El nombre del documento no puede estar vacío.",
                nameof(nombre)
            );
        }

        var vagon =
            _repositorio.ObtenerVagon(id)
            ?? throw new InvalidOperationException("El documento no existe.");

        if (esMultiple)
        {
            if (modeloCadenaHijaId is null)
            {
                throw new InvalidOperationException(
                    "Un documento ramificado debe apuntar a un modelo de cadena hija."
                );
            }

            if (modeloCadenaHijaId.Value == vagon.PlantillaId)
            {
                throw new InvalidOperationException(
                    "Un documento ramificado no puede apuntar a su propio modelo."
                );
            }

            if (_repositorio.ObtenerPlantilla(modeloCadenaHijaId.Value) is null)
            {
                throw new InvalidOperationException("El modelo de cadena hija no existe.");
            }
        }
        else
        {
            modeloCadenaHijaId = null;
        }

        _repositorio.ActualizarVagon(id, nombre.Trim(), esMultiple, esAnexo, modeloCadenaHijaId);
    }

    public void BorrarVagon(long id) => _repositorio.BorrarVagon(id);

    public IReadOnlyList<NodoPlantilla> ObtenerArbolPlantilla(long plantillaId)
    {
        var vagones = _repositorio.ObtenerVagonesPlantilla(plantillaId);
        return ConstruirArbolPlantilla(vagones, null);
    }

    private static IReadOnlyList<NodoPlantilla> ConstruirArbolPlantilla(
        IReadOnlyList<PlantillaVagon> vagones,
        long? padreId
    ) =>
        vagones
            .Where(v => v.PadreId == padreId)
            .OrderBy(v => v.Orden)
            .Select(v => new NodoPlantilla
            {
                Vagon = v,
                Hijos = ConstruirArbolPlantilla(vagones, v.Id),
            })
            .ToList();

    // ----- Cadenas documentales (instancias) -----

    public InstanciaLinea CrearInstancia(
        long modeloId,
        string nombre,
        long? cadenaMadreId = null,
        long? instanciaVagonPadreId = null
    )
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException(
                "El nombre de la cadena no puede estar vacío.",
                nameof(nombre)
            );
        }

        return CrearInstanciaInterna(modeloId, nombre.Trim(), cadenaMadreId, instanciaVagonPadreId);
    }

    private InstanciaLinea CrearInstanciaInterna(
        long modeloId,
        string nombre,
        long? cadenaMadreId,
        long? instanciaVagonPadreId
    )
    {
        var modelo =
            _repositorio.ObtenerPlantilla(modeloId)
            ?? throw new InvalidOperationException("El modelo de cadena no existe.");

        var estructura = ConstruirEstructura(modeloId);
        var json = JsonSerializer.Serialize(estructura);

        var instancia = _repositorio.AgregarInstancia(
            modelo.Id,
            modelo.Nombre,
            nombre,
            DateTime.Now,
            json,
            cadenaMadreId,
            instanciaVagonPadreId
        );
        CopiarDocumentos(instancia.Id, estructura);
        return instancia;
    }

    public IReadOnlyList<InstanciaLinea> ObtenerInstancias() => _repositorio.ObtenerInstancias();

    public void BorrarInstancia(long id) => _repositorio.BorrarInstancia(id);

    public InstanciaLinea ObtenerInstancia(long id) =>
        _repositorio.ObtenerInstancia(id)
        ?? throw new InvalidOperationException("La cadena no existe.");

    public IReadOnlyList<InstanciaLinea> ObtenerCadenasHijas(long instanciaVagonPadreId) =>
        _repositorio.ObtenerCadenasHijas(instanciaVagonPadreId);

    public IReadOnlyList<NodoInstancia> ObtenerArbolInstancia(long instanciaId)
    {
        var vagones = _repositorio.ObtenerVagonesInstancia(instanciaId);
        return ConstruirArbolInstancia(vagones, null);
    }

    private static IReadOnlyList<NodoInstancia> ConstruirArbolInstancia(
        IReadOnlyList<InstanciaVagon> vagones,
        long? padreId
    ) =>
        vagones
            .Where(v => v.PadreId == padreId)
            .OrderBy(v => v.Orden)
            .Select(v => new NodoInstancia
            {
                Vagon = v,
                Hijos = ConstruirArbolInstancia(vagones, v.Id),
            })
            .ToList();

    public InstanciaLinea AgregarCadenaHija(
        long instanciaPadreId,
        long instanciaVagonPadreId,
        string? nombre = null
    )
    {
        var padre = ObtenerInstancia(instanciaPadreId);
        var documento =
            _repositorio.ObtenerVagonInstancia(instanciaVagonPadreId)
            ?? throw new InvalidOperationException("El documento no existe en la cadena.");

        if (!documento.EsMultiple)
        {
            throw new InvalidOperationException(
                "Solo un documento ramificado puede tener cadenas hijas."
            );
        }

        var estructura = Deserializar(padre);
        var nodo = documento.PlantillaVagonId is long plantillaVagonId
            ? BuscarNodo(estructura, plantillaVagonId)
            : BuscarNodoPorNombre(
                estructura,
                documento.Nombre,
                documento.Orden,
                documento.EsMultiple,
                documento.EsAnexo
            );
        if (nodo is null)
        {
            throw new InvalidOperationException(
                "No se encontró el documento en el modelo de la cadena."
            );
        }

        if (nodo.ModeloCadenaHijaId is not long modeloHijoId)
        {
            throw new InvalidOperationException(
                "El documento ramificado no tiene un modelo de cadena hija asociado."
            );
        }

        var modeloHijo =
            _repositorio.ObtenerPlantilla(modeloHijoId)
            ?? throw new InvalidOperationException("El modelo de cadena hija ya no existe.");

        return CrearInstanciaInterna(
            modeloHijoId,
            nombre ?? modeloHijo.Nombre,
            instanciaPadreId,
            instanciaVagonPadreId
        );
    }

    public InstanciaVagon AgregarAnexo(
        long instanciaId,
        long plantillaVagonId,
        long principalInstanciaVagonId
    )
    {
        var instancia = ObtenerInstancia(instanciaId);
        var nodo =
            BuscarNodo(Deserializar(instancia), plantillaVagonId)
            ?? throw new InvalidOperationException(
                "El documento de la plantilla no existe en la cadena."
            );

        if (!nodo.EsAnexo)
        {
            throw new InvalidOperationException("El documento no es un anexo.");
        }

        return _repositorio.AgregarVagonInstancia(
            instanciaId,
            principalInstanciaVagonId,
            plantillaVagonId,
            nodo.Nombre,
            esMultiple: false,
            esAnexo: true
        );
    }

    public void VincularDocumento(long instanciaVagonId, string rutaDocumento)
    {
        if (string.IsNullOrWhiteSpace(rutaDocumento))
        {
            throw new ArgumentException(
                "La ruta del documento no puede estar vacía.",
                nameof(rutaDocumento)
            );
        }

        _repositorio.ActualizarDocumentoVagon(
            instanciaVagonId,
            rutaDocumento,
            Path.GetFileName(rutaDocumento)
        );
    }

    public void DesvincularDocumento(long instanciaVagonId) =>
        _repositorio.ActualizarDocumentoVagon(instanciaVagonId, null, null);

    public void QuitarVagonInstancia(long id) => _repositorio.BorrarVagonInstancia(id);

    public IReadOnlyList<NodoEstructura> ObtenerEstructura(long instanciaId) =>
        Deserializar(ObtenerInstancia(instanciaId));

    public NodoEstructura? BuscarNodoEstructura(long instanciaId, long plantillaVagonId) =>
        BuscarNodo(Deserializar(ObtenerInstancia(instanciaId)), plantillaVagonId);

    public InstanciaLinea CrearCadenaVacia(long modeloId)
    {
        var modelo =
            _repositorio.ObtenerPlantilla(modeloId)
            ?? throw new InvalidOperationException("El modelo de cadena no existe.");

        var existentes = _repositorio
            .ObtenerInstancias()
            .Count(i => i.PlantillaIdOrigen == modeloId);
        return CrearInstanciaInterna(modeloId, $"{modelo.Nombre} {existentes + 1}", null, null);
    }

    public IReadOnlyList<InstanciaLinea> ObtenerCadenasDeModelo(long modeloId) =>
        _repositorio.ObtenerInstancias().Where(i => i.PlantillaIdOrigen == modeloId).ToList();

    public IReadOnlyList<CoincidenciaCadena> BuscarDocumentoEnCadenas(
        long modeloId,
        string rutaDocumento
    )
    {
        var resultado = new List<CoincidenciaCadena>();
        foreach (var cadena in ObtenerCadenasDeModelo(modeloId))
        {
            BuscarEnCadena(cadena, cadena, rutaDocumento, resultado);
        }

        return resultado;
    }

    public IReadOnlyList<CoincidenciaCadena> BuscarCadenasDeDocumento(string rutaDocumento)
    {
        var resultado = new List<CoincidenciaCadena>();
        foreach (var cadena in _repositorio.ObtenerInstancias())
        {
            BuscarEnCadena(cadena, cadena, rutaDocumento, resultado);
        }

        return resultado;
    }

    public IReadOnlyList<InstanciaVagon> ObtenerCaminoPlano(long cadenaRaizId, long documentoId)
    {
        var camino = new List<InstanciaVagon>();
        return ConstruirCamino(cadenaRaizId, documentoId, camino)
            ? camino
            : Array.Empty<InstanciaVagon>();
    }

    public IReadOnlyList<InstanciaVagon> ObtenerCaminoCompleto(long cadenaRaizId, long documentoId)
    {
        var camino = new List<InstanciaVagon>();
        AgregarCaminoCompleto(cadenaRaizId, documentoId, camino);
        return camino;
    }

    private void AgregarCaminoCompleto(long cadenaId, long documentoId, List<InstanciaVagon> camino)
    {
        var vagones = _repositorio.ObtenerVagonesInstancia(cadenaId);
        var raices = vagones.Where(d => d.PadreId is null).OrderBy(d => d.Orden).ToList();

        foreach (var documento in raices)
        {
            camino.Add(documento);
            foreach (
                var anexo in vagones.Where(d => d.PadreId == documento.Id).OrderBy(d => d.Orden)
            )
            {
                camino.Add(anexo);
            }
        }

        var cadenasHijas = new List<InstanciaLinea>();
        foreach (var ramificado in raices.Where(d => d.EsMultiple))
        {
            cadenasHijas.AddRange(_repositorio.ObtenerCadenasHijas(ramificado.Id));
        }

        var elegida =
            cadenasHijas.FirstOrDefault(h => ContieneDocumento(h.Id, documentoId))
            ?? cadenasHijas.OrderBy(h => h.FechaCreacion).FirstOrDefault();

        if (elegida is not null)
        {
            AgregarCaminoCompleto(elegida.Id, documentoId, camino);
        }
    }

    public IReadOnlyList<InstanciaVagon> ObtenerTodosLosDocumentos(long cadenaId)
    {
        var resultado = new List<InstanciaVagon>();
        RecolectarDocumentos(cadenaId, resultado);
        return resultado;
    }

    private void RecolectarDocumentos(long cadenaId, List<InstanciaVagon> resultado)
    {
        foreach (var documento in _repositorio.ObtenerVagonesInstancia(cadenaId))
        {
            resultado.Add(documento);
            foreach (var hija in _repositorio.ObtenerCadenasHijas(documento.Id))
            {
                RecolectarDocumentos(hija.Id, resultado);
            }
        }
    }

    private bool ContieneDocumento(long cadenaId, long documentoId)
    {
        foreach (var documento in _repositorio.ObtenerVagonesInstancia(cadenaId))
        {
            if (documento.Id == documentoId)
            {
                return true;
            }

            foreach (var hija in _repositorio.ObtenerCadenasHijas(documento.Id))
            {
                if (ContieneDocumento(hija.Id, documentoId))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void BuscarEnCadena(
        InstanciaLinea raiz,
        InstanciaLinea actual,
        string rutaDocumento,
        List<CoincidenciaCadena> resultado
    )
    {
        foreach (var documento in _repositorio.ObtenerVagonesInstancia(actual.Id))
        {
            if (
                documento.RutaDocumento is not null
                && string.Equals(
                    documento.RutaDocumento,
                    rutaDocumento,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                resultado.Add(new CoincidenciaCadena { CadenaRaiz = raiz, Documento = documento });
            }

            foreach (var hija in _repositorio.ObtenerCadenasHijas(documento.Id))
            {
                BuscarEnCadena(raiz, hija, rutaDocumento, resultado);
            }
        }
    }

    private bool ConstruirCamino(long cadenaId, long documentoId, List<InstanciaVagon> camino)
    {
        foreach (
            var documento in _repositorio
                .ObtenerVagonesInstancia(cadenaId)
                .Where(d => d.PadreId is null)
                .OrderBy(d => d.Orden)
        )
        {
            camino.Add(documento);
            if (documento.Id == documentoId)
            {
                return true;
            }

            foreach (var hija in _repositorio.ObtenerCadenasHijas(documento.Id))
            {
                var subcamino = new List<InstanciaVagon>();
                if (ConstruirCamino(hija.Id, documentoId, subcamino))
                {
                    camino.AddRange(subcamino);
                    return true;
                }
            }
        }

        return false;
    }

    private void CopiarDocumentos(long instanciaId, IReadOnlyList<NodoEstructura> estructura)
    {
        foreach (var nodo in estructura.OrderBy(n => n.Orden))
        {
            if (nodo.EsAnexo)
            {
                continue;
            }

            _repositorio.AgregarVagonInstancia(
                instanciaId,
                null,
                nodo.PlantillaVagonId,
                nodo.Nombre,
                nodo.EsMultiple,
                esAnexo: false
            );
        }
    }

    private List<NodoEstructura> ConstruirEstructura(long plantillaId)
    {
        var arbol = ObtenerArbolPlantilla(plantillaId);
        return arbol.Select(ConvertirEstructura).ToList();
    }

    private NodoEstructura ConvertirEstructura(NodoPlantilla nodo)
    {
        var nombreModeloHijo = nodo.Vagon.ModeloCadenaHijaId is long modeloHijoId
            ? _repositorio.ObtenerPlantilla(modeloHijoId)?.Nombre
            : null;

        return new NodoEstructura
        {
            PlantillaVagonId = nodo.Vagon.Id,
            Nombre = nodo.Vagon.Nombre,
            EsMultiple = nodo.Vagon.EsMultiple,
            EsAnexo = nodo.Vagon.EsAnexo,
            Orden = nodo.Vagon.Orden,
            ModeloCadenaHijaId = nodo.Vagon.ModeloCadenaHijaId,
            NombreModeloCadenaHija = nombreModeloHijo,
            Hijos = nodo.Hijos.Select(ConvertirEstructura).ToList(),
        };
    }

    private static IReadOnlyList<NodoEstructura> Deserializar(InstanciaLinea instancia) =>
        JsonSerializer.Deserialize<List<NodoEstructura>>(instancia.EstructuraJson)
        ?? new List<NodoEstructura>();

    private static NodoEstructura? BuscarNodo(
        IReadOnlyList<NodoEstructura> nodos,
        long plantillaVagonId
    )
    {
        foreach (var nodo in nodos)
        {
            if (nodo.PlantillaVagonId == plantillaVagonId)
            {
                return nodo;
            }

            var encontrado = BuscarNodo(nodo.Hijos, plantillaVagonId);
            if (encontrado is not null)
            {
                return encontrado;
            }
        }

        return null;
    }

    private static NodoEstructura? BuscarNodoPorNombre(
        IReadOnlyList<NodoEstructura> nodos,
        string nombre,
        int orden,
        bool esMultiple,
        bool esAnexo
    )
    {
        foreach (var nodo in nodos)
        {
            if (
                nodo.Nombre == nombre
                && nodo.Orden == orden
                && nodo.EsMultiple == esMultiple
                && nodo.EsAnexo == esAnexo
            )
                return nodo;
            var hijo = BuscarNodoPorNombre(nodo.Hijos, nombre, orden, esMultiple, esAnexo);
            if (hijo is not null)
                return hijo;
        }
        return null;
    }
}
