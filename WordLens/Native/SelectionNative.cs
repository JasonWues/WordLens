using System;
using System.Runtime.InteropServices;

namespace WordLens.Native
{
    internal partial class SelectionNative
    {
        private const string LibName = "native";

        [LibraryImport(LibName, EntryPoint = "get_selection_text")]
        private static partial IntPtr GetSelectionTextPtr();

        [LibraryImport(LibName, EntryPoint = "free_c_string")]
        private static partial void FreeCString(IntPtr ptr);

        public static string GetSelectionText()
        {

            var resultPtr = IntPtr.Zero;
            string resultString = null;

            try
            {
                resultPtr = GetSelectionTextPtr();

                if (resultPtr == IntPtr.Zero)
                {
                    return null;
                }
                resultString = Marshal.PtrToStringUTF8(resultPtr);

            }
            finally
            {
                if (resultPtr != IntPtr.Zero)
                {
                    FreeCString(resultPtr);
                }
            }

            return resultString;
        }
    }
}