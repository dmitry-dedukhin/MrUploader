using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace MrUploader
{
	public delegate void ProgressChangedEvent(object sender, UploadProgressChangedEventArgs args);
	public class UploadProgressChangedEventArgs
	{
		public long TotalBytesUploaded { get; set; }
		public long TotalBytes { get; set; }

		public UploadProgressChangedEventArgs() { }

		public UploadProgressChangedEventArgs(long totalBytesUploaded, long totalBytes)
		{
			TotalBytes = totalBytes;
			TotalBytesUploaded = totalBytesUploaded;
		}
	}
}
