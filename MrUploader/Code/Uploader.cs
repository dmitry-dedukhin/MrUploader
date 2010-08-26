using System;
using System.Net;
using System.IO;
using System.ComponentModel;
using System.Windows.Threading;
using System.Windows.Browser;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Adler32;

namespace MrUploader
{
	public enum FileUploadStatus
	{
		Pending,
		Uploading,
		Continue,
		Complete,
		Failed,
		Canceled,
		Retry
	}

	public class Constants
	{
		// common uploader constants
		public readonly static int MinChunkSize = 50 * 1024;
		public readonly static int MaxChunkSize = 512 * 1024;
		public readonly static int MaxChunkRetries = 10;
		public readonly static double PercentPrecision = 1.0;
		public readonly static int JsNotifyInterval = 100; // milliseconds
		public readonly static int JsNotifyTryies = 1000;
		public readonly static int RetryTimeoutBase = 5000; // milliseconds
		// upload error codes
		public readonly static int NoError = 0;
		public readonly static int HttpError = 1;
		public readonly static int IOError = 2;
		public readonly static int SequrityError = 3;
		public readonly static int OtherError = 4;
		// constants related to computing file hash
		public readonly static int NumPoints = 50;
		public readonly static int MaxPartSize = 100 * 1024;
		// constants related to IsolatedStorage
		public readonly static int MinFilesizeToAdd = 1 * 1024 * 1024;
		public readonly static int MaxIsoStorageSize = 500 * 1024;
		public readonly static TimeSpan KeepInIsoStorage = new TimeSpan(6, 0, 0); // 6 hours
	}

	public class FileUpload : INotifyPropertyChanged
	{
		public event ProgressChangedEvent UploadProgressChanged;
		public event EventHandler StatusChanged;

		public string SessionId;
		public Uri UploadUrl { get; set; }
		public string AdditionalData { get; set; }
		public string UniqueKey { get; set; }
		public int ErrorCode { get; set; }
		public string ErrorDescr { get; set; }
		// ResponseText contains either comma separated list of already uploaded ranges or server response
		public string ResponseText { get; set; }
		public long ChunkSize;

		private int chunkRetries;
		private Dispatcher Dispatcher;
		private bool cancel = false;
		private DispatcherTimer uploadRetryTimer;
		private long currentChunkStartPos;
		private long currentChunkEndPos;

		private FileInfo file;
		public FileInfo File
		{
			get { return file; }
			set
			{
				file = value;
				FileLength = file.Length;
				UniqueKey = "";
				try
				{
					if (FileLength < Constants.MinFilesizeToAdd)
					{
						throw new Exception();
					}
					// Adler32 version to compute "unique" file hash
					// UniqueKey will be Constants.NumPoints * sizeof(uint) length
					int part_size = (int)((file.Length / Constants.NumPoints) < Constants.MaxPartSize ? file.Length / Constants.NumPoints : Constants.MaxPartSize);
					byte[] buffer = new Byte[part_size];
					byte[] adler_sum = new Byte[Constants.NumPoints * sizeof(uint) / sizeof(byte)];
					int current_point = 0;
					int bytesRead = 0;
					Stream fs = file.OpenRead();
					AdlerChecksum a32 = new AdlerChecksum();
					while (current_point < Constants.NumPoints && (bytesRead = fs.Read(buffer, 0, part_size)) != 0)
					{
						a32.MakeForBuff(buffer, bytesRead);
						int mask = 0xFF;
						for (int i = 0; i < sizeof(uint) / sizeof(byte); i++)
						{
							UniqueKey += (char)((mask << (i * sizeof(byte)) & a32.ChecksumValue) >> (i * sizeof(byte)));
						}
						fs.Position = ++current_point * file.Length / Constants.NumPoints;
					}
				}
				catch (Exception) { }
			}
		}

		public string Name { get { return File.Name; } }

