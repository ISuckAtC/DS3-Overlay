using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

namespace DS3_Overlay
{
    public struct PlayerInfo
    {
        public int HP;
        public IntPtr HPPtr;
        public int MaxHP;
        public IntPtr MaxHPPtr;
        public string Name;
        public string ChrType;
        public bool Connected;
    }
    public partial class Form1 : Form
    {
        #region DllImports
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        [Out] byte[] lpBuffer,
        int dwSize,
        out IntPtr lpNumberOfBytesRead);

        [DllImport("User32.dll", SetLastError = true)]
        static extern short GetKeyState(int nVirtKey);

        [DllImport("User32.dll", SetLastError = true)]
        public static extern long GetWindowLong(
        IntPtr handle,
        int nIndex
        );

        [DllImport("User32.dll", SetLastError = true)]
        public static extern long SetWindowLong(
        IntPtr handle,
        int nIndex,
        long dwNewLong
        );
        #endregion

        static byte[] ReadMem(IntPtr baseAdd, int size, int caller = 0)
        {
            byte[] buf = new byte[size];
            IntPtr bRead = new IntPtr();
            ReadProcessMemory(Process.GetProcessById(Ds3ProcessId).Handle, baseAdd, buf, size, out bRead);
            lastErr = Marshal.GetLastWin32Error();
            if (lastErr > 0) 
            {
                Console.WriteLine("ERROR: " + lastErr + " | caller: " + caller);
                if (lastErr == 6)
                {
                    DS3Handle = Process.GetProcessesByName("DarkSoulsIII")[0].Handle;
                    if (!chill)
                    {
                        Console.WriteLine("Entering chill zone");
                        chill = true;
                        Thread chillout = new Thread(() => {
                            Thread.Sleep(2000);
                            Console.WriteLine("Exiting chill zone");
                            chill = false;
                        });
                        chillout.Start();
                    }
                    
                }
            }
            return buf;
        }
        static IntPtr PointerOffset(IntPtr ptr, long[] offsets)
        {

            foreach (long offset in offsets)
            {
                ptr = new IntPtr(BitConverter.ToInt64(ReadMem(ptr, 8)) + offset);
            }
            return ptr;
        }
        static void SetBases(Process ds3)
        {
            DS3Handle = ds3.Handle;
            Ds3ProcessId = ds3.Id;
            BaseDS3 = ds3.MainModule.BaseAddress;
            BaseA = new IntPtr(BaseDS3.ToInt64() + long.Parse("4740178", System.Globalization.NumberStyles.HexNumber));
            BaseB = new IntPtr(BaseDS3.ToInt64() + long.Parse("4768E78", System.Globalization.NumberStyles.HexNumber));
            BaseC = new IntPtr(BaseDS3.ToInt64() + long.Parse("4743AB0", System.Globalization.NumberStyles.HexNumber));
            BaseD = new IntPtr(BaseDS3.ToInt64() + long.Parse("4743A80", System.Globalization.NumberStyles.HexNumber));
            BaseE = new IntPtr(BaseDS3.ToInt64() + long.Parse("473FD08", System.Globalization.NumberStyles.HexNumber));
            BaseF = new IntPtr(BaseDS3.ToInt64() + long.Parse("473AD78", System.Globalization.NumberStyles.HexNumber));
            BaseZ = new IntPtr(BaseDS3.ToInt64() + long.Parse("4768F98", System.Globalization.NumberStyles.HexNumber));
            Param = new IntPtr(BaseDS3.ToInt64() + long.Parse("4782838", System.Globalization.NumberStyles.HexNumber));
            GameFlagData = new IntPtr(BaseDS3.ToInt64() + long.Parse("473BE28", System.Globalization.NumberStyles.HexNumber));
            LockBonus_ptr = new IntPtr(BaseDS3.ToInt64() + long.Parse("4766CA0", System.Globalization.NumberStyles.HexNumber));
            DrawNearOnly_ptr = new IntPtr(BaseDS3.ToInt64() + long.Parse("4766555", System.Globalization.NumberStyles.HexNumber));
            debug_flags = new IntPtr(BaseDS3.ToInt64() + long.Parse("4768F68", System.Globalization.NumberStyles.HexNumber));
        }
        static byte[] Snap(byte[] b)
        {
            byte[] o = new byte[b.Length / 2];
            for (int i = 0; i < b.Length; i += 2) o[i / 2] = b[i];
            return o;
        }

        #region BasePointers
        static public IntPtr BaseDS3;
        static public IntPtr DS3Handle;
        static public IntPtr BaseA;
        static public IntPtr BaseB;
        static public IntPtr BaseC;
        static public IntPtr BaseD;
        static public IntPtr BaseE;
        static public IntPtr BaseF;
        static public IntPtr BaseZ;
        static public IntPtr Param;
        static public IntPtr GameFlagData;
        static public IntPtr LockBonus_ptr;
        static public IntPtr DrawNearOnly_ptr;
        static public IntPtr debug_flags;
        #endregion

        static public int lastErr;
        static public PlayerInfo[] Players;
        static public IntPtr[] PlayerPointers;

        static Graphics graphics;
        static Font drawFont;
        static Brush drawBrush;
        static Point DrawPoint;
        static int PlayerUIOffset = 100;

        static int Ds3ProcessId;

