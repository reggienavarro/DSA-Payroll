using System;

namespace PAYROLL
{
    // Computes employee contributions from monthly basic salary. Verify agency
    // schedules before using these estimates for production payroll.
    public static class DeductionsCalculator
    {
        public static decimal MonthlySalaryFromHourlyRate(decimal hourlyRate)
            => Math.Round(hourlyRate * 8m * 5m * 52m / 12m, 2);

        // Employee share is 5% of the SSS Monthly Salary Credit (MSC). The schedule moves
        // in 500-peso steps from 5,000 to 35,000, and each step covers the salaries
        // around it (4,750-5,249.99 -> 5,000; 5,250-5,749.99 -> 5,500), so the salary
        // is rounded to the NEAREST 500, not down to the step below.
        public static decimal Sss(decimal basicSalary)
        {
            decimal msc = Math.Floor((basicSalary + 250m) / 500m) * 500m;
            msc = Math.Clamp(msc, 5000m, 35000m);
            return Math.Round(msc * 0.05m, 2);
        }

        // PhilHealth: 5% total, split evenly between employee and employer.
        public static decimal PhilHealth(decimal basicSalary)
        {
            decimal basis = Math.Clamp(basicSalary, 10000m, 100000m);
            return Math.Round(basis * 0.025m, 2);
        }

        // Pag-IBIG employee share is 1% at/under 1,500, else 2%, capped at
        // the 10,000 Monthly Fund Salary ceiling.
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