# Payroll System

A Windows Forms payroll and employee-management application backed by MySQL.

## Project map

```text
App/                         Application startup
Configuration/               Local database settings and a safe template
Forms/                       Screens, grouped by the people and tasks they serve
  Authentication/            Login and registration
  Attendance/                Leave and undertime screens
  Company/                   Company setup and calendar
  Employee/                  Employee portal and employee management
  Management/                Admin dashboard and management alerts
  Payroll/                   Payslip screens
  Tickets/                   Ticket creation, response, and details
Models/Payroll/              Payslip data model
Services/                    Database and business logic, grouped by feature
  Attendance/ Company/ Management/ Notifications/ Payroll/ Tickets/
UI/                          Shared controls, styles, icons, and charts
Utilities/                   Reusable calculations and helpers
```

## Set up the database connection

The real connection settings belong in the ignored local file `Configuration/AppConfig.local.cs`.
If it is not present, copy the example and enter your MySQL connection details:

```powershell
Copy-Item Configuration\AppConfig.example.cs Configuration\AppConfig.local.cs
```

Do not commit `AppConfig.local.cs` or put database passwords in source files that are shared.
The example file is excluded from compilation so it can safely remain beside the local file.

## Build and run

Open `PAYROLL.slnx` in Visual Studio, or run these commands from the project folder:

```powershell
dotnet restore PAYROLL.csproj
dotnet run --project PAYROLL.csproj
```

The project targets `net10.0-windows` and requires the Windows Desktop runtime and access to the configured MySQL database.