        static bool chill;
        public Form1()
        {
            InitializeComponent();

            DrawPoint = new Point(1600, 200);
            
            Process ds3 = Process.GetProcessesByName("DarkSoulsIII")[0];
            SetBases(ds3);

            PlayerPointers = new IntPtr[5];
            for(int i = 0; i < 5; ++i)
            {
                PlayerPointers[i] = PointerOffset(BaseB, new long[] {0x40, 0x38 * (i + 1)});
            }
            Players = new PlayerInfo[5];

            graphics = CreateGraphics();
            drawFont = new Font("Arial", 15);
            drawBrush = new SolidBrush(Color.White);


            long initialStyle = GetWindowLong(this.Handle, -20);
            SetWindowLong(this.Handle, -20, initialStyle | 0x80000 | 0x20);

            Thread connections = new Thread(() => WatchConnections());
            Thread playerData = new Thread(() => WatchPlayerData());

            connections.Start();
            playerData.Start();


            Thread test = new Thread(() => {
                int i = 0;
                while(true)
                {
                    Console.WriteLine();
                    byte[] bytes = ReadMem(PlayerPointers[0], 8);
                    Console.Write(i++ + ">");
                    foreach (byte b in bytes) Console.Write(b.ToString("X2"));
                    Console.WriteLine("\n" + BitConverter.ToInt64(bytes) + "\n\n");
                    Thread.Sleep(1000);
                }
            });
            //test.Start();

            //Console.WriteLine(BitConverter.ToInt32(ReadMem(PointerOffset(BaseB, new long[] {0x40, 0x38, 0x1FA0, 0x70}), 4)));


            System.Windows.Forms.Timer draw = new System.Windows.Forms.Timer();
            draw.Interval = 16;
            draw.Tick += Draw;
            draw.Start();
        }

        public void Draw(object sender, EventArgs e)
        {
            if (chill) return;
            int playerCount = 0;
            TextRenderer.DrawText(graphics, "Funny player viewer by Ryonic", drawFont, new Point(DrawPoint.X, DrawPoint.Y - 50), Color.White, Color.Black);
            for (int i = 0; i < Players.Length; ++i)
            {
                if (!Players[i].Connected) continue;
                TextRenderer.DrawText(graphics, "[" + (i + 1).ToString() + "] " + Players[i].Name, drawFont, new Point(DrawPoint.X, DrawPoint.Y + (PlayerUIOffset * playerCount)), Color.White, Color.Black);
                TextRenderer.DrawText(graphics, Players[i].ChrType, drawFont, new Point(DrawPoint.X + 150, DrawPoint.Y + (PlayerUIOffset * playerCount)), Color.White, Color.Black);
                graphics.FillRectangle(Brushes.DarkGray, new Rectangle(DrawPoint.X, (DrawPoint.Y + (PlayerUIOffset * playerCount) + 30), 300, 20));
                int health = (int)(((float)Players[i].HP / Players[i].MaxHP) * 300);
                if (health > 300) 
                {
                    playerCount++;
                    continue;
                }
                graphics.FillRectangle(Brushes.Red, new Rectangle(DrawPoint.X + 1, (DrawPoint.Y + (PlayerUIOffset * playerCount) + 31), health - 2, 18));
                playerCount++;
            }
        }

        public void WatchConnections()
        {
            try
            {
            while(true)
            {
                if (chill) continue;
                for (int i = 0; i < Players.Length; ++i)
                {
                    if (BitConverter.ToInt64(ReadMem(PlayerPointers[i], 8, 2)) != 0) // Is not empty pointer?
                    {
                        if (!Players[i].Connected)
                        {
                            Players[i].Name = Encoding.UTF8.GetString(Snap(ReadMem(PointerOffset(PlayerPointers[i], new long[] {0x1FA0, 0x88}), 32)));
                            int chrType = BitConverter.ToInt32(ReadMem(PointerOffset(PlayerPointers[i], new long[] {0x70}), 4));
                            switch(chrType)
                            {
                                case 0:
                                    Players[i].ChrType = "Host";
                                    break;
                                case 1:
                                    Players[i].ChrType = "White Phantom";
                                    break;
                                case 2:
                                    Players[i].ChrType = "Dark Spirit";
                                    break;
                                default:
                                    Players[i].ChrType = "Unknown";
                                    break;
                            }
                            Players[i].HPPtr = PointerOffset(PlayerPointers[i], new long[] {0x1FA0, 0x18});
                            Players[i].MaxHPPtr = PointerOffset(PlayerPointers[i], new long[] {0x1FA0, 0x1C});
                            Console.WriteLine("\nPlayer [" + Players[i].Name + "] connected!");
                            Players[i].Connected = true;
                            graphics.Clear(Color.Blue);
                        }
                    }
                    else if (Players[i].Connected)
                    {
                        Console.WriteLine("Player " + (i + 1) + " disconnected!");
                        Players[i].Connected = false;
                        graphics.Clear(Color.Blue);
                    }
                }
                Thread.Sleep(50);
            }
            } catch (Exception e) {Console.WriteLine(e.Message + "\n" + e.StackTrace);} 
        }

        public void WatchPlayerData()
        {
            while(true)
            {
                if (chill) continue;
                for (int i = 0; i < Players.Length; ++i)
                {
                    if (Players[i].Connected)
                    {
                        Players[i].HP = BitConverter.ToInt32(ReadMem(Players[i].HPPtr, 4, 1));
                        Players[i].MaxHP = BitConverter.ToInt32(ReadMem(Players[i].MaxHPPtr, 4, 1));
                    }
                }
                Thread.Sleep(16);
            }
        }

    }
}
