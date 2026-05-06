using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;

namespace AIT_App.Services
{
    // Класс для работы с базой данных MySQL.
    // Содержит три основных метода: SELECT, скалярный запрос и запрос без результата (INSERT/UPDATE/DELETE).
    public class DataBaseCon
    {
        // Строка подключения к БД — читается из config.json при создании объекта
        private string _connectionString;

        public DataBaseCon()
        {
            // Загружаем строку подключения из файла config.json
            _connectionString = ConnectionStringService.Load();
        }

        // Выполняет SELECT-запрос и возвращает таблицу с результатами.
        // parameters — словарь параметров вида { "имя": значение }, чтобы защититься от SQL-инъекций.
        // Возвращает null если произошла ошибка.
        public DataTable ExecuteQuery(string sql, Dictionary<string, object> parameters = null)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                using var command = new MySqlCommand(sql, connection);

                // Добавляем параметры запроса если они переданы
                if (parameters != null)
                {
                    foreach (var param in parameters)
                        command.Parameters.AddWithValue("@" + param.Key, param.Value ?? DBNull.Value);
                }

                // Заполняем таблицу данными через адаптер
                var table = new DataTable();
                using var adapter = new MySqlDataAdapter(command);
                adapter.Fill(table);
                return table;
            }
            catch (Exception ex)
            {
                // Выводим ошибку в консоль для отладки
                Console.WriteLine("Ошибка ExecuteQuery: " + ex.Message);
                return null;
            }
        }

        // Выполняет запрос и возвращает одно значение (например COUNT или ID).
        // Возвращает null если произошла ошибка или результат пустой.
        public object ExecuteScalar(string sql, Dictionary<string, object> parameters = null)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                using var command = new MySqlCommand(sql, connection);

                if (parameters != null)
                {
                    foreach (var param in parameters)
                        command.Parameters.AddWithValue("@" + param.Key, param.Value ?? DBNull.Value);
                }

                var result = command.ExecuteScalar();

                // DBNull означает что в БД значение NULL — возвращаем C# null
                return result == DBNull.Value ? null : result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка ExecuteScalar: " + ex.Message);
                return null;
            }
        }

        // Выполняет INSERT, UPDATE или DELETE.
        // Возвращает: количество затронутых строк, -2 если запись уже существует (дубликат), -1 при ошибке.
        public int ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                connection.Open();

                using var command = new MySqlCommand(sql, connection);

                if (parameters != null)
                {
                    foreach (var param in parameters)
                        command.Parameters.AddWithValue("@" + param.Key, param.Value ?? DBNull.Value);
                }

                return command.ExecuteNonQuery();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                // Ошибка 1062 — попытка добавить дублирующую запись (нарушение уникального ключа)
                Console.WriteLine("Дубликат: " + ex.Message);
                return -2;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка ExecuteNonQuery: " + ex.Message);
                return -1;
            }
        }

        // Проверяет соединение с базой данных.
        // Возвращает (true, null) если всё хорошо, или (false, "текст ошибки") если нет.
        public (bool Ok, string Error) ConnectionCheck()
        {
            return ConnectionCheck(_connectionString);
        }

        // Конвертирует DataTable в List<Dictionary<string, object>> для использования
        // как ItemsSource в Avalonia DataGrid: {Binding [ColumnName]} работает через
        // стандартный Dictionary-индексер, тогда как DataRowView не поддерживается.
        public static List<Dictionary<string, object>> ToRowList(DataTable table)
        {
            var list = new List<Dictionary<string, object>>();
            if (table == null) return list;
            foreach (DataRow row in table.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in table.Columns)
                    dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                list.Add(dict);
            }
            return list;
        }

        // Статичная версия — принимает произвольную строку подключения (для SettingsWindow)
        public static (bool Ok, string Error) ConnectionCheck(string connectionString)
        {
            try
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
