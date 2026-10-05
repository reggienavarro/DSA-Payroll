namespace PAYROLL
{
    public sealed class LeaveCreditBalance
    {
        public int EmployeeId;
        public string EmployeeName = "";
        public int LeaveYear;
        public decimal EntitledDays;
        public decimal UsedDays;
        public decimal AvailableDays => EntitledDays - UsedDays;
    }
}
