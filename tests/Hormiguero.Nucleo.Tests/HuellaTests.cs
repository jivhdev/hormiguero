using System.Security.Cryptography;
using Hormiguero.Nucleo.Utilidades;

namespace Hormiguero.Nucleo.Tests;

public class HuellaTests
{
    private static string CarpetaTemporal() =>
        Path.Combine(Path.GetTempPath(), "HormigueroTests", Guid.NewGuid().ToString("N"));

    private static byte[] Datos(int largo) =>
        Enumerable.Range(0, largo).Select(i => (byte)(i % 251)).ToArray();

    private static string HuellaEsperada(byte[] datos) =>
        Convert.ToHexString(SHA256.HashData(datos)).ToLowerInvariant();

    private static string CrearArchivo(string carpeta, string nombre, byte[] datos)
    {
        Directory.CreateDirectory(carpeta);
        string ruta = Path.Combine(carpeta, nombre);
        File.WriteAllBytes(ruta, datos);
        return ruta;
    }

    [Fact]
    public void Mismo_contenido_misma_huella()
    {
        string carpeta = CarpetaTemporal();
        try
        {
            byte[] datos = Datos(200_000);
            string rutaA = CrearArchivo(carpeta, "a.pdf", datos);
            string rutaB = CrearArchivo(carpeta, "b-distinto-nombre.pdf", datos);

            Assert.Equal(Huella.Calcular(rutaA), Huella.Calcular(rutaB));
            Assert.Equal(HuellaEsperada(datos), Huella.Calcular(rutaA));
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void Un_byte_distinto_cambia_la_huella()
    {
        string carpeta = CarpetaTemporal();
        try
        {
            byte[] datosA = Datos(200_000);
            byte[] datosB = (byte[])datosA.Clone();
            datosB[100_000] ^= 0xFF;

            string rutaA = CrearArchivo(carpeta, "a.pdf", datosA);
            string rutaB = CrearArchivo(carpeta, "b.pdf", datosB);

            Assert.NotEqual(Huella.Calcular(rutaA), Huella.Calcular(rutaB));
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void Archivo_vacio()
    {
        string carpeta = CarpetaTemporal();
        try
        {
            string rutaVacio = CrearArchivo(carpeta, "vacio.txt", Array.Empty<byte>());

            Assert.StartsWith("e3b0c442", Huella.Calcular(rutaVacio));
            Assert.EndsWith("b855", Huella.Calcular(rutaVacio));
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public void No_bloquea_el_archivo()
    {
        string carpeta = CarpetaTemporal();
        try
        {
            byte[] datos = Datos(200_000);
            string ruta = CrearArchivo(carpeta, "compartido.pdf", datos);

            string huellaEnOtroHilo = "";
            var listo = new ManualResetEventSlim(false);
            var hilo = new Thread(() =>
            {
                try
                {
                    huellaEnOtroHilo = Huella.Calcular(ruta);
                }
                finally
                {
                    listo.Set();
                }
            });
            hilo.Start();

            // Mientras el otro hilo calcula la huella, esta escritura debe funcionar.
            listo.Wait(1000);
            byte[] datosNuevos = Datos(200_001);
            using (var escritura = new FileStream(ruta, FileMode.Create, FileAccess.Write))
            {
                escritura.Write(datosNuevos);
            }

            Assert.True(listo.Wait(5000));
            hilo.Join();
            Assert.Equal(HuellaEsperada(datos), huellaEnOtroHilo);
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }
}
