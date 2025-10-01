namespace WH_Maintenance_Request_System.Models
{
    public class MonitorModel
    {
        public int No { get; set; }
        public string SerialNumber { get; set; }
        public string Model { get; set; }
        public string Type { get; set; }
        public string Location { get; set; }
        public string IP { get; set; }
        public string HostName { get; set; }
        public string Status { get; set; }
        public DateTime? PingDate { get; set; }
        public Dictionary<string, string> DailyStatus { get; set; } = new Dictionary<string, string>();
    }

    public class EquipmentSummary
    {
        public int Total { get; set; }
        public int Online { get; set; }
        public int Offline { get; set; }
        public int Repair { get; set; }
    }

    public class RepairRequestModel
    {
        public string EquipmentId { get; set; }
        public string SerialNumber { get; set; }
        public string Model { get; set; }
        public string Type { get; set; }
        public string Location { get; set; }
        public string IP { get; set; }
        public string HostName { get; set; }
        public string ProblemDescription { get; set; }
    }

    public class EquipmentModel
    {
        public string SerialNumber { get; set; }
        public string Status { get; set; }
    }

}