		private void CalcNextChunkRanges()
		{
			BytesUploaded = 0;
			currentChunkStartPos = 0;
			currentChunkEndPos = (currentChunkStartPos + ChunkSize < FileLength ? currentChunkStartPos + ChunkSize : FileLength - 1);
			if (ResponseText != null && ResponseText.Length != 0)
			{
				// We can not check response.StatusCode, see comments in constructor of FileUploadControl
				if (Regex.IsMatch(ResponseText, @"^\d+-\d+/\d+$")) // we got 201 response
				{
					long holeStart = 0, holeEnd = 0;
					// let's find first hole in ranges
					foreach(string str in ResponseText.Split(','))
					{
						string[] r = str.Split('/')[0].Split('-');
						long start = long.Parse(r[0]);
						long end = long.Parse(r[1]);
						BytesUploaded += end - start;
						if (holeEnd != 0)
						{
							continue;
						}
						if (start != 0)
						{
							holeEnd = start - 1;
						}
						else
						{
							holeStart = end + 1;
						}
					}
					currentChunkStartPos = holeStart;
					if (holeEnd == 0)
					{
						holeEnd = FileLength - 1;
					}
					currentChunkEndPos = (holeEnd - holeStart < ChunkSize ? holeEnd : currentChunkStartPos + ChunkSize);
				}
				else // we got 200 response
				{
					BytesUploaded = FileLength;
				}
			}
		}

		private long fileLength;
		public long FileLength
		{
			get { return fileLength; }
			set
			{
				fileLength = value;
				ChunkSize = (long)(fileLength / (100 / Constants.PercentPrecision));
				if (ChunkSize < Constants.MinChunkSize)
					ChunkSize = Constants.MinChunkSize;

				if (ChunkSize > Constants.MaxChunkSize)
					ChunkSize = Constants.MaxChunkSize;
			}
		}

		private string debugText;
		public string DebugText
		{
			get { return debugText; }
			set
			{
				debugText = value;
				this.Dispatcher.BeginInvoke(delegate()
				{
					if (PropertyChanged != null)
						PropertyChanged(this, new PropertyChangedEventArgs("DebugText"));
				});
			}
		}

		private long bytesUploaded;
		public long BytesUploaded
		{
			get { return bytesUploaded; }
			set
			{
				bytesUploaded = value;
			}
		}

		private FileUploadStatus status;
		public FileUploadStatus Status
		{
			get { return status; }
			set
			{
				status = value;
				this.Dispatcher.BeginInvoke(delegate()
				{
					if (StatusChanged != null)
						StatusChanged(this, null);
				});
			}
		}

		public FileUpload(Dispatcher dispatcher)
		{
			Dispatcher = dispatcher;
			Status = FileUploadStatus.Pending;
			SessionId = (1100000000 + new Random().Next(10000000, 99999999)).ToString();
			uploadRetryTimer = new DispatcherTimer();
			uploadRetryTimer.Tick += new EventHandler(UploadFileRetryEx);
		}

		public FileUpload(Dispatcher dispatcher, FileInfo fileToUpload) : this(dispatcher)
		{
			File = fileToUpload;
			StorageFile sf = new UserStorage().GetFileInfo(this);
			if (sf != null && sf.SessionId != null && sf.UploadUrl != null)
			{
				ResponseText = sf.UploadedRanges;
				SessionId = sf.SessionId;
				UploadUrl = new Uri(sf.UploadUrl);
			}
		}

		public void StartUpload()
		{
			if (File == null || UploadUrl == null)
			{
				Status = FileUploadStatus.Failed;
				return;
			}
			chunkRetries = Constants.MaxChunkRetries;
			Status = FileUploadStatus.Uploading;
			UploadFileEx();
		}

		public void CancelUpload()
		{
			cancel = true;
		}

		public void RetryUpload()
		{
			uploadRetryTimer.Interval = new TimeSpan(0, 0, 0, 0, Constants.RetryTimeoutBase * (Constants.MaxChunkRetries - chunkRetries + 1));
			uploadRetryTimer.Start();
		}
		public void UploadFileRetryEx(object o, EventArgs sender)
		{
			uploadRetryTimer.Stop();
			this.UploadFileEx();
		}

		private void setErrorDescription(Exception ex, string where)
		{
			ErrorDescr += where + "{" + ex.Message + " / " + ex.InnerException.Message + " / STACK: " + ex.StackTrace + "}";
		}

