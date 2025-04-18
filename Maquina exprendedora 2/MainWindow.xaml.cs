using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using MySql.Data.MySqlClient;

namespace MaquinaExpendedora
{
    public partial class MainWindow : Window
    {
        // Diccionario para almacenar productos y precios (ahora solo 5 productos)
        private Dictionary<int, double> productos = new Dictionary<int, double>
        {
            { 1, 13.0 },
            { 2, 30.0 },
            { 3, 3.0 },
            { 4, 58.0 },
            { 5, 15.0 }
        };

        private double dineroIngresado = 0.0;
        private double precioSeleccionado = 0.0;
        private int productoSeleccionado = 0;

        // Cadena de conexión a la base de datos
        private string connectionString = "Server=localhost;Database=maquina_expendedora;Uid=root;Pwd=;";

        public MainWindow()
        {
            InitializeComponent();
        }

        // Método para conectar a la base de datos y obtener la cantidad de un producto
        private int ObtenerCantidadProducto(int idProducto)
        {
            int cantidad = 0;
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var query = $"SELECT cantidad FROM productos WHERE id_producto = {idProducto}";
                var command = new MySqlCommand(query, connection);
                var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    cantidad = reader.GetInt32("cantidad");
                }
                connection.Close();
            }
            return cantidad;
        }

        // Método para actualizar la cantidad de un producto después de la compra
        private void ActualizarCantidadProducto(int idProducto)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var query = $"UPDATE productos SET cantidad = cantidad - 1 WHERE id_producto = {idProducto}";
                var command = new MySqlCommand(query, connection);
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        // Método para registrar una venta en la base de datos
        private void RegistrarVenta(int idProducto, double total, double dineroIngresado, double cambio)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var query = $"INSERT INTO ventas (id_producto, cantidad, total, dinero_ingresado, cambio) VALUES ({idProducto}, 1, {total}, {dineroIngresado}, {cambio})";
                var command = new MySqlCommand(query, connection);
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        // Evento para seleccionar un producto con los botones numéricos
        private void Numero_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse((sender as System.Windows.Controls.Button)?.Content.ToString(), out int productoId) && productos.ContainsKey(productoId))
            {
                productoSeleccionado = productoId;
                precioSeleccionado = productos[productoId];
                pantallaMini.Text = $"Producto {productoId} Precio: ${precioSeleccionado}";

                // Verificar la cantidad disponible del producto en la base de datos
                int cantidadDisponible = ObtenerCantidadProducto(productoId);
                if (cantidadDisponible > 0)
                {
                    pantallaMini.Text += $"\nCantidad disponible: {cantidadDisponible}";
                }
                else
                {
                    pantallaMini.Text += "\nProducto agotado.";
                }
            }
        }

        // Evento para agregar dinero
        private void Pago_Click(object sender, RoutedEventArgs e)
        {
            // Intentamos obtener la cantidad del botón
            if (double.TryParse((sender as System.Windows.Controls.Button)?.Content.ToString().Split(' ')[0], out double cantidad))
            {
                dineroIngresado += cantidad;
                pantallaMini.Text = $"Dinero ingresado: ${dineroIngresado}";

                // Actualizar el inventario de monedas en la base de datos
                ActualizarInventarioMonedas(cantidad);
            }
        }

        // Método para calcular y mostrar el cambio en monedas y billetes
        private void MostrarCambio(double cambio)
        {
            // Denominaciones de billetes y monedas
            List<double> denominaciones = new List<double> { 50, 20, 10, 5, 2, 1 };
            Dictionary<double, int> cambioDesglosado = new Dictionary<double, int>();

            // Desglosar el cambio en monedas y billetes
            foreach (var denominacion in denominaciones)
            {
                int cantidad = (int)(cambio / denominacion);
                if (cantidad > 0)
                {
                    cambioDesglosado[denominacion] = cantidad;
                    cambio -= cantidad * denominacion; // Reducir el cambio en función de lo entregado
                }
            }

            // Mostrar el cambio desglosado
            string mensajeCambio = "Cambio entregado: ";
            foreach (var item in cambioDesglosado)
            {
                mensajeCambio += $"\n{item.Value} x ${item.Key}";
            }
            MessageBox.Show(mensajeCambio, "Cambio");

            // Actualizar la base de datos para disminuir las monedas/billetes
            ActualizarCambioEnBaseDeDatos(cambioDesglosado);
        }

        private void ActualizarInventarioMonedas(double cantidadMoneda)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                // Actualizar la cantidad de monedas en la tabla 'cambio'
                var updateQuery = $"UPDATE cambio SET cantidad = cantidad + 1 WHERE denominacion = {cantidadMoneda}";
                var updateCommand = new MySqlCommand(updateQuery, connection);
                updateCommand.ExecuteNonQuery();

                connection.Close();
            }
        }

        private void ActualizarCambioEnBaseDeDatos(Dictionary<double, int> cambioDesglosado)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                // Iterar por cada denominación y actualizar el inventario en la tabla 'cambio'
                foreach (var item in cambioDesglosado)
                {
                    double denominacion = item.Key;
                    int cantidad = item.Value;

                    // Verificar si la cantidad disponible es suficiente
                    var checkQuery = $"SELECT cantidad FROM cambio WHERE denominacion = {denominacion}";
                    var checkCommand = new MySqlCommand(checkQuery, connection);
                    var reader = checkCommand.ExecuteReader();

                    if (reader.Read())
                    {
                        int cantidadDisponible = reader.GetInt32("cantidad");
                        if (cantidadDisponible >= cantidad)
                        {
                            // Actualizar la cantidad de cada denominación en la tabla 'cambio'
                            reader.Close();  // Cerrar el lector antes de hacer otra consulta

                            var updateQuery = $"UPDATE cambio SET cantidad = cantidad - {cantidad} WHERE denominacion = {denominacion}";
                            var updateCommand = new MySqlCommand(updateQuery, connection);
                            updateCommand.ExecuteNonQuery();
                        }
                        else
                        {
                            // Si no hay suficiente cambio, mostrar un mensaje de advertencia
                            MessageBox.Show($"No hay suficiente cambio para entregar ${denominacion}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }

                    reader.Close();
                }

                connection.Close();
            }
        }

        private void RegistrarIngreso(double totalVenta)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                // Insertar directamente el monto de la compra en la tabla de ingresos
                var queryInsert = $"INSERT INTO ingresos (total_ingresos) VALUES ({totalVenta})";
                var commandInsert = new MySqlCommand(queryInsert, connection);
                commandInsert.ExecuteNonQuery();

                connection.Close();
            }
        }

        // Confirmar la compra
        private void ConfirmarCompra_Click(object sender, RoutedEventArgs e)
        {
            if (productoSeleccionado == 0)
            {
                pantallaMini.Text = "Seleccione un producto primero.";
                return;
            }

            if (dineroIngresado >= precioSeleccionado)
            {
                double cambio = dineroIngresado - precioSeleccionado;
                pantallaMini.Text = $"Compra realizada. Cambio: ${cambio}";

                // Verificar la cantidad disponible del producto
                int cantidadDisponible = ObtenerCantidadProducto(productoSeleccionado);
                if (cantidadDisponible > 0)
                {
                    // Registrar la venta en la base de datos
                    RegistrarVenta(productoSeleccionado, precioSeleccionado, dineroIngresado, cambio);

                    // Actualizar la cantidad del producto en la base de datos
                    ActualizarCantidadProducto(productoSeleccionado);

                    // Registrar los ingresos de esta venta
                    RegistrarIngreso(precioSeleccionado);

                    // Mostrar el cambio desglosado
                    MostrarCambio(cambio);
                }
                else
                {
                    pantallaMini.Text = "Producto agotado.";
                }

                Resetear();
            }
            else
            {
                pantallaMini.Text = $"Dinero insuficiente. Falta: ${precioSeleccionado - dineroIngresado}";
            }
        }

        // Resetear los valores después de una compra
        private void Resetear()
        {
            dineroIngresado = 0.0;
            precioSeleccionado = 0.0;
            productoSeleccionado = 0;
            pantallaMini.Text = "Seleccione un producto.";
        }

        // Cancelar la operación
        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            Resetear();
        }

        // Resetear la máquina expendedora
        private void ResetearMaquina_Click(object sender, RoutedEventArgs e)
        {
            // Restablecer valores en la interfaz
            dineroIngresado = 0.0;
            precioSeleccionado = 0.0;
            productoSeleccionado = 0;
            pantallaMini.Text = "Máquina resetada. Seleccione un producto.";

            // Resetear el stock de productos en la base de datos
            ResetearStockProductos();
        }

        // Método para resetear el stock de productos en la base de datos
        private void ResetearStockProductos()
        {
            
            var productosIniciales = new Dictionary<int, int>
    {
        { 1, 10 }, 
        { 2, 10 }, 
        { 3, 10 },
        { 4, 10 },
        { 5, 10 }  
    };

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                foreach (var producto in productosIniciales)
                {
                    int idProducto = producto.Key;
                    int cantidadInicial = producto.Value;

                    var updateQuery = $"UPDATE productos SET cantidad = {cantidadInicial} WHERE id_producto = {idProducto}";
                    var updateCommand = new MySqlCommand(updateQuery, connection);
                    updateCommand.ExecuteNonQuery();
                }

                connection.Close();
            }
        }


    }
}