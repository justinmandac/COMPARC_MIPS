using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniMIPS_v_0_2
{
    public static class mipsparser
    {
        public static string errorcollection = "";

        public static string[] registers = "r0 r1 r2 r3 r4 r5 r6 r7 r8 r9 r10 r11 r12 r13 r14 r15 r16 r17 r18 r19 r20 r21 r22 r23 r24 r25 r26 r27 r28 r29 r30 r31".Split(' ');

        public static ArrayList registersAL = new ArrayList(registers);

        public static string[] rtype = "daddu dsubu and dsrlv slt".Split(' ');

        public static ArrayList rtypeAL = new ArrayList(rtype);

        public static string[] rtypefunc = "45 47 36 22 42".Split(' ');

        public static ArrayList rtypefuncAL = new ArrayList(rtypefunc);

        public static string[] itype = "beqz ld sd daddiu ori".Split(' ');

        public static ArrayList itypeAL = new ArrayList(itype);

        public static string[] itypeopcode = "4 55 63 25 13".Split(' ');

        public static ArrayList itypeopcodeAL = new ArrayList(itypeopcode);

        public static string[] jtype = "j".Split(' ');

        public static ArrayList jtypeAL = new ArrayList(jtype);

        public static string[] jtypeopcode = "2".Split(' ');

        public static ArrayList jtypeopcodeAL = new ArrayList(jtypeopcode);

        public static ArrayList processASM(string[] lines)
        {
            int linenum = 1;
            mipsparser.errorcollection = "";
            ArrayList res = new ArrayList();
            Dictionary<string,int> labelmap = new Dictionary<string,int>();
            ArrayList procline = new ArrayList();
            foreach (string line in lines)
            {
               
                string[] attempt = line.Split(':');
                if (attempt.Length > 1)
                {
                    labelmap.Add(attempt[0].ToLower(), linenum);
                    int index = line.IndexOf(attempt[0]+":");
                    string newline = (index < 0) ? line : line.Remove(index, (attempt[0] + ":").Length);
                    procline.Add(newline.ToLower());
                }
                else
                {
                    procline.Add(line);
                }
                linenum++;
            }

            linenum = 1;

            foreach (string line in procline)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    string[] normlinetok = line.ToLower().Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (rtypeAL.Contains(normlinetok[0]))
                    {
                        if (normlinetok.Length != 4)
                            mipsparser.errorcollection += "Line " + linenum.ToString() + ": incomplete number of arguments\n";
                        else
                            if(registersAL.Contains(normlinetok[1]) && registersAL.Contains(normlinetok[2]) && registersAL.Contains(normlinetok[3]))
                            {
                                uint optrans = 0;
                                uint opc = 0 << 26;
                                uint rd = ((uint)Array.IndexOf(registers, normlinetok[1])) << 6;
                                uint rs = ((uint)Array.IndexOf(registers, normlinetok[2])) << 21;
                                uint rt = ((uint)Array.IndexOf(registers, normlinetok[3])) << 16;
                                uint fnc = uint.Parse(rtypefunc[Array.IndexOf(rtype, normlinetok[0])]);

                                optrans = opc | rd | rs | rt | fnc;
                                res.Add(optrans);
                            }
                            else
                            {
                                mipsparser.errorcollection += "Line " + linenum.ToString() + ": invalid register specified\n";
                            }
                    }
                    else if (itypeAL.Contains(normlinetok[0]))
                    {
                        if ("beqz"==normlinetok[0])
                        {
                            if(normlinetok.Length != 3)
                            {
                                mipsparser.errorcollection += "Line " + linenum.ToString() + " : incomplete number of arguments\n";
                            }
                            else
                            {
                                if (registersAL.Contains(normlinetok[1]) && labelmap.ContainsKey(normlinetok[2])) 
                                {
                                    uint optrans = 0;
                                    uint opc = 4 << 26;
                                    uint rs = ((uint)Array.IndexOf(registers, normlinetok[1])) << 21;
                                    short offset = (short) (labelmap[normlinetok[2]] - (linenum + 1));
                                    UInt16 imm = (UInt16)offset;

                                    optrans = opc | rs | imm;
                                    res.Add(optrans);
                                }
                                else
                                {
                                    mipsparser.errorcollection += "Line " + linenum.ToString() + " invalid register or label specified\n";
                                }
                            }
                        }
                        else if ("ld"==(normlinetok[0])||"sd"==(normlinetok[0]))
                        {
                            if (normlinetok.Length != 3)
                                mipsparser.errorcollection += "Line " + linenum.ToString() + " invalid number of arguments specified\n";
                            else
                            {
                                string[] parsed3 = normlinetok[2].Split(new char[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parsed3.Length != 2)
                                    mipsparser.errorcollection += "Line " + linenum.ToString() + ": register or offset specified\n";
                                else
                                {
                                    if( !(registersAL.Contains(normlinetok[1])  || registersAL.Contains(parsed3[1]) ))
                                    {
                                        mipsparser.errorcollection += "Line " + linenum.ToString() + ": register or offset specified\n";
                                    }
                                    else
                                    {
                                        Int16 offset;
                                        if(Int16.TryParse(parsed3[0], System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset))
                                        {
                                            uint optrans = 0;
                                            uint opc = uint.Parse(itypeopcode[Array.IndexOf(itype, normlinetok[0])]) << 26;
                                            uint rt = ((uint)Array.IndexOf(registers, normlinetok[1])) << 16;
                                            uint rs = ((uint)Array.IndexOf(registers, parsed3[1])) << 21;
                                            uint imm = (uint)offset;

                                            optrans = opc | rs | rt | imm;
                                            res.Add(optrans);
                                        } 
                                        else
                                        {
                                            mipsparser.errorcollection += "Line " + linenum.ToString() + ": register or offset specified\n";
                                        }
                                    }
                                }

                                
                            }
                        }
                        else if ("daddiu"==(normlinetok[0]) || "ori"==normlinetok[0])
                        {
                            if (normlinetok.Length != 4)
                                mipsparser.errorcollection += "Line " + linenum.ToString() + ": incomplete number of arguments\n";
                            else
                                if (!(registersAL.Contains(normlinetok[1]) || registersAL.Contains(normlinetok[2])))
                                {
                                    mipsparser.errorcollection += "Line " + linenum.ToString() + ": invalid register specified\n";
                                    
                                }
                                else
                                {
                                    short offset;
                                    System.Globalization.NumberStyles sty = System.Globalization.NumberStyles.None;
                                    string tok = normlinetok[3];
                                    if (tok[0] == '#')
                                    {
                                        sty = System.Globalization.NumberStyles.HexNumber;
                                        tok = tok.TrimStart('#');
                                    }
                                    if (Int16.TryParse(tok,sty,CultureInfo.InvariantCulture,out offset))
                                    {
                                        uint optrans = 0;
                                        uint opc = uint.Parse(itypeopcode[Array.IndexOf(itype, normlinetok[0])]) << 26;
                                        uint rs = ((uint)Array.IndexOf(registers, normlinetok[2])) << 21;
                                        uint rd = ((uint)Array.IndexOf(registers, normlinetok[1])) << 16;
                                        uint imm = (uint) offset;

                                        optrans = opc | rd | rs | imm;
                                        res.Add(optrans);
                                    }
                                }
                        }
                        
                        //itype here
                    }
                    else if (jtypeAL.Contains(normlinetok[0]))
                    {
                        if (normlinetok.Length != 2)
                            mipsparser.errorcollection += "Line " + linenum.ToString() + ": invalid number of arguments\n";
                        else
                        {
                            if (!labelmap.ContainsKey(normlinetok[1]))
                            {
                                mipsparser.errorcollection += "Line " + linenum.ToString() + ": invalid label\n";
                            }
                            else
                            {
                                int offset = labelmap[normlinetok[1]];
                                int mask = 67108863;
                                uint imm = (uint)offset;
                                uint optrans = (uint) (mask & imm);
                                uint opc = uint.Parse(jtypeopcode[Array.IndexOf(jtype, normlinetok[0])]) << 26;
                                optrans = opc | imm;
                                res.Add(optrans);
                            }
                        }
                        //jtype here
                    }
                    else
                    {
                        mipsparser.errorcollection += "Line " + linenum.ToString() + ": unidentified instruction type\n";
                    }
                }

                linenum++;
            }
            if (errorcollection != "")
                res = null;
            return res;
        }
    }
}
