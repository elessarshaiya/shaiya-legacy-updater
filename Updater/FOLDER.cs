using System.Collections.Generic;

namespace Shaiya_Invasion_Updater
{
  public class FOLDER
  {
    public string FolderName;
    public List<FILE> Files = new List<FILE>();
    public List<FOLDER> Folders = new List<FOLDER>();
    public FOLDER Parent;

    public bool ContainsFile(string FileName)
    {
      foreach (FILE file in this.Files)
      {
        if (file.FileName.ToLower() == FileName.ToLower())
          return true;
      }
      return false;
    }

    public bool ContainsFolder(string FolderName)
    {
      foreach (FOLDER folder in this.Folders)
      {
        if (folder.FolderName.ToLower() == FolderName.ToLower())
          return true;
      }
      return false;
    }
  }
}
