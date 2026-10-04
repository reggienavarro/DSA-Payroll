using System;

namespace PAYROLL
{
    // One itemized payslip for one employee, one cutoff period. The five
    // totals are computed properties, not stored columns — they can never
    // drift out of sync with the line items that make them up.
    public class Payslip
    {
        public int PayslipId;
        public int EmployeeId;
        public string EmployeeName = "";
        public string DepartmentName = "Unassigned";
        public DateTime CreatedAt;
        public DateTime CutoffStart;
        public DateTime CutoffEnd;

        // Earnings
        public decimal NoOfDays, HourlyRate, BasicPay, NdPayPrem, RiceAllowance,
            DailyMeal, Uniform, Laundry, TotalOtPay, RegHolPayPrem, SpHolPayPrem,
            LeaveWithPay, Adjustment;

        // Deductions
        public decimal Absences, LateUtOb, SssContribution, PhilHealthContribution,
            HmdfContribution, Loans;

        // Bonuses
        public decimal AttendanceBonus, TenureBonus, Oic, Account, Incentives,
            InternalCommission;

        public decimal Total =>
            BasicPay + NdPayPrem + RiceAllowance + DailyMeal + Uniform + Laundry +
            TotalOtPay + RegHolPayPrem + SpHolPayPrem + LeaveWithPay + Adjustment;

        public decimal TotalDeductions =>
            Absences + LateUtOb + SssContribution + PhilHealthContribution + HmdfContribution + Loans;

        public decimal NetPay => Total - TotalDeductions;

        public decimal TotalBonus =>
            AttendanceBonus + TenureBonus + Oic + Account + Incentives + InternalCommission;

        public decimal TotalAmountReceivable => NetPay + TotalBonus;
    }

    // Company-wide starting values for a new payslip. These are defaults for
    // a student demo — a real company would make these configurable (e.g. in
    // a Settings screen), which is worth mentioning to your panel as a
    // "next step" rather than something hardcoded forever.
    public static class PayrollDefaults
    {
        public const decimal RiceAllowance = 750m;
        public const decimal DailyMeal = 0m;
        public const decimal Uniform = 208m;
        public const decimal Laundry = 150m;
        public const decimal Incentives = 1100m;
        public const decimal DefaultHourlyRate = 65m;
    }
}
