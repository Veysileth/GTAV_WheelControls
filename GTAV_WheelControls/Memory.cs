using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace GTAV_WheelControls
{
    public static class Memory
    {
        [DllImport("kernel32.dll")]
        public static extern int WriteProcessMemory(IntPtr Handle, IntPtr Address, byte[] buffer, int Size, int BytesWritten = 0);

        [DllImport("kernel32.dll")]
        public static extern int ReadProcessMemory(IntPtr Handle, IntPtr Address, byte[] buffer, int Size, int BytesRead = 0);

        public static Process GTAprocess;
        private static ProcessModule GTAmodule;
        private static IntPtr GTAhandle;
        private static long GTAaddress;
        private static IntPtr GTApointer;

        private static IntPtr WorldPTR;
        private static int[] OFFSETS_Speed = { 0x8, 0xD30, 0xAA8 };

        private static IntPtr GetPointerAddress(IntPtr Pointer, int[] Offsets = null)
        {
            byte[] Buffer = new byte[16];
            ReadProcessMemory(GTAhandle, Pointer, Buffer, Buffer.Length);

            if (Offsets != null)
            {
                foreach (int offset in Offsets)
                {
                    IntPtr bufferPTR = (IntPtr)BitConverter.ToInt64(Buffer, 0);
                    Pointer = IntPtr.Add(bufferPTR, offset);
                    ReadProcessMemory(GTAhandle, Pointer, Buffer, Buffer.Length);
                }
            }
            return Pointer;
        }

        private static float ReadFloat(IntPtr Address)
        {
            byte[] Buffer = new byte[4]; ;
            ReadProcessMemory(GTAhandle, Address, Buffer, 4);
            return BitConverter.ToSingle(Buffer, 0);
        }

        private static int ReadInteger(IntPtr Address, int Length)
        {
            byte[] Buffer = new byte[Length];
            ReadProcessMemory(GTAhandle, Address, Buffer, Length);
            return BitConverter.ToInt32(Buffer, 0);
        }

        public static float GetVehicleSpeed()
        {
            if (GTAprocess == null || GTAprocess.HasExited) { GetGTAProcess(); return 0F; }
            IntPtr temp = GetPointerAddress(WorldPTR, OFFSETS_Speed);
            float value = ReadFloat(temp);
            return value * 3.75F;
        }

        public static void GetGTAProcess()
        {
            if (GTAprocess == null || GTAprocess.HasExited)
            {
                Process[] process = Process.GetProcessesByName("GTA5");
                if (process.Length > 0)
                {
                    GTAprocess = process[0];
                    GTAhandle = GTAprocess.Handle;
                    Thread.Sleep(100);
                    GTAmodule = GTAprocess.MainModule;
                    GTApointer = GTAprocess.MainModule.BaseAddress;
                    GTAaddress = GTAprocess.MainModule.BaseAddress.ToInt64();

                    IntPtr intPtr;
                    intPtr = PatternScanMod("48 8B 05 ? ? ? ? 45 ? ? ? ? 48 8B 48 08 48 85 C9 74 07");
                    WorldPTR = (intPtr + ReadInteger(intPtr + 3, 4) + 7);
                }
            }
        }

        private static bool CheckPattern(string pattern, byte[] array2check)
        {
            string[] strBytes = pattern.Split(' ');
            int x = 0;
            foreach (byte b in array2check)
            {
                if (strBytes[x] == "?" || strBytes[x] == "??")
                {
                    x++;
                }
                else if (byte.Parse(strBytes[x], NumberStyles.HexNumber) == b)
                {
                    x++;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private static IntPtr PatternScanMod(string pattern)
        {
            IntPtr baseAddy = (IntPtr)GTAaddress;
            uint dwSize = (uint)GTAprocess.MainModule.ModuleMemorySize;
            byte[] memDump = new byte[dwSize];
            ReadProcessMemory(GTAhandle, baseAddy, memDump, memDump.Length);
            string[] pBytes = pattern.Split(' ');
            try
            {
                for (int y = 0; y < memDump.Length; y++)
                {
                    if (memDump[y] == byte.Parse(pBytes[0], NumberStyles.HexNumber))
                    {
                        byte[] checkArray = new byte[pBytes.Length];
                        for (int x = 0; x < pBytes.Length; x++)
                        {
                            checkArray[x] = memDump[y + x];
                        }
                        if (CheckPattern(pattern, checkArray))
                        {
                            return baseAddy + y;
                        }
                        else
                        {
                            y += pBytes.Length - (pBytes.Length / 2);
                        }
                    }
                }
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }
    }
}