using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Net.NetworkInformation;
using System.Security.Claims;
using WH_Maintenance_Request_System.Models;

namespace WH_Maintenance_Request_System.Controllers
{
    [Authorize]
    public class MonitorController : Controller
    {
        private readonly string _connStr;

        public MonitorController(IConfiguration configuration)
        {
            _connStr = configuration.GetConnectionString("SRT_WH_MaintenanceConnection");
        }

        public IActionResult Index()
        {
            UpdateStatus();
            return View();
        }

        public IActionResult UpdateStatus()
        {
            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    string sql = @"
                                UPDATE e
                                SET e.status = 
                                        CASE 
                                            WHEN p.status = 'Success' THEN 'Online'
                                            ELSE 'Offline'
                                        END,
                                    e.modify_date = GETDATE(),
                                    e.modify_by = 'system'
                                FROM equipment e
                                LEFT JOIN (
                                    SELECT pr.hostname, pr.ip_address, pr.it_asset, pr.status
                                    FROM ping_result pr
                                    WHERE CAST(pr.create_date AS DATE) = CAST(GETDATE() AS DATE)
                                ) p
                                    ON e.hostname = p.hostname
                                   AND e.ip_address = p.ip_address
                                   AND e.it_asset = p.it_asset
                                WHERE e.is_active = 1 AND e.status <> 'Repair';";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        return Json(new { success = true });
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("MonitorController : UpdateStatus " + ex.Message);
                return StatusCode(500, "Error updating status");
            }
        }

