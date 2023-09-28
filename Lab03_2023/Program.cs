using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lab03_2023
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var buffer = new byte[1024];
            buffer[0] = 16;
            Array.Resize(ref buffer, buffer.Length + 10);
            //добавим синхрокомбинацию
            for (var i = 0; i < 10; i++) buffer[i + buffer.Length - 10] = (byte)(i + 1);
            //закодируем пустой буфер    
            var coderBuffer = Encode(buffer);
            //выполним перемежение и скремблирование
            var muxBuffer = Mux(coderBuffer);
            var scramblerBuffer = Scramble(muxBuffer);
            //внесем ошибки
            var random = new Random();
            var errors = new byte[] { 1, 4, 8, 16, 32, 64, 128 };
            for (int i = 32; i < coderBuffer.Length; i += 32)
            {
                coderBuffer[i] ^= errors[random.Next(0, errors.Length)];
            }

            //выполним дескремблирование
            var descrambleBuffer = DeScramble(scramblerBuffer);
            //выполним деперемежение
            var demuxBuffer = DeMux(descrambleBuffer);
            //декодируем комбинцию
            var decodeBuffer = Decode(demuxBuffer);
            //удалим синхрокомбинацию
            var position = FindCombination(decodeBuffer, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            Array.Resize(ref decodeBuffer, position);
            Array.Resize(ref buffer, buffer.Length - 10);
        }

        internal static int[,] _G = { {1, 0, 0, 1, 1, 0},
                              {0, 1, 0, 1, 0, 1},
                              {0, 0, 1, 0, 1, 1}};

        internal static int[] _sindroms = { 6, 5, 3, 4, 2, 1 };

        //internal int[,] _P = { { 0, 1 }, { 0, 2 }, { 1, 2 } };

        internal static int[,] _H = { { 1, 1, 0, 1, 0, 0},
                              { 1, 0, 1, 0, 1, 0},
                              { 0, 1, 1, 0, 0, 1 }};

        internal static int k = 3;
        internal static int n = 6;
        internal static int p = 3;

        #region Получение и установка конкретного бита
        internal static byte GetBit(byte item, int offset)
        {
            if (offset > 7) return 0;
            return (byte)((item >> offset) & 0x01);
        }

        internal static byte SetBit(byte item, int offset, byte value)
        {
            if (offset > 7) return item;
            if (GetBit(item, offset) == value) return item;
            return item ^= (byte)(1 << offset);
        }
        //получение и установка конкретного бита в массиве
        internal static byte GetBit(byte[] buffer, int offset)
        {
            var numberByte = offset >> 3;
            var numberBit = offset % 8;
            if (numberByte >= buffer.Length) return 0;
            return GetBit(buffer[numberByte], numberBit);
        }

        internal static void SetBit(byte[] buffer, int offset, byte value)
        {
            var numberByte = offset >> 3;
            var numberBit = offset % 8;
            buffer[numberByte] = SetBit(buffer[numberByte], numberBit, value);
        }

        internal static int FindCombination(byte[] buffer, byte[] combination)
        {
            var position = -1;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == combination[0])
                {
                    position = 1;
                    for (int j = 1; j < combination.Length; j++)
                    {
                        if (buffer[i + j] != combination[j]) break;
                        position++;
                    }
                    if (position == combination.Length) return i;
                }

            }
            return -1;
        }
        #endregion

        #region Кодирование и декодирование
        internal static byte[] Decode(byte[] buffer)
        {
            var sizeBit = buffer.Length * 8;
            var ostSizeBit = sizeBit % n;
            sizeBit -= ostSizeBit;
            var resultBuffer = new byte[sizeBit];
            var resultOffset = 0;
            for (int i = 0; i < sizeBit; i += n)
            {
                SetBit(resultBuffer, resultOffset, GetBit(buffer, i));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, GetBit(buffer, i + 1));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, GetBit(buffer, i + 2));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, GetBit(buffer, i + 3));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, GetBit(buffer, i + 4));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, GetBit(buffer, i + 5));
                resultOffset++;

                var sindrom = (byte)(GetBit(buffer, i)
                    ^ GetBit(buffer, i + 1)
                    ^ GetBit(buffer, i + 3)) << 2;
                sindrom += (byte)(GetBit(buffer, i) ^ GetBit(buffer, i + 2) ^ GetBit(buffer, i + 4)) << 1;
                sindrom += (byte)(GetBit(buffer, i + 1) ^ GetBit(buffer, i + 2) ^ GetBit(buffer, i + 5));

                if (sindrom != 0)
                {
                    var offsetCoderWorld = resultOffset - n;
                    for (var s = 0; s < n; s++)
                    {
                        if (_sindroms[s] == sindrom)
                        {
                            SetBit(resultBuffer, offsetCoderWorld + s, (byte)(GetBit(resultBuffer, offsetCoderWorld + s) ^ 1));
                            break;
                        }
                    }
                }
                resultOffset -= p;
            }
            var newLen = (int)Math.Ceiling((decimal)resultOffset / 8);
            Array.Resize(ref resultBuffer, newLen);
            return resultBuffer;
        }

        internal static byte[] Encode(byte[] buffer)
        {
            var sizeBit = buffer.Length * 8;
            var resultBuffer = new byte[sizeBit * 2];
            var resultOffset = 0;
            for (int i = 0; i < sizeBit; i += k)
            {
                SetBit(resultBuffer, resultOffset, GetBit(buffer, i));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, GetBit(buffer, i + 1));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, GetBit(buffer, i + 2));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, (byte)(GetBit(buffer, i) ^ GetBit(buffer, i + 1)));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, (byte)(GetBit(buffer, i) ^ GetBit(buffer, i + 2)));
                resultOffset++;
                SetBit(resultBuffer, resultOffset, (byte)(GetBit(buffer, i + 1) ^ GetBit(buffer, i + 2)));
                resultOffset++;
            }
            var newLen = (int)Math.Ceiling((decimal)resultOffset / 8);
            Array.Resize(ref resultBuffer, newLen);
            return resultBuffer;
        }
        #endregion

        #region Мультиплексирование и демультиплексирование
        internal static int _N = 5;
        internal static byte[] Mux(byte[] buffer)
        {
            var sizeBit = buffer.Length * 8;
            var sizeOfMuxBuffer = _N * _N;
            var muxBuffer = new byte[sizeOfMuxBuffer];
            var muxPosition = 0;
            var numberBit = 0;
            var resultBuffer = new byte[2 * buffer.Length];
            var resultOffset = 0;
            while (true)
            {
                if (numberBit >= sizeBit && muxPosition == 0) break;
                muxBuffer[muxPosition] = (byte)(numberBit < sizeBit ? GetBit(buffer, numberBit) : 0);
                muxPosition += _N;
                if (muxPosition >= sizeOfMuxBuffer)
                {
                    muxPosition = muxPosition % _N + 1;
                    if (muxPosition >= _N)
                    {
                        //сохраним данные в результирующий массив
                        for (int i = 0; i < sizeOfMuxBuffer; i++)
                        {
                            SetBit(resultBuffer, resultOffset, muxBuffer[i]);
                            resultOffset++;
                        }
                        muxPosition = 0;
                    }
                }
                numberBit++;
            }
            var newLen = (int)Math.Ceiling((decimal)resultOffset / 8);
            Array.Resize(ref resultBuffer, newLen);
            return resultBuffer;
        }

        internal static byte[] DeMux(byte[] buffer)
        {
            var sizeBit = buffer.Length * 8;
            var sizeOfMuxBuffer = _N * _N;
            var muxBuffer = new byte[sizeOfMuxBuffer];
            var muxPosition = 0;
            var numberBit = 0;
            var resultBuffer = new byte[2 * buffer.Length];
            var resultOffset = 0;
            while (true)
            {
                if (numberBit >= sizeBit) break;
                muxBuffer[muxPosition] = (byte)(numberBit < sizeBit ? GetBit(buffer, numberBit) : 0);
                muxPosition++;
                if (muxPosition >= sizeOfMuxBuffer)
                {
                    muxPosition = 0;
                    //сохраним данные в результирующий массив
                    for (int i = 0; i < sizeOfMuxBuffer; i++)
                    {
                        SetBit(resultBuffer, resultOffset, muxBuffer[muxPosition]);
                        muxPosition = (muxPosition + _N) >= sizeOfMuxBuffer ? muxPosition % _N + 1 : muxPosition + _N;
                        resultOffset++;
                    }
                    muxPosition = 0;
                }
                numberBit++;
            }
            var newLen = (int)Math.Ceiling((decimal)resultOffset / 8);
            Array.Resize(ref resultBuffer, newLen);
            return resultBuffer;
        }
        #endregion

        #region Скремблирование и дескремблирование
        internal static byte GetBit(ushort item, int offset)
        {
            if (offset > 15) return 0;
            return (byte)((item >> offset) & 0x01);
        }

        internal static ushort SetBit(ushort item, int offset, byte value)
        {
            if (offset > 15) return item;
            if (GetBit(item, offset) == value) return item;
            return item ^= (byte)(1 << offset);
        }

        internal static byte GetBit(uint item, int offset)
        {
            if (offset > 31) return 0;
            return (byte)((item >> offset) & 0x01);
        }

        internal static uint SetBit(uint item, int offset, byte value)
        {
            if (offset > 31) return item;
            if (GetBit(item, offset) == value) return item;
            return item ^= (byte)(1 << offset);
        }


        internal static byte[] Scramble(byte[] buffer)
        {
            uint _registr = 169;
            var sizeBit = buffer.Length * 8;
            var resultBuffer = new byte[buffer.Length];
            var resultOffset = 0;
            for (int i = 0; i < sizeBit; i++)
            {
                var bit = (byte)(GetBit(_registr, 14)
                                ^ GetBit(_registr, 13)
                                /*^ _registr.GetBit(0)*/);
                SetBit(resultBuffer, resultOffset,
                    (byte)(GetBit(buffer, i)
                    ^ bit));
                resultOffset++;

                _registr = (ushort)(_registr << 1);
                _registr = SetBit(_registr, 0, bit);
            }
            return resultBuffer;
        }

        internal static byte[] DeScramble(byte[] buffer)
        {
            uint _registr = 169;
            var sizeBit = buffer.Length * 8;
            var resultBuffer = new byte[buffer.Length];
            var resultOffset = 0;
            for (int i = 0; i < sizeBit; i++)
            {
                var bit = (byte)(GetBit(_registr, 14)
                                ^ GetBit(_registr, 13)
                                /*^ _registr.GetBit(0)*/);
                SetBit(resultBuffer, resultOffset,
                    (byte)(GetBit(buffer, i)
                    ^ bit));
                resultOffset++;

                _registr = (ushort)(_registr << 1);
                _registr = SetBit(_registr, 0, bit);
            }
            return resultBuffer;
        }
        #endregion
    }
}
