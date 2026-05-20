using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Shaiya_Invasion_Updater
{
  public class SAH
  {
    public string Path;
    public int TotalFileCount;
    public FOLDER Data = new FOLDER()
    {
      FolderName = nameof (Data)
    };
    public bool OK;
    private bool _writePlainSah;

    public SAH(string Path)
    {
      this.Path = Path;
      this.Read();
    }

    private void Read()
    {
      try
      {
        byte[] numArray;
        using (BinaryReader binaryReader = new BinaryReader((Stream) File.OpenRead(this.Path)))
        {
          numArray = binaryReader.ReadBytes((int) binaryReader.BaseStream.Length);
        }
        if (numArray.Length >= 3 && Encoding.ASCII.GetString(numArray, 0, 3) == nameof (SAH))
        {
          this._writePlainSah = true;
        }
        else
        {
          numArray = this.DecryptBuffer(numArray);
          this._writePlainSah = false;
        }
        if (numArray.Length < 3 || Encoding.ASCII.GetString(numArray, 0, 3) != nameof (SAH))
          return;
        this.TotalFileCount = BitConverter.ToInt32(numArray, 7);
        int startIndex1 = 51;
        int int32_1 = BitConverter.ToInt32(numArray, startIndex1);
        int index1 = startIndex1 + 4;
        Encoding.ASCII.GetString(numArray, index1, int32_1).Replace("\0", "");
        int startIndex2 = index1 + int32_1;
        int int32_2 = BitConverter.ToInt32(numArray, startIndex2);
        int startIndex3 = startIndex2 + 4;
        if (int32_2 > 0)
        {
          for (int index2 = 0; index2 < int32_2; ++index2)
          {
            FILE file = new FILE();
            int int32_3 = BitConverter.ToInt32(numArray, startIndex3);
            int index3 = startIndex3 + 4;
            file.FileName = Encoding.ASCII.GetString(numArray, index3, int32_3).Replace("\0", "");
            int startIndex4 = index3 + int32_3;
            file.Start = BitConverter.ToUInt64(numArray, startIndex4);
            int startIndex5 = startIndex4 + 8;
            file.Length = BitConverter.ToUInt32(numArray, startIndex5);
            int startIndex6 = startIndex5 + 4;
            file.Version = BitConverter.ToUInt32(numArray, startIndex6);
            startIndex3 = startIndex6 + 4;
            file.Parent = this.Data;
            this.Data.Files.Add(file);
          }
        }
        int int32_4 = BitConverter.ToInt32(numArray, startIndex3);
        int Offset = startIndex3 + 4;
        if (int32_4 > 0)
        {
          for (int index4 = 0; index4 < int32_4; ++index4)
            this.Data.Folders.Add(this.Read_Folder(this.Data, numArray, ref Offset));
        }
        this.OK = true;
      }
      catch (Exception ex)
      {
        ExceptionManager.Submit(ex);
      }
    }

    private FOLDER Read_Folder(FOLDER Parent, byte[] FileData, ref int Offset)
    {
      FOLDER Parent1 = new FOLDER();
      Parent1.Parent = Parent;
      int int32_1 = BitConverter.ToInt32(FileData, Offset);
      Offset += 4;
      Parent1.FolderName = Encoding.ASCII.GetString(FileData, Offset, int32_1).Replace("\0", "");
      Offset += int32_1;
      int int32_2 = BitConverter.ToInt32(FileData, Offset);
      Offset += 4;
      if (int32_2 > 0)
      {
        for (int index = 0; index < int32_2; ++index)
        {
          FILE file = new FILE();
          int int32_3 = BitConverter.ToInt32(FileData, Offset);
          Offset += 4;
          file.FileName = Encoding.ASCII.GetString(FileData, Offset, int32_3).Replace("\0", "");
          Offset += int32_3;
          file.Start = BitConverter.ToUInt64(FileData, Offset);
          Offset += 8;
          file.Length = BitConverter.ToUInt32(FileData, Offset);
          Offset += 4;
          file.Version = BitConverter.ToUInt32(FileData, Offset);
          Offset += 4;
          file.Parent = Parent;
          Parent1.Files.Add(file);
        }
      }
      int int32_4 = BitConverter.ToInt32(FileData, Offset);
      Offset += 4;
      if (int32_4 > 0)
      {
        for (int index = 0; index < int32_4; ++index)
          Parent1.Folders.Add(this.Read_Folder(Parent1, FileData, ref Offset));
      }
      return Parent1;
    }

    public void Write_SAH()
    {
      try
      {
        if (File.Exists(this.Path))
          File.Delete(this.Path);
        List<byte> Buffer = new List<byte>();
        this.TotalFileCount = this.CountFiles(this.Data);
        Buffer.AddRange((IEnumerable<byte>) Encoding.ASCII.GetBytes(nameof (SAH)));
        Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(0));
        Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(this.TotalFileCount));
        Buffer.AddRange((IEnumerable<byte>) new byte[40]);
        Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(1));
        Buffer.Add((byte) 0);
        Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(this.Data.Files.Count));
        if (this.Data.Files.Count > 0)
        {
          foreach (FILE file in this.Data.Files)
            this.Write_File(file, ref Buffer);
        }
        Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(this.Data.Folders.Count));
        if (this.Data.Folders.Count > 0)
        {
          foreach (FOLDER folder in this.Data.Folders)
            this.Write_Folder(folder, ref Buffer);
        }
        Buffer.AddRange((IEnumerable<byte>) new byte[8]);
        byte[] rawBuffer = Buffer.ToArray();
        byte[] buffer = this._writePlainSah ? rawBuffer : this.EncryptBuffer(rawBuffer);
        using (BinaryWriter binaryWriter = new BinaryWriter((Stream) File.Open(this.Path, FileMode.Create, FileAccess.Write, FileShare.None)))
          binaryWriter.Write(buffer);
      }
      catch (Exception ex)
      {
        ExceptionManager.Submit(ex);
      }
    }

    private void Write_File(FILE file, ref List<byte> Buffer)
    {
      Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(file.FileName.Length + 1));
      Buffer.AddRange((IEnumerable<byte>) Encoding.ASCII.GetBytes(file.FileName + (object) char.MinValue));
      Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(file.Start));
      Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(file.Length));
      Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(file.Version));
    }

    private void Write_Folder(FOLDER folder, ref List<byte> Buffer)
    {
      Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(folder.FolderName.Length + 1));
      Buffer.AddRange((IEnumerable<byte>) Encoding.ASCII.GetBytes(folder.FolderName + (object) char.MinValue));
      Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(folder.Files.Count));
      if (folder.Files.Count > 0)
      {
        foreach (FILE file in folder.Files)
          this.Write_File(file, ref Buffer);
      }
      Buffer.AddRange((IEnumerable<byte>) BitConverter.GetBytes(folder.Folders.Count));
      if (folder.Folders.Count <= 0)
        return;
      foreach (FOLDER folder1 in folder.Folders)
        this.Write_Folder(folder1, ref Buffer);
    }

    public void MergeWithPatch(SAH Patch)
    {
      try
      {
        using (BinaryReader PatchReader = new BinaryReader((Stream) File.Open(Patch.Path.Replace("sah", "saf"), FileMode.Open, FileAccess.Read, FileShare.Read)))
        {
          using (BinaryWriter ClientWriter = new BinaryWriter((Stream) File.Open(this.Path.Replace("sah", "saf"), FileMode.Open, FileAccess.Write, FileShare.None)))
          {
            FOLDER data = this.Data;
            if (Patch.Data.Files.Count > 0)
            {
              foreach (FILE file1 in Patch.Data.Files)
              {
                FILE f = file1;
                if (data.ContainsFile(f.FileName))
                  this.UpdateFile(data.Files.First<FILE>((Func<FILE, bool>) (file => file.FileName.ToLower() == f.FileName.ToLower())), f, PatchReader, ClientWriter);
                else
                  this.InsertFile(f, data, PatchReader, ClientWriter);
              }
            }
            if (Patch.Data.Folders.Count > 0)
            {
              foreach (FOLDER folder in Patch.Data.Folders)
              {
                if (!data.ContainsFolder(folder.FolderName))
                  data.Folders.Add(new FOLDER()
                  {
                    FolderName = folder.FolderName,
                    Parent = data
                  });
                this.MergeFolder(folder, data, PatchReader, ClientWriter);
              }
            }
          }
        }
        this.Write_SAH();
      }
      catch (Exception ex)
      {
        ExceptionManager.Submit(ex);
        throw;
      }
    }

    private void InsertFile(
      FILE f,
      FOLDER CurrentFolder,
      BinaryReader PatchReader,
      BinaryWriter ClientWriter)
    {
      FILE file = new FILE()
      {
        FileName = f.FileName,
        Start = (ulong) ClientWriter.BaseStream.Length,
        Length = f.Length,
        Version = 0,
        Parent = CurrentFolder
      };
      PatchReader.BaseStream.Position = (long) f.Start;
      byte[] buffer = PatchReader.ReadBytes((int) f.Length);
      ClientWriter.BaseStream.Position = ClientWriter.BaseStream.Length;
      ClientWriter.Write(buffer);
      CurrentFolder.Files.Add(file);
    }

    private void UpdateFile(
      FILE f,
      FILE new_f,
      BinaryReader PatchReader,
      BinaryWriter ClientWriter)
    {
      PatchReader.BaseStream.Position = (long) new_f.Start;
      byte[] buffer = PatchReader.ReadBytes((int) new_f.Length);
      ClientWriter.BaseStream.Position = f.Length < new_f.Length ? ClientWriter.BaseStream.Length : (long) f.Start;
      f.Start = (ulong) ClientWriter.BaseStream.Position;
      f.Length = new_f.Length;
      ++f.Version;
      ClientWriter.Write(buffer);
    }

    private void MergeFolder(
      FOLDER f,
      FOLDER CurrentFolder,
      BinaryReader PatchReader,
      BinaryWriter ClientWriter)
    {
      CurrentFolder = CurrentFolder.Folders.First<FOLDER>((Func<FOLDER, bool>) (folder => folder.FolderName.ToLower() == f.FolderName.ToLower()));
      if (f.Files.Count > 0)
      {
        foreach (FILE file1 in f.Files)
        {
          FILE file = file1;
          if (CurrentFolder.ContainsFile(file.FileName))
            this.UpdateFile(CurrentFolder.Files.First<FILE>((Func<FILE, bool>) (fl => fl.FileName.ToLower() == file.FileName.ToLower())), file, PatchReader, ClientWriter);
          else
            this.InsertFile(file, CurrentFolder, PatchReader, ClientWriter);
        }
      }
      if (f.Folders.Count > 0)
      {
        foreach (FOLDER folder in f.Folders)
        {
          if (!CurrentFolder.ContainsFolder(folder.FolderName))
            CurrentFolder.Folders.Add(new FOLDER()
            {
              FolderName = folder.FolderName,
              Parent = CurrentFolder
            });
          this.MergeFolder(folder, CurrentFolder, PatchReader, ClientWriter);
        }
      }
      CurrentFolder = CurrentFolder.Parent;
    }

    private int CountFiles(FOLDER folder)
    {
      int count = folder.Files.Count;
      foreach (FOLDER child in folder.Folders)
        count += this.CountFiles(child);
      return count;
    }

    private byte[] EncryptBuffer(byte[] Buffer)
    {
      for (int index = 0; index < Buffer.Length; ++index)
      {
        byte num = (byte) ((uint) this.ROL((byte) ((uint) this.ROL(Buffer[index], (byte) 3) ^ (uint) sbyte.MaxValue), (byte) 1) ^ (uint) (byte) index);
        Buffer[index] = num;
      }
      return Buffer;
    }

    private byte[] DecryptBuffer(byte[] Buffer)
    {
      for (int index = 0; index < Buffer.Length; ++index)
      {
        byte num = this.ROR((byte) ((uint) this.ROR((byte) ((uint) Buffer[index] ^ (uint) (byte) index), (byte) 1) ^ (uint) sbyte.MaxValue), (byte) 3);
        Buffer[index] = num;
      }
      return Buffer;
    }

    private byte ROL(byte value, byte count) => (byte) ((int) value << (int) count | (int) value >> 8 - (int) count);

    private byte ROR(byte value, byte count) => (byte) ((int) value >> (int) count | (int) value << 8 - (int) count);
  }
}
