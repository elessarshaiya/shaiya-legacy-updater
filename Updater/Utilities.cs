using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Shaiya_Invasion_Updater
{
  public static class Utilities
  {
    public static void ExecuteOnMainThread(Action A) => Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Delegate) A);

    public static class Hardware
    {
      public static string HWID = "";
      public static string CPU_ID = "";
      public static string CPU_NAME = "";
      public static string MOBO_ID = "";
      public static string MOBO_NAME = "";
      public static int RAM_SIZE = 0;
      public static string GPU_NAME = "";
      public static short GPU_RAM = 0;

      public static void ReadInfo()
      {
        Utilities.Hardware.CPU();
        Utilities.Hardware.MOBO();
        Utilities.Hardware.RAM();
        Utilities.Hardware.GPU();
        byte[] bytes = Encoding.ASCII.GetBytes(Utilities.Hardware.CPU_ID + "\t" + Utilities.Hardware.CPU_NAME + "\r\n" + Utilities.Hardware.MOBO_ID + "\t" + Utilities.Hardware.MOBO_NAME + "\r\n" + (object) Utilities.Hardware.RAM_SIZE + "\r\n" + Utilities.Hardware.GPU_NAME + "\t" + (object) Utilities.Hardware.GPU_RAM);
        Utilities.Hardware.HWID = BitConverter.ToString(new MD5CryptoServiceProvider().ComputeHash(bytes, 0, bytes.Length)).Replace("-", "");
      }

      private static void CPU()
      {
        try
        {
          foreach (ManagementObject managementObject in new ManagementObjectSearcher("SELECT Name, ProcessorId FROM Win32_Processor").Get())
          {
            try
            {
              Utilities.Hardware.CPU_NAME = managementObject["Name"].ToString();
              Utilities.Hardware.CPU_ID = managementObject["ProcessorId"].ToString();
              break;
            }
            catch
            {
            }
          }
        }
        catch
        {
        }
      }

      private static void MOBO()
      {
        try
        {
          foreach (ManagementObject managementObject in new ManagementObjectSearcher("SELECT Manufacturer, SerialNumber, Product FROM Win32_BaseBoard").Get())
          {
            try
            {
              Utilities.Hardware.MOBO_NAME = managementObject["Manufacturer"].ToString() + " " + managementObject["Product"];
              Utilities.Hardware.MOBO_ID = managementObject["SerialNumber"].ToString();
              break;
            }
            catch
            {
            }
          }
        }
        catch
        {
        }
      }

      private static void RAM()
      {
        try
        {
          uint TEMP_RAM = (uint) (new ComputerInfo().TotalPhysicalMemory / 1048576UL);
          List<uint> source = new List<uint>();
          for (uint y = 0; y < 32U; ++y)
            source.Add((uint) Math.Pow(2.0, (double) y));
          Utilities.Hardware.RAM_SIZE = (int) source.OrderBy<uint, long>((Func<uint, long>) (p2 => Math.Abs((long) (p2 - TEMP_RAM)))).First<uint>();
        }
        catch
        {
        }
      }

      private static void GPU()
      {
        try
        {
          foreach (ManagementObject managementObject in new ManagementObjectSearcher("SELECT Description, AdapterRAM FROM Win32_VideoController").Get())
          {
            try
            {
              Utilities.Hardware.GPU_NAME = managementObject["Description"].ToString();
              uint TEMP_RAM = (uint) managementObject["AdapterRAM"] / 1048576U;
              List<uint> source = new List<uint>();
              for (uint y = 0; y < 32U; ++y)
                source.Add((uint) Math.Pow(2.0, (double) y));
              Utilities.Hardware.GPU_RAM = (short) source.OrderBy<uint, long>((Func<uint, long>) (p2 => Math.Abs((long) (p2 - TEMP_RAM)))).First<uint>();
              break;
            }
            catch
            {
            }
          }
        }
        catch
        {
        }
      }
    }
  }
}
