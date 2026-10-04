using Xunit;

// Varias clases de test comparten estado global (BaseDeDatos.RutaArchivo es un campo
// estatico usado para apuntar a una base de datos temporal por test): correrlas en
// paralelo hace que se pisen entre si. La suite es chica, no vale la pena complicar
// el aislamiento — se desactiva la paralelizacion para toda la suite.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
