using System;

namespace PAYROLL
{
    // Computes the four standard Philippine payroll deductions from a basic
    // monthly salary. Rates below are the official 2025 figures as of when
    // this was written — SSS, PhilHealth, Pag-IBIG and BIR all revise these
    // periodically, so double-check against their current circulars before
    // relying on this for real payroll. Good enough for a student project;
    // this is not legal or tax advice.
    public static class DeductionsCalculator
    {
        // SSS: 2025 rate = 15% total (10% employer / 5% employee).
        // Monthly Salary Credit is floored at 5,000 and capped at 35,000.
        public static decimal Sss(decimal basicSalary)
        {
            decimal msc = Math.Clamp(basicSalary, 5000m, 35000m);
            return Math.Round(msc * 0.05m, 2);
        }

        // PhilHealth: 2025 rate = 5% total, split evenly (2.5% each side).
        // Floor 10,000 / ceiling 100,000 monthly basic salary.
        public static decimal PhilHealth(decimal basicSalary)
        {
            decimal basis = Math.Clamp(basicSalary, 10000m, 100000m);
            return Math.Round(basis * 0.025m, 2);
        }

        // Pag-IBIG: employee share is 1% at/under ₱1,500, else 2%, applied to
        // salary capped at the ₱10,000 Monthly Fund Salary ceiling — so the
        // employee share tops out at ₱200/month.
        public static decimal PagIbig(decimal basicSalary)
        {
            decimal basis = Math.Min(basicSalary, 10000m);
            decimal rate = basicSalary <= 1500m ? 0.01m : 0.02m;
            return Math.Round(basis * rate, 2);
        }

        // BIR withholding tax — TRAIN law monthly brackets, effective 2023
        // onward (income up to ₱250,000/year, i.e. ~₱20,833/month, is exempt).
        // Applied to taxable income = basic salary minus SSS/PhilHealth/Pag-IBIG.
        public static decimal WithholdingTax(decimal taxableMonthlyIncome)
        {
            decimal t = taxableMonthlyIncome;
            if (t <= 20833m) return 0m;
            if (t <= 33332m) return Math.Round((t - 20833m) * 0.15m, 2);
            if (t <= 66666m) return Math.Round(1875m + (t - 33333m) * 0.20m, 2);
            if (t <= 166666m) return Math.Round(8541.80m + (t - 66667m) * 0.25m, 2);
            if (t <= 666666m) return Math.Round(33541.80m + (t - 166667m) * 0.30m, 2);
            return Math.Round(183541.80m + (t - 666667m) * 0.35m, 2);
        }

        // Runs all four and returns each line item plus the grand total —
        // this is what both Form1 (to fill the Deductions field) and the
        // employee self-service portal (to show the breakdown) call.
        public static (decimal sss, decimal philHealth, decimal pagIbig, decimal tax, decimal total)
            Compute(decimal basicSalary)
        {
            decimal sss = Sss(basicSalary);
            decimal philHealth = PhilHealth(basicSalary);
            decimal pagIbig = PagIbig(basicSalary);
            decimal taxableIncome = basicSalary - sss - philHealth - pagIbig;
            decimal tax = WithholdingTax(taxableIncome);
            return (sss, philHealth, pagIbig, tax, sss + philHealth + pagIbig + tax);
        }
    }
}