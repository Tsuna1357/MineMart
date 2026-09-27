using System;

namespace MineMart.Core.Math
{
    /// <summary>
    /// Bộ chuẩn hóa và rút gọn hiển thị số lớn cho game Idle Tycoon.
    /// Hỗ trợ giá trị kiểu double từ hàng đơn vị đến K, M, B, T, aa..az, ba..bz đến zz.
    /// Toàn bộ hậu tố được tiền tính toán một lần (Precomputed) để tra cứu O(1) siêu tốc và Zero-GC.
    /// </summary>
    public static class NumberFormatter
    {
        // 5 hậu tố cơ sở: "" (0), "K" (1), "M" (2), "B" (3), "T" (4)
        // Tiếp theo là 26x26 = 676 hậu tố đôi từ "aa" đến "zz"
        private const int BASE_SUFFIX_COUNT = 5;
        private const int DOUBLE_LETTER_COUNT = 26 * 26; // 676
        private static readonly string[] Suffixes;

        static NumberFormatter()
        {
            Suffixes = new string[BASE_SUFFIX_COUNT + DOUBLE_LETTER_COUNT];
            Suffixes[0] = "";
            Suffixes[1] = "K";
            Suffixes[2] = "M";
            Suffixes[3] = "B";
            Suffixes[4] = "T";

            int index = BASE_SUFFIX_COUNT;
            for (int i = 0; i < 26; i++)
            {
                char first = (char)('a' + i);
                for (int j = 0; j < 26; j++)
                {
                    char second = (char)('a' + j);
                    Suffixes[index++] = $"{first}{second}";
                }
            }
        }

        /// <summary>
        /// Rút gọn số tiền hiển thị theo chuẩn Idle Game.
        /// Ví dụ: 950 -> "950", 1500 -> "1.50K", 10^90 -> "1.00az", 10^168 -> "1.00bz"
        /// </summary>
        public static string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            if (value < 0) return "-" + Format(-value);
            if (value < 1000.0) return System.Math.Floor(value).ToString("F0");

            int exp = (int)(System.Math.Log10(value) / 3);
            if (exp >= Suffixes.Length) exp = Suffixes.Length - 1;

            double normalized = value / System.Math.Pow(10, exp * 3);
            return $"{normalized:F2}{Suffixes[exp]}";
        }
    }
}
