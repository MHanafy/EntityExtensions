/***************************************************************************************************************
 * Date         Author      Change
 * 13/09/2017   MH          #2249: Improve websheets performance: Deep dependency check algorithm
 * **************************************************************************************************************/

namespace EntityExtensions.Helpers
{
    public static class HashHelper
    {
        //Copied from http://www.marcsmusings.org/2007/08/vtos-rtos-and-gethashcode-oh-my.html
        public static int CombineHashCodes(params int[] hashes)
        {
            unchecked
            {
                var hash = 0;

                for (var index = hashes.Length - 1; index >= 0; index--)
                {
                    hash <<= 5;
                    hash ^= hashes[index];
                }
                return hash;
            }

        }

        public static int CombineHashCodes(params object[] objects)
        {
            unchecked
            {
                var hash = 0;
                for (var index = objects.Length - 1; index >= 0; index--)
                {
                    var entryHash = 0x61E04917; // slurped from .Net runtime internals...
                    var entry = objects[index];

                    if (entry != null)
                    {
                        entryHash = entry is object[] subObjects ? CombineHashCodes(subObjects) : entry.GetHashCode();
                    }

                    hash <<= 5;
                    hash ^= entryHash;
                }
                return hash;
            }
        }

        public static int CombineHashCodes(int hash1, int hash2)
        {
            return (hash1 << 5)
                   ^ hash2;
        }

        public static int CombineHashCodes(int hash1, int hash2, int hash3)
        {
            return (((hash1 << 5)
                     ^ hash2) << 5)
                   ^ hash3;
        }

        public static int CombineHashCodes(int hash1, int hash2, int hash3, int hash4)
        {
            return (((((hash1 << 5)
                       ^ hash2) << 5)
                     ^ hash3) << 5)
                   ^ hash4;
        }

        public static int CombineHashCodes(int hash1, int hash2, int hash3, int hash4, int hash5)
        {
            return (((((((hash1 << 5)
                         ^ hash2) << 5)
                       ^ hash3) << 5)
                     ^ hash4) << 5)
                   ^ hash5;
        }

        public static int CombineHashCodes(object object1, object object2)
        {
            return CombineHashCodes(object1?.GetHashCode() ?? 0
                , object2?.GetHashCode() ?? 0);
        }

        public static int CombineHashCodes(object object1, object object2, object object3)
        {
            return CombineHashCodes(object1?.GetHashCode() ?? 0
                , object2?.GetHashCode() ?? 0
                , object3?.GetHashCode() ?? 0);
        }

        public static int CombineHashCodes(object object1, object object2, object object3, object object4)
        {
            return CombineHashCodes(object1?.GetHashCode() ?? 0
                , object2?.GetHashCode() ?? 0
                , object3?.GetHashCode() ?? 0
                , object4?.GetHashCode() ?? 0);
        }
    }
}