		public void UploadFileEx()
		{
			ErrorCode = Constants.NoError;
			ErrorDescr = "";
			try
			{
				if (currentChunkStartPos == 0 && currentChunkEndPos == 0) // first chunk upload
				{
					CalcNextChunkRanges();
				}
				UriBuilder ub = new UriBuilder(UploadUrl);
				HttpWebRequest webrequest = (HttpWebRequest)WebRequest.Create(ub.Uri);
				webrequest.Method = "POST";
				webrequest.ContentType = "application/octet-stream";
				// Some russian letters in filename lead to exception, so we do uri encode on client side
				// and uri decode on server side
				webrequest.Headers["Content-Disposition"] = "attachment; filename=\"" + HttpUtility.UrlEncode(File.Name) + "\"";
				webrequest.Headers["X-Content-Range"] = "bytes " + currentChunkStartPos + "-" + currentChunkEndPos + "/" + FileLength;
				webrequest.Headers["Session-ID"] = SessionId;
				webrequest.BeginGetRequestStream(new AsyncCallback(WriteCallback), webrequest);
			}
			catch (WebException ex)
			{
				ErrorCode = Constants.HttpError;
				setErrorDescription(ex, "Ex1");
			}
			catch (IOException ex)
			{
				ErrorCode = Constants.IOError;
				setErrorDescription(ex, "Ex1");
			}
			catch (Exception ex)
			{
				ErrorCode = Constants.OtherError;
				setErrorDescription(ex, "Ex1");
			}
			if (ErrorCode != Constants.NoError)
			{
				SetFileStatus();
			}
		}

		private void WriteCallback(IAsyncResult asynchronousResult)
		{
			ErrorCode = Constants.NoError;
			try
			{
				HttpWebRequest webrequest = (HttpWebRequest)asynchronousResult.AsyncState;
				Stream requestStream = webrequest.EndGetRequestStream(asynchronousResult);

				int curChunkSize = (int)(currentChunkEndPos - currentChunkStartPos + 1);
				byte[] buffer = new Byte[curChunkSize];
				int bytesRead = 0;

				Stream fileStream = File.OpenRead();

				fileStream.Position = currentChunkStartPos;
				bytesRead = fileStream.Read(buffer, 0, curChunkSize);
				requestStream.Write(buffer, 0, bytesRead);

				requestStream.Flush();
				fileStream.Close();
				requestStream.Close();
				webrequest.BeginGetResponse(new AsyncCallback(ReadCallback), webrequest);
			}
			catch (WebException ex)
			{
				ErrorCode = Constants.HttpError;
				setErrorDescription(ex, "Ex2");
			}
			catch (IOException ex)
			{
				ErrorCode = Constants.IOError;
				setErrorDescription(ex, "Ex2");
			}
			catch (Exception ex)
			{
				ErrorCode = Constants.OtherError;
				setErrorDescription(ex, "Ex2");
			}
			if (ErrorCode != Constants.NoError)
			{
				SetFileStatus();
			}
		}

		private void ReadCallback(IAsyncResult asynchronousResult)
		{
			ErrorCode = Constants.NoError;
			HttpWebRequest webrequest = (HttpWebRequest)asynchronousResult.AsyncState;
			try
			{
				HttpWebResponse response = (HttpWebResponse)webrequest.EndGetResponse(asynchronousResult); // WebException
				if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
				{
					throw new WebException("Not 20x response code");
				}
				if (response == null)
				{
					throw new WebException("Null response");
				}
				StreamReader reader = new StreamReader(response.GetResponseStream());
				ResponseText = reader.ReadToEnd(); // IOException
				reader.Close();
				response.Close();
			}
			catch (WebException ex)
			{
				ErrorCode = Constants.HttpError;
				setErrorDescription(ex, "Ex3");
			}
			catch (IOException ex)
			{
				ErrorCode = Constants.IOError;
				setErrorDescription(ex, "Ex3");
			}
			catch (Exception ex)
			{
				ErrorCode = Constants.OtherError;
				setErrorDescription(ex, "Ex3");
			}
			SetFileStatus();
		}

		private void SetFileStatus()
		{
			if (cancel)
			{
				Status = FileUploadStatus.Canceled;
			}
			else if (ErrorCode != Constants.NoError)
			{
				chunkRetries--;
				if (chunkRetries > 0)
				{
					Status = FileUploadStatus.Retry;
				}
				else
				{
					Status = FileUploadStatus.Failed;
				}
			}
			else
			{
				CalcNextChunkRanges();
				if (UploadProgressChanged != null)
				{
					UploadProgressChangedEventArgs args = new UploadProgressChangedEventArgs(BytesUploaded, FileLength);
					this.Dispatcher.BeginInvoke(delegate()
					{
						UploadProgressChanged(this, args);
					});
				}
				if (BytesUploaded < FileLength)
				{
					new UserStorage().AddOrUpdateFileInfo(this);
					chunkRetries = Constants.MaxChunkRetries;
					Status = FileUploadStatus.Continue;
				}
				else
				{
					new UserStorage().DeleteFileInfo(this);
					Status = FileUploadStatus.Complete;
				}
			}
		}

		#region INotifyPropertyChanged Members
		public event PropertyChangedEventHandler PropertyChanged;
		#endregion

	}
}
