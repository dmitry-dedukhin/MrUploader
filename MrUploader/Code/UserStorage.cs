using System;
using System.Linq;
using System.IO.IsolatedStorage;
using System.Collections.Generic;

namespace MrUploader
{
	public class StorageFile
	{
		public string Key { get; set; }
		public string SessionId { get; set; }
		public string UploadUrl { get; set; }
		public string UploadedRanges { get; set; }
		public DateTime CreatedOn { get; set; }
	}
	public class UserStorage
	{
		private IsolatedStorageSettings settings;
		public UserStorage()
		{
			try
			{
				settings = IsolatedStorageSettings.ApplicationSettings;
				Cleanup();
			}
			catch (Exception) { }
		}
		public void Cleanup()
		{
			IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication();
			// remove old items
			foreach(KeyValuePair<string,object> kv in settings.Where(f => ((StorageFile)f.Value).CreatedOn < DateTime.Now.Subtract(Constants.KeepInIsoStorage)))
			{
				settings.Remove(kv.Key);
			}
			// if we use too much space, remove the most earlier items
			if (isf.Quota - isf.AvailableFreeSpace > Constants.MaxIsoStorageSize)
			{
				foreach (KeyValuePair<string, object> kv in settings.OrderBy(f => ((StorageFile)f.Value).CreatedOn))
				{
					settings.Remove(kv.Key);
				}
			}
		}
		public StorageFile GetFileInfo(FileUpload fu)
		{
			StorageFile sf = null;
			if (fu.UniqueKey != "")
			{
				try
				{
					sf = (StorageFile)settings.First(f => f.Key == fu.UniqueKey).Value;
				}
				catch (Exception) { }
			}
			return sf;
		}
		public void AddOrUpdateFileInfo(FileUpload fu)
		{
			if (fu.UniqueKey == "")
			{
				return;
			}
			StorageFile sf = null;
			try
			{
				sf = (StorageFile)settings.First(f => f.Key == fu.UniqueKey).Value;
			}
			catch (Exception) { }
			if (sf != null)
			{
				sf.UploadedRanges = fu.ResponseText;
			}
			else
			{
				sf = new StorageFile();
				sf.Key = fu.UniqueKey;
				sf.SessionId = fu.SessionId;
				sf.UploadUrl = fu.UploadUrl.ToString();
				sf.UploadedRanges = fu.ResponseText;
				sf.CreatedOn = DateTime.Now;
				try
				{
					settings.Add(sf.Key, sf);
				}
				catch (Exception) { }
			}
			Flush();
		}
		public void DeleteFileInfo(FileUpload fu)
		{
			if (fu.UniqueKey != "")
			{
				try
				{
					settings.Remove(fu.UniqueKey);
				}
				catch (Exception) { }
				Flush();
			}
		}
		public void Flush()
		{
			try
			{
				settings.Save();
			}
			catch (Exception) { }
		}
	}
}
