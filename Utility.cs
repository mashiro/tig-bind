using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;

namespace Spica.Applications.TwitterIrcGateway.AddIns.OtherSource
{
	public static class OtherSourceUtility
	{
		private static readonly String _sharedKey = "Spica.Applications.TwitterIrcGateway.AddIns.OtherSource";
		private static readonly Regex _regexLineBreak = new Regex(@"\r\n|\r|\n");

		#region Crypt
		public static String Encrypt(String s)
		{
			if (!String.IsNullOrEmpty(s))
			{
				byte[] src = Encoding.UTF8.GetBytes(s);
				byte[] key = Encoding.UTF8.GetBytes(_sharedKey);
				s = Convert.ToBase64String(Encrypt(Aes.Create(), src, key));
			}

			return s;
		}

		public static String Decrypt(String s)
		{
			if (!String.IsNullOrEmpty(s))
			{
				byte[] src = Convert.FromBase64String(s);
				byte[] key = Encoding.UTF8.GetBytes(_sharedKey);
				s = Encoding.UTF8.GetString(Decrypt(Aes.Create(), src, key));
			}

			return s;
		}

		public static byte[] Encrypt(SymmetricAlgorithm algo, byte[] src, byte[] key)
		{
			algo.Key = GenerateKey(key, algo.Key.Length);
			algo.IV = GenerateKey(key, algo.IV.Length);
			return Encrypt(algo, src);
		}

		public static byte[] Encrypt(SymmetricAlgorithm algorithm, byte[] src)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (CryptoStream cs = new CryptoStream(ms, algorithm.CreateEncryptor(), CryptoStreamMode.Write))
				{
					cs.Write(src, 0, src.Length);
					cs.FlushFinalBlock();
					return ms.ToArray();
				}
			}
		}

		public static byte[] Decrypt(SymmetricAlgorithm algo, byte[] src, byte[] key)
		{
			algo.Key = GenerateKey(key, algo.Key.Length);
			algo.IV = GenerateKey(key, algo.IV.Length);
			return Decrypt(algo, src);
		}

		public static byte[] Decrypt(SymmetricAlgorithm algorithm, byte[] src)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (CryptoStream cs = new CryptoStream(ms, algorithm.CreateDecryptor(), CryptoStreamMode.Write))
				{
					cs.Write(src, 0, src.Length);
					cs.FlushFinalBlock();
					return ms.ToArray();
				}
			}
		}

		private static byte[] GenerateKey(byte[] src, int size)
		{
			byte[] dest = new byte[size];
			if (src.Length <= dest.Length)
			{
				for (int i = 0; i < src.Length; ++i)
					dest[i] = src[i];
			}
			else
			{
				int n = 0;
				for (int i = 0; i < src.Length; ++i)
				{
					dest[n++] ^= src[i];
					if (n >= dest.Length) n = 0;
				}
			}

			return dest;
		}
		#endregion

		/// <summary>
		/// 改行を削除します。
		/// </summary>
		public static String RemoveLineBreak(String input)
		{
			return _regexLineBreak.Replace(input, String.Empty);
		}

		/// <summary>
		/// 改行を置換します。
		/// </summary>
		public static String ReplaceLineBreak(String input, String replacement)
		{
			return _regexLineBreak.Replace(input, replacement);
		}

		/// <summary>
		/// 改行で分割します。
		/// </summary>
		public static String[] SplitLineBreak(String input)
		{
			return _regexLineBreak.Split(input);
		}
	}
}