        [HttpGet]
        public IActionResult GetEquipmentTypes()
        {
            var types = new List<string>();

            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    var cmd = new SqlCommand("SELECT DISTINCT type_name FROM master_type", conn);
                    conn.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            types.Add(reader["type_name"].ToString());
                        }
                    }
                }

                return Json(types);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("MonitorController : GetEquipmentTypes " + ex.Message);
                return StatusCode(500, "Failed to load equipment types. Please contact the administrator.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetEquipmentSummary()
        {
            try
            {
                var summary = new EquipmentSummary();

                using (var conn = new SqlConnection(_connStr))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand(@"
                                SELECT status, COUNT(*) AS total 
                                FROM equipment 
                                WHERE is_active = 1 
                                GROUP BY status", conn);

                    var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var status = reader["status"].ToString();
                        var count = Convert.ToInt32(reader["total"]);

                        summary.Total += count;

                        if (status == "Online") summary.Online = count;
                        else if (status == "Offline") summary.Offline = count;
                        else if (status == "Repair") summary.Repair = count;
                    }
                }

                return Json(summary);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("MonitorController : GetEquipmentSummary " + ex.Message);
                return Json(new { success = false, message = "Failed to fetch equipment summary. Please contact the administrator." });
            }
        }

        [HttpGet]
        public IActionResult GetEquipmentData(string type, string status, DateTime? startDate, DateTime? endDate)
        {
            var result = new List<MonitorModel>();
            var today = DateTime.Today;

            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    conn.Open();

                    if (status != null && status.Equals("Repair", StringComparison.OrdinalIgnoreCase))
                    {
                        // กรณี Repair: เอาจาก equipment table เท่านั้น
                        string sqlRepair = @"
                                            SELECT serial_number, it_asset, model, type, location, ip_address, status, hostname
                                            FROM equipment
                                            WHERE is_active = 1 AND status = 'Repair'
                                        ";
                        if (!string.IsNullOrEmpty(type))
                            sqlRepair += " AND type = @type";

                        using (var cmd = new SqlCommand(sqlRepair, conn))
                        {
                            if (!string.IsNullOrEmpty(type))
                                cmd.Parameters.AddWithValue("@type", type);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var monitor = new MonitorModel
                                    {
                                        SerialNumber = reader["serial_number"].ToString(),
                                        Model = reader["model"].ToString(),
                                        Type = reader["type"].ToString(),
                                        Location = reader["location"].ToString(),
                                        IP = reader["ip_address"].ToString(),
                                        Status = reader["status"].ToString(),
                                        HostName = reader["hostname"].ToString(),
                                        DailyStatus = new Dictionary<string, string>()
                                    };

                                    monitor.DailyStatus[today.ToString("dd MMM")] = "Repair";

                                    result.Add(monitor);
                                }
                            }
                        }
                    }
                    else
                    {
                        var sDate = startDate ?? today;
                        var eDate = endDate ?? today;

                        string sql = @"
                                    SELECT 
                                        eq.serial_number,
                                        eq.it_asset,
                                        eq.model,
                                        eq.type,
                                        eq.location,
                                        eq.ip_address,
                                        eq.status,
                                        eq.hostname,
                                        pr.status AS PingStatus,
                                        pr.ping_date
                                    FROM equipment eq
                                    LEFT JOIN ping_result pr 
                                           ON eq.hostname = pr.hostname 
                                          AND eq.ip_address = pr.ip_address 
                                          AND eq.it_asset = pr.it_asset
                                          AND pr.ping_date BETWEEN @StartDate AND @EndDate
                                    WHERE eq.is_active = 1
                                ";

                        if (!string.IsNullOrEmpty(type))
                            sql += " AND eq.type = @type";

                        sql += " ORDER BY eq.hostname, pr.ping_date;";

                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@StartDate", sDate);
                            cmd.Parameters.AddWithValue("@EndDate", eDate);
                            if (!string.IsNullOrEmpty(type))
                                cmd.Parameters.AddWithValue("@type", type);

                            using (var reader = cmd.ExecuteReader())
                            {
                                var dict = new Dictionary<string, MonitorModel>();

                                while (reader.Read())
                                {
                                    string hostname = reader["hostname"].ToString();

                                    if (!dict.TryGetValue(hostname, out var monitor))
                                    {
                                        monitor = new MonitorModel
                                        {
                                            SerialNumber = reader["serial_number"].ToString(),
                                            Model = reader["model"].ToString(),
                                            Type = reader["type"].ToString(),
                                            Location = reader["location"].ToString(),
                                            IP = reader["ip_address"].ToString(),
                                            Status = reader["status"].ToString(),
                                            HostName = hostname,
                                            DailyStatus = new Dictionary<string, string>()
                                        };

                                        // สร้าง entry สำหรับทุกวันในช่วง ให้เริ่มเป็น Offline
                                        for (var date = sDate; date <= eDate; date = date.AddDays(1))
                                        {
                                            monitor.DailyStatus[date.ToString("dd MMM")] = "Offline";
                                        }

                                        dict[hostname] = monitor;
                                    }

                                    if (reader["ping_date"] != DBNull.Value)
                                    {
                                        DateTime pingDate = (DateTime)reader["ping_date"];
                                        string shortDate = pingDate.ToString("dd MMM");
                                        string pingStatus = reader["PingStatus"].ToString() == "Success" ? "Online" : "Offline";

                                        monitor.DailyStatus[shortDate] = pingStatus;
                                    }
                                }

                                // Filter status เฉพาะวันที่ปัจจุบัน
                                if (!string.IsNullOrEmpty(status))
                                {
                                    string statusUpper = status.Equals("Online", StringComparison.OrdinalIgnoreCase) ? "Online" : "Offline";
                                    result = dict.Values
                                        .Where(m => m.DailyStatus[today.ToString("dd MMM")] == statusUpper)
                                        .ToList();
                                }
                                else
                                {
                                    result = dict.Values.ToList();
                                }
                            }
                        }
                    }
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("MonitorController : GetEquipmentData " + ex.Message);
                return StatusCode(500, "Failed to load equipment data. Please contact the administrator.");
            }
        }

        [HttpPost]
        public IActionResult SubmitRepairRequest([FromBody] RepairRequestModel model)
        {
            try
            {
                // จำลองผลแบบสุ่ม
                var rand = new Random();
                bool isSuccess = rand.Next(0, 2) == 1; // 0 หรือ 1

                if (isSuccess)
                {
                    return Json(new { success = true, message = "Repair request submitted successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to submit repair request. Please try again." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error: " + ex.Message);
            }
        }

        [HttpPost]
        public IActionResult UpdateEquipment([FromBody] EquipmentModel equipment)
        {
            try
            {
                using (var conn = new SqlConnection(_connStr))
                {
                    var cmd = new SqlCommand(@"
                                            UPDATE equipment
                                            SET 
                                                status = @status,
                                                modify_date = GETDATE(),
                                                modify_by = @id_employee
                                            WHERE serial_number = @serialNumber", conn);

                    cmd.Parameters.AddWithValue("@serialNumber", equipment.SerialNumber);
                    cmd.Parameters.AddWithValue("@status", equipment.Status ?? (object)DBNull.Value);

                    var idEmployee = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    cmd.Parameters.AddWithValue("@id_employee", idEmployee);

                    conn.Open();
                    int rows = cmd.ExecuteNonQuery();

                    if (rows > 0)
                        return Json(new { success = true, message = "Update successful!" });
                    else
                        return Json(new { success = false, message = "No record updated." });
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorLog("MonitorController : UpdateEquipment " + ex.Message);
                return StatusCode(500, "Error updating equipment.");
            }
        }


    }
}
