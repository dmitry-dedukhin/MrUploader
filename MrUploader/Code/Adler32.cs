using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

/// <summary>
/// File Manipulation Helper.
/// Written by Youry Jukov (yjukov@hotmail.com)
/// </summary>
namespace Adler32
{
	/// <summary>
	///  Adler 32 check sum calculation
	///  (From en.wikipedia.org)
	///
	///  Adler-32 is a checksum algorithm which was invented by Mark Adler.
	///  It is almost as reliable as a 32-bit cyclic redundancy check for 
	///  protecting against accidental modification of data, such as distortions 
	///  occurring during a transmission.
	///  An Adler-32 checksum is obtained by calculating two 16-bit checksums A and B and 
	///  concatenating their bits into a 32-bit integer. A is the sum of all bytes in the 
	///  string, B is the sum of the individual values of A from each step.
	///  At the beginning of an Adler-32 run, A is initialized to 1, B to 0.
	///  The sums are done modulo 65521 (the largest prime number smaller than 216). 
	///  The bytes are stored in network order (big endian), B occupying 
	///  the two most significant bytes.
	///  The function may be expressed as
	///
	///  A = 1 + D1 + D2 + ... + DN (mod 65521)
	///  B = (1 + D1) + (1 + D1 + D2) + ... + (1 + D1 + D2 + ... + DN) (mod 65521)
	///	= N×D1 + (N-1)×D2 + (N-2)×D3 + ... + DN + N (mod 65521)
	///  
	///  Adler-32(D) = B * 65536 + A
	///
	///  where D is the string of bytes for which the checksum is to be calculated,
	///  and N is the length of D.
	/// </summary>
	public class AdlerChecksum
	{
		// parameters
		#region
		/// <summary>
		/// AdlerBase is Adler-32 checksum algorithm parameter.
		/// </summary>
		public const uint AdlerBase  = 0xFFF1;
		/// <summary>
		/// AdlerStart is Adler-32 checksum algorithm parameter.
		/// </summary>
		public const uint AdlerStart = 0x0001;
		/// <summary>
		/// AdlerBuff is Adler-32 checksum algorithm parameter.
		/// </summary>
		//public const uint AdlerBuff = 0x0400;
		public const uint AdlerBuff = 1 * 1024 * 1024;
		/// Adler-32 checksum value
		private uint m_unChecksumValue = 0;
		#endregion
		/// <value>
		/// ChecksumValue is property which enables the user
		/// to get Adler-32 checksum value for the last calculation 
		/// </value>
		public uint ChecksumValue
		{
			get
			{
				return m_unChecksumValue;
			}
		}
		/// <summary>
		/// Calculate Adler-32 checksum for buffer
		/// </summary>
		/// <param name="bytesBuff">Bites array for checksum calculation</param>
		/// <param name="unAdlerCheckSum">Checksum start value (default=1)</param>
		/// <returns>Returns true if the checksum values is successflly calculated</returns>
		private bool MakeForBuff(byte[] bytesBuff, uint unAdlerCheckSum, int length)
		{
			if (Object.Equals(bytesBuff, null))
			{
				m_unChecksumValue = 0;
				return false;
			}
			int nSize = bytesBuff.GetLength(0);
			nSize = nSize > length ? length : nSize;
			if (nSize == 0)
			{
				m_unChecksumValue = 0;
				return false;
			}
			uint unSum1 = unAdlerCheckSum & 0xFFFF;
			uint unSum2 = (unAdlerCheckSum >> 16) & 0xFFFF;
			for (int i = 0; i < nSize; i++)
			{
				unSum1 = (unSum1 + bytesBuff[i]) % AdlerBase;
				unSum2 = (unSum1 + unSum2) % AdlerBase;
			}
			m_unChecksumValue = (unSum2 << 16) + unSum1;
			return true;
		}
		/// <summary>
		/// Calculate Adler-32 checksum for buffer
		/// </summary>
		/// <param name="bytesBuff">Bites array for checksum calculation</param>
		/// <returns>Returns true if the checksum values is successflly calculated</returns>
		public bool MakeForBuff(byte[] bytesBuff, int length)
		{
			return MakeForBuff(bytesBuff, AdlerStart, length);
		}
		/// <summary>
		/// Calculate Adler-32 checksum for file
		/// </summary>
		/// <param name="sPath">Path to file for checksum calculation</param>
		/// <returns>Returns true if the checksum values is successflly calculated</returns>
		public bool MakeForFile(FileStream fs)
		{
			try
			{
				if (Object.Equals(fs, null))
				{
					m_unChecksumValue = 0;
					return false;
				}
				if (fs.Length == 0)
				{
					m_unChecksumValue = 0;
					return false;
				}
				m_unChecksumValue = AdlerStart;
				byte[] bytesBuff = new byte[AdlerBuff];
				int bytesRead = 0;
				while ((bytesRead = fs.Read(bytesBuff, 0, (int)AdlerBuff)) != 0)
				{
					if (!MakeForBuff(bytesBuff, m_unChecksumValue, bytesRead))
					{
						m_unChecksumValue = 0;
						return false;
					}
				}
			}
			catch
			{
				m_unChecksumValue = 0;
				return false;
			}
			return true;
		}
		/// <summary>
		/// Equals determines whether two files (buffers) 
		/// have the same checksum value (identical).
		/// </summary>
		/// <param name="obj">A AdlerChecksum object for comparison</param>
		/// <returns>Returns true if the value of checksum is the same
		/// as this instance; otherwise, false
		/// </returns>
		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;
			if (this.GetType() != obj.GetType())
				return false;
			AdlerChecksum other = (AdlerChecksum)obj;
			return (this.ChecksumValue == other.ChecksumValue);
		}
		/// <summary>
		/// operator== determines whether AdlerChecksum objects are equal.
		/// </summary>
		/// <param name="objA">A AdlerChecksum object for comparison</param>
		/// <param name="objB">A AdlerChecksum object for comparison</param>
		/// <returns>Returns true if the values of its operands are equal</returns>
		public static bool operator ==(AdlerChecksum objA, AdlerChecksum objB)
		{
			if (Object.Equals(objA, null) && Object.Equals(objB, null)) return true;
			if (Object.Equals(objA, null) || Object.Equals(objB, null)) return false;
			return objA.Equals(objB);
		}
		/// <summary>
		/// operator!= determines whether AdlerChecksum objects are not equal.
		/// </summary>
		/// <param name="objA">A AdlerChecksum object for comparison</param>
		/// <param name="objB">A AdlerChecksum object for comparison</param>
		/// <returns>Returns true if the values of its operands are not equal</returns>
		public static bool operator !=(AdlerChecksum objA, AdlerChecksum objB)
		{
			return !(objA == objB);
		}
		/// <summary>
		/// GetHashCode returns hash code for this instance.
		/// </summary>
		/// <returns>hash code of AdlerChecksum</returns>
		public override int GetHashCode()
		{
			return ChecksumValue.GetHashCode();
		}
		/// <summary>
		/// ToString is a method for current AdlerChecksum object
		/// representation in textual form.
		/// </summary>
		/// <returns>Returns current checksum or
		/// or "Unknown" if checksum value is unavailable 
		/// </returns>
		public override string ToString()
		{
			if (ChecksumValue != 0)
				return ChecksumValue.ToString();
			return "";
		}
	}
}
