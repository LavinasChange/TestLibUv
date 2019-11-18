/***************************************************************************
 *                                Utility.cs
 *                            -------------------
 *   begin                : May 1, 2002
 *   copyright            : (C) The RunUO Software Team
 *   email                : info@runuo.com
 *
 *   $Id$
 *
 ***************************************************************************/

/***************************************************************************
 *
 *   This program is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU General Public License as published by
 *   the Free Software Foundation; either version 2 of the License, or
 *   (at your option) any later version.
 *
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace Tela
{
  public static class Utility
  {
    private static Encoding m_UTF8, m_UTF8WithEncoding;

    private static Dictionary<IPAddress, IPAddress> _ipAddressTable;

    private static Stack<ConsoleColor> m_ConsoleColors = new Stack<ConsoleColor>();

    public static Encoding UTF8 => m_UTF8 ?? (m_UTF8 = new UTF8Encoding(false, false));
    public static Encoding UTF8WithEncoding => m_UTF8WithEncoding ?? (m_UTF8WithEncoding = new UTF8Encoding(true, false));

    public static void Separate(StringBuilder sb, string value, string separator)
    {
      if (sb.Length > 0)
        sb.Append(separator);

      sb.Append(value);
    }

    public static string Intern(string str) => str == null ? null : str.Length == 0 ? string.Empty : string.Intern(str);

    public static void Intern(ref string str)
    {
      str = Intern(str);
    }

    public static IPAddress Intern(IPAddress ipAddress)
    {
      if (_ipAddressTable == null)
        _ipAddressTable = new Dictionary<IPAddress, IPAddress>();

      if (!_ipAddressTable.TryGetValue(ipAddress, out IPAddress interned))
      {
        interned = ipAddress;
        _ipAddressTable[ipAddress] = interned;
      }

      return interned;
    }

    public static void Intern(ref IPAddress ipAddress)
    {
      ipAddress = Intern(ipAddress);
    }

    public static bool IsValidIP(string text)
    {
      IPMatch(text, IPAddress.None, out bool valid);

      return valid;
    }

    public static bool IPMatch(string val, IPAddress ip) => IPMatch(val, ip, out _);

    public static string FixHtml(string str)
    {
      if (str == null)
        return "";

      bool hasOpen = str.IndexOf('<') >= 0;
      bool hasClose = str.IndexOf('>') >= 0;
      bool hasPound = str.IndexOf('#') >= 0;

      if (!hasOpen && !hasClose && !hasPound)
        return str;

      StringBuilder sb = new StringBuilder(str);

      if (hasOpen)
        sb.Replace('<', '(');

      if (hasClose)
        sb.Replace('>', ')');

      if (hasPound)
        sb.Replace('#', '-');

      return sb.ToString();
    }

    public static bool IPMatchCIDR(string cidr, IPAddress ip)
    {
      if (ip == null || ip.AddressFamily == AddressFamily.InterNetworkV6)
        return false; //Just worry about IPv4 for now


      /*
      string[] str = cidr.Split( '/' );

      if ( str.Length != 2 )
        return false;

      /* **************************************************
      IPAddress cidrPrefix;

      if ( !IPAddress.TryParse( str[0], out cidrPrefix ) )
        return false;
       * */

      /*
      string[] dotSplit = str[0].Split( '.' );

      if ( dotSplit.Length != 4 )		//At this point and time, and for speed sake, we'll only worry about IPv4
        return false;

      byte[] bytes = new byte[4];

      for ( int i = 0; i < 4; i++ )
      {
        byte.TryParse( dotSplit[i], out bytes[i] );
      }

      uint cidrPrefix = OrderedAddressValue( bytes );

      int cidrLength = Utility.ToInt32( str[1] );
      //The below solution is the fastest solution of the three

      */

      byte[] bytes = new byte[4];
      string[] split = cidr.Split('.');
      bool cidrBits = false;
      int cidrLength = 0;

      for (int i = 0; i < 4; i++)
      {
        int part = 0;

        int partBase = 10;

        string pattern = split[i];

        for (int j = 0; j < pattern.Length; j++)
        {
          char c = pattern[j];


          if (c == 'x' || c == 'X')
          {
            partBase = 16;
          }
          else if (c >= '0' && c <= '9')
          {
            int offset = c - '0';

            if (cidrBits)
            {
              cidrLength *= partBase;
              cidrLength += offset;
            }
            else
            {
              part *= partBase;
              part += offset;
            }
          }
          else if (c >= 'a' && c <= 'f')
          {
            int offset = 10 + (c - 'a');

            if (cidrBits)
            {
              cidrLength *= partBase;
              cidrLength += offset;
            }
            else
            {
              part *= partBase;
              part += offset;
            }
          }
          else if (c >= 'A' && c <= 'F')
          {
            int offset = 10 + (c - 'A');

            if (cidrBits)
            {
              cidrLength *= partBase;
              cidrLength += offset;
            }
            else
            {
              part *= partBase;
              part += offset;
            }
          }
          else if (c == '/')
          {
            if (cidrBits || i != 3) //If there's two '/' or the '/' isn't in the last byte
              return false;

            partBase = 10;
            cidrBits = true;
          }
          else
          {
            return false;
          }
        }

        bytes[i] = (byte)part;
      }

      uint cidrPrefix = OrderedAddressValue(bytes);

      return IPMatchCIDR(cidrPrefix, ip, cidrLength);
    }

    public static bool IPMatchCIDR(IPAddress cidrPrefix, IPAddress ip, int cidrLength)
    {
      if (cidrPrefix == null || ip == null || cidrPrefix.AddressFamily == AddressFamily.InterNetworkV6
      ) //Ignore IPv6 for now
        return false;

      uint cidrValue = SwapUnsignedInt((uint)GetLongAddressValue(cidrPrefix));
      uint ipValue = SwapUnsignedInt((uint)GetLongAddressValue(ip));

      return IPMatchCIDR(cidrValue, ipValue, cidrLength);
    }

    public static bool IPMatchCIDR(uint cidrPrefixValue, IPAddress ip, int cidrLength)
    {
      if (ip == null || ip.AddressFamily == AddressFamily.InterNetworkV6)
        return false;

      uint ipValue = SwapUnsignedInt((uint)GetLongAddressValue(ip));

      return IPMatchCIDR(cidrPrefixValue, ipValue, cidrLength);
    }

    public static bool IPMatchCIDR(uint cidrPrefixValue, uint ipValue, int cidrLength)
    {
      if (cidrLength <= 0 || cidrLength >= 32) //if invalid cidr Length, just compare IPs
        return cidrPrefixValue == ipValue;

      uint mask = uint.MaxValue << 32 - cidrLength;

      return (cidrPrefixValue & mask) == (ipValue & mask);
    }

    private static uint OrderedAddressValue(byte[] bytes)
    {
      if (bytes.Length != 4)
        return 0;

      return (uint)(bytes[0] << 0x18 | bytes[1] << 0x10 | bytes[2] << 8 | bytes[3]) & 0xffffffff;
    }

    private static uint SwapUnsignedInt(uint source) =>
      (source & 0x000000FF) << 0x18
      | (source & 0x0000FF00) << 8
      | (source & 0x00FF0000) >> 8
      | (source & 0xFF000000) >> 0x18;

    public static bool TryConvertIPv6toIPv4(ref IPAddress address)
    {
      if (!Socket.OSSupportsIPv6 || address.AddressFamily == AddressFamily.InterNetwork)
        return true;

      byte[] addr = address.GetAddressBytes();
      if (addr.Length == 16) //sanity 0 - 15 //10 11 //12 13 14 15
      {
        if (addr[10] != 0xFF || addr[11] != 0xFF)
          return false;

        for (int i = 0; i < 10; i++)
          if (addr[i] != 0)
            return false;

        byte[] v4Addr = new byte[4];

        for (int i = 0; i < 4; i++) v4Addr[i] = addr[12 + i];

        address = new IPAddress(v4Addr);
        return true;
      }

      return false;
    }

    public static bool IPMatch(string val, IPAddress ip, out bool valid)
    {
      valid = true;

      string[] split = val.Split('.');

      for (int i = 0; i < 4; ++i)
      {
        int lowPart, highPart;

        if (i >= split.Length)
        {
          lowPart = 0;
          highPart = 255;
        }
        else
        {
          string pattern = split[i];

          if (pattern == "*")
          {
            lowPart = 0;
            highPart = 255;
          }
          else
          {
            lowPart = 0;
            highPart = 0;

            bool highOnly = false;
            int lowBase = 10;
            int highBase = 10;

            for (int j = 0; j < pattern.Length; ++j)
            {
              char c = pattern[j];

              if (c == '?')
              {
                if (!highOnly)
                {
                  lowPart *= lowBase;
                  lowPart += 0;
                }

                highPart *= highBase;
                highPart += highBase - 1;
              }
              else if (c == '-')
              {
                highOnly = true;
                highPart = 0;
              }
              else if (c == 'x' || c == 'X')
              {
                lowBase = 16;
                highBase = 16;
              }
              else if (c >= '0' && c <= '9')
              {
                int offset = c - '0';

                if (!highOnly)
                {
                  lowPart *= lowBase;
                  lowPart += offset;
                }

                highPart *= highBase;
                highPart += offset;
              }
              else if (c >= 'a' && c <= 'f')
              {
                int offset = 10 + (c - 'a');

                if (!highOnly)
                {
                  lowPart *= lowBase;
                  lowPart += offset;
                }

                highPart *= highBase;
                highPart += offset;
              }
              else if (c >= 'A' && c <= 'F')
              {
                int offset = 10 + (c - 'A');

                if (!highOnly)
                {
                  lowPart *= lowBase;
                  lowPart += offset;
                }

                highPart *= highBase;
                highPart += offset;
              }
              else
              {
                valid = false; //high & lowp art would be 0 if it got to here.
              }
            }
          }
        }

        int b = (byte)(GetAddressValue(ip) >> i * 8);

        if (b < lowPart || b > highPart)
          return false;
      }

      return true;
    }

    public static bool IPMatchClassC(IPAddress ip1, IPAddress ip2) => (GetAddressValue(ip1) & 0xFFFFFF) == (GetAddressValue(ip2) & 0xFFFFFF);

    /* Should probably be rewritten to use an ITile interface

    public static bool CanMobileFit( int z, StaticTile[] tiles )
    {
      int checkHeight = 15;
      int checkZ = z;

      for ( int i = 0; i < tiles.Length; ++i )
      {
        StaticTile tile = tiles[i];

        if ( ((checkZ + checkHeight) > tile.Z && checkZ < (tile.Z + tile.Height))*/
    /* || (tile.Z < (checkZ + checkHeight) && (tile.Z + tile.Height) > checkZ)*/ /* )
				{
					return false;
				}
				else if ( checkHeight == 0 && tile.Height == 0 && checkZ == tile.Z )
				{
					return false;
				}
			}

			return true;
		}

		public static bool IsInContact( StaticTile check, StaticTile[] tiles )
		{
			int checkHeight = check.Height;
			int checkZ = check.Z;

			for ( int i = 0; i < tiles.Length; ++i )
			{
				StaticTile tile = tiles[i];

				if ( ((checkZ + checkHeight) > tile.Z && checkZ < (tile.Z + tile.Height))*/
    /* || (tile.Z < (checkZ + checkHeight) && (tile.Z + tile.Height) > checkZ)*/ /* )
				{
					return true;
				}
				else if ( checkHeight == 0 && tile.Height == 0 && checkZ == tile.Z )
				{
					return true;
				}
			}

			return false;
		}
		*/

    public static object GetArrayCap(Array array, int index, object emptyValue = null)
    {
      if (array.Length > 0)
      {
        if (index < 0)
          index = 0;
        else if (index >= array.Length) index = array.Length - 1;

        return array.GetValue(index);
      }

      return emptyValue;
    }

    public static void FormatBuffer(TextWriter output, Stream input, int length)
    {
      output.WriteLine("        0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F");
      output.WriteLine("       -- -- -- -- -- -- -- --  -- -- -- -- -- -- -- --");

      int byteIndex = 0;

      int whole = length >> 4;
      int rem = length & 0xF;

      for (int i = 0; i < whole; ++i, byteIndex += 16)
      {
        StringBuilder bytes = new StringBuilder(49);
        StringBuilder chars = new StringBuilder(16);

        for (int j = 0; j < 16; ++j)
        {
          int c = input.ReadByte();

          bytes.Append(c.ToString("X2"));

          if (j != 7)
            bytes.Append(' ');
          else
            bytes.Append("  ");

          if (c >= 0x20 && c < 0x7F)
            chars.Append((char)c);
          else
            chars.Append('.');
        }

        output.Write(byteIndex.ToString("X4"));
        output.Write("   ");
        output.Write(bytes.ToString());
        output.Write("  ");
        output.WriteLine(chars.ToString());
      }

      if (rem != 0)
      {
        StringBuilder bytes = new StringBuilder(49);
        StringBuilder chars = new StringBuilder(rem);

        for (int j = 0; j < 16; ++j)
          if (j < rem)
          {
            int c = input.ReadByte();

            bytes.Append(c.ToString("X2"));

            if (j != 7)
              bytes.Append(' ');
            else
              bytes.Append("  ");

            if (c >= 0x20 && c < 0x7F)
              chars.Append((char)c);
            else
              chars.Append('.');
          }
          else
          {
            bytes.Append("   ");
          }

        output.Write(byteIndex.ToString("X4"));
        output.Write("   ");
        output.Write(bytes.ToString());
        output.Write("  ");
        output.WriteLine(chars.ToString());
      }
    }

    public static void PushColor(ConsoleColor color)
    {
      try
      {
        m_ConsoleColors.Push(Console.ForegroundColor);
        Console.ForegroundColor = color;
      }
      catch
      {
        // ignored
      }
    }

    public static void PopColor()
    {
      try
      {
        Console.ForegroundColor = m_ConsoleColors.Pop();
      }
      catch
      {
        // ignored
      }
    }

    public static bool NumberBetween(double num, int bound1, int bound2, double allowance)
    {
      if (bound1 > bound2)
      {
        int i = bound1;
        bound1 = bound2;
        bound2 = i;
      }

      return num < bound2 + allowance && num > bound1 - allowance;
    }

    public static List<TOutput> CastListContravariant<TInput, TOutput>(List<TInput> list) where TInput : TOutput
    {
      return list.ConvertAll(value => (TOutput)value);
    }

    public static List<TOutput> CastListCovariant<TInput, TOutput>(List<TInput> list) where TOutput : TInput
    {
      return list.ConvertAll(value => (TOutput)value);
    }

    public static List<TOutput> SafeConvertList<TInput, TOutput>(List<TInput> list) where TOutput : class
    {
      List<TOutput> output = new List<TOutput>(list.Capacity);

      for (int i = 0; i < list.Count; i++)
      {
        TOutput t = list[i] as TOutput;

        if (t != null)
          output.Add(t);
      }

      return output;
    }

    #region To[Something]

    public static bool ToBoolean(string value)
    {
      bool.TryParse(value, out bool b);

      return b;
    }

    public static double ToDouble(string value)
    {
      double.TryParse(value, out double d);

      return d;
    }

    public static TimeSpan ToTimeSpan(string value)
    {
      TimeSpan.TryParse(value, out TimeSpan t);

      return t;
    }

    public static int ToInt32(string value)
    {
      int i;

      if (value.StartsWith("0x"))
        int.TryParse(value.Substring(2), NumberStyles.HexNumber, null, out i);
      else
        int.TryParse(value, out i);

      return i;
    }

    public static uint ToUInt32(string value)
    {
      uint i;

      if (value.StartsWith("0x"))
        uint.TryParse(value.Substring(2), NumberStyles.HexNumber, null, out i);
      else
        uint.TryParse(value, out i);

      return i;
    }

    #endregion

    #region Get[Something]

    public static double GetXMLDouble(string doubleString, double defaultValue)
    {
      try
      {
        return XmlConvert.ToDouble(doubleString);
      }
      catch
      {
        return double.TryParse(doubleString, out double val) ? val : defaultValue;
      }
    }

    public static int GetXMLInt32(string intString, int defaultValue)
    {
      try
      {
        return XmlConvert.ToInt32(intString);
      }
      catch
      {
        return int.TryParse(intString, out int val) ? val : defaultValue;
      }
    }

    public static uint GetXMLUInt32(string uintString, uint defaultValue)
    {
      try
      {
        return XmlConvert.ToUInt32(uintString);
      }
      catch
      {
        return uint.TryParse(uintString, out uint val) ? val : defaultValue;
      }
    }

    public static DateTime GetXMLDateTime(string dateTimeString, DateTime defaultValue)
    {
      try
      {
        return XmlConvert.ToDateTime(dateTimeString, XmlDateTimeSerializationMode.Utc);
      }
      catch
      {
        return DateTime.TryParse(dateTimeString, out DateTime d) ? d : defaultValue;
      }
    }

    public static DateTimeOffset GetXMLDateTimeOffset(string dateTimeOffsetString, DateTimeOffset defaultValue)
    {
      try
      {
        return XmlConvert.ToDateTimeOffset(dateTimeOffsetString);
      }
      catch
      {
        return DateTimeOffset.TryParse(dateTimeOffsetString, out DateTimeOffset d) ? d : defaultValue;
      }
    }

    public static TimeSpan GetXMLTimeSpan(string timeSpanString, TimeSpan defaultValue)
    {
      try
      {
        return XmlConvert.ToTimeSpan(timeSpanString);
      }
      catch
      {
        return defaultValue;
      }
    }

    public static string GetAttribute(XmlElement node, string attributeName, string defaultValue = null) => node?.Attributes[attributeName]?.Value ?? defaultValue;

    public static string GetText(XmlElement node, string defaultValue) => node == null ? defaultValue : node.InnerText;

    public static int GetAddressValue(IPAddress address) => BitConverter.ToInt32(address.GetAddressBytes(), 0);

    public static long GetLongAddressValue(IPAddress address) => BitConverter.ToInt64(address.GetAddressBytes(), 0);

    #endregion
  }
}
