// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;

namespace ORTS.Common.Input
{
    public enum RailDriverDisplaySign : byte
    {
        Blank = 0x0,
        Digit_0 = 0x3f,
        Digit_1 = 0x06,
        Digit_2 = 0x5b,
        Digit_3 = 0x4f,
        Digit_4 = 0x66,
        Digit_5 = 0x6d,
        Digit_6 = 0x7d,
        Digit_7 = 0x07,
        Digit_8 = 0x7f,
        Digit_9 = 0x6f,
        Decimal = 0x80,
        Hyphen = 0x40,
        Char_A = 0x77,
        Char_b = 0x7c,
        Char_C = 0x39,
        Char_c = 0x58,
        Char_d = 0x58,
        Char_E = 0x79,
        Char_F = 0x71,
        Char_H = 0x76,
        Char_h = 0x74,
        Char_L = 0x38,
        Char_l = 0x30,
        Char_n = 0x54,
        Char_o = 0x5c,
        Char_P = 0x73,
        Char_r = 0x50,
        Char_t = 0x78,
        Char_U = 0x3E,
        Char_u = 0x1c,
    }

    public class RailDriverDevice
    {
        public static readonly byte[] LedDigits = { (byte)RailDriverDisplaySign.Digit_0, (byte)RailDriverDisplaySign.Digit_1, (byte)RailDriverDisplaySign.Digit_2,
            (byte)RailDriverDisplaySign.Digit_3, (byte)RailDriverDisplaySign.Digit_4, (byte)RailDriverDisplaySign.Digit_5, (byte)RailDriverDisplaySign.Digit_6,
            (byte)RailDriverDisplaySign.Digit_7, (byte)RailDriverDisplaySign.Digit_8, (byte)RailDriverDisplaySign.Digit_9 };
        public static readonly byte[] LedDecimalDigits = { (byte)RailDriverDisplaySign.Digit_0 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_1 | (byte)RailDriverDisplaySign.Decimal,
            (byte)RailDriverDisplaySign.Digit_2 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_3 | (byte)RailDriverDisplaySign.Decimal,
            (byte)RailDriverDisplaySign.Digit_4 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_5 | (byte)RailDriverDisplaySign.Decimal,
            (byte)RailDriverDisplaySign.Digit_6 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_7 | (byte)RailDriverDisplaySign.Decimal,
            (byte)RailDriverDisplaySign.Digit_8 | (byte)RailDriverDisplaySign.Decimal, (byte)RailDriverDisplaySign.Digit_9 | (byte)RailDriverDisplaySign.Decimal };

        static readonly RailDriverDevice InstanceValue = new RailDriverDevice();

        public int WriteBufferSize => 0;
        public int ReadBufferSize => 0;
        public bool Enabled => false;
        public static RailDriverDevice Instance => InstanceValue;

        RailDriverDevice() { }

        public int WriteData(byte[] writeBuffer) => -1;
        public byte[] GetReadBuffer() => new byte[0];
        public int ReadCurrentData(ref byte[] data) => -1;
        public void Shutdown() { }
        public void SetLeds(byte led1, byte led2, byte led3) { }
        public void SetLeds(RailDriverDisplaySign led1, RailDriverDisplaySign led2, RailDriverDisplaySign led3) { }

        public void SetLedsNumeric(uint value)
        {
            if (value > 999)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Display Value needs to be between 0 and 999");
        }

        public void SetLedsNumeric(double value)
        {
            if (value < 0 || value > 999.9)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Display Value needs to be between 0.0 and 999.9");
        }

        public void ClearDisplay() { }
        public void EnableSpeaker(bool state) { }
    }
}
