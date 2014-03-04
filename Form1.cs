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

        private int rwb = 0;
        
        //Buffer
        private Hashtable IF  = new Hashtable();
        private Hashtable ID  = new Hashtable();
        private Hashtable EX  = new Hashtable();
        private Hashtable MEM = new Hashtable();
        private Hashtable WB = new Hashtable();

        private byte[] readbuffer = new byte[8];
        private Boolean readsignal = false;

        //Pipeline
        private Hashtable IFID  = new Hashtable();
        private Hashtable IDEX  = new Hashtable();
        private Hashtable EXMEM = new Hashtable();
        private Hashtable MEMWB = new Hashtable();
        private Hashtable WBR = new Hashtable();
        private string[] test = "r10(1000)".Split(new char[] {'(',')'}, StringSplitOptions.RemoveEmptyEntries);
        private Boolean executionFinished = false;
        private Boolean writesignal = false;

        public MainWindow()
        {
            InitializeComponent();

            clockButton.Text = "CLOCK UP";
            dt_GPR = new DataTable("gpr");
            dt_DATA_MEM = new DataTable("data");
            dt_PROG_MEM = new DataTable("prog");

            GLOBAL_COUNTER = 0;
            for (int i = 0; i < 32; i++)
                usagemonitor.Add(new Stack());

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
                dt_GPR.Rows.Add(x,(Int64)0);
            }
           
            bs_GPR.DataSource = dt_GPR;          
            gprView.DataSource = dt_GPR;
            gprView.DataSource = bs_GPR;
            //housekeeping
            gprView.Columns[0].ReadOnly = true;
            gprView.Columns[1].DefaultCellStyle.Format = "X"; //set cell formatting to hexadecimal
            gprView.Refresh();

        }

        private void init_dt_gpr_clear()
        {
            dt_GPR.Rows.Clear();
            dt_GPR.Columns.Clear();
            dt_GPR.Columns.Add("index");
            dt_GPR.Columns.Add("value", typeof(Int64));

            for (int x = 0; x < 32; x++)
            {
                dt_GPR.Rows.Add(x, (Int64)0);
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
            dt_DATA_MEM.Columns.Add("value",typeof(byte));

            for (int x = DATA_SEGMENT_START; x <= DATA_SEGMENT_END; x++)
            {
                dt_DATA_MEM.Rows.Add((Int64)x,(byte)0x0);
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
        private void init_dt_mem_clear()
        {
            dt_DATA_MEM.Rows.Clear();
            dt_DATA_MEM.Columns.Clear();
            dt_DATA_MEM.Columns.Add("address", typeof(Int64));
            dt_DATA_MEM.Columns.Add("value", typeof(byte));

            for (int x = DATA_SEGMENT_START; x <= DATA_SEGMENT_END; x++)
            {
                dt_DATA_MEM.Rows.Add((Int64)x, (byte)0x0);
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
            dt_PROG_MEM.Columns.Add("value", typeof(UInt32));

            for (int x = 0; x <= CODE_SEGMENT_END; )
            {
                dt_PROG_MEM.Rows.Add((Int64)x, (UInt32)0x0);
                x = x + 4;
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
        
        private void init_dt_prog(ArrayList vals)
        {
            dt_PROG_MEM.Rows.Clear();
            dt_PROG_MEM.Columns.Remove("address");
            dt_PROG_MEM.Columns.Remove("value");
            dt_PROG_MEM.Columns.Add("address", typeof(Int64));
            dt_PROG_MEM.Columns.Add("value", typeof(UInt32));

            for (int x = 0; x <= CODE_SEGMENT_END; )
            {
                if(vals.Count - 1 < x/4)
                    dt_PROG_MEM.Rows.Add((Int64)x, (UInt32)0x0);
                else
                    dt_PROG_MEM.Rows.Add((Int64)x, (UInt32)vals[x/4]);
                x = x + 4;
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

            WBR.Add("Rn", 0);
        }
        
        //initializes intermediate buffers
        private void init_buffs()
        {
            IF.Add("NPC", 0);
            IF.Add("PC", 0);
            IF.Add("IR", 0);

            ID.Add("IR", 0);
            ID.Add("A", 0);
            ID.Add("B", 0);
            ID.Add("IMM", 0);
            ID.Add("NPC", 0);

            EX.Add("IR", 0);
            EX.Add("ALU", 0);
            EX.Add("B", 0);
            EX.Add("COND", 0);

            MEM.Add("IR", 0);
            MEM.Add("ALU", 0);
            MEM.Add("LMD", 0);
            MEM.Add("MEM_ALU", 0);

            WB.Add("Rn", 0);

            
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

            WB_textbox.Text = String.Format("{0,10:X}", WB["Rn"]);
            WB_RN_Textbox.Text = String.Format("{0,10:X}", WBR["Rn"]);
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
        private byte read_data_memory(int  address)
        {

            if (address < DATA_SEGMENT_START || address > DATA_SEGMENT_END)
            {
                return 0xFF;
            }
            else
            {
                return (byte)dt_DATA_MEM.Rows[address].ItemArray[1];
            }

        }
        private UInt32 read_prog_memory(int address)
        {

            if (address < 0 || address > CODE_SEGMENT_END)
            {
                return 0xFEEDFACE;
            }
            else
            {
                return (UInt32)dt_PROG_MEM.Rows[address/4].ItemArray[1];
            }

        }
       
        //writes a value into a general purpose register R<index>
        //returns -1 for incorrect index ranges; -2 for trying to write to R0; and 0 for a succesful write.
        private int write_reg_content(int index , UInt64 data)
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
        private int write_data_memory(int address, byte data)
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

        private bool stallfromfetch = false;
        private bool stallfromdecode = false;
        private bool stallfromexecute = false;
        private bool stallfrommem = false;
        private bool stallfromwb = false;

        private byte[] writebuffer = new byte[8];
        private Int64[] regbuffer = new Int64[32];
        private ArrayList usagemonitor = new ArrayList();
        private int[] usagemonitorflag = new int[32];
        private void fetch()
        {
            if(!stallfromdecode && !stallfromexecute && !stallfrommem && !stallfromwb)
            {
                IF["IR"] = read_prog_memory((int)IFID["PC"]);
                int var = (int)IFID["IR"];
                int npc = branchorjump((UInt32)var);
                if (npc < 0)
                {
                    npc = (int)IFID["PC"] + 4;
                }
                IF["NPC"] = npc;
                IF["PC"] = npc;
            }
            
        }

        private int branchorjump(UInt32 ir)
        {
            //later for branching and jumping
            return -1;
        }
        private void decode()
        {
            if (!stallfromexecute && !stallfrommem && !stallfromwb && (int)IFID["IR"] != 0)
            {
                int curop = opcode((UInt32)IFID["IR"]);
                if (curop == 4 && ((Stack)(usagemonitor[rsregOrA((UInt32)IFID["IR"])])).Count > 0)
                {
                    stallfromdecode = true;
                }
                else
                {
                    stallfromdecode = false;
                    ID["IR"] = IFID["IR"];
                    ID["A"] = read_reg_content(rsregOrA((UInt32)IFID["IR"]));
                    ID["B"] = read_reg_content(rtregOrB((UInt32)IFID["IR"]));
                    ID["Imm"] = immoffset((UInt32)IFID["IR"]);

                    if (curop == 0)
                    {
                        int rd = rdreg((UInt32)IFID["IR"]);
                        ((Stack)(usagemonitor[rd])).Push(1);
                    }
                    else if (curop == 13 || curop == 25 || curop == 55)
                    {
                        int rd = rtregOrB((UInt32)IFID["IR"]);
                        ((Stack)(usagemonitor[rd])).Push(1);
                    }
                }
            }
        }
        private void execute()
        {
            if (!stallfrommem && !stallfromwb && (int)IDEX["IR"] != 0)
            {
                int curop = opcode((UInt32)IDEX["IR"]);
                if (curop == 0)
                {
                    if (((Stack)(usagemonitor[rsregOrA((UInt32)IDEX["IR"])])).Count > 0 || ((Stack)(usagemonitor[rtregOrB((UInt32)IDEX["IR"])])).Count > 0)
                    {
                        stallfromexecute = true;
                    }
                    else
                    {
                        stallfromexecute = false;
                        EX["IR"] = IDEX["IR"];
                        EX["B"] = IDEX["B"];
                        int funcode = funccode((UInt32)IDEX["IR"]);
                        long res = 0;
                        if (funcode == 45)
                            res = regbuffer[rsregOrA((UInt32)IDEX["IR"])] + regbuffer[rtregOrB((UInt32)IDEX["IR"])];
                        if (funcode == 47)
                            res = regbuffer[rsregOrA((UInt32)IDEX["IR"])] - regbuffer[rtregOrB((UInt32)IDEX["IR"])];
                        if (funcode == 36)
                            res = regbuffer[rsregOrA((UInt32)IDEX["IR"])] & regbuffer[rtregOrB((UInt32)IDEX["IR"])];
                        if (funcode == 22)
                            res = regbuffer[rsregOrA((UInt32)IDEX["IR"])] >> (byte)regbuffer[rtregOrB((UInt32)IDEX["IR"])];
                        if (funcode == 42)
                            if (regbuffer[rsregOrA((UInt32)IDEX["IR"])] < regbuffer[rtregOrB((UInt32)IDEX["IR"])])
                                res = 1;
                            else
                                res = 0;
                        EX["ALU"] = res;
                        int rd = rdreg((UInt32)IDEX["IR"]);
                        regbuffer[rd] = res;
                        usagemonitorflag[rd] = 1;
                    }
                } else if (curop ==4 )
                {
                    EX["IR"] = IDEX["IR"];
                    EX["B"] = IDEX["B"];
                    EX["ALU"] = (long)immoffset((UInt32)IDEX["IR"]) * 4 + (long)IDEX["NPC"];
                } else if (curop ==2 )
                {
                    EX["IR"] = IDEX["IR"];
                    EX["B"] = IDEX["B"];
                    EX["ALU"] = (long)joffset((UInt32)IDEX["IR"]) * 4;
                }
                else if (curop == 13 || curop == 25)
                {
                    if (((Stack)(usagemonitor[rsregOrA((UInt32)IDEX["IR"])])).Count > 0)
                    {
                        stallfromexecute = true;
                    }
                    else
                    {
                        stallfromexecute = false;
                        EX["IR"] = IDEX["IR"];
                        EX["B"] = IDEX["B"];

                        long res = 0;
                        if (curop == 13)
                            res = (long) ((UInt64)IDEX["Imm"] | (UInt64)regbuffer[rsregOrA((UInt32)IDEX["IR"])]);
                        if (curop == 25)
                            res = (long)(IDEX["Imm"]) + regbuffer[rsregOrA((UInt32)IDEX["IR"])];
                        int rd = rtregOrB((UInt32)IDEX["IR"]);
                        EX["ALU"] = res;
                        regbuffer[rd] = res;
                        usagemonitorflag[rd] = 1;
                    }
                }
                else if (curop == 63 || curop == 55)
                {
                    if (((Stack)(usagemonitor[rsregOrA((UInt32)IDEX["IR"])])).Count > 0)
                    {
                        stallfromexecute = true;
                    }
                    else
                    {
                        stallfromexecute = false;
                        EX["IR"] = IDEX["IR"];
                        EX["B"] = IDEX["B"];

                        long res = 0;
                        res = (long)(IDEX["Imm"]) + regbuffer[rsregOrA((UInt32)IDEX["IR"])];
                        EX["ALU"] = res;
                    }
                }
                   
                
            }
        }
        private void memory_access()
        {
            if ((int)EXMEM["IR"] != 0)
            {
                int curop = opcode((UInt32)EXMEM["IR"]);
                if (curop == 55)
                {
                    //load here to LMD

                    MEM["IR"] = EXMEM["IR"];
                    MEM["ALU"] = EXMEM["ALU"];
                    long add = (long)EXMEM["ALU"];
                    for (int i = 7; i >= 0; i--)
                    {
                        readbuffer[7] = (byte)read_data_memory((int)add);
                        add = add + 1;
                    }
                    readsignal = true;
                }
                if (curop == 63)
                {
                    if (((Stack)(usagemonitor[rtregOrB((UInt32)EXMEM["IR"])])).Count > 0)
                    {
                        stallfrommem = true;
                    }
                    else
                    {
                        stallfrommem = false;
                        MEM["IR"] = EXMEM["IR"];
                        MEM["ALU"] = EXMEM["ALU"];
                        UInt64 fortrans = (UInt64)regbuffer[rtregOrB((UInt32)EXMEM["IR"])];
                        for (int i = 7; i >= 0; i--)
                        {
                            byte temp = (byte)fortrans;
                            writebuffer[i] = temp;
                            fortrans = fortrans >> 8;
                        }
                        writesignal = true;
                        //store word here
                    }
                }
                else
                {
                    MEM["IR"] = EXMEM["IR"];
                    MEM["ALU"] = EXMEM["ALU"];
                }
            }

        }
        private void writeback()
        {
            if ((int)EXMEM["IR"] != 0)
            {
                int curop = opcode((UInt32)MEMWB["IR"]);
                if (curop == 0)
                {
                    rwb = rdreg((UInt32)MEMWB["IR"]);
                    WB["Rn"] = MEMWB["ALU"];
                }
                else if (curop == 13 || curop == 25)
                {
                    rwb = rtregOrB((UInt32)MEMWB["IR"]);
                    WB["Rn"] = MEMWB["ALU"];
                }
                else if (curop == 55)
                {
                    rwb = rtregOrB((UInt32)MEMWB["IR"]);
                    WB["Rn"] = MEMWB["LMD"];
                }
            }
            
            
        }


        private int opcode(UInt32 ir) 
        {
            UInt32 mask = 0xFC000000;
            UInt32 opcode = ir & mask >> 26;

            return (int)opcode;
        }
        private int rsregOrA(UInt32 ir)
        {
            UInt32 mask = 0x3E00000;
            UInt32 rs = ir & mask >> 21;
            return (int)rs;
        }
        private int rtregOrB(UInt32 ir)
        {
            UInt32 mask = 0x1F0000;
            UInt32 rt = ir & mask >> 16;
            return (int)rt;
        }
        private int immoffset(UInt32 ir)
        {
            UInt32 mask = 0x0000FFFF;
            UInt32 imm = ir & mask;
            return (int)imm;
        }
        private int rdreg(UInt32 ir)
        {
            UInt32 mask = 0xF800;
            UInt32 rd = mask & ir >> 11;

            return (int)rd;
        }
        private int funccode(UInt32 ir)
        {
            UInt32 mask = 0x3F;
            UInt32 fnc = mask & ir;

            return (int)fnc;
        }
        private int joffset(UInt32 ir)
        {
            UInt32 mask = 0x3FFFFFF;
            UInt32 joff = mask & ir;
            return (int)joff;
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
                writeback();
                memory_access();
                execute();
                decode();
                fetch();
                //toggle clock state
                CLOCK_STATE = !CLOCK_STATE;
                clockButton.Text = "CLOCK DOWN";
                CLOCK_COUNT++;
            }
            else
            { // CLOCK GOES FROM HIGH TO LOW
                buffer_IF();
                buffer_ID();
                buffer_EX();
                buffer_MEM();

                for (int i = 0; i < 32; i++)
                {
                    if (usagemonitorflag[i] == 1)
                    {
                        usagemonitorflag[i] = 0;
                        ((Stack)(usagemonitor[i])).Pop();
                    }
                }
                if (writesignal)
                {
                    int effadd = (int)MEMWB["ALU"];
                    for (int i = 7; i >= 0; i--)
                    {

                        write_data_memory(effadd + i, writebuffer[i]);
                        writebuffer[i] = 0;
                    }
                    writesignal = false;
                }
                if (readsignal)
                {
                    MEMWB["LMD"] = 0xDEADBEEF;
                    readsignal = false;
                }

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
            ArrayList res = mipsparser.processASM(program_input_TextBox.Lines);
            if (res == null)
                MessageBox.Show(mipsparser.errorcollection);
            else
                init_dt_prog(res);
            startButton.Enabled = true;
        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            init_dt_mem_clear();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            init_dt_gpr_clear();
            for (int i = 0; i < 32; i++)
            {
                regbuffer[i] = 0;
            }
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            for(int i = 0; i < 32; i++)
            {
                regbuffer[i] = read_reg_content(i);
            }
        }




        
        
    }
}
