namespace PAYROLL
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            txtEmployeeID = new TextBox();
            txtEmployeeName = new TextBox();
            label2 = new Label();
            txtPosition = new TextBox();
            label3 = new Label();
            txtBasicSalary = new TextBox();
            label4 = new Label();
            txtHoursWorked = new TextBox();
            label5 = new Label();
            txtOvertimeHours = new TextBox();
            label6 = new Label();
            txtDeductions = new TextBox();
            label7 = new Label();
            grpEmployeeInfo = new GroupBox();
            lblTitle = new Label();
            groupBox1 = new GroupBox();
            btnCalculate = new Button();
            label8 = new Label();
            txtRegularPay = new TextBox();
            label9 = new Label();
            txtOvertimePay = new TextBox();
            label11 = new Label();
            txtGrossPay = new TextBox();
            label13 = new Label();
            txtNetPay = new TextBox();
            btnAdd = new Button();
            btnUpdate = new Button();
            btnDelete = new Button();
            btnClear = new Button();
            dgvPayroll = new DataGridView();
            grpEmployeeInfo.SuspendLayout();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPayroll).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(19, 38);
            label1.Name = "label1";
            label1.Size = new Size(73, 15);
            label1.TabIndex = 0;
            label1.Text = "Employee ID";
            // 
            // txtEmployeeID
            // 
            txtEmployeeID.Location = new Point(169, 30);
            txtEmployeeID.Name = "txtEmployeeID";
            txtEmployeeID.Size = new Size(100, 23);
            txtEmployeeID.TabIndex = 1;
            // 
            // txtEmployeeName
            // 
            txtEmployeeName.Location = new Point(169, 68);
            txtEmployeeName.Name = "txtEmployeeName";
            txtEmployeeName.Size = new Size(100, 23);
            txtEmployeeName.TabIndex = 3;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(19, 76);
            label2.Name = "label2";
            label2.Size = new Size(94, 15);
            label2.TabIndex = 2;
            label2.Text = "Employee Name";
            // 
            // txtPosition
            // 
            txtPosition.Location = new Point(169, 106);
            txtPosition.Name = "txtPosition";
            txtPosition.Size = new Size(100, 23);
            txtPosition.TabIndex = 5;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(19, 114);
            label3.Name = "label3";
            label3.Size = new Size(50, 15);
            label3.TabIndex = 4;
            label3.Text = "Position";
            // 
            // txtBasicSalary
            // 
            txtBasicSalary.Location = new Point(169, 144);
            txtBasicSalary.Name = "txtBasicSalary";
            txtBasicSalary.Size = new Size(100, 23);
            txtBasicSalary.TabIndex = 7;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(19, 152);
            label4.Name = "label4";
            label4.Size = new Size(68, 15);
            label4.TabIndex = 6;
            label4.Text = "Hourly Rate";
            // 
            // txtHoursWorked
            // 
            txtHoursWorked.Location = new Point(169, 182);
            txtHoursWorked.Name = "txtHoursWorked";
            txtHoursWorked.Size = new Size(100, 23);
            txtHoursWorked.TabIndex = 9;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(19, 190);
            label5.Name = "label5";
            label5.Size = new Size(83, 15);
            label5.TabIndex = 8;
            label5.Text = "Hours Worked";
            // 
            // txtOvertimeHours
            // 
            txtOvertimeHours.Location = new Point(169, 220);
            txtOvertimeHours.Name = "txtOvertimeHours";
            txtOvertimeHours.Size = new Size(100, 23);
            txtOvertimeHours.TabIndex = 11;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(19, 228);
            label6.Name = "label6";
            label6.Size = new Size(91, 15);
            label6.TabIndex = 10;
            label6.Text = "Overtime Hours";
            // 
            // txtDeductions
            // 
            txtDeductions.Location = new Point(169, 258);
            txtDeductions.Name = "txtDeductions";
            txtDeductions.Size = new Size(100, 23);
            txtDeductions.TabIndex = 13;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(19, 266);
            label7.Name = "label7";
            label7.Size = new Size(67, 15);
            label7.TabIndex = 12;
            label7.Text = "Deductions";
            // 
            // grpEmployeeInfo
            // 
            grpEmployeeInfo.Controls.Add(label1);
            grpEmployeeInfo.Controls.Add(txtEmployeeID);
            grpEmployeeInfo.Controls.Add(label2);
            grpEmployeeInfo.Controls.Add(txtEmployeeName);
            grpEmployeeInfo.Controls.Add(label3);
            grpEmployeeInfo.Controls.Add(txtPosition);
            grpEmployeeInfo.Controls.Add(label4);
            grpEmployeeInfo.Controls.Add(txtBasicSalary);
            grpEmployeeInfo.Location = new Point(12, 59);
            grpEmployeeInfo.Name = "grpEmployeeInfo";
            grpEmployeeInfo.Size = new Size(380, 190);
            grpEmployeeInfo.TabIndex = 15;
            grpEmployeeInfo.TabStop = false;
            grpEmployeeInfo.Text = "EMPLOYEE INFORMATION";
            // 
            // lblTitle
            // 
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblTitle.Location = new Point(0, 0);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(800, 56);
            lblTitle.TabIndex = 16;
            lblTitle.Text = "PAYROLL MANAGEMENT SYSTEM";
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(btnCalculate);
            groupBox1.Controls.Add(label8);
            groupBox1.Controls.Add(txtRegularPay);
            groupBox1.Controls.Add(label9);
            groupBox1.Controls.Add(txtOvertimePay);
            groupBox1.Controls.Add(label11);
            groupBox1.Controls.Add(txtGrossPay);
            groupBox1.Controls.Add(label13);
            groupBox1.Controls.Add(txtNetPay);
            groupBox1.Location = new Point(398, 59);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(380, 328);
            groupBox1.TabIndex = 17;
            groupBox1.TabStop = false;
            groupBox1.Text = "PAYROLL CALCULATION";
            // 
            // btnCalculate
            // 
            btnCalculate.Location = new Point(25, 219);
            btnCalculate.Name = "btnCalculate";
            btnCalculate.Size = new Size(75, 23);
            btnCalculate.TabIndex = 18;
            btnCalculate.Text = "Calculate";
            btnCalculate.UseVisualStyleBackColor = true;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(19, 38);
            label8.Name = "label8";
            label8.Size = new Size(72, 15);
            label8.TabIndex = 0;
            label8.Text = "Regular Pay:";
            // 
            // txtRegularPay
            // 
            txtRegularPay.Location = new Point(169, 30);
            txtRegularPay.Name = "txtRegularPay";
            txtRegularPay.ReadOnly = true;
            txtRegularPay.Size = new Size(100, 23);
            txtRegularPay.TabIndex = 1;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(19, 76);
            label9.Name = "label9";
            label9.Size = new Size(81, 15);
            label9.TabIndex = 2;
            label9.Text = "Overtime Pay:";
            // 
            // txtOvertimePay
            // 
            txtOvertimePay.Location = new Point(169, 68);
            txtOvertimePay.Name = "txtOvertimePay";
            txtOvertimePay.ReadOnly = true;
            txtOvertimePay.Size = new Size(100, 23);
            txtOvertimePay.TabIndex = 3;
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(19, 114);
            label11.Name = "label11";
            label11.Size = new Size(61, 15);
            label11.TabIndex = 4;
            label11.Text = "Gross Pay:";
            // 
            // txtGrossPay
            // 
            txtGrossPay.Location = new Point(169, 106);
            txtGrossPay.Name = "txtGrossPay";
            txtGrossPay.ReadOnly = true;
            txtGrossPay.Size = new Size(100, 23);
            txtGrossPay.TabIndex = 5;
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Location = new Point(19, 152);
            label13.Name = "label13";
            label13.Size = new Size(51, 15);
            label13.TabIndex = 6;
            label13.Text = "Net Pay:";
            // 
            // txtNetPay
            // 
            txtNetPay.Location = new Point(169, 144);
            txtNetPay.Name = "txtNetPay";
            txtNetPay.ReadOnly = true;
            txtNetPay.Size = new Size(100, 23);
            txtNetPay.TabIndex = 7;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(98, 424);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(75, 23);
            btnAdd.TabIndex = 19;
            btnAdd.Text = "ADD";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // btnUpdate
            // 
            btnUpdate.Location = new Point(229, 424);
            btnUpdate.Name = "btnUpdate";
            btnUpdate.Size = new Size(75, 23);
            btnUpdate.TabIndex = 20;
            btnUpdate.Text = "UPDATE";
            btnUpdate.UseVisualStyleBackColor = true;
            btnUpdate.Click += btnUpdate_Click;
            // 
            // btnDelete
            // 
            btnDelete.Location = new Point(423, 424);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(75, 23);
            btnDelete.TabIndex = 21;
            btnDelete.Text = "DELETE";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDelete_Click;
            // 
            // btnClear
            // 
            btnClear.Location = new Point(567, 424);
            btnClear.Name = "btnClear";
            btnClear.Size = new Size(75, 23);
            btnClear.TabIndex = 22;
            btnClear.Text = "CLEAR";
            btnClear.UseVisualStyleBackColor = true;
            btnClear.Click += btnClear_Click;
            // 
            // dgvPayroll
            // 
            dgvPayroll.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPayroll.Location = new Point(31, 490);
            dgvPayroll.Name = "dgvPayroll";
            dgvPayroll.ReadOnly = true;
            dgvPayroll.Size = new Size(737, 224);
            dgvPayroll.TabIndex = 23;
            dgvPayroll.CellClick += dgvPayroll_CellClick;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 764);
            Controls.Add(btnClear);
            Controls.Add(btnDelete);
            Controls.Add(btnUpdate);
            Controls.Add(btnAdd);
            Controls.Add(groupBox1);
            Controls.Add(lblTitle);
            Controls.Add(grpEmployeeInfo);
            Controls.Add(dgvPayroll);
            Name = "Form1";
            Text = "Form1";
            grpEmployeeInfo.ResumeLayout(false);
            grpEmployeeInfo.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPayroll).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Label label1;
        private TextBox txtEmployeeID;
        private TextBox txtEmployeeName;
        private Label label2;
        private TextBox txtPosition;
        private Label label3;
        private TextBox txtBasicSalary;
        private Label label4;
        private TextBox txtHoursWorked;
        private Label label5;
        private TextBox txtOvertimeHours;
        private Label label6;
        private TextBox txtDeductions;
        private Label label7;
        private GroupBox grpEmployeeInfo;
        private Label lblTitle;
        private GroupBox groupBox1;
        private Label label8;
        private TextBox txtRegularPay;
        private Label label9;
        private TextBox txtOvertimePay;
        private Label label11;
        private TextBox txtGrossPay;
        private Label label13;
        private TextBox txtNetPay;
        private Button btnCalculate;
        private Button btnAdd;
        private Button btnUpdate;
        private Button btnDelete;
        private Button btnClear;
        private DataGridView dgvPayroll;
    }
}
