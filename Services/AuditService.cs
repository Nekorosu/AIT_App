using System;
using System.Collections.Generic;

namespace AIT_App.Services
{
    // Сервис аудита — записывает важные действия пользователей в таблицу audit_log.
    // Рядовые пользователи и преподаватели не имеют доступа к этим данным.
    // Ошибки записи в лог не прерывают основной поток работы приложения.
    public static class AuditService
    {
        public static void Log(string action, string details = null)
        {
            try
            {
                var db = new DataBaseCon();
                string sql = @"
                    INSERT INTO `audit_log` (`login`, `action`, `details`, `created_at`)
                    VALUES (@login, @action, @details, NOW())";

                db.ExecuteNonQuery(sql, new Dictionary<string, object>
                {
                    { "login",   Session.CurrentLogin ?? "" },
                    { "action",  action },
                    { "details", string.IsNullOrEmpty(details) ? (object)DBNull.Value : details }
                });
            }
            catch
            {
                // Сбой аудита не должен ломать работу приложения
            }
        }
    }
}
