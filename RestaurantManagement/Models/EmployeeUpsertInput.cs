using System;

namespace QuanLyNhaHang.Models
{
    public class EmployeeUpsertInput
    {
        public string EmployeeId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public bool IsFulltime { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public DateTime DateStartWork { get; set; }
        public string AccountId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
