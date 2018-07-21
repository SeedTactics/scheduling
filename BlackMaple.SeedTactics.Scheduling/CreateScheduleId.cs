/* Copyright (c) 2018, John Lenz

All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * Neither the name of John Lenz, Black Maple Software, SeedTactics,
      nor the names of other contributors may be used to endorse or
      promote products derived from this software without specific
      prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Collections.Generic;

namespace BlackMaple.SeedTactics.Scheduling
{
  public static class CreateScheduleId
  {
    private static object _cntrLock = new object();
    private static int MachineId = Environment.MachineName.GetHashCode() & 0x00ffffff;
    private static short ProcessId = (short)System.Diagnostics.Process.GetCurrentProcess().Id;
    private static uint DownloadCntr = (uint)(new Random()).Next(0, 65536);

    public static string Create()
    {
      lock (_cntrLock)
      {
        //Mongo ObjectID:
        // - 4-byte value for seconds since unix epoch
        // - 3-byte machine identifier
        // - 2-byte process identifier
        // - 3-byte counter
        var UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ulong timestamp = (ulong)(DateTime.UtcNow - UnixEpoch).TotalSeconds;
        var increment = DownloadCntr++;

        byte[] bytes = new byte[12];
        bytes[0] = (byte)(timestamp >> 24);
        bytes[1] = (byte)(timestamp >> 16);
        bytes[2] = (byte)(timestamp >> 8);
        bytes[3] = (byte)(timestamp);
        bytes[4] = (byte)(MachineId >> 16);
        bytes[5] = (byte)(MachineId >> 8);
        bytes[6] = (byte)(MachineId);
        bytes[7] = (byte)(ProcessId >> 8);
        bytes[8] = (byte)(ProcessId);
        bytes[9] = (byte)(increment >> 16);
        bytes[10] = (byte)(increment >> 8);
        bytes[11] = (byte)(increment);

        //use url-safe base 64
        return Base64LexEncode(bytes);
      }
    }


    private static Dictionary<char, char> CharConvert = new Dictionary<char, char> {
      {'A', '0'},
      {'B', '1'},
      {'C', '2'},
      {'D', '3'},
      {'E', '4'},
      {'F', '5'},
      {'G', '6'},
      {'H', '7'},
      {'I', '8'},
      {'J', '9'},
      {'K', 'A'},
      {'L', 'B'},
      {'M', 'C'},
      {'N', 'D'},
      {'O', 'E'},
      {'P', 'F'},
      {'Q', 'G'},
      {'R', 'H'},
      {'S', 'I'},
      {'T', 'J'},
      {'U', 'K'},
      {'V', 'L'},
      {'W', 'M'},
      {'X', 'N'},
      {'Y', 'O'},
      {'Z', 'P'},
      {'a', 'Q'},
      {'b', 'R'},
      {'c', 'S'},
      {'d', 'T'},
      {'e', 'U'},
      {'f', 'V'},
      {'g', 'W'},
      {'h', 'X'},
      {'i', 'Y'},
      {'j', 'Z'},
      {'k', '_'},
      {'l', 'a'},
      {'m', 'b'},
      {'n', 'c'},
      {'o', 'd'},
      {'p', 'e'},
      {'q', 'f'},
      {'r', 'g'},
      {'s', 'h'},
      {'t', 'i'},
      {'u', 'j'},
      {'v', 'k'},
      {'w', 'l'},
      {'x', 'm'},
      {'y', 'n'},
      {'z', 'o'},
      {'0', 'p'},
      {'1', 'q'},
      {'2', 'r'},
      {'3', 's'},
      {'4', 't'},
      {'5', 'u'},
      {'6', 'v'},
      {'7', 'w'},
      {'8', 'x'},
      {'9', 'y'},
      {'+', 'z'},
      {'/', '~'},
    };

    //Base 64 encoding does not preserve lexicographic order, so use a custom encoding
    private static string Base64LexEncode(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        var res = new System.Text.StringBuilder();
        foreach (char c in base64) {
          if (c != '=')
            res.Append(CharConvert[c]);
        }
        return res.ToString();
    }
  }
}