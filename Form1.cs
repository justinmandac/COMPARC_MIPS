using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiniMIPS_v_0_2
{
    public partial class MainWindow : Form
    {
       
        private DataTable dt_GPR;
        private DataTable dt_DATA_MEM;
        private DataTable dt_PROG_MEM;
        private BindingSource bs_GPR  = new BindingSource();
        private BindingSource bs_DATA = new BindingSource();
        private BindingSource bs_PROG = new BindingSource();

        private const int CODE_SEGMENT_END   = 0x1FFF;
        private const int DATA_SEGMENT_START = 0x2000;
        private const int DATA_SEGMENT_END   = 0x3FFF;

        //FALSE = CLOCK UP (LOW TO HIGH)
        //TRUE  = CLOCK DOWN (HIGH TO LOW)
        private bool CLOCK_STATE = false;
        private uint CLOCK_COUNT = 0;
        private int GLOBAL_COUNTER; //maximum current value of cycle counter
        
        //PIPELINE STAGES
        private Hashtable IF  = new Hashtable();
        private Hashtable ID  = new Hashtable();
        private Hashtable EX  = new Hashtable();
        private Hashtable MEM = new Hashtable();
        private Int64     WB  = 0xDEADBEEF;
        
        //INTERMEDIATE BUFFERS
        private Hashtable IFID  = new Hashtable();
        private Hashtable IDEX  = new Hashtable();
        private Hashtable EXMEM = new Hashtable();
        private Hashtable MEMWB = new Hashtable();

        private Boolean executionFinished = false; 

        public MainWindow()
        {
            InitializeComponent();

            clockButton.Text = "CLOCK UP";
            dt_GPR = new DataTable("gpr");
            dt_DATA_MEM = new DataTable("data");
            dt_PROG_MEM = new DataTable("prog");

            GLOBAL_COUNTER = 0;
            CLOCK_COUNT = 0;
            cycle_TextBox.Text = GLOBAL_COUNTER.ToString();

            init_dt_gpr();
            init_dt_mem();
            init_dt_prog();
            init_stages();
            init_buffs();

            update_display();

        }

        //INITIALIZATION ROUTINES

        //initializes each entry in DataTable dt_GPR with dummy values. Each entry corresponds to a general purpose register.
        private void init_dt_gpr()
        {
            dt_GPR.Columns.Add("index");
            dt_GPR.Columns.Add("value",typeof(Int64));

            for (int x = 0; x < 32; x++)
            {
                dt_GPR.Rows.Add(x,(Int64)0xDEADBEEF);
            }
           
            bs_GPR.DataSource = dt_GPR;          
            gprView.DataSource = dt_GPR;
            gprView.DataSource = bs_GPR;
            //housekeeping
            gprView.Columns[0].ReadOnly = true;
            gprView.Columns[1].DefaultCellStyle.Format = "X"; //set cell formatting to hexadecimal
            gprView.Refresh();

        }
        private void init_dt_mem()
        {
            dt_DATA_MEM.Columns.Add("address",typeof(Int64));
            dt_DATA_MEM.Columns.Add("value",typeof(Int64));

            for (int x = DATA_SEGMENT_START; x <= DATA_SEGMENT_END; x++)
            {
                dt_DATA_MEM.Rows.Add((Int64)x,(Int64)0xDEADBEEF);
            }

            bs_DATA.DataSource = dt_DATA_MEM;
            dataView.DataSource = dt_DATA_MEM;
            dataView.DataSource = bs_DATA;
            
            //housekeeping
            dataView.Columns[0].DefaultCellStyle.Format = "X";
            dataView.Columns[0].ReadOnly = true;
            dataView.Columns[1].DefaultCellStyle.Format = "X";//set cell formatting to hexadecimal
            dataView.Refresh();
        }
        private void init_dt_prog()
        {
            dt_PROG_MEM.Columns.Add("address", typeof(Int64));
            dt_PROG_MEM.Columns.Add("value", typeof(Int64));

            for (int x = 0; x <= CODE_SEGMENT_END; x++)
            {
                dt_PROG_MEM.Rows.Add((Int64)x, (Int64)0xDEADBEEF);
            }

            bs_PROG.DataSource = dt_PROG_MEM;
            progView.DataSource = dt_PROG_MEM;
            progView.DataSource = bs_PROG;

            //housekeeping
            progView.Columns[0].DefaultCellStyle.Format = "X";
            progView.Columns[0].ReadOnly = true;
            progView.Columns[1].DefaultCellStyle.Format = "X";//set cell formatting to hexadecimal
            progView.Refresh();
        }
        
        //initializes pipeline stages
        private void init_stages()
        {
            IF.Add("NPC", 0);
            IF.Add("PC" , 0);
            IF.Add("IR" , 0xDEADBEEF);

            ID.Add("IR" , 0xDEADBEEF);
            ID.Add("A"  , 0xDEADBEEF);
            ID.Add("B"  , 0xDEADBEEF);
            ID.Add("IMM", 0xDEADBEEF);
            ID.Add("NPC", 0xDEADBEEF);


            EX.Add("IR", 0xDEADBEEF);
            EX.Add("ALU", 0xDEADBEEF);
            EX.Add("B", 0xDEADBEEF);
            EX.Add("COND", 0xDEADBEEF);

            MEM.Add("IR", 0xDEADBEEF);
            MEM.Add("ALU", 0xDEADBEEF);
            MEM.Add("LMD", 0xDEADBEEF);
            MEM.Add("MEM_ALU", 0xDEADBEEF);
        }
        
        //initializes intermediate buffers
        private void init_buffs()
        {
            IFID.Add("NPC", 0);
            IFID.Add("PC", 0);
            IFID.Add("IR", 0);

            IDEX.Add("IR", 0);
            IDEX.Add("A", 0);
            IDEX.Add("B", 0);
            IDEX.Add("IMM", 0);
            IDEX.Add("NPC", 0);

            EXMEM.Add("IR", 0);
            EXMEM.Add("ALU", 0);
            EXMEM.Add("B", 0);
            EXMEM.Add("COND", 0);

            MEMWB.Add("IR", 0);
            MEMWB.Add("ALU", 0);
            MEMWB.Add("LMD", 0);
            MEMWB.Add("MEM_ALU", 0);
        }

        //updates the contents of UI textboxes.
        private void update_display()
        {
            IF_IR_Textbox.Text  = String.Format("{0,10:X}",IF["IR"]);
            IF_PC_Textbox.Text  = String.Format("{0,10:X}", IF["PC"]);
            IF_NPC_Textbox.Text = String.Format("{0,10:X}", IF["NPC"]);

            IFID_IR_Textbox.Text  = String.Format("{0,10:X}", IFID["IR"]);
            IFID_PC_Textbox.Text  = String.Format("{0,10:X}", IFID["PC"]);
            IFID_NPC_Textbox.Text = String.Format("{0,10:X}", IFID["NPC"]);

            ID_IR_Textbox.Text  = String.Format("{0,10:X}", ID["IR"]);
            ID_NPC_Textbox.Text = String.Format("{0,10:X}", ID["NPC"]);
            ID_A_Textbox.Text   = String.Format("{0,10:X}", ID["A"]);
            ID_B_Textbox.Text   = String.Format("{0,10:X}", ID["B"]);
            ID_IMM_Textbox.Text = String.Format("{0,10:X}", ID["IMM"]);

            IDEX_IR_Textbox.Text  = String.Format("{0,10:X}", IDEX["IR"]);
            IDEX_NPC_Textbox.Text = String.Format("{0,10:X}", IDEX["NPC"]);
            IDEX_A_Textbox.Text   = String.Format("{0,10:X}", IDEX["A"]);
            IDEX_B_Textbox.Text   = String.Format("{0,10:X}", IDEX["B"]);
            IDEX_IMM_Textbox.Text = String.Format("{0,10:X}", IDEX["IMM"]);

            EX_IR_Textbox.Text  = String.Format("{0,10:X}", EX["IR"]);
            EX_ALU_Textbox.Text = String.Format("{0,10:X}", EX["ALU"]);
            EX_COND_Textbox.Text= String.Format("{0,10:X}", EX["COND"]);
            EX_B_Textbox.Text   = String.Format("{0,10:X}", EX["B"]);

            EXMEM_IR_Textbox.Text   = String.Format("{0,10:X}", EXMEM["IR"]);
            EXMEM_ALU_Textbox.Text  = String.Format("{0,10:X}", EXMEM["ALU"]);
            EXMEM_COND_Textbox.Text = String.Format("{0,10:X}", EXMEM["COND"]);
            EXMEM_B_Textbox.Text    = String.Format("{0,10:X}", EXMEM["B"]);

            MEM_IR_Textbox.Text     = String.Format("{0,10:X}", MEM["IR"]);
            MEM_ALU_Textbox.Text    = String.Format("{0,10:X}",MEM["ALU"]);
            MEM_LMD_Textbox.Text    = String.Format("{0,10:X}",MEM["LMD"]);
            MEM_MEMALU_Textbox.Text = String.Format("{0,10:X}", MEM["MEM_ALU"]);

            MEMWB_IR_Textbox.Text     = String.Format("{0,10:X}", MEMWB["IR"]);
            MEMWB_ALU_Textbox.Text    = String.Format("{0,10:X}", MEMWB["ALU"]);
            MEMWB_LMD_Textbox.Text    = String.Format("{0,10:X}", MEMWB["LMD"]);
            MEMWB_MEMALU_Textbox.Text = String.Format("{0,10:X}", MEMWB["MEM_ALU"]);

            WB_RN_Textbox.Text = String.Format("{0,10:X}", WB);
        }

        //returns the value of general purpose register R<index> stored in GPR[index].
        //has internal error checking. 
        //returns 0xFEEDFACE when an invalid index is used.
        private Int64 read_reg_content(int index)
        {
            if (index > 31 || index < 0)
            {
                return 0xFEEDFACE;
            }
            else if (index == 0)
            {
                return 0;
            }
            else
            {
                return (Int64)dt_GPR.Rows[index].ItemArray[1];
            }
        }
        //
        private Int64 read_data_memory(int  address)
        {

            if (address < DATA_SEGMENT_START || address > DATA_SEGMENT_END)
            {
                return 0xFEEDFACE;
            }
            else
            {
                return (Int64)dt_DATA_MEM.Rows[address].ItemArray[1];
            }

        }
       
        //writes a value into a general purpose register R<index>
        //returns -1 for incorrect index ranges; -2 for trying to write to R0; and 0 for a succesful write.
        private int write_reg_content(int index , Int64 data)
        {
            if (index > 31 || index < 0)
            {
                return -1;
            }
            else if (index == 0)
            {
                return -2;
            }
            else
            {
                dt_GPR.Rows[index][1] = data;
                return 0;
            }
        }
        
        //writes data into data memory located at <address>
        //returns -1 for trying to access an incorrect address; 0 for a successful write.
        private int write_data_memory(int address, Int64 data)
        {
            if (address < DATA_SEGMENT_START || address > DATA_SEGMENT_END)
            {
                return -1;
            }
            else
            {                
                dt_DATA_MEM.Rows[address][1] = data;
                return 0;
            }
        }

        private void fetch()
        {

        }
        private void decode()
        {
        }
        private void execute()
        {
        }
        private void memory_access()
        {
        }
        private void writeback()
        {
        }
        
        //Instructions will be sent to  their corresponding buffers upon CLOCK UP
        //buffer_IF() copies the contents of IF to IFID
        private void buffer_IF()
        {
            IFID["IR"] = IF["IR"];
            IFID["NPC"]= IF["NPC"];
            IFID["PC"] = IF["PC"];
        }
        //buffer_ID() copies the contents of ID to IDEX
        private void buffer_ID()
        {
            IDEX["IR"]  = ID["IR"];
            IDEX["NPC"] = ID["NPC"];
            IDEX["A"]   = ID["A"];
            IDEX["B"]   = ID["B"];
            IDEX["IMM"] = ID["IMM"];
        }
        //buffer_EX() copies the contents of EX to EXMEM
        private void buffer_EX()
        {
            EXMEM["IR"]   = EX["IR"];
            EXMEM["ALU"]  = EX["ALU"];
            EXMEM["COND"] = EX["COND"];
            EXMEM["B"]    = EX["B"];
        }
        //buffer_MEM() copies the contents of MEM to MEMWB
        private void buffer_MEM()
        {
            MEMWB["IR"]  = MEM["IR"];
            MEMWB["ALU"] = MEM["ALU"];
            MEMWB["LMD"] = MEM["LMD"];
            MEMWB["MEM_ALU"] = MEM["MEM_ALU"];
        }

        //Transfers are done upon CLOCK DOWN
        //IFID_to_ID transfers the relevant registers from IFID to the ID stage
        //process values acquired. 
        private void IFID_to_ID()
        {
            ID["IR"] = IFID["IR"];
            
        }
        //IDEX_to_EX() transfers the relevant registers from IDEX to the EX stage
        private void IDEX_to_EX()
        {
            EX["IR"] = IDEX["IR"];
        }
        //
        private void EXMEM_to_MEM()
        {
            MEM["IR"] = EXMEM["IR"];
        }
        private void MEM_to_WB()
        {
        }

        private void gprView_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            
            if (gprView.Columns[1].Name == "value")
            {
                try
                {
                    
                    Int64 hex = Convert.ToInt64(e.Value.ToString(), 64);
                    e.Value = hex;
                    e.ParsingApplied = true;
                }
                catch
                {
                    e.ParsingApplied = false;
                }
            }
        }


        private void button1_Click_1(object sender, EventArgs e)
        {
           
            Console.WriteLine(write_reg_content(2, (Int64)0xFED22));
             Console.WriteLine(String.Format("{0,10:X}", read_reg_content(2)));
        }

        private void clockButton_Click(object sender, EventArgs e)
        {
            if (!CLOCK_STATE) //CLOCK GOES FROM LOW TO HIGH
            {
                //buffer contents of pipeline stages
                buffer_IF();
                buffer_ID();
                buffer_EX();
                buffer_MEM();
                //toggle clock state
                CLOCK_STATE = !CLOCK_STATE;
                clockButton.Text = "CLOCK DOWN";
                CLOCK_COUNT++;
            }
            else
            { // CLOCK GOES FROM HIGH TO LOW
                IFID_to_ID();
                IDEX_to_EX();
                EXMEM_to_MEM();
                CLOCK_STATE = !CLOCK_STATE;
                clockButton.Text = "CLOCK UP";
                CLOCK_COUNT++;
            }
            
            if (CLOCK_COUNT % 2 == 0)
            {
                //one cycle == clock going up then going down. 
                GLOBAL_COUNTER = (int)CLOCK_COUNT / 2;
                Console.WriteLine("Clock cycle: {0}", GLOBAL_COUNTER);
                //update counter
                cycle_TextBox.Text = GLOBAL_COUNTER.ToString();
            }

            update_display();
        }

        private void loadProgramButton_Click(object sender, EventArgs e)
        {
            //error checking of code
            //parse
            //load to program memory
            startButton.Enabled = true;
        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }
        
        
    }
}
