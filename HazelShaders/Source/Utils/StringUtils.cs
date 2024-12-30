using System;

namespace HazelShaders
{
    public static class StringUtils
    {
        public static int FindFirstOf(this string str, string value, int startIndex = 0)
        {
            for (int i = startIndex; i < str.Length; i++)
            {
                if (value.IndexOf(str[i]) != -1)
                    return i;
            }
            return -1;
        }

        public static int FindFirstNotOf(this string str, string value, int startIndex = 0)
        {
            for (int i = startIndex; i < str.Length; i++)
            {
                if (value.IndexOf(str[i]) == -1)
                    return i;
            }
            return -1;
        }

        public static int WhiteSpaceAtStart(this string str)
        {
            int count = 0;
            int ix = 0;
            while (Char.IsWhiteSpace(str[ix++]) && ix < str.Length)
                count++;
            return count;
        }

        public static int WhiteSpaceAtEnd(this string str)
        {
            int count = 0;
            int ix = str.Length - 1;
            while (Char.IsWhiteSpace(str[ix--]) && ix >= 0)
                count++;
            return count;
        }
    }
}
